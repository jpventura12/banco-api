using Banco.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Banco.Infrastructure.Bcra;

public sealed class BcraCircuitBreakerState
{
    public CircuitBreakerStateProvider Provider { get; } = new();
}

public static class BcraResiliencia
{
    public const string NombrePipeline = "bcra-pipeline";

    public static void Configurar(ResiliencePipelineBuilder<HttpResponseMessage> pipeline, ResilienceHandlerContext context)
    {
        var opciones = context.ServiceProvider.GetRequiredService<IOptions<BcraOptions>>().Value;
        var estadoCircuito = context.ServiceProvider.GetRequiredService<BcraCircuitBreakerState>();
        var logger = context.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Banco.Infrastructure.Bcra.Resiliencia");

        pipeline

            .AddTimeout(new TimeoutStrategyOptions
            {
                Name = "bcra-timeout-total",
                Timeout = TimeSpan.FromSeconds(opciones.TotalTimeoutSeconds),
                OnTimeout = args =>
                {
                    logger.LogWarning("BCRA: se superó el timeout total de {Timeout}", args.Timeout);
                    return default;
                }
            })

            .AddRetry(new HttpRetryStrategyOptions
            {
                Name = "bcra-retry",
                MaxRetryAttempts = opciones.RetryCount,

                Delay = TimeSpan.FromMilliseconds(opciones.RetryBaseDelayMs),
                BackoffType = DelayBackoffType.Exponential,

                UseJitter = true,

                ShouldRetryAfterHeader = true,

                OnRetry = args =>
                {
                    logger.LogWarning(
                        "BCRA: reintento {Intento}/{Maximo} en {Espera} ms. Motivo: {Motivo}",
                        args.AttemptNumber + 1, opciones.RetryCount, (int)args.RetryDelay.TotalMilliseconds, DescribirFalla(args.Outcome));
                    return default;
                }
            })

            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                Name = "bcra-circuit-breaker",

                FailureRatio = 1.0,
                MinimumThroughput = opciones.CircuitBreakerFailures,
                SamplingDuration = TimeSpan.FromSeconds(opciones.CircuitBreakerSamplingSeconds),
                BreakDuration = TimeSpan.FromSeconds(opciones.CircuitBreakerDurationSeconds),

                StateProvider = estadoCircuito.Provider,

                OnOpened = args =>
                {
                    logger.LogError(
                        "BCRA: circuito ABIERTO por {Duracion}. Última falla: {Motivo}",
                        args.BreakDuration, DescribirFalla(args.Outcome));
                    return default;
                },
                OnHalfOpened = _ =>
                {
                    logger.LogWarning("BCRA: circuito SEMIABIERTO, se prueba una llamada");
                    return default;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("BCRA: circuito CERRADO, el servicio respondió bien");
                    return default;
                }
            })

            .AddTimeout(new TimeoutStrategyOptions
            {
                Name = "bcra-timeout-intento",
                Timeout = TimeSpan.FromSeconds(opciones.TimeoutSeconds),
                OnTimeout = args =>
                {
                    logger.LogWarning("BCRA: el intento superó el timeout de {Timeout}", args.Timeout);
                    return default;
                }
            });
    }

    private static string DescribirFalla(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is { } ex
            ? $"{ex.GetType().Name}: {Excepciones.Describir(ex)}"
            : $"HTTP {(int?)outcome.Result?.StatusCode}";
}
