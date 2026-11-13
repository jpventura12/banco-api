using System.Net;
using System.Text;
using Banco.Application.Abstractions;
using Banco.Domain.ValueObjects;
using Banco.Infrastructure;
using Banco.Infrastructure.Bcra;
using Banco.Infrastructure.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Moq.Protected;
using Polly.CircuitBreaker;

namespace Banco.UnitTests.Bcra;

public sealed class BcraResilienciaTests : IDisposable
{
    private static readonly Cuit UnCuit = Cuit.Crear("30500010912");

    private readonly Mock<HttpMessageHandler> _handler = new();
    private ServiceProvider? _services;

    public void Dispose() => _services?.Dispose();

    private IBcraClient CrearCliente(int retryCount = 2, int timeoutSeconds = 1, int failures = 5, int breakSeconds = 1)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BcraOptions:BaseAddress"] = "https://bcra.test/",
                ["BcraOptions:DeudasPath"] = "CentralDeDeudores/v1.0/Deudas/{0}",
                ["BcraOptions:TimeoutSeconds"] = timeoutSeconds.ToString(),
                ["BcraOptions:TotalTimeoutSeconds"] = "20",
                ["BcraOptions:RetryCount"] = retryCount.ToString(),
                ["BcraOptions:RetryBaseDelayMs"] = "0",
                ["BcraOptions:CircuitBreakerFailures"] = failures.ToString(),
                ["BcraOptions:CircuitBreakerDurationSeconds"] = breakSeconds.ToString(),
                ["BcraOptions:CircuitBreakerSamplingSeconds"] = "30"
            })
            .Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddBcraClient(configuracion).ConfigurePrimaryHttpMessageHandler(() => _handler.Object);

        _services = servicios.BuildServiceProvider();
        return _services.GetRequiredService<IBcraClient>();
    }

    private static HttpResponseMessage Respuesta(HttpStatusCode status, string json = "") => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private void ResponderSiempre(Func<HttpResponseMessage> respuesta) =>
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(respuesta);

    private void VerificarLlamadas(int cantidad) =>
        _handler.Protected().Verify("SendAsync", Times.Exactly(cantidad), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());

    [Fact]
    public async Task Respuesta_200_se_mapea_al_dominio()
    {
        var cliente = CrearCliente();
        ResponderSiempre(() => Respuesta(HttpStatusCode.OK, BcraFixtures.Deudas200));

        var situacion = await cliente.ConsultarDeudasAsync(UnCuit, CancellationToken.None);

        Assert.NotNull(situacion);
        Assert.Equal(3, situacion.Entidades.Count);
        VerificarLlamadas(1);
    }

    [Fact]
    public async Task Retry_ante_5xx_transitorios_y_luego_exito()
    {
        var cliente = CrearCliente(retryCount: 2);

        _handler.Protected()
            .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(Respuesta(HttpStatusCode.InternalServerError, BcraFixtures.Deudas500))
            .ReturnsAsync(Respuesta(HttpStatusCode.ServiceUnavailable))
            .ReturnsAsync(Respuesta(HttpStatusCode.OK, BcraFixtures.Deudas200));

        var situacion = await cliente.ConsultarDeudasAsync(UnCuit, CancellationToken.None);

        Assert.NotNull(situacion);
        VerificarLlamadas(3);
    }

    [Fact]
    public async Task Agotados_los_reintentos_lanza_BcraNoDisponibleException()
    {
        var cliente = CrearCliente(retryCount: 2);
        ResponderSiempre(() => Respuesta(HttpStatusCode.InternalServerError, BcraFixtures.Deudas500));

        var ex = await Assert.ThrowsAsync<BcraNoDisponibleException>(() => cliente.ConsultarDeudasAsync(UnCuit, CancellationToken.None));

        Assert.Contains("500", ex.Message);
        VerificarLlamadas(3);
    }

    [Fact]
    public async Task Error_de_red_se_reintenta_y_termina_en_BcraNoDisponibleException()
    {
        var cliente = CrearCliente(retryCount: 2);
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<BcraNoDisponibleException>(() => cliente.ConsultarDeudasAsync(UnCuit, CancellationToken.None));

        VerificarLlamadas(3);
    }

    [Fact]
    public async Task Timeout_por_intento_corta_la_llamada_lenta_reintenta_y_termina_en_BcraNoDisponibleException()
    {
        var cliente = CrearCliente(retryCount: 1, timeoutSeconds: 1);

        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                return Respuesta(HttpStatusCode.OK, BcraFixtures.Deudas200);
            });

        var inicio = DateTime.UtcNow;
        var ex = await Assert.ThrowsAsync<BcraNoDisponibleException>(() => cliente.ConsultarDeudasAsync(UnCuit, CancellationToken.None));

        Assert.Contains("Timeout", ex.Message);
        Assert.True(DateTime.UtcNow - inicio < TimeSpan.FromSeconds(10), "El timeout configurable debe cortar mucho antes de los 30 s");
        VerificarLlamadas(2);
    }

    [Fact]
    public async Task Un_404_con_formato_BCRA_es_SinRegistros_y_no_se_reintenta()
    {
        var cliente = CrearCliente();
        ResponderSiempre(() => Respuesta(HttpStatusCode.NotFound, BcraFixtures.Deudas404));

        var situacion = await cliente.ConsultarDeudasAsync(UnCuit, CancellationToken.None);

        Assert.Null(situacion);
        VerificarLlamadas(1);
    }

    [Fact]
    public async Task Un_404_sin_formato_BCRA_indica_ruta_mal_configurada()
    {
        var cliente = CrearCliente();
        ResponderSiempre(() => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("<html>Not Found</html>", Encoding.UTF8, "text/html") });

        var ex = await Assert.ThrowsAsync<BcraNoDisponibleException>(() => cliente.ConsultarDeudasAsync(UnCuit, CancellationToken.None));

        Assert.Contains("DeudasPath", ex.Message);
        VerificarLlamadas(1);
    }

    [Fact]
    public async Task Un_400_no_se_reintenta()
    {
        var cliente = CrearCliente();
        ResponderSiempre(() => Respuesta(HttpStatusCode.BadRequest, BcraFixtures.Deudas400));

        var ex = await Assert.ThrowsAsync<BcraNoDisponibleException>(() => cliente.ConsultarDeudasAsync(UnCuit, CancellationToken.None));

        Assert.Contains("11 dígitos", ex.Message);
        VerificarLlamadas(1);
    }

    [Fact]
    public async Task Circuit_breaker_se_abre_tras_N_fallos_deja_de_llamar_al_BCRA_y_se_recupera()
    {
        var cliente = CrearCliente(retryCount: 2, failures: 5, breakSeconds: 1);
        var estado = _services!.GetRequiredService<BcraCircuitBreakerState>();
        var health = new BcraCircuitHealthCheck(estado);
        ResponderSiempre(() => Respuesta(HttpStatusCode.InternalServerError, BcraFixtures.Deudas500));

        await Assert.ThrowsAsync<BcraNoDisponibleException>(() => cliente.ConsultarDeudasAsync(UnCuit, CancellationToken.None));
        await Assert.ThrowsAsync<BcraNoDisponibleException>(() => cliente.ConsultarDeudasAsync(UnCuit, CancellationToken.None));
        Assert.Equal(CircuitState.Open, estado.Provider.CircuitState);
        VerificarLlamadas(5);

        var ex = await Assert.ThrowsAsync<BcraNoDisponibleException>(() => cliente.ConsultarDeudasAsync(UnCuit, CancellationToken.None));
        Assert.Contains("Circuito abierto", ex.Message);
        VerificarLlamadas(5);

        var reporte = await health.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Degraded, reporte.Status);
        Assert.Equal("Open", reporte.Data["circuito"]);

        await Task.Delay(TimeSpan.FromMilliseconds(1300));
        ResponderSiempre(() => Respuesta(HttpStatusCode.OK, BcraFixtures.Deudas200));

        var situacion = await cliente.ConsultarDeudasAsync(UnCuit, CancellationToken.None);

        Assert.NotNull(situacion);
        Assert.Equal(CircuitState.Closed, estado.Provider.CircuitState);
        Assert.Equal(HealthStatus.Healthy, (await health.CheckHealthAsync(new HealthCheckContext())).Status);
    }

    [Fact]
    public async Task Propaga_el_CorrelationId_en_la_llamada_saliente()
    {
        var cliente = CrearCliente();

        HttpRequestMessage? enviado = null;
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => enviado = request)
            .ReturnsAsync(() => Respuesta(HttpStatusCode.NotFound, BcraFixtures.Deudas404));

        _services!.GetRequiredService<ICorrelationIdAccessor>().CorrelationId = "test-correlation-123";
        await cliente.ConsultarDeudasAsync(UnCuit, CancellationToken.None);

        Assert.NotNull(enviado);
        Assert.Equal("test-correlation-123", enviado.Headers.GetValues(CorrelationIdHandler.Header).Single());
        Assert.Equal("https://bcra.test/CentralDeDeudores/v1.0/Deudas/30500010912", enviado.RequestUri!.ToString());
    }
}
