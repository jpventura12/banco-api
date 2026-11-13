using Banco.Infrastructure.Bcra;
using Banco.Infrastructure.Options;
using Banco.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;

namespace Banco.Infrastructure.HealthChecks;

public sealed class SqliteHealthCheck(SqliteConnectionFactory connectionFactory, IOptions<DatabaseOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var esperada = options.Value.ExpectedSchemaVersion;
        try
        {
            await using var conexion = connectionFactory.Crear();
            await conexion.OpenAsync(cancellationToken);

            await using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT MAX(version) FROM SchemaVersion";
            var valor = await comando.ExecuteScalarAsync(cancellationToken);

            int? version = valor is null or DBNull ? null : Convert.ToInt32(valor);

            var data = new Dictionary<string, object>
            {
                ["versionEsquema"] = version?.ToString() ?? "sin versión",
                ["versionEsperada"] = esperada
            };

            return version >= esperada
                ? HealthCheckResult.Healthy("Base de datos accesible", data)
                : HealthCheckResult.Unhealthy($"Versión de esquema {version?.ToString() ?? "inexistente"}; se espera {esperada}. Ejecutar database/masterSQL.sql.", data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("No se puede acceder a la base de datos", ex);
        }
    }
}

public sealed class BcraCircuitHealthCheck(BcraCircuitBreakerState estado) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var circuito = estado.Provider.CircuitState;
        var data = new Dictionary<string, object> { ["circuito"] = circuito.ToString() };

        var resultado = circuito switch
        {
            CircuitState.Closed => HealthCheckResult.Healthy("Circuito cerrado: el BCRA responde normalmente", data),
            CircuitState.HalfOpen => HealthCheckResult.Degraded("Circuito semiabierto: probando si el BCRA se recuperó", data: data),
            CircuitState.Open => HealthCheckResult.Degraded("Circuito abierto: no se consulta al BCRA, se usa cache/fallback", data: data),
            _ => HealthCheckResult.Degraded($"Circuito {circuito}", data: data)
        };

        return Task.FromResult(resultado);
    }
}

public sealed class MemoryCacheHealthCheck(IMemoryCache cache) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var stats = cache.GetCurrentStatistics();
        var data = new Dictionary<string, object>
        {
            ["entradas"] = stats?.CurrentEntryCount ?? 0,
            ["hits"] = stats?.TotalHits ?? 0,
            ["misses"] = stats?.TotalMisses ?? 0
        };

        return Task.FromResult(HealthCheckResult.Healthy("MemoryCache operativo", data));
    }
}
