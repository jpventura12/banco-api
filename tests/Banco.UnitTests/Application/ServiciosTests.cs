using Banco.Application.Abstractions;
using Banco.Application.Common;
using Banco.Application.Services;
using Banco.Domain.Bcra;
using Banco.Domain.Entities;
using Banco.Domain.ValueObjects;
using Moq;

namespace Banco.UnitTests.Application;

public class ClientesServiceTests
{
    private readonly Mock<IClienteRepository> _clientes = new();
    private readonly Mock<ICuentaRepository> _cuentas = new();
    private readonly Mock<ISituacionCrediticiaProvider> _bcra = new();
    private readonly ClientesService _servicio;

    public ClientesServiceTests()
    {
        _servicio = new ClientesService(_clientes.Object, _cuentas.Object, _bcra.Object);
    }

    [Fact]
    public async Task ObtenerPorCuit_con_cuit_invalido_devuelve_Invalido_sin_tocar_la_base_ni_el_BCRA()
    {
        var resultado = await _servicio.ObtenerPorCuitAsync("123", CancellationToken.None);

        Assert.Equal(EstadoResultado.Invalido, resultado.Estado);
        _clientes.VerifyNoOtherCalls();
        _bcra.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ObtenerPorCuit_de_cliente_inexistente_devuelve_NoEncontrado_y_no_consulta_al_BCRA()
    {
        _clientes.Setup(r => r.ObtenerPorCuitAsync("30500010912", It.IsAny<CancellationToken>())).ReturnsAsync((Cliente?)null);

        var resultado = await _servicio.ObtenerPorCuitAsync("30-50001091-2", CancellationToken.None);

        Assert.Equal(EstadoResultado.NoEncontrado, resultado.Estado);
        _bcra.Verify(b => b.ObtenerAsync(It.IsAny<Cuit>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ObtenerPorCuit_arma_el_cliente_con_cuentas_y_situacion_BCRA_en_pesos()
    {
        _clientes.Setup(r => r.ObtenerPorCuitAsync("30500010912", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Cliente { Id = 6, Nombre = "BNA", Cuit = "30500010912" });
        _cuentas.Setup(r => r.ListarPorClienteAsync(6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Cuenta> { new() { Id = 7, IdCliente = 6, Numero = "000100000001", CodSucursal = "0001", Saldo = 1000m } });
        _bcra.Setup(b => b.ObtenerAsync(It.Is<Cuit>(c => c.Valor == "30500010912"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoSituacionCrediticia
            {
                Estado = EstadoConsultaBcra.Ok,
                Fuente = FuenteDatoBcra.Bcra,
                Situacion = new SituacionCrediticia(Cuit.Crear("30500010912"), "BNA", "202409",
                    [DeudaEntidad.DesdeInforme("BANCO X", 2, 12.5m, 0)])
            });

        var resultado = await _servicio.ObtenerPorCuitAsync("30500010912", CancellationToken.None);

        Assert.Equal(EstadoResultado.Ok, resultado.Estado);
        var cliente = resultado.Valor!;
        Assert.Single(cliente.Cuentas);
        Assert.Equal("Ok", cliente.SituacionBcra.Estado);
        Assert.Equal("bcra", cliente.SituacionBcra.Fuente);
        Assert.Equal(2, cliente.SituacionBcra.PeorSituacion);
        Assert.Equal(12_500m, cliente.SituacionBcra.DeudaTotalPesos);
        Assert.Equal(12_500m, Assert.Single(cliente.SituacionBcra.Entidades).MontoPesos);
    }

    [Fact]
    public async Task ObtenerPorCuit_con_BCRA_no_disponible_igual_devuelve_el_cliente()
    {
        _clientes.Setup(r => r.ObtenerPorCuitAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Cliente { Id = 1, Nombre = "Juan", Cuit = "20123456786" });
        _cuentas.Setup(r => r.ListarPorClienteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Cuenta>());
        _bcra.Setup(b => b.ObtenerAsync(It.IsAny<Cuit>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoSituacionCrediticia.NoDisponible("Timeout"));

        var resultado = await _servicio.ObtenerPorCuitAsync("20123456786", CancellationToken.None);

        Assert.Equal(EstadoResultado.Ok, resultado.Estado);
        Assert.Equal("NoDisponible", resultado.Valor!.SituacionBcra.Estado);
        Assert.Empty(resultado.Valor.SituacionBcra.Entidades);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Listar_valida_la_paginacion(int page, int pageSize)
    {
        var resultado = await _servicio.ListarAsync(page, pageSize, CancellationToken.None);

        Assert.Equal(EstadoResultado.Invalido, resultado.Estado);
    }

    [Fact]
    public async Task Listar_calcula_el_offset_segun_la_pagina()
    {
        _clientes.Setup(r => r.ListarAsync(20, 10, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Cliente>());
        _clientes.Setup(r => r.ContarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(25);

        var resultado = await _servicio.ListarAsync(3, 10, CancellationToken.None);

        Assert.Equal(25, resultado.Valor!.Total);
        _clientes.Verify(r => r.ListarAsync(20, 10, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class CuentasServiceTests
{
    private readonly Mock<ICuentaRepository> _cuentas = new();
    private readonly Mock<IClienteRepository> _clientes = new();

    [Fact]
    public async Task Hasta_es_inclusivo_se_consulta_hasta_el_inicio_del_dia_siguiente()
    {
        _cuentas.Setup(r => r.ObtenerPorIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Cuenta { Id = 1, IdCliente = 1, Numero = "1", CodSucursal = "0001" });
        _clientes.Setup(r => r.ObtenerPorIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Cliente { Id = 1, Nombre = "Juan", Cuit = "20123456786" });
        _cuentas.Setup(r => r.ListarMovimientosAsync(1, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Movimiento>());

        var servicio = new CuentasService(_cuentas.Object, _clientes.Object);
        await servicio.ObtenerConMovimientosAsync(1, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), CancellationToken.None);

        _cuentas.Verify(r => r.ListarMovimientosAsync(1,
            new DateTime(2026, 10, 1, 0, 0, 0),
            new DateTime(2026, 10, 3, 0, 0, 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Desde_posterior_a_hasta_es_invalido()
    {
        var servicio = new CuentasService(_cuentas.Object, _clientes.Object);

        var resultado = await servicio.ObtenerConMovimientosAsync(1, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 1), CancellationToken.None);

        Assert.Equal(EstadoResultado.Invalido, resultado.Estado);
    }

    [Fact]
    public async Task Cuenta_inexistente_devuelve_NoEncontrado()
    {
        _cuentas.Setup(r => r.ObtenerPorIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Cuenta?)null);
        var servicio = new CuentasService(_cuentas.Object, _clientes.Object);

        var resultado = await servicio.ObtenerConMovimientosAsync(99, null, null, CancellationToken.None);

        Assert.Equal(EstadoResultado.NoEncontrado, resultado.Estado);
    }
}
