using Banco.Application.Abstractions;
using Banco.Domain.Bcra;
using Banco.Domain.ValueObjects;
using Banco.Infrastructure.Bcra;
using Banco.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Banco.UnitTests.Bcra;

public class SituacionCrediticiaProviderTests : IDisposable
{
    private static readonly Cuit CuitA = Cuit.Crear("30500010912");
    private static readonly Cuit CuitB = Cuit.Crear("30546689979");

    private readonly Mock<IBcraClient> _bcra = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 100 });

    private readonly FakeTimeProvider _tiempo = new(new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
    private readonly SituacionCrediticiaProvider _provider;

    public SituacionCrediticiaProviderTests()
    {
        var opciones = MsOptions.Create(new CacheOptions
        {
            BcraExpirationSeconds = 60,
            BcraNotFoundExpirationSeconds = 30,
            BcraStaleRetentionSeconds = 3600
        });

        _provider = new SituacionCrediticiaProvider(
            _bcra.Object, _cache, new BcraCacheLocks(), opciones, _tiempo, NullLogger<SituacionCrediticiaProvider>.Instance);
    }

    public void Dispose() => _cache.Dispose();

    private static SituacionCrediticia Situacion(Cuit cuit) =>
        new(cuit, "EMPRESA", "202409", [DeudaEntidad.DesdeInforme("BANCO X", 1, 10m, 0)]);

    [Fact]
    public async Task Primer_request_es_MISS_y_el_segundo_al_mismo_CUIT_es_HIT_sin_volver_a_llamar_al_BCRA()
    {
        _bcra.Setup(b => b.ConsultarDeudasAsync(CuitA, It.IsAny<CancellationToken>())).ReturnsAsync(Situacion(CuitA));

        var primero = await _provider.ObtenerAsync(CuitA, CancellationToken.None);
        var segundo = await _provider.ObtenerAsync(CuitA, CancellationToken.None);

        Assert.Equal(FuenteDatoBcra.Bcra, primero.Fuente);
        Assert.Equal(FuenteDatoBcra.Cache, segundo.Fuente);
        Assert.Equal(EstadoConsultaBcra.Ok, segundo.Estado);
        Assert.Same(primero.Situacion, segundo.Situacion);

        _bcra.Verify(b => b.ConsultarDeudasAsync(CuitA, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cada_CUIT_tiene_su_propia_entrada_de_cache()
    {
        _bcra.Setup(b => b.ConsultarDeudasAsync(It.IsAny<Cuit>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cuit c, CancellationToken _) => Situacion(c));

        await _provider.ObtenerAsync(CuitA, CancellationToken.None);
        await _provider.ObtenerAsync(CuitB, CancellationToken.None);
        await _provider.ObtenerAsync(CuitA, CancellationToken.None);

        _bcra.Verify(b => b.ConsultarDeudasAsync(CuitA, It.IsAny<CancellationToken>()), Times.Once);
        _bcra.Verify(b => b.ConsultarDeudasAsync(CuitB, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Al_vencer_la_expiracion_configurada_vuelve_a_consultar_al_BCRA()
    {
        _bcra.Setup(b => b.ConsultarDeudasAsync(CuitA, It.IsAny<CancellationToken>())).ReturnsAsync(Situacion(CuitA));

        await _provider.ObtenerAsync(CuitA, CancellationToken.None);
        _tiempo.Advance(TimeSpan.FromSeconds(59));
        var antesDeVencer = await _provider.ObtenerAsync(CuitA, CancellationToken.None);
        _tiempo.Advance(TimeSpan.FromSeconds(2));
        var despuesDeVencer = await _provider.ObtenerAsync(CuitA, CancellationToken.None);

        Assert.Equal(FuenteDatoBcra.Cache, antesDeVencer.Fuente);
        Assert.Equal(FuenteDatoBcra.Bcra, despuesDeVencer.Fuente);
        _bcra.Verify(b => b.ConsultarDeudasAsync(CuitA, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Un_404_del_BCRA_se_cachea_como_SinRegistros_con_su_propia_expiracion()
    {
        _bcra.Setup(b => b.ConsultarDeudasAsync(CuitA, It.IsAny<CancellationToken>())).ReturnsAsync((SituacionCrediticia?)null);

        var primero = await _provider.ObtenerAsync(CuitA, CancellationToken.None);
        var segundo = await _provider.ObtenerAsync(CuitA, CancellationToken.None);
        _tiempo.Advance(TimeSpan.FromSeconds(31));
        await _provider.ObtenerAsync(CuitA, CancellationToken.None);

        Assert.Equal(EstadoConsultaBcra.SinRegistros, primero.Estado);
        Assert.Equal(FuenteDatoBcra.Cache, segundo.Fuente);
        _bcra.Verify(b => b.ConsultarDeudasAsync(CuitA, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Fallback_si_el_BCRA_falla_devuelve_el_dato_vencido_marcado_como_desactualizado()
    {
        var original = Situacion(CuitA);
        _bcra.SetupSequence(b => b.ConsultarDeudasAsync(CuitA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(original)
            .ThrowsAsync(new BcraNoDisponibleException("Circuito abierto"));

        var fresco = await _provider.ObtenerAsync(CuitA, CancellationToken.None);
        _tiempo.Advance(TimeSpan.FromMinutes(5));
        var fallback = await _provider.ObtenerAsync(CuitA, CancellationToken.None);

        Assert.Equal(EstadoConsultaBcra.Ok, fallback.Estado);
        Assert.Equal(FuenteDatoBcra.CacheVencido, fallback.Fuente);
        Assert.True(fallback.Desactualizado);
        Assert.Same(original, fallback.Situacion);
        Assert.Equal(fresco.ConsultadoEn, fallback.ConsultadoEn);
        Assert.Equal("Circuito abierto", fallback.Detalle);
    }

    [Fact]
    public async Task Fallback_si_el_BCRA_falla_y_no_hay_nada_en_cache_devuelve_NoDisponible_sin_lanzar()
    {
        _bcra.Setup(b => b.ConsultarDeudasAsync(CuitA, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BcraNoDisponibleException("Timeout consultando al BCRA."));

        var resultado = await _provider.ObtenerAsync(CuitA, CancellationToken.None);

        Assert.Equal(EstadoConsultaBcra.NoDisponible, resultado.Estado);
        Assert.Null(resultado.Situacion);
        Assert.Null(resultado.Fuente);
        Assert.Equal("Timeout consultando al BCRA.", resultado.Detalle);
    }

    [Fact]
    public async Task Requests_concurrentes_al_mismo_CUIT_hacen_una_sola_llamada_al_BCRA()
    {
        var liberar = new TaskCompletionSource<SituacionCrediticia?>();
        _bcra.Setup(b => b.ConsultarDeudasAsync(CuitA, It.IsAny<CancellationToken>())).Returns(liberar.Task);

        var requests = Enumerable.Range(0, 10).Select(_ => _provider.ObtenerAsync(CuitA, CancellationToken.None)).ToList();

        await Task.Delay(100);
        liberar.SetResult(Situacion(CuitA));
        var resultados = await Task.WhenAll(requests);

        Assert.All(resultados, r => Assert.Equal(EstadoConsultaBcra.Ok, r.Estado));
        _bcra.Verify(b => b.ConsultarDeudasAsync(CuitA, It.IsAny<CancellationToken>()), Times.Once);
    }
}
