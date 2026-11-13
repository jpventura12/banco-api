using System.Diagnostics;
using Banco.Application.Abstractions;
using Banco.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Banco.Infrastructure.Bcra;

public sealed class CorrelationIdHandler(ICorrelationIdAccessor correlationId) : DelegatingHandler
{
    public const string Header = "X-Correlation-ID";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (correlationId.CorrelationId is { Length: > 0 } id)
        {
            request.Headers.Remove(Header);
            request.Headers.TryAddWithoutValidation(Header, id);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

public sealed class BcraLoggingHandler(IOptions<BcraOptions> options, ILogger<BcraLoggingHandler> logger) : DelegatingHandler
{
    private const int MaxBody = 2000;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        logger.LogInformation("BCRA --> {Method} {Url}", request.Method, request.RequestUri);
        var sw = Stopwatch.StartNew();

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await base.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "BCRA x-- {Method} {Url} falló tras {ElapsedMs} ms: {Error}",
                request.Method, request.RequestUri, sw.ElapsedMilliseconds, $"{ex.GetType().Name}: {Excepciones.Describir(ex)}");
            throw;
        }

        var nivel = (int)respuesta.StatusCode >= 500 ? LogLevel.Warning : LogLevel.Information;
        logger.Log(nivel,
            "BCRA <-- {StatusCode} {Method} {Url} en {ElapsedMs} ms",
            (int)respuesta.StatusCode, request.Method, request.RequestUri, sw.ElapsedMilliseconds);

        if (options.Value.LogResponseBody)
        {
            await respuesta.Content.LoadIntoBufferAsync();
            var body = await respuesta.Content.ReadAsStringAsync(cancellationToken);
            logger.LogInformation("BCRA <-- body: {Body}", body.Length > MaxBody ? body[..MaxBody] + "…(truncado)" : body);
        }

        return respuesta;
    }
}
