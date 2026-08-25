namespace Diger.TramitesEstado.Domain.Entities;

public sealed class TramiteSiger : BaseAuditableEntity
{
    /// <summary>Identificador en el sistema SIGER. Vacío cuando la ficha nació en este portal
    /// —promovida desde un expediente— y por tanto no existe en SIGER. Ese vacío es la marca:
    /// de él salen las insignias del expediente, el aviso del detalle y el filtro del
    /// inventario, sin necesidad de una columna aparte.</summary>
    public int? IdSiger { get; set; }
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

    // ── Campos para la ficha pública (Ventanilla Digital) ──────────────────
    /// <summary>Si el trámite se puede hacer desde SOL. No confundir con Modalidad
    /// (cómo es el trámite) ni con DisponibleEnLinea (legado, sin usar — ver M-04 del plan).</summary>
    public bool EstaEnSol { get; set; }
    /// <summary>
    /// La URL completa heredada, de antes de que la dirección se compusiera (D-14).
    ///
    /// <b>No se captura más por acá.</b> Desde la Fase 7 lo que se escribe es
    /// <see cref="SolTramo"/> y la dirección se arma; este campo solo sigue vivo para no tocar
    /// lo que ya estaba cargado. Nunca lo lea directo para pintar un enlace: use
    /// <c>DireccionSol.Componer</c>, que decide entre las dos.
    /// </summary>
    public string? SolUrl { get; set; }

    /// <summary>
    /// El tramo final de la dirección en SOL — lo único que captura el trámite (D-13). Lo que va
    /// delante lo pone la institución.
    ///
    /// Se guarda ya normalizado (sin barras al principio ni al final); de eso se encarga
    /// <c>DireccionSol.Normalizar</c> en el único punto donde se escribe.
    /// </summary>
    public string? SolTramo { get; set; }
    public DateTime? SolVerificadoEl { get; set; }
    public int? CategoriaId { get; set; }
    public string? CostoTexto { get; set; }
    /// <summary>Tres estados: null = no capturado, false = tiene costo, true = gratuito.
    /// Nunca se infiere de un CostoTexto vacío — ver Fase 0 / nota del script A.</summary>
    public bool? CostoEsGratuito { get; set; }
    public string? TiempoTexto { get; set; }
    /// <summary>Catálogo cerrado: Virtual, Presencial o Hibrido (CHECK en la base).</summary>
    public string? Modalidad { get; set; }
    public bool EsPopular { get; set; }

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
    /// <summary>Null si no se capturó — la ficha pública muestra «Paso N», nunca una
    /// Descripcion truncada (produce títulos malos, ya probado).</summary>
    public string? Titulo { get; set; }
    /// <summary>Incluye "Interno" (a diferencia de Modalidad a nivel de trámite): un paso puede
    /// ser un procesamiento donde el ciudadano no hace nada.</summary>
    public string? Modalidad { get; set; }
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
