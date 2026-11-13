using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Banco.Infrastructure.Bcra;

internal static class DiagnosticoTls
{
    public static string? UltimoRechazo { get; private set; }

    public static RemoteCertificateValidationCallback Crear(ILogger logger) =>

        (_, certificado, cadena, errores) =>
        {
            if (errores == SslPolicyErrors.None)
            {
                return true;
            }

            var emisor = certificado?.Issuer ?? "(sin certificado)";
            var sujeto = certificado?.Subject ?? "(sin certificado)";

            var elementos = cadena?.ChainElements.Select(e => e.Certificate.Subject) ?? [];

            var problemas = cadena?.ChainStatus.Select(s => $"{s.Status}: {s.StatusInformation.Trim()}") ?? [];

            UltimoRechazo = $"{string.Join(", ", cadena?.ChainStatus.Select(s => s.Status.ToString()) ?? [errores.ToString()])}; emisor {emisor}";

            logger.LogError(
                "TLS: certificado del BCRA rechazado ({Errores}). Sujeto: {Sujeto} | Emisor: {Emisor} | Cadena: {Cadena} | Problemas: {Problemas}",
                errores, sujeto, emisor, string.Join(" -> ", elementos), string.Join(" | ", problemas));

            return false;
        };
}
