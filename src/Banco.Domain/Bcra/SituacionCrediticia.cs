using Banco.Domain.ValueObjects;

namespace Banco.Domain.Bcra;

public sealed class SituacionCrediticia
{
    public SituacionCrediticia(Cuit cuit, string? denominacion, string? periodo, IEnumerable<DeudaEntidad> entidades)
    {
        Cuit = cuit;

        Denominacion = string.IsNullOrWhiteSpace(denominacion) ? null : denominacion.Trim();
        Periodo = periodo;

        Entidades = entidades.ToList().AsReadOnly();
    }

    public Cuit Cuit { get; }

    public string? Denominacion { get; }

    public string? Periodo { get; }

    public IReadOnlyList<DeudaEntidad> Entidades { get; }

    public int? PeorSituacion => Entidades.Count == 0 ? null : Entidades.Max(e => e.Situacion);

    public decimal DeudaTotalPesos => Entidades.Sum(e => e.MontoPesos);
}
