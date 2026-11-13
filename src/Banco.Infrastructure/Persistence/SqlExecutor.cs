using System.Diagnostics;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Banco.Infrastructure.Persistence;

public sealed partial class SqlExecutor(SqliteConnectionFactory connectionFactory, ILogger<SqlExecutor> logger)
{
    static SqlExecutor()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parametros, CancellationToken ct) =>
        EjecutarAsync(sql, parametros, ct, async (cn, cmd) =>
        {
            var filas = (await cn.QueryAsync<T>(cmd)).AsList();
            return ((IReadOnlyList<T>)filas, filas.Count);
        });

    public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parametros, CancellationToken ct) =>
        EjecutarAsync(sql, parametros, ct, async (cn, cmd) =>
        {
            var fila = await cn.QuerySingleOrDefaultAsync<T>(cmd);
            return (fila, fila is null ? 0 : 1);
        });

    public Task<T?> ExecuteScalarAsync<T>(string sql, object? parametros, CancellationToken ct) =>
        EjecutarAsync(sql, parametros, ct, async (cn, cmd) =>
        {
            var valor = await cn.ExecuteScalarAsync<T>(cmd);
            return (valor, 1);
        });

    private async Task<TResultado> EjecutarAsync<TResultado>(
        string sql,
        object? parametros,
        CancellationToken ct,
        Func<SqliteConnection, CommandDefinition, Task<(TResultado Resultado, int Filas)>> ejecutar)
    {
        var sqlCompacto = Compactar(sql);
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conexion = connectionFactory.Crear();

            var (resultado, filas) = await ejecutar(conexion, new CommandDefinition(sql, parametros, cancellationToken: ct));

            logger.LogInformation(
                "SQL OK en {ElapsedMs} ms ({Filas} filas): {Sql} | Parámetros: {@Parametros}",
                sw.ElapsedMilliseconds, filas, sqlCompacto, parametros);

            return resultado;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "SQL ERROR tras {ElapsedMs} ms: {Sql} | Parámetros: {@Parametros}",
                sw.ElapsedMilliseconds, sqlCompacto, parametros);
            throw;
        }
    }

    private static string Compactar(string sql) => EspaciosRegex().Replace(sql, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosRegex();
}
