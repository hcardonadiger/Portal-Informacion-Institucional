namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Trámite a modelar dentro de un expediente (ficha del trámite).</summary>
public sealed class ExpedienteTramite : BaseEntity
{
    public int    ExpedienteId  { get; set; }
    public int    TramiteIndex  { get; set; } // posición 0-based dentro del expediente

    /// <summary>
    /// Identidad estable del trámite dentro del expediente, invariante a los reacomodos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ni <see cref="Id"/> ni <see cref="TramiteIndex"/> sirven para recordar nada de un trámite
    /// entre un guardado y el siguiente. El <c>Id</c> cambia porque guardar un expediente borra
    /// y reinserta todos sus hijos —<c>LimpiarHijos()</c> y volver a agregar—; y el
    /// <c>TramiteIndex</c> se asigna por la posición en el arreglo del formulario, así que quitar
    /// un trámite del medio o reordenarlos por arrastre renumera todo lo que va detrás.
    /// </para>
    /// <para>
    /// Esta clave viaja dentro de la fila del formulario, igual que <see cref="TramiteSigerId"/>,
    /// y por eso se mueve con su trámite en vez de quedarse fija en una posición. Es lo que
    /// permite que una decisión de conciliación siga apuntando al trámite correcto después de
    /// que alguien reacomode el expediente.
    /// </para>
    /// </remarks>
    public Guid ClaveEstable { get; set; }

    public string  NombreTramite   { get; set; } = default!;
    public string? NombreCorto     { get; set; }
    public string? AreaResponsable { get; set; }

    public DateOnly   FechaCreacion  { get; set; }
    public EstadoTramite EstadoTramite { get; set; } = EstadoTramite.Pendiente;

    // Ficha
    /// <summary>
    /// Modalidad del <b>catálogo cerrado</b> de la ficha pública: Virtual, Presencial o Hibrido.
    /// Antes de la Fase 8 era texto libre; el texto original de cada trámite se conservó en
    /// <see cref="ModalidadDetalle"/>. Lo protege un CHECK en la base.
    /// </summary>
    public string? Modalidad   { get; set; }
    public string? PlazoLegal  { get; set; }
    public string? Tercero     { get; set; }
    public string? TiempoReal  { get; set; }
    public string? MetodoPago  { get; set; }
    public string? PagoBanco   { get; set; }
    public string? PagoCuenta  { get; set; }
    public string? TgrInst     { get; set; }
    public string? TgrRubro    { get; set; }
    public string? TgrMonto    { get; set; }
    public string? DocEntregado { get; set; }
    public string? Objetivo    { get; set; }
    public string? Alcance     { get; set; }
    public string? AlcanceObs  { get; set; }
    public string? Descripcion { get; set; }
    public string? Dirigido    { get; set; }
    public string? Horario     { get; set; }
    public string? Telefono    { get; set; }
    public string? EmailTramite { get; set; }
    public string? SitioWeb    { get; set; }


    // ── Campos de la ficha pública (plan Fase 8, D-12 y D-17) ───────────────
    //
    // Están acá y no solo en TramiteSiger porque D-17 invierte quién manda: en cuanto una
    // ficha queda enlazada a un expediente, sus campos de contenido se vuelven de solo lectura
    // en la ficha y solo se editan por acá. Un campo que el expediente no sepa guardar es un
    // campo que, a partir de ese momento, nadie puede editar en ninguna parte.

    /// <summary>Categoría del catálogo público. Es la misma tabla que usa la ficha.</summary>
    public int? CategoriaId { get; set; }

    /// <summary>
    /// El texto libre de modalidad tal como lo escribió el analista, antes de normalizarlo.
    ///
    /// Existe porque el catálogo cerrado pierde matiz: «En línea (total)» y «En línea» acaban
    /// las dos en <c>Virtual</c>, y ese «(total)» lo escribió alguien queriendo decir algo. Se
    /// conserva aparte en vez de descartarlo, porque después de convertir no hay forma de
    /// recuperarlo.
    /// </summary>
    public string? ModalidadDetalle { get; set; }

    /// <summary>Tres estados: null = no capturado, false = tiene costo, true = gratuito. Nunca
    /// se infiere de que no haya monto escrito.</summary>
    public bool? EsGratuito { get; set; }

    /// <summary>Cuánto vale el documento que entrega el trámite.</summary>
    public string? VigenciaDocumento { get; set; }

    /// <summary>Si el trámite es permanente, estacional, de una sola vez…</summary>
    public string? Temporalidad { get; set; }

    /// <summary>Notas de DIGER sobre la ficha. No las ve el ciudadano.</summary>
    public string? ObservacionesDiger { get; set; }

    /// <summary>Si el trámite se puede hacer desde SOL.</summary>
    public bool EstaEnSol { get; set; }

    /// <summary>El tramo final de la dirección en SOL (D-13). Lo que va delante lo pone la
    /// institución; ver <c>DireccionSol</c>.</summary>
    public string? SolTramo { get; set; }
    public int? TramiteSigerId { get; set; }
}

/// <summary>Requisito de un trámite y la acción propuesta en el modelo.</summary>
public sealed class TramiteRequisito : BaseEntity
{
    public int    ExpedienteId { get; set; }
    public int    TramiteIndex { get; set; }
    public int    Orden        { get; set; }
    public string Requisito    { get; set; } = default!;
    public string? Obs         { get; set; }
    public AccionRequisito? Accion { get; set; }
    public string? Justificacion  { get; set; }

    /// <summary>Plantilla de la que se copió (null = escrito a mano para este expediente).</summary>
    public int?  PlantillaOrigenId { get; set; }
    /// <summary>true = ya no se sincroniza con la plantilla; queda fijo y editable para este expediente.</summary>
    public bool  EsPersonalizado   { get; set; }
}

/// <summary>Nodo del constructor de flujos (actual o propuesto) de un trámite.</summary>
public sealed class FlujoNodo : BaseEntity
{
    public int           ExpedienteId { get; set; }
    public int           TramiteIndex { get; set; }
    public FaseFlujo     Fase         { get; set; }
    public int           Orden        { get; set; }
    public TipoNodoFlujo Tipo         { get; set; }
    public string?       Titulo       { get; set; }
    public string?       Area         { get; set; }
    public string?       Tiempo       { get; set; }
    public string?       DocEmitido   { get; set; }
    public string?       Obs          { get; set; }
    public string?       RetornoA     { get; set; }
}
