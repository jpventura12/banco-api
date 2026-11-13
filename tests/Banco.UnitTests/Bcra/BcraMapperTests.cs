using System.Text.Json;
using Banco.Domain.ValueObjects;
using Banco.Infrastructure.Bcra;

namespace Banco.UnitTests.Bcra;

public class BcraMapperTests
{
    [Fact]
    public void Toma_las_entidades_de_todos_los_periodos_y_ante_repetidas_la_mas_reciente()
    {
        var respuesta = JsonSerializer.Deserialize<BcraRespuesta<BcraDeudasResultado>>(BcraFixtures.Deudas200, BcraJson.Opciones)!;

        var situacion = BcraMapper.ADominio(Cuit.Crear("30500010912"), respuesta.Results!);

        Assert.Equal("EMPRESA DE PRUEBA S.A.", situacion.Denominacion);
        Assert.Equal("202409", situacion.Periodo);
        Assert.Equal(3, situacion.Entidades.Count);

        var bna = situacion.Entidades.Single(e => e.Entidad == "BANCO DE LA NACION ARGENTINA");
        Assert.Equal(59_000m, bna.MontoPesos);
        Assert.Null(bna.DiasAtrasoPago);
        Assert.Equal(new DateOnly(2024, 5, 30), bna.FechaSituacion1);
        Assert.Equal("202409", bna.Periodo);

        var bancoX = situacion.Entidades.Single(e => e.Entidad == "BANCO X");
        Assert.Equal(3, bancoX.Situacion);
        Assert.Equal(1_250_500m, bancoX.MontoPesos);
        Assert.Equal(95, bancoX.DiasAtrasoPago);
        Assert.True(bancoX.Refinanciaciones);

        var vieja = situacion.Entidades.Single(e => e.Entidad == "ENTIDAD VIEJA");
        Assert.Equal("202408", vieja.Periodo);

        Assert.Equal(5, situacion.PeorSituacion);
        Assert.Equal(2_308_500m, situacion.DeudaTotalPesos);
    }

    [Fact]
    public void Mapea_una_respuesta_real_de_la_API_del_BCRA()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "bcra-deudas-real-30500010912.json"));
        var respuesta = JsonSerializer.Deserialize<BcraRespuesta<BcraDeudasResultado>>(json, BcraJson.Opciones)!;

        var situacion = BcraMapper.ADominio(Cuit.Crear("30500010912"), respuesta.Results!);

        Assert.Equal("BANCO DE LA NACION ARGENTINA", situacion.Denominacion);
        Assert.Equal("202609", situacion.Periodo);
        Assert.Equal(22, situacion.Entidades.Count);
        Assert.Equal("202608", situacion.Entidades.Single(e => e.Entidad == "Naldo Lombardi S.A.").Periodo);
        Assert.Contains(situacion.Entidades, e => e.Entidad == "NUEVO BANCO DE ENTRE RÍOS S.A.");
        Assert.Equal(1, situacion.PeorSituacion);
        Assert.Equal(329_155_846_000m, situacion.DeudaTotalPesos);
    }

    [Fact]
    public void Sin_periodos_devuelve_situacion_sin_entidades()
    {
        var resultado = new BcraDeudasResultado { Identificacion = 30500010912, Denominacion = "X", Periodos = null };

        var situacion = BcraMapper.ADominio(Cuit.Crear("30500010912"), resultado);

        Assert.Empty(situacion.Entidades);
        Assert.Null(situacion.Periodo);
    }
}

internal static class BcraFixtures
{
    public const string Deudas200 = """
        {
          "status": 200,
          "results": {
            "identificacion": 30500010912,
            "denominacion": "EMPRESA DE PRUEBA S.A.",
            "periodos": [
              {
                "periodo": "202408",
                "entidades": [
                  { "entidad": "ENTIDAD VIEJA", "situacion": 5, "monto": 999.0, "diasAtrasoPago": 0,
                    "refinanciaciones": false, "recategorizacionOblig": false, "situacionJuridica": false,
                    "irrecDisposicionTecnica": false, "enRevision": false, "procesoJud": false },
                  { "entidad": "BANCO X", "situacion": 5, "monto": 1.0, "diasAtrasoPago": 0,
                    "refinanciaciones": false, "recategorizacionOblig": false, "situacionJuridica": false,
                    "irrecDisposicionTecnica": false, "enRevision": false, "procesoJud": false }
                ]
              },
              {
                "periodo": "202409",
                "entidades": [
                  { "entidad": "BANCO DE LA NACION ARGENTINA", "situacion": 1, "fechaSit1": "2024-05-30", "monto": 59.0,
                    "diasAtrasoPago": 0, "refinanciaciones": false, "recategorizacionOblig": false, "situacionJuridica": false,
                    "irrecDisposicionTecnica": false, "enRevision": false, "procesoJud": false },
                  { "entidad": "BANCO X", "situacion": 3, "fechaSit1": null, "monto": 1250.5,
                    "diasAtrasoPago": 95, "refinanciaciones": true, "recategorizacionOblig": false, "situacionJuridica": false,
                    "irrecDisposicionTecnica": false, "enRevision": false, "procesoJud": false }
                ]
              }
            ]
          }
        }
        """;

    public const string Deudas404 = """
        { "status": 404, "errorMessages": ["No se encontró datos para la identificación ingresada."] }
        """;

    public const string Deudas400 = """
        { "status": 400, "errorMessages": ["Parámetro erróneo: Ingresar 11 dígitos para realizar la consulta."] }
        """;

    public const string Deudas500 = """
        { "status": 500, "errorMessages": ["Se produjo un error al ejecutar la acción."] }
        """;
}
