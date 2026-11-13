using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Banco.Application.Dtos;

namespace Banco.Api.Tests;

public class ClientesEndpointsTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _http = api.CreateClient();

    [Fact]
    public async Task Listado_de_clientes_paginado()
    {
        var pagina = await _http.GetFromJsonAsync<PaginaDto<ClienteResumenDto>>("/api/clientes?page=1&pageSize=3");

        Assert.NotNull(pagina);
        Assert.Equal(8, pagina.Total);
        Assert.Equal(3, pagina.Items.Count);
        Assert.Equal("Juan", pagina.Items[0].Nombre);
    }

    [Fact]
    public async Task Paginacion_invalida_devuelve_400_ProblemDetails_con_correlationId()
    {
        var respuesta = await _http.GetAsync("/api/clientes?pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(
            respuesta.Headers.GetValues("X-Correlation-ID").Single(),
            problema.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Cliente_por_CUIT_con_guiones_incluye_cuentas_y_BCRA_sin_registros()
    {
        var cliente = await _http.GetFromJsonAsync<ClienteDetalleDto>("/api/clientes/20-12345678-6");

        Assert.NotNull(cliente);
        Assert.Equal("Pérez", cliente.Apellido);
        Assert.Equal(2, cliente.Cuentas.Count);
        Assert.Equal(1_445_810.75m, cliente.Cuentas[0].Saldo);

        Assert.Equal("SinRegistros", cliente.SituacionBcra.Estado);
        Assert.Equal("bcra", cliente.SituacionBcra.Fuente);
    }

    [Fact]
    public async Task Cliente_con_deudas_en_el_BCRA_y_segundo_request_desde_cache()
    {
        const string cuit = "30546689979";
        api.BcraResponde(cuit, () => ApiFactory.Json(HttpStatusCode.OK, """
            { "status": 200, "results": { "identificacion": 30546689979, "denominacion": "YPF SA",
              "periodos": [ { "periodo": "202409", "entidades": [
                { "entidad": "BANCO DE LA NACION ARGENTINA", "situacion": 1, "fechaSit1": "2020-01-31", "monto": 1500.5,
                  "diasAtrasoPago": 0, "refinanciaciones": false, "recategorizacionOblig": false, "situacionJuridica": false,
                  "irrecDisposicionTecnica": false, "enRevision": false, "procesoJud": false } ] } ] } }
            """));

        var primero = await _http.GetFromJsonAsync<ClienteDetalleDto>($"/api/clientes/{cuit}");
        var segundo = await _http.GetFromJsonAsync<ClienteDetalleDto>($"/api/clientes/{cuit}");

        Assert.Equal("Ok", primero!.SituacionBcra.Estado);
        Assert.Equal("bcra", primero.SituacionBcra.Fuente);
        Assert.Equal(1_500_500m, primero.SituacionBcra.DeudaTotalPesos);
        Assert.Equal("Normal", primero.SituacionBcra.DescripcionPeorSituacion);

        Assert.Equal("cache", segundo!.SituacionBcra.Fuente);
        Assert.Equal(1, api.LlamadasAlBcra(cuit));
    }

    [Fact]
    public async Task Si_el_BCRA_no_responde_devuelve_200_con_los_datos_locales_y_BCRA_NoDisponible()
    {
        const string cuit = "30639453738";
        api.BcraFalla(cuit);

        var cliente = await _http.GetFromJsonAsync<ClienteDetalleDto>($"/api/clientes/{cuit}");

        Assert.NotNull(cliente);
        Assert.Single(cliente.Cuentas);
        Assert.Equal("NoDisponible", cliente.SituacionBcra.Estado);
        Assert.Null(cliente.SituacionBcra.Fuente);

        Assert.Equal(2, api.LlamadasAlBcra(cuit));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("30500010913")]
    public async Task CUIT_invalido_devuelve_400(string cuit)
    {
        var respuesta = await _http.GetAsync($"/api/clientes/{cuit}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task CUIT_valido_que_no_es_cliente_devuelve_404()
    {
        var respuesta = await _http.GetAsync("/api/clientes/23401112227");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}

public class CuentasEndpointsTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _http = api.CreateClient();

    [Fact]
    public async Task Cuenta_con_movimientos_ordenados_y_saldo_consistente()
    {
        var cuenta = await _http.GetFromJsonAsync<CuentaDetalleDto>("/api/cuentas/1");

        Assert.NotNull(cuenta);
        Assert.Equal("000100012345", cuenta.Numero);
        Assert.Equal("0001", cuenta.CodSucursal);
        Assert.Equal("20123456786", cuenta.Titular.Cuit);
        Assert.Equal(8, cuenta.Movimientos.Count);

        Assert.Equal(cuenta.Saldo, cuenta.Movimientos.Sum(m => m.Importe));

        Assert.True(cuenta.Movimientos.Zip(cuenta.Movimientos.Skip(1)).All(p => p.First.Fecha >= p.Second.Fecha));

        Assert.Contains(cuenta.Movimientos, m => m.Importe < 0 && m.Tipo == "EXTRACCIÓN EN EFECTIVO (CAJERO)");
    }

    [Fact]
    public async Task Filtro_por_fechas_con_hasta_inclusivo()
    {
        var cuenta = await _http.GetFromJsonAsync<CuentaDetalleDto>("/api/cuentas/1?desde=2026-10-01&hasta=2026-10-02");

        Assert.Equal(2, cuenta!.Movimientos.Count);
    }

    [Fact]
    public async Task Cuenta_inexistente_devuelve_404()
    {
        var respuesta = await _http.GetAsync("/api/cuentas/999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}

public class ObservabilidadTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _http = api.CreateClient();

    [Fact]
    public async Task Respeta_el_X_Correlation_ID_entrante_y_lo_propaga_al_BCRA()
    {
        const string cuit = "30703088534";
        api.BcraResponde(cuit, () => ApiFactory.Json(HttpStatusCode.NotFound, """{ "status": 404, "errorMessages": ["x"] }"""));

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/clientes/{cuit}");
        request.Headers.Add("X-Correlation-ID", "prueba-123");
        var respuesta = await _http.SendAsync(request);

        var saliente = api.Bcra.Invocations
            .Select(i => i.Arguments[0] as HttpRequestMessage)
            .Last(r => r is not null && r.RequestUri!.AbsolutePath.EndsWith(cuit));

        Assert.Equal("prueba-123", respuesta.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Equal("prueba-123", saliente!.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task Sin_X_Correlation_ID_genera_un_GUID()
    {
        var respuesta = await _http.GetAsync("/api/clientes");

        var id = respuesta.Headers.GetValues("X-Correlation-ID").Single();
        Assert.True(Guid.TryParse(id, out _));
    }

    [Fact]
    public async Task X_Correlation_ID_con_caracteres_invalidos_se_reemplaza()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/clientes");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", "<script>");

        var respuesta = await _http.SendAsync(request);

        Assert.True(Guid.TryParse(respuesta.Headers.GetValues("X-Correlation-ID").Single(), out _));
    }

    [Fact]
    public async Task Health_informa_base_circuito_BCRA_y_cache()
    {
        var respuesta = await _http.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());

        var checks = json.RootElement.GetProperty("checks").EnumerateArray()
            .ToDictionary(c => c.GetProperty("name").GetString()!, c => c.GetProperty("status").GetString());
        Assert.Equal("Healthy", checks["sqlite"]);
        Assert.Equal("Healthy", checks["bcra-circuit"]);
        Assert.Equal("Healthy", checks["memory-cache"]);
    }
}

public class ScriptsSqlTests
{
    [Fact]
    public void Master_y_seed_son_idempotentes()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"banco-idempotencia-{Guid.NewGuid():N}.db");
        try
        {
            BaseDePrueba.Crear(ruta);
            BaseDePrueba.Crear(ruta);

            Assert.Equal(8, BaseDePrueba.Escalar(ruta, "SELECT COUNT(*) FROM Clientes"));
            Assert.Equal(9, BaseDePrueba.Escalar(ruta, "SELECT COUNT(*) FROM Cuentas"));
            Assert.Equal(25, BaseDePrueba.Escalar(ruta, "SELECT COUNT(*) FROM Movimientos"));
            Assert.Equal(1, BaseDePrueba.Escalar(ruta, "SELECT COUNT(*) FROM SchemaVersion"));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(ruta);
        }
    }

    [Fact]
    public void El_saldo_de_cada_cuenta_coincide_con_la_suma_de_sus_movimientos()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"banco-saldos-{Guid.NewGuid():N}.db");
        try
        {
            BaseDePrueba.Crear(ruta);

            var inconsistentes = BaseDePrueba.Escalar(ruta, """
                SELECT COUNT(*) FROM (
                  SELECT c.id FROM Cuentas c LEFT JOIN Movimientos m ON m.idCuenta = c.id
                  GROUP BY c.id HAVING ROUND(c.saldo - COALESCE(SUM(m.importe), 0), 2) <> 0)
                """);

            Assert.Equal(0, inconsistentes);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(ruta);
        }
    }
}
