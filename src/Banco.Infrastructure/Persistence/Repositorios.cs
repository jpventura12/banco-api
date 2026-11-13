using System.Globalization;
using Banco.Application.Abstractions;
using Banco.Domain.Entities;

namespace Banco.Infrastructure.Persistence;

public sealed class ClienteRepository(SqlExecutor sql) : IClienteRepository
{
    private const string Columnas = "id, nombre, apellido, cuit, created_at, updated_at";

    public Task<IReadOnlyList<Cliente>> ListarAsync(int skip, int take, CancellationToken ct) =>
        sql.QueryAsync<Cliente>(
            $"SELECT {Columnas} FROM Clientes ORDER BY id LIMIT @Take OFFSET @Skip",
            new { Take = take, Skip = skip }, ct);

    public async Task<int> ContarAsync(CancellationToken ct) =>
        await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Clientes", null, ct);

    public Task<Cliente?> ObtenerPorCuitAsync(string cuit, CancellationToken ct) =>
        sql.QuerySingleOrDefaultAsync<Cliente>(
            $"SELECT {Columnas} FROM Clientes WHERE cuit = @Cuit",
            new { Cuit = cuit }, ct);

    public Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken ct) =>
        sql.QuerySingleOrDefaultAsync<Cliente>(
            $"SELECT {Columnas} FROM Clientes WHERE id = @Id",
            new { Id = id }, ct);
}

public sealed class CuentaRepository(SqlExecutor sql) : ICuentaRepository
{
    private const string ColumnasCuenta = "id, idCliente, numero, codSucursal, CAST(saldo AS REAL) AS saldo, created_at, updated_at";
    private const string ColumnasMovimiento = "id, idCuenta, fecha, tipo, descripcion, CAST(importe AS REAL) AS importe, created_at, updated_at";

    private const string FormatoFecha = "yyyy-MM-dd HH:mm:ss";

    public Task<IReadOnlyList<Cuenta>> ListarPorClienteAsync(int idCliente, CancellationToken ct) =>
        sql.QueryAsync<Cuenta>(
            $"SELECT {ColumnasCuenta} FROM Cuentas WHERE idCliente = @IdCliente ORDER BY id",
            new { IdCliente = idCliente }, ct);

    public Task<Cuenta?> ObtenerPorIdAsync(int id, CancellationToken ct) =>
        sql.QuerySingleOrDefaultAsync<Cuenta>(
            $"SELECT {ColumnasCuenta} FROM Cuentas WHERE id = @Id",
            new { Id = id }, ct);

    public Task<IReadOnlyList<Movimiento>> ListarMovimientosAsync(int idCuenta, DateTime? desde, DateTime? hastaExclusive, CancellationToken ct) =>
        sql.QueryAsync<Movimiento>(

            $"""
             SELECT {ColumnasMovimiento}
             FROM Movimientos
             WHERE idCuenta = @IdCuenta
               AND (@Desde IS NULL OR fecha >= @Desde)
               AND (@Hasta IS NULL OR fecha <  @Hasta)
             ORDER BY fecha DESC, id DESC
             """,
            new
            {
                IdCuenta = idCuenta,
                Desde = desde?.ToString(FormatoFecha, CultureInfo.InvariantCulture),
                Hasta = hastaExclusive?.ToString(FormatoFecha, CultureInfo.InvariantCulture)
            }, ct);
}
