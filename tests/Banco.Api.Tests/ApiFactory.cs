using System.Net;
using System.Text;
using Banco.Infrastructure.Bcra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;

namespace Banco.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public string RutaBase { get; } = Path.Combine(Path.GetTempPath(), $"banco-tests-{Guid.NewGuid():N}.db");

    public Mock<HttpMessageHandler> Bcra { get; } = new();

    public ApiFactory()
    {
        BaseDePrueba.Crear(RutaBase);

        Bcra.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => Json(HttpStatusCode.NotFound, """{ "status": 404, "errorMessages": ["No se encontró datos para la identificación ingresada."] }"""));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuracion) => configuracion.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DatabaseOptions:ConnectionString"] = $"Data Source={RutaBase};Mode=ReadWrite;Foreign Keys=True",

            ["BcraOptions:BaseAddress"] = "https://bcra.test/",

            ["BcraOptions:RetryCount"] = "1",
            ["BcraOptions:RetryBaseDelayMs"] = "0",
            ["BcraOptions:TimeoutSeconds"] = "2",

            ["Serilog:WriteTo:1:Args:path"] = Path.Combine(Path.GetTempPath(), "banco-tests-logs", "banco-.log")
        }));

        builder.ConfigureTestServices(servicios =>
            servicios.AddHttpClient(BcraHttpClient.NombreCliente)
                .ConfigurePrimaryHttpMessageHandler(() => Bcra.Object));
    }

    public void BcraResponde(string cuit, Func<HttpResponseMessage> respuesta) =>
        Bcra.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.AbsolutePath.EndsWith("/" + cuit)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(respuesta);

    public void BcraFalla(string cuit) =>
        Bcra.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.AbsolutePath.EndsWith("/" + cuit)),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

    public int LlamadasAlBcra(string cuit) =>
        Bcra.Invocations.Count(i =>
            i.Method.Name == "SendAsync"
            && i.Arguments[0] is HttpRequestMessage r
            && r.RequestUri!.AbsolutePath.EndsWith("/" + cuit));

    public static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(RutaBase);
        }
        catch (IOException)
        {
        }
    }
}

public static class BaseDePrueba
{
    public static void Crear(string ruta)
    {
        EjecutarScript(ruta, "masterSQL.sql");
        EjecutarScript(ruta, "seed.sql");
    }

    public static void EjecutarScript(string ruta, string script)
    {
        var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "database", script));

        using var conexion = new SqliteConnection($"Data Source={ruta};Mode=ReadWriteCreate");
        conexion.Open();
        using var comando = conexion.CreateCommand();

        comando.CommandText = sql;
        comando.ExecuteNonQuery();
    }

    public static long Escalar(string ruta, string sql)
    {
        using var conexion = new SqliteConnection($"Data Source={ruta}");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;

        return Convert.ToInt64(comando.ExecuteScalar());
    }
}
