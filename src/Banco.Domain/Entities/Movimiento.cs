namespace Banco.Domain.Entities;

public sealed class Movimiento
{
    public int Id { get; init; }

    public int IdCuenta { get; init; }

    public DateTime Fecha { get; init; }

    public string Tipo { get; init; } = string.Empty;

    public string? Descripcion { get; init; }

    public decimal Importe { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public bool EsCredito => Importe >= 0;
}
