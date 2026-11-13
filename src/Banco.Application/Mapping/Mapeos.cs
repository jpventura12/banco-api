using Banco.Application.Abstractions;
using Banco.Application.Dtos;
using Banco.Domain.Bcra;
using Banco.Domain.Entities;

namespace Banco.Application.Mapping;

public static class Mapeos
{
    public static ClienteResumenDto ADto(this Cliente c) => new(c.Id, c.Nombre, c.Apellido, c.Cuit);

    public static CuentaResumenDto ADto(this Cuenta c) => new(c.Id, c.Numero, c.CodSucursal, c.Saldo);

    public static MovimientoDto ADto(this Movimiento m) => new(m.Id, m.Fecha, m.Tipo, m.Descripcion, m.Importe);

    public static SituacionBcraDto ADto(this ResultadoSituacionCrediticia r)
    {
        var s = r.Situacion;
        return new SituacionBcraDto(

            Estado: r.Estado.ToString(),

            Fuente: r.Fuente switch
            {
                FuenteDatoBcra.Bcra => "bcra",
                FuenteDatoBcra.Cache => "cache",
                FuenteDatoBcra.CacheVencido => "cache-vencido",
                _ => null
            },
            Desactualizado: r.Desactualizado,
            ConsultadoEn: r.ConsultadoEn,
            Detalle: r.Detalle,
            Denominacion: s?.Denominacion,
            Periodo: s?.Periodo,
            PeorSituacion: s?.PeorSituacion,

            DescripcionPeorSituacion: s?.PeorSituacion is int peor ? SituacionDeudor.Describir(peor) : null,

            DeudaTotalPesos: s?.DeudaTotalPesos ?? 0m,

            Entidades: s?.Entidades.Select(ADto).ToList() ?? []);
    }

    public static DeudaEntidadDto ADto(this DeudaEntidad e) => new(
        e.Entidad,
        e.Periodo,
        e.Situacion,
        e.DescripcionSituacion,
        e.MontoPesos,
        e.DiasAtrasoPago,
        e.FechaSituacion1,
        e.Refinanciaciones,
        e.RecategorizacionObligatoria,
        e.SituacionJuridica,
        e.IrrecuperablePorDisposicionTecnica,
        e.EnRevision,
        e.ProcesoJudicial);
}
