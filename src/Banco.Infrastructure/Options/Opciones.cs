using System.ComponentModel.DataAnnotations;

namespace Banco.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string Seccion = "DatabaseOptions";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ExpectedSchemaVersion { get; set; } = 1;
}

public sealed class BcraOptions
{
    public const string Seccion = "BcraOptions";

    [Required, Url]
    public string BaseAddress { get; set; } = "https://api.bcra.gob.ar/";

    [Required]
    public string DeudasPath { get; set; } = "CentralDeDeudores/v1.0/Deudas/{0}";

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 5;

    [Range(1, 600)]
    public int TotalTimeoutSeconds { get; set; } = 30;

    [Range(1, 10)]
    public int RetryCount { get; set; } = 3;

    [Range(0, 60_000)]
    public int RetryBaseDelayMs { get; set; } = 200;

    [Range(2, 1000)]
    public int CircuitBreakerFailures { get; set; } = 5;

    [Range(1, 3600)]
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    [Range(1, 3600)]
    public int CircuitBreakerSamplingSeconds { get; set; } = 60;

    public bool LogResponseBody { get; set; } = true;
}

public sealed class CacheOptions
{
    public const string Seccion = "CacheOptions";

    [Range(1, int.MaxValue)]
    public int BcraExpirationSeconds { get; set; } = 3600;

    [Range(1, int.MaxValue)]
    public int BcraNotFoundExpirationSeconds { get; set; } = 600;

    [Range(0, int.MaxValue)]
    public int BcraStaleRetentionSeconds { get; set; } = 86_400;

    [Range(1, 10_000_000)]
    public int SizeLimit { get; set; } = 10_000;
}
