namespace Banco.Domain.Bcra;

public sealed record DeudaEntidad
{
    public const decimal FactorMilesAPesos = 1000m;

    public required string Entidad { get; init; }

    public required int Situacion { get; init; }

    public required decimal MontoPesos { get; init; }

    public string? Periodo { get; init; }

    public int? DiasAtrasoPago { get; init; }

    public DateOnly? FechaSituacion1 { get; init; }

    public bool Refinanciaciones { get; init; }

    public bool RecategorizacionObligatoria { get; init; }

    public bool SituacionJuridica { get; init; }

    public bool IrrecuperablePorDisposicionTecnica { get; init; }

    public bool EnRevision { get; init; }

    public bool ProcesoJudicial { get; init; }

    public string DescripcionSituacion => SituacionDeudor.Describir(Situacion);

    public static DeudaEntidad DesdeInforme(
        string entidad,
        int situacion,
        decimal montoEnMiles,
        int diasAtrasoPago,
        DateOnly? fechaSituacion1 = null,
        bool refinanciaciones = false,
        bool recategorizacionObligatoria = false,
        bool situacionJuridica = false,
        bool irrecuperablePorDisposicionTecnica = false,
        bool enRevision = false,
        bool procesoJudicial = false,
        string? periodo = null) => new()
    {
        Entidad = entidad.Trim(),
        Situacion = situacion,
        MontoPesos = montoEnMiles * FactorMilesAPesos,
        Periodo = periodo,
        DiasAtrasoPago = diasAtrasoPago > 0 ? diasAtrasoPago : null,
        FechaSituacion1 = fechaSituacion1,
        Refinanciaciones = refinanciaciones,
        RecategorizacionObligatoria = recategorizacionObligatoria,
        SituacionJuridica = situacionJuridica,
        IrrecuperablePorDisposicionTecnica = irrecuperablePorDisposicionTecnica,
        EnRevision = enRevision,
        ProcesoJudicial = procesoJudicial
    };
}
