using Banco.Application.Abstractions;
using Banco.Infrastructure.Bcra;
using Banco.Infrastructure.HealthChecks;
using Banco.Infrastructure.Options;
using Banco.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Banco.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IPostConfigureOptions<DatabaseOptions>, ResolverRutaSqlite>();

        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<SqlExecutor>();

        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<ICuentaRepository, CuentaRepository>();

        services.AddBcraCache(configuration);
        services.AddBcraClient(configuration);

        services.AddHealthChecks()
            .AddCheck<SqliteHealthCheck>("sqlite", failureStatus: HealthStatus.Unhealthy, tags: ["db"])
            .AddCheck<BcraCircuitHealthCheck>("bcra-circuit", failureStatus: HealthStatus.Degraded, tags: ["bcra"])
            .AddCheck<MemoryCacheHealthCheck>("memory-cache", failureStatus: HealthStatus.Degraded, tags: ["cache"]);

        return services;
    }

    public static IServiceCollection AddBcraCache(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CacheOptions>()
            .Bind(configuration.GetSection(CacheOptions.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddMemoryCache();

        services.AddOptions<MemoryCacheOptions>()
            .Configure<IOptions<CacheOptions>>((memoria, cache) =>
            {
                memoria.TrackStatistics = true;
                memoria.SizeLimit = cache.Value.SizeLimit;
            });

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<BcraCacheLocks>();

        services.AddScoped<ISituacionCrediticiaProvider, SituacionCrediticiaProvider>();

        return services;
    }

    public static IHttpClientBuilder AddBcraClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BcraOptions>()
            .Bind(configuration.GetSection(BcraOptions.Seccion))
            .ValidateDataAnnotations()
            .Validate(o => o.TotalTimeoutSeconds >= o.TimeoutSeconds,
                "BcraOptions:TotalTimeoutSeconds debe ser mayor o igual a TimeoutSeconds.")
            .Validate(o => o.DeudasPath.Contains("{0}", StringComparison.Ordinal),
                "BcraOptions:DeudasPath debe contener {0} (lugar del CUIT).")
            .ValidateOnStart();

        services.TryAddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
        services.TryAddSingleton<BcraCircuitBreakerState>();
        services.AddTransient<CorrelationIdHandler>();
        services.AddTransient<BcraLoggingHandler>();

        var builder = services.AddHttpClient<IBcraClient, BcraHttpClient>(BcraHttpClient.NombreCliente, (sp, http) =>
        {
            var opciones = sp.GetRequiredService<IOptions<BcraOptions>>().Value;

            http.BaseAddress = new Uri(opciones.BaseAddress.EndsWith('/') ? opciones.BaseAddress : opciones.BaseAddress + "/");

            http.Timeout = Timeout.InfiniteTimeSpan;
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        builder.ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            SslOptions =
            {
                RemoteCertificateValidationCallback = DiagnosticoTls.Crear(
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger("Banco.Infrastructure.Bcra.Tls"))
            }
        });

        builder.AddResilienceHandler(BcraResiliencia.NombrePipeline, BcraResiliencia.Configurar);
        builder.AddHttpMessageHandler<CorrelationIdHandler>();
        builder.AddHttpMessageHandler<BcraLoggingHandler>();

        return builder;
    }
}
