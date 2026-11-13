namespace Banco.Domain.Entities;

public sealed class Cuenta
{
    public int Id { get; init; }

    public int IdCliente { get; init; }

    public string Numero { get; init; } = string.Empty;

    public string CodSucursal { get; init; } = string.Empty;

    public decimal Saldo { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}
