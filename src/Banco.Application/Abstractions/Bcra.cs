using Banco.Domain.Bcra;
using Banco.Domain.ValueObjects;

namespace Banco.Application.Abstractions;

public interface IBcraClient
{
    Task<SituacionCrediticia?> ConsultarDeudasAsync(Cuit cuit, CancellationToken ct);
}

public interface ISituacionCrediticiaProvider
{
    Task<ResultadoSituacionCrediticia> ObtenerAsync(Cuit cuit, CancellationToken ct);
}

public sealed class BcraNoDisponibleException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public enum EstadoConsultaBcra
{
    Ok,

    SinRegistros,

    NoDisponible
}

public enum FuenteDatoBcra
{
    Bcra,

    Cache,

    CacheVencido
}

public sealed record ResultadoSituacionCrediticia
{
    public required EstadoConsultaBcra Estado { get; init; }

    public FuenteDatoBcra? Fuente { get; init; }

    public bool Desactualizado { get; init; }

    public DateTimeOffset? ConsultadoEn { get; init; }

    public SituacionCrediticia? Situacion { get; init; }

    public string? Detalle { get; init; }

    public static ResultadoSituacionCrediticia NoDisponible(string detalle) =>
        new() { Estado = EstadoConsultaBcra.NoDisponible, Detalle = detalle };
}
