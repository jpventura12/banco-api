using System.Diagnostics.CodeAnalysis;

namespace Banco.Domain.ValueObjects;

public sealed record Cuit
{
    private static readonly int[] Pesos = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    public string Valor { get; }

    private Cuit(string valor) => Valor = valor;

    public static bool TryCrear(string? texto, [NotNullWhen(true)] out Cuit? cuit, [NotNullWhen(false)] out string? error)
    {
        cuit = null;

        if (string.IsNullOrWhiteSpace(texto))
        {
            error = "El CUIT es obligatorio.";
            return false;
        }

        var normalizado = texto.Replace("-", string.Empty).Replace(" ", string.Empty).Trim();

        if (normalizado.Length != 11 || !normalizado.All(char.IsAsciiDigit))
        {
            error = "El CUIT debe tener 11 dígitos (con o sin guiones).";
            return false;
        }

        var verificadorEsperado = CalcularDigitoVerificador(normalizado.AsSpan(0, 10));
        if (verificadorEsperado is null || verificadorEsperado != normalizado[10] - '0')
        {
            error = "El dígito verificador del CUIT es inválido.";
            return false;
        }

        cuit = new Cuit(normalizado);
        error = null;
        return true;
    }

    public static Cuit Crear(string texto) =>
        TryCrear(texto, out var cuit, out var error) ? cuit : throw new ArgumentException(error, nameof(texto));

    internal static int? CalcularDigitoVerificador(ReadOnlySpan<char> primerosDiez)
    {
        var suma = 0;
        for (var i = 0; i < 10; i++)
        {
            suma += (primerosDiez[i] - '0') * Pesos[i];
        }

        var resultado = 11 - (suma % 11);
        return resultado switch
        {
            11 => 0,
            10 => null,
            _ => resultado
        };
    }

    public string Formateado => $"{Valor[..2]}-{Valor[2..10]}-{Valor[10]}";

    public override string ToString() => Valor;
}
