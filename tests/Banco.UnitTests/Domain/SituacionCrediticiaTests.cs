using Banco.Domain.Bcra;
using Banco.Domain.ValueObjects;

namespace Banco.UnitTests.Domain;

public class SituacionCrediticiaTests
{
    private static readonly Cuit UnCuit = Cuit.Crear("30500010912");

    [Fact]
    public void DesdeInforme_convierte_el_monto_de_miles_a_pesos()
    {
        var deuda = DeudaEntidad.DesdeInforme("BANCO X", situacion: 1, montoEnMiles: 59.5m, diasAtrasoPago: 0);

        Assert.Equal(59_500m, deuda.MontoPesos);
    }

    [Fact]
    public void DesdeInforme_interpreta_dias_de_atraso_0_como_no_aplica()
    {
        Assert.Null(DeudaEntidad.DesdeInforme("X", 1, 1m, diasAtrasoPago: 0).DiasAtrasoPago);
        Assert.Equal(45, DeudaEntidad.DesdeInforme("X", 3, 1m, diasAtrasoPago: 45).DiasAtrasoPago);
    }

    [Fact]
    public void DesdeInforme_limpia_espacios_del_nombre_de_la_entidad()
    {
        Assert.Equal("BANCO X", DeudaEntidad.DesdeInforme("  BANCO X  ", 1, 1m, 0).Entidad);
    }

    [Fact]
    public void PeorSituacion_es_la_maxima_y_la_deuda_total_es_la_suma_en_pesos()
    {
        var situacion = new SituacionCrediticia(UnCuit, "EMPRESA", "202409",
        [
            DeudaEntidad.DesdeInforme("A", 1, 10m, 0),
            DeudaEntidad.DesdeInforme("B", 3, 20m, 0),
            DeudaEntidad.DesdeInforme("C", 2, 5.5m, 0)
        ]);

        Assert.Equal(3, situacion.PeorSituacion);
        Assert.Equal(35_500m, situacion.DeudaTotalPesos);
    }

    [Fact]
    public void Sin_entidades_no_hay_peor_situacion_y_la_deuda_es_cero()
    {
        var situacion = new SituacionCrediticia(UnCuit, " ", null, []);

        Assert.Null(situacion.PeorSituacion);
        Assert.Equal(0m, situacion.DeudaTotalPesos);
        Assert.Null(situacion.Denominacion);
    }

    [Theory]
    [InlineData(1, "Normal")]
    [InlineData(2, "Seguimiento especial / Riesgo bajo")]
    [InlineData(5, "Irrecuperable")]
    [InlineData(6, "Desconocida (6)")]
    public void Describir_situacion(int situacion, string esperado)
    {
        Assert.Equal(esperado, SituacionDeudor.Describir(situacion));
    }
}
