namespace Diger.TramitesEstado.Domain.Entities;

public sealed class TramiteSiger : BaseAuditableEntity
{
    public int IdSiger { get; set; }
    public string Codigo { get; set; } = default!;
    public string Nombre { get; set; } = default!;
    public string Institucion { get; set; } = default!;
    public string? Sigla { get; set; }
    public string? Dependencia { get; set; }
    public string? Descripcion { get; set; }
    public string? Objetivo { get; set; }
    public string? DirigidoA { get; set; }
    public string? EstadoSiger { get; set; }
    public bool Publicado { get; set; }
    public bool DisponibleEnLinea { get; set; }
    public bool EnPlanDigitalizacion { get; set; }
    public string? VigenciaDocumento { get; set; }
    public string? Temporalidad { get; set; }
    public string? DiagramaUrl { get; set; }
    public string? EnlacePrincipal { get; set; }
    public string? ObservacionesDiger { get; set; }
    public DateTime? FechaIngreso { get; set; }
    public DateTime? UltimaModificacion { get; set; }

    public string? InstitucionId { get; set; }

    public List<PasoSiger> Pasos { get; set; } = [];
    public List<RequisitoSiger> Requisitos { get; set; } = [];
    public List<EntregableSiger> Entregables { get; set; } = [];
    public List<LugarAtencionSiger> LugaresAtencion { get; set; } = [];
    public List<EnlaceSiger> Enlaces { get; set; } = [];
    public List<TareaDigitalizacionSiger> TareasDigitalizacion { get; set; } = [];
}

public sealed class PasoSiger : BaseEntity
{
    public int TramiteSigerId { get; set; }
    public int NumeroPaso { get; set; }
    public string Descripcion { get; set; } = default!;
    public string? LugarDependencia { get; set; }
    public string? SalidaResultado { get; set; }
    public string? TiempoRegistrado { get; set; }
}

public sealed class RequisitoSiger : BaseEntity
{
    public int TramiteSigerId { get; set; }
    public int Numero { get; set; }
    public string Requisito { get; set; } = default!;
    public string? Tipo { get; set; }
    public string? DocumentoSoporte { get; set; }
    public string? Formato { get; set; }
}

public sealed class EntregableSiger : BaseEntity
{
    public int TramiteSigerId { get; set; }
    public int Numero { get; set; }
    public string Entregable { get; set; } = default!;
    public string? Formato { get; set; }
    public string? Presentacion { get; set; }
}

public sealed class LugarAtencionSiger : BaseEntity
{
    public int TramiteSigerId { get; set; }
    public int Numero { get; set; }
    public string Lugar { get; set; } = default!;
    public string? Ciudad { get; set; }
    public string? Direccion { get; set; }
    public string? Telefonos { get; set; }
}

public sealed class EnlaceSiger : BaseEntity
{
    public int TramiteSigerId { get; set; }
    public int Numero { get; set; }
    public string Url { get; set; } = default!;
    public string? Tipo { get; set; }
}

public sealed class TareaDigitalizacionSiger : BaseEntity
{
    public int TramiteSigerId { get; set; }
    public int NumeroTarea { get; set; }
    public string Descripcion { get; set; } = default!;
    public string? Estado { get; set; }
    public DateTime? FechaCumplimiento { get; set; }
}
