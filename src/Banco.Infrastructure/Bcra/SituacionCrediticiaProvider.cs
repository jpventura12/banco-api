using System.Collections.Concurrent;
using Banco.Application.Abstractions;
using Banco.Domain.Bcra;
using Banco.Domain.ValueObjects;
using Banco.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Banco.Infrastructure.Bcra;

internal sealed record EntradaCacheBcra(SituacionCrediticia? Situacion, DateTimeOffset ObtenidoEn);

public sealed class BcraCacheLocks
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public SemaphoreSlim Obtener(string clave) => _locks.GetOrAdd(clave, _ => new SemaphoreSlim(1, 1));
}

public sealed class SituacionCrediticiaProvider(
    IBcraClient bcra,
    IMemoryCache cache,
    BcraCacheLocks locks,
    IOptions<CacheOptions> options,
    TimeProvider timeProvider,
    ILogger<SituacionCrediticiaProvider> logger) : ISituacionCrediticiaProvider
{
    public static string ClaveCache(Cuit cuit) => $"bcra:deudas:{cuit.Valor}";

    public async Task<ResultadoSituacionCrediticia> ObtenerAsync(Cuit cuit, CancellationToken ct)
    {
        var clave = ClaveCache(cuit);

        if (TryObtenerFresca(clave, loguear: true, out var fresca))
        {
            return Desde(fresca, FuenteDatoBcra.Cache, desactualizado: false);
        }

        var semaforo = locks.Obtener(clave);
        await semaforo.WaitAsync(ct);
        try
        {
            if (TryObtenerFresca(clave, loguear: false, out fresca))
            {
                logger.LogInformation("Cache HIT {CacheKey} (cargada por un request concurrente)", clave);
                return Desde(fresca, FuenteDatoBcra.Cache, desactualizado: false);
            }

            try
            {
                var situacion = await bcra.ConsultarDeudasAsync(cuit, ct);
                var entrada = new EntradaCacheBcra(situacion, timeProvider.GetUtcNow());

                cache.Set(clave, entrada, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TiempoDeRetencion(),
                    Size = 1
                });
                logger.LogInformation("Cache SET {CacheKey} (vigencia {Vigencia})", clave, Vigencia(entrada));

                return Desde(entrada, FuenteDatoBcra.Bcra, desactualizado: false);
            }
            catch (BcraNoDisponibleException ex)
            {
                if (cache.TryGetValue(clave, out EntradaCacheBcra? vencida) && vencida is not null)
                {
                    logger.LogWarning(
                        "Fallback: BCRA no disponible ({Motivo}). Se devuelve dato VENCIDO de cache {CacheKey} obtenido el {ObtenidoEn:O}",
                        ex.Message, clave, vencida.ObtenidoEn);

                    return Desde(vencida, FuenteDatoBcra.CacheVencido, desactualizado: true) with { Detalle = ex.Message };
                }

                logger.LogError("Fallback: BCRA no disponible ({Motivo}) y sin dato en cache para {CacheKey}", ex.Message, clave);
                return ResultadoSituacionCrediticia.NoDisponible(ex.Message);
            }
        }
        finally
        {
            semaforo.Release();
        }
    }

    private bool TryObtenerFresca(string clave, bool loguear, out EntradaCacheBcra entrada)
    {
        if (cache.TryGetValue(clave, out EntradaCacheBcra? encontrada) && encontrada is not null)
        {
            if (timeProvider.GetUtcNow() - encontrada.ObtenidoEn < Vigencia(encontrada))
            {
                if (loguear)
                {
                    logger.LogInformation("Cache HIT {CacheKey}", clave);
                }

                entrada = encontrada;
                return true;
            }

            if (loguear)
            {
                logger.LogInformation("Cache STALE {CacheKey}: vencida, se consulta al BCRA", clave);
            }
        }
        else if (loguear)
        {
            logger.LogInformation("Cache MISS {CacheKey}", clave);
        }

        entrada = null!;
        return false;
    }

    private TimeSpan Vigencia(EntradaCacheBcra entrada) => TimeSpan.FromSeconds(
        entrada.Situacion is null ? options.Value.BcraNotFoundExpirationSeconds : options.Value.BcraExpirationSeconds);

    private TimeSpan TiempoDeRetencion()
    {
        var o = options.Value;
        var segundos = Math.Max(o.BcraStaleRetentionSeconds, Math.Max(o.BcraExpirationSeconds, o.BcraNotFoundExpirationSeconds));
        return TimeSpan.FromSeconds(segundos);
    }

    private static ResultadoSituacionCrediticia Desde(EntradaCacheBcra entrada, FuenteDatoBcra fuente, bool desactualizado) => new()
    {
        Estado = entrada.Situacion is null ? EstadoConsultaBcra.SinRegistros : EstadoConsultaBcra.Ok,
        Fuente = fuente,
        Desactualizado = desactualizado,
        ConsultadoEn = entrada.ObtenidoEn,
        Situacion = entrada.Situacion
    };
}
