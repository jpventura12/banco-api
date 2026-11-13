using System.Text.RegularExpressions;
using Banco.Application.Abstractions;
using Serilog.Context;

namespace Banco.Api.Middleware;

public sealed partial class CorrelationIdMiddleware(
    RequestDelegate next,
    ICorrelationIdAccessor accessor,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string Header = "X-Correlation-ID";

    public const string PropiedadLog = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var entrante = context.Request.Headers[Header].ToString();

        var correlationId = EsValido(entrante) ? entrante : Guid.NewGuid().ToString();

        accessor.CorrelationId = correlationId;
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(PropiedadLog, correlationId))
        {
            if (!string.IsNullOrEmpty(entrante) && entrante != correlationId)
            {
                logger.LogWarning("X-Correlation-ID entrante inválido; se generó uno nuevo");
            }

            await next(context);
        }
    }

    private static bool EsValido(string valor) => valor.Length is > 0 and <= 64 && FormatoValido().IsMatch(valor);

    [GeneratedRegex("^[A-Za-z0-9._:-]+$")]
    private static partial Regex FormatoValido();
}
