using Banco.Application.Abstractions;
using Banco.Application.Common;
using Banco.Application.Dtos;
using Banco.Application.Mapping;

namespace Banco.Application.Services;

public sealed class CuentasService(ICuentaRepository cuentas, IClienteRepository clientes)
{
    public async Task<Resultado<CuentaDetalleDto>> ObtenerConMovimientosAsync(int id, DateOnly? desde, DateOnly? hasta, CancellationToken ct)
    {
        if (desde is not null && hasta is not null && desde > hasta)
        {
            return Resultado<CuentaDetalleDto>.Invalido("'desde' no puede ser posterior a 'hasta'.");
        }

        var cuenta = await cuentas.ObtenerPorIdAsync(id, ct);
        if (cuenta is null)
        {
            return Resultado<CuentaDetalleDto>.NoEncontrado($"No existe la cuenta {id}.");
        }

        var titular = await clientes.ObtenerPorIdAsync(cuenta.IdCliente, ct)
            ?? throw new InvalidOperationException($"La cuenta {id} referencia al cliente inexistente {cuenta.IdCliente}.");

        var movimientos = await cuentas.ListarMovimientosAsync(
            cuenta.Id,
            desde?.ToDateTime(TimeOnly.MinValue),
            hasta?.AddDays(1).ToDateTime(TimeOnly.MinValue),
            ct);

        return Resultado<CuentaDetalleDto>.Ok(new CuentaDetalleDto(
            cuenta.Id,
            cuenta.Numero,
            cuenta.CodSucursal,
            cuenta.Saldo,
            titular.ADto(),
            movimientos.Select(m => m.ADto()).ToList()));
    }
}
