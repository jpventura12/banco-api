namespace Banco.Application.Dtos;

public sealed record PaginaDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record ClienteResumenDto(int Id, string Nombre, string? Apellido, string Cuit);

public sealed record CuentaResumenDto(int Id, string Numero, string CodSucursal, decimal Saldo);

public sealed record ClienteDetalleDto(
    int Id,
    string Nombre,
    string? Apellido,
    string Cuit,
    IReadOnlyList<CuentaResumenDto> Cuentas,
    SituacionBcraDto SituacionBcra);

public sealed record MovimientoDto(int Id, DateTime Fecha, string Tipo, string? Descripcion, decimal Importe);

public sealed record CuentaDetalleDto(
    int Id,
    string Numero,
    string CodSucursal,
    decimal Saldo,
    ClienteResumenDto Titular,
    IReadOnlyList<MovimientoDto> Movimientos);

public sealed record SituacionBcraDto(
    string Estado,
    string? Fuente,
    bool Desactualizado,
    DateTimeOffset? ConsultadoEn,
    string? Detalle,
    string? Denominacion,
    string? Periodo,
    int? PeorSituacion,
    string? DescripcionPeorSituacion,
    decimal DeudaTotalPesos,
    IReadOnlyList<DeudaEntidadDto> Entidades);

public sealed record DeudaEntidadDto(
    string Entidad,
    string? Periodo,
    int Situacion,
    string DescripcionSituacion,
    decimal MontoPesos,
    int? DiasAtrasoPago,
    DateOnly? FechaSituacion1,
    bool Refinanciaciones,
    bool RecategorizacionObligatoria,
    bool SituacionJuridica,
    bool IrrecuperablePorDisposicionTecnica,
    bool EnRevision,
    bool ProcesoJudicial);
