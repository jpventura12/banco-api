using Banco.Domain.Entities;

namespace Banco.Application.Abstractions;

public interface IClienteRepository
{
    Task<IReadOnlyList<Cliente>> ListarAsync(int skip, int take, CancellationToken ct);

    Task<int> ContarAsync(CancellationToken ct);

    Task<Cliente?> ObtenerPorCuitAsync(string cuit, CancellationToken ct);

    Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken ct);
}

public interface ICuentaRepository
{
    Task<IReadOnlyList<Cuenta>> ListarPorClienteAsync(int idCliente, CancellationToken ct);

    Task<Cuenta?> ObtenerPorIdAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<Movimiento>> ListarMovimientosAsync(int idCuenta, DateTime? desde, DateTime? hastaExclusive, CancellationToken ct);
}
