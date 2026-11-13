using Banco.Api.Infrastructure;
using Banco.Application.Dtos;
using Banco.Application.Services;

namespace Banco.Api.Endpoints;

public static class ClientesEndpoints
{
    public static IEndpointRouteBuilder MapClientesEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/clientes").WithTags("Clientes");

        grupo.MapGet("/", ListarAsync)
            .WithName("ListarClientes")
            .WithSummary("Listado paginado de clientes")
            .Produces<PaginaDto<ClienteResumenDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        grupo.MapGet("/{cuit}", ObtenerPorCuitAsync)
            .WithName("ObtenerClientePorCuit")
            .WithSummary("Cliente por CUIT, con sus cuentas y la situación crediticia del BCRA")
            .WithDescription(
                "Acepta el CUIT con o sin guiones. Si el BCRA no responde, devuelve 200 con los datos locales y " +
                "situacionBcra.estado = NoDisponible, o el último dato en cache marcado como desactualizado.")
            .Produces<ClienteDetalleDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ListarAsync(
        ClientesService servicio, HttpContext context, CancellationToken ct, int page = 1, int pageSize = 20) =>
        (await servicio.ListarAsync(page, pageSize, ct)).AHttp(context);

    private static async Task<IResult> ObtenerPorCuitAsync(
        string cuit, ClientesService servicio, HttpContext context, CancellationToken ct) =>
        (await servicio.ObtenerPorCuitAsync(cuit, ct)).AHttp(context);
}

public static class CuentasEndpoints
{
    public static IEndpointRouteBuilder MapCuentasEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/cuentas").WithTags("Cuentas");

        grupo.MapGet("/{id:int}", ObtenerAsync)
            .WithName("ObtenerCuenta")
            .WithSummary("Datos de una cuenta y sus movimientos (más recientes primero)")
            .Produces<CuentaDetalleDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ObtenerAsync(
        int id, CuentasService servicio, HttpContext context, CancellationToken ct, DateOnly? desde = null, DateOnly? hasta = null) =>
        (await servicio.ObtenerConMovimientosAsync(id, desde, hasta, ct)).AHttp(context);
}
