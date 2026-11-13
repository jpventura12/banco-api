using Banco.Application.Abstractions;
using Banco.Application.Common;
using Banco.Application.Dtos;
using Banco.Application.Mapping;
using Banco.Domain.ValueObjects;

namespace Banco.Application.Services;

public sealed class ClientesService(
    IClienteRepository clientes,
    ICuentaRepository cuentas,
    ISituacionCrediticiaProvider situacionCrediticia)
{
    public const int PageSizeMaximo = 100;

    public async Task<Resultado<PaginaDto<ClienteResumenDto>>> ListarAsync(int page, int pageSize, CancellationToken ct)
    {
        if (page < 1)
        {
            return Resultado<PaginaDto<ClienteResumenDto>>.Invalido("page debe ser mayor o igual a 1.");
        }

        if (pageSize is < 1 or > PageSizeMaximo)
        {
            return Resultado<PaginaDto<ClienteResumenDto>>.Invalido($"pageSize debe estar entre 1 y {PageSizeMaximo}.");
        }

        var items = await clientes.ListarAsync((page - 1) * pageSize, pageSize, ct);

        var total = await clientes.ContarAsync(ct);

        return Resultado<PaginaDto<ClienteResumenDto>>.Ok(
            new PaginaDto<ClienteResumenDto>(items.Select(c => c.ADto()).ToList(), page, pageSize, total));
    }

    public async Task<Resultado<ClienteDetalleDto>> ObtenerPorCuitAsync(string cuitTexto, CancellationToken ct)
    {
        if (!Cuit.TryCrear(cuitTexto, out var cuit, out var error))
        {
            return Resultado<ClienteDetalleDto>.Invalido(error);
        }

        var cliente = await clientes.ObtenerPorCuitAsync(cuit.Valor, ct);
        if (cliente is null)
        {
            return Resultado<ClienteDetalleDto>.NoEncontrado($"No existe un cliente con CUIT {cuit.Valor}.");
        }

        var cuentasCliente = await cuentas.ListarPorClienteAsync(cliente.Id, ct);

        var bcra = await situacionCrediticia.ObtenerAsync(cuit, ct);

        return Resultado<ClienteDetalleDto>.Ok(new ClienteDetalleDto(
            cliente.Id,
            cliente.Nombre,
            cliente.Apellido,
            cliente.Cuit,
            cuentasCliente.Select(c => c.ADto()).ToList(),
            bcra.ADto()));
    }
}
