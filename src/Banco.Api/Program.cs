using System.Reflection;
using Banco.Api.Endpoints;
using Banco.Api.Infrastructure;
using Banco.Api.Middleware;
using Banco.Application.Abstractions;
using Banco.Application.Services;
using Banco.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Serilog;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog(
        (context, services, logger) => logger
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext(),
        preserveStaticLogger: true);

    builder.Services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();

    builder.Services.AddScoped<ClientesService>();
    builder.Services.AddScoped<CuentasService>();

    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddProblemDetails(options =>
        options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier);

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Banco API",
            Version = "v1",
            Description = "Clientes, cuentas y movimientos (SQLite) + situación crediticia de la Central de Deudores del BCRA."
        });

        var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
        if (File.Exists(xml))
        {
            options.IncludeXmlComments(xml);
        }
    });

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<RequestResponseLoggingMiddleware>();
    app.UseExceptionHandler();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapClientesEndpoints();
    app.MapCuentasEndpoints();

    app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.EscribirAsync });

    await VerificarBaseAlIniciarAsync(app);

    await app.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "La API terminó por un error no controlado durante el arranque");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static async Task VerificarBaseAlIniciarAsync(WebApplication app)
{
    var health = app.Services.GetRequiredService<HealthCheckService>();
    var reporte = await health.CheckHealthAsync(check => check.Name == "sqlite");
    var resultado = reporte.Entries["sqlite"];

    if (resultado.Status == HealthStatus.Healthy)
    {
        app.Logger.LogInformation("Base de datos OK: {Detalle}", resultado.Description);
    }
    else
    {
        app.Logger.LogWarning(resultado.Exception,
            "La base de datos no está lista: {Detalle}. Ejecutar database/masterSQL.sql y database/seed.sql (ver README).",
            resultado.Description);
    }
}

public partial class Program;
