namespace Banco.Domain.Bcra;

public static class SituacionDeudor
{
    public const int Normal = 1;

    public const int Irrecuperable = 5;

    public static bool EsValida(int situacion) => situacion is >= Normal and <= Irrecuperable;

    public static string Describir(int situacion) => situacion switch
    {
        1 => "Normal",
        2 => "Seguimiento especial / Riesgo bajo",
        3 => "Con problemas / Riesgo medio",
        4 => "Alto riesgo de insolvencia / Riesgo alto",
        5 => "Irrecuperable",
        _ => $"Desconocida ({situacion})"
    };
}
