using System.Diagnostics;
using Banco.Application.Common;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Banco.Api.Infrastructure;

internal static class HttpResultados
{
    public static IResult AHttp<T>(this Resultado<T> resultado, HttpContext context) => resultado.Estado switch
    {
        EstadoResultado.Ok => TypedResults.Ok(resultado.Valor),

        EstadoResultado.Invalido => Problema(context, StatusCodes.Status400BadRequest, "Solicitud inválida", resultado.Error),

        EstadoResultado.NoEncontrado => Problema(context, StatusCodes.Status404NotFound, "No encontrado", resultado.Error),

        _ => throw new UnreachableException($"Estado no contemplado: {resultado.Estado}")
    };

    private static IResult Problema(HttpContext context, int status, string titulo, string? detalle) =>
        TypedResults.Problem(
            detail: detalle,
            statusCode: status,
            title: titulo,
            extensions: new Dictionary<string, object?> { ["correlationId"] = context.TraceIdentifier });
}

internal static class HealthResponseWriter
{
    public static Task EscribirAsync(HttpContext context, HealthReport reporte) =>

        context.Response.WriteAsJsonAsync(new
        {
            status = reporte.Status.ToString(),
            totalDurationMs = Math.Round(reporte.TotalDuration.TotalMilliseconds, 1),
            correlationId = context.TraceIdentifier,
            checks = reporte.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                error = e.Value.Exception?.Message,
                data = e.Value.Data
            })
        });
}
