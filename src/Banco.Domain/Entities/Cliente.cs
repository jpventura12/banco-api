namespace Banco.Domain.Entities;

public sealed class Cliente
{
    public int Id { get; init; }

    public string Nombre { get; init; } = string.Empty;

    public string? Apellido { get; init; }

    public string Cuit { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}
