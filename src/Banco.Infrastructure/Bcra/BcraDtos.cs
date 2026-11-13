using System.Text.Json;
using System.Text.Json.Serialization;

namespace Banco.Infrastructure.Bcra;

internal sealed class BcraRespuesta<T>
{
    public int Status { get; set; }

    public T? Results { get; set; }

    public List<string>? ErrorMessages { get; set; }
}

internal sealed class BcraDeudasResultado
{
    public long Identificacion { get; set; }

    public string? Denominacion { get; set; }

    public List<BcraPeriodo>? Periodos { get; set; }
}

internal sealed class BcraPeriodo
{
    public string? Periodo { get; set; }

    public List<BcraEntidad>? Entidades { get; set; }
}

internal sealed class BcraEntidad
{
    public string? Entidad { get; set; }

    public int Situacion { get; set; }

    public string? FechaSit1 { get; set; }

    public decimal Monto { get; set; }

    public int DiasAtrasoPago { get; set; }

    public bool Refinanciaciones { get; set; }

    public bool RecategorizacionOblig { get; set; }

    public bool SituacionJuridica { get; set; }

    public bool IrrecDisposicionTecnica { get; set; }

    public bool EnRevision { get; set; }

    public bool ProcesoJud { get; set; }
}

internal static class BcraJson
{
    public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}
