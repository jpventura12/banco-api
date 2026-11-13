using System.Globalization;
using Banco.Domain.Bcra;
using Banco.Domain.ValueObjects;

namespace Banco.Infrastructure.Bcra;

internal static class BcraMapper
{
    public static SituacionCrediticia ADominio(Cuit cuit, BcraDeudasResultado resultado)
    {
        var periodos = (resultado.Periodos ?? [])
            .Where(p => p is not null)
            .OrderByDescending(p => p.Periodo ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        var entidades = periodos

            .SelectMany(p => (p.Entidades ?? []).Where(e => e is not null).Select(e => (p.Periodo, Entidad: e)))

            .DistinctBy(x => string.IsNullOrWhiteSpace(x.Entidad.Entidad) ? Guid.NewGuid().ToString() : x.Entidad.Entidad.Trim(),
                StringComparer.OrdinalIgnoreCase)

            .Select(x => (x.Periodo, e: x.Entidad))

            .Select(x => DeudaEntidad.DesdeInforme(
                entidad: string.IsNullOrWhiteSpace(x.e.Entidad) ? "(sin nombre)" : x.e.Entidad,
                situacion: x.e.Situacion,
                montoEnMiles: x.e.Monto,
                diasAtrasoPago: x.e.DiasAtrasoPago,
                fechaSituacion1: ParsearFecha(x.e.FechaSit1),
                refinanciaciones: x.e.Refinanciaciones,
                recategorizacionObligatoria: x.e.RecategorizacionOblig,
                situacionJuridica: x.e.SituacionJuridica,
                irrecuperablePorDisposicionTecnica: x.e.IrrecDisposicionTecnica,
                enRevision: x.e.EnRevision,
                procesoJudicial: x.e.ProcesoJud,
                periodo: x.Periodo));

        return new SituacionCrediticia(cuit, resultado.Denominacion, periodos.FirstOrDefault()?.Periodo, entidades);
    }

    private static DateOnly? ParsearFecha(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        return DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? DateOnly.FromDateTime(fecha)
            : null;
    }
}
