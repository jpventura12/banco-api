using Banco.Domain.ValueObjects;

namespace Banco.UnitTests.Domain;

public class CuitTests
{
    [Theory]
    [InlineData("30500010912", "30500010912")]
    [InlineData("30-50001091-2", "30500010912")]
    [InlineData(" 20-12345678-6 ", "20123456786")]
    [InlineData("27345678900", "27345678900")]
    public void TryCrear_acepta_cuit_valido_con_o_sin_guiones_y_lo_normaliza(string texto, string esperado)
    {
        var ok = Cuit.TryCrear(texto, out var cuit, out var error);

        Assert.True(ok, error);
        Assert.Equal(esperado, cuit!.Valor);
    }

    [Theory]
    [InlineData((string?)null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789")]
    [InlineData("305000109123")]
    [InlineData("3050001091A")]
    public void TryCrear_rechaza_formato_invalido(string? texto)
    {
        var ok = Cuit.TryCrear(texto, out var cuit, out var error);

        Assert.False(ok);
        Assert.Null(cuit);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryCrear_rechaza_digito_verificador_incorrecto()
    {
        var ok = Cuit.TryCrear("30500010913", out _, out var error);

        Assert.False(ok);
        Assert.Contains("verificador", error);
    }

    [Fact]
    public void TryCrear_rechaza_base_cuyo_digito_verificador_calculado_es_10()
    {
        Assert.False(Cuit.TryCrear("20123456001", out _, out _));
    }

    [Fact]
    public void Formateado_agrega_los_guiones()
    {
        Assert.Equal("30-50001091-2", Cuit.Crear("30500010912").Formateado);
    }

    [Fact]
    public void Dos_cuit_con_el_mismo_valor_son_iguales_aunque_se_escriban_distinto()
    {
        Assert.Equal(Cuit.Crear("30-50001091-2"), Cuit.Crear("30500010912"));
    }

    [Fact]
    public void Crear_lanza_ArgumentException_si_es_invalido()
    {
        Assert.Throws<ArgumentException>(() => Cuit.Crear("123"));
    }
}
