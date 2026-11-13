using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Authentication;
using System.Text.Json;
using Banco.Application.Abstractions;
using Banco.Domain.Bcra;
using Banco.Domain.ValueObjects;
using Banco.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Banco.Infrastructure.Bcra;

public sealed class BcraHttpClient(HttpClient http, IOptions<BcraOptions> options, ILogger<BcraHttpClient> logger) : IBcraClient
{
    public const string NombreCliente = "bcra";

    public async Task<SituacionCrediticia?> ConsultarDeudasAsync(Cuit cuit, CancellationToken ct)
    {
        var ruta = string.Format(CultureInfo.InvariantCulture, options.Value.DeudasPath, cuit.Valor).TrimStart('/');

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.GetAsync(ruta, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (BrokenCircuitException ex)
        {
            throw new BcraNoDisponibleException("Circuito abierto: no se consulta al BCRA temporalmente.", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            throw new BcraNoDisponibleException("Timeout consultando al BCRA.", ex);
        }
        catch (HttpRequestException ex) when (ex.GetBaseException() is AuthenticationException)
        {
            throw new BcraNoDisponibleException(
                $"Certificado TLS del BCRA rechazado: {DiagnosticoTls.UltimoRechazo ?? Excepciones.Describir(ex)}. Ver README > Problemas conocidos.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new BcraNoDisponibleException($"Error de comunicación con el BCRA: {Excepciones.Describir(ex)}", ex);
        }
        catch (OperationCanceledException ex)
        {
            throw new BcraNoDisponibleException("La consulta al BCRA fue cancelada por timeout.", ex);
        }

        using (respuesta)
        {
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return await InterpretarNotFoundAsync(cuit, respuesta, ct);
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                var errores = await LeerErroresAsync(respuesta, ct);
                throw new BcraNoDisponibleException(
                    $"El BCRA respondió {(int)respuesta.StatusCode}: {errores}");
            }

            BcraRespuesta<BcraDeudasResultado>? cuerpo;
            try
            {
                cuerpo = await respuesta.Content.ReadFromJsonAsync<BcraRespuesta<BcraDeudasResultado>>(BcraJson.Opciones, ct);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                throw new BcraNoDisponibleException("La respuesta del BCRA no tiene el formato esperado.", ex);
            }

            if (cuerpo?.Results is null)
            {
                throw new BcraNoDisponibleException("La respuesta del BCRA no trae 'results'.");
            }

            return BcraMapper.ADominio(cuit, cuerpo.Results);
        }
    }

    private async Task<SituacionCrediticia?> InterpretarNotFoundAsync(Cuit cuit, HttpResponseMessage respuesta, CancellationToken ct)
    {
        BcraRespuesta<JsonElement>? cuerpo = null;
        try
        {
            cuerpo = await respuesta.Content.ReadFromJsonAsync<BcraRespuesta<JsonElement>>(BcraJson.Opciones, ct);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
        }

        if (cuerpo?.Status == (int)HttpStatusCode.NotFound)
        {
            logger.LogInformation("BCRA sin registros para el CUIT {Cuit}", cuit.Valor);
            return null;
        }

        throw new BcraNoDisponibleException(
            $"El BCRA respondió 404 sin el formato esperado: revisar BcraOptions:DeudasPath ('{options.Value.DeudasPath}').");
    }

    private static async Task<string> LeerErroresAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        try
        {
            var cuerpo = await respuesta.Content.ReadFromJsonAsync<BcraRespuesta<JsonElement>>(BcraJson.Opciones, ct);

            if (cuerpo?.ErrorMessages is { Count: > 0 } mensajes)
            {
                return string.Join(" | ", mensajes);
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
        }

        return respuesta.ReasonPhrase ?? "sin detalle";
    }
}
