namespace Banco.Infrastructure.Bcra;

internal static class Excepciones
{
    public static string Describir(Exception ex)
    {
        var mensajes = new List<string>();

        for (var actual = ex; actual is not null; actual = actual.InnerException)
        {
            if (!mensajes.Contains(actual.Message))
            {
                mensajes.Add(actual.Message);
            }
        }

        return string.Join(" -> ", mensajes);
    }
}
