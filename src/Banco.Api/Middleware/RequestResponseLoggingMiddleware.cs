using System.Diagnostics;
using System.Text;

namespace Banco.Api.Middleware;

public sealed class RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
{
    private const int MaxBody = 4096;

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        logger.LogInformation("HTTP --> {Method} {Path}{QueryString}", request.Method, request.Path, request.QueryString);
        var sw = Stopwatch.StartNew();

        if (!logger.IsEnabled(LogLevel.Debug))
        {
            try
            {
                await next(context);
            }
            finally
            {
                LoguearRespuesta(context, sw);
            }

            return;
        }

        var bodyOriginal = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = bodyOriginal;
            buffer.Position = 0;

            var cuerpo = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)Math.Min(buffer.Length, MaxBody));
            logger.LogDebug("HTTP <-- body: {Body}{Truncado}", cuerpo, buffer.Length > MaxBody ? "…(truncado)" : string.Empty);

            buffer.Position = 0;
            await buffer.CopyToAsync(bodyOriginal);
            LoguearRespuesta(context, sw);
        }
    }

    private void LoguearRespuesta(HttpContext context, Stopwatch sw)
    {
        var status = context.Response.StatusCode;
        var nivel = status >= 500 ? LogLevel.Error : status >= 400 ? LogLevel.Warning : LogLevel.Information;
        logger.Log(nivel, "HTTP <-- {StatusCode} {Method} {Path} en {ElapsedMs} ms",
            status, context.Request.Method, context.Request.Path, sw.ElapsedMilliseconds);
    }
}
