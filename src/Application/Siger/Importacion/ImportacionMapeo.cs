using Diger.TramitesEstado.Application.Siger.Promocion;

namespace Diger.TramitesEstado.Application.Siger.Importacion;

/// <summary>
/// Cómo una ficha SIGER se convierte en un trámite de expediente. Es el camino de vuelta del que
/// hace <see cref="PromocionMapeo"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Los pasos del proceso no se traen</b> (D-11). Siguen siendo propiedad de SIGER, y el
/// expediente modela el flujo con otro vocabulario —nodos, fases, retornos, ramas— que no es una
/// lista de pasos numerados. Volcar unos sobre otros produciría un flujo falso que además pisaría
/// el modelado de quien sí levantó el trámite.
/// </para>
/// <para>
/// <b>Tampoco se traen los enlaces ni las tareas de digitalización</b>, por lo mismo: el
/// expediente no tiene dónde ponerlos y fabricar un hueco solo para no perderlos convertiría el
/// formulario en un espejo de SIGER en vez de una herramienta de levantamiento.
/// </para>
/// </remarks>
public static class ImportacionMapeo
{
    /// <summary>Crea el trámite del expediente a partir de la ficha, ya enlazado a ella.</summary>
    /// <param name="indice">Posición dentro del expediente destino.</param>
    public static ExpedienteTramite CrearTramite(TramiteSiger f, int indice) => new()
    {
        TramiteIndex  = indice,
        ClaveEstable  = Guid.NewGuid(),
        NombreTramite = f.Nombre,
        AreaResponsable = f.Dependencia,
        FechaCreacion = DateOnly.FromDateTime(DateTime.Today),
        EstadoTramite = EstadoTramite.Pendiente,

        Descripcion = f.Descripcion,
        Objetivo    = f.Objetivo,
        Dirigido    = f.DirigidoA,
        SitioWeb    = f.EnlacePrincipal,

        // El tiempo de la ficha es uno solo; en el expediente hay dos campos —el real observado y
        // el plazo de ley— y no se sabe cuál de los dos es. Se pone en el real, que es el que la
        // promoción vuelve a leer primero: así importar y volver a pasar no cambia el dato.
        TiempoReal = f.TiempoTexto,

        Modalidad        = ModalidadNormalizador.Normalizar(f.Modalidad),
        ModalidadDetalle = f.Modalidad,

        CategoriaId        = f.CategoriaId,
        EsGratuito         = f.CostoEsGratuito,
        VigenciaDocumento  = f.VigenciaDocumento,
        Temporalidad       = f.Temporalidad,
        ObservacionesDiger = f.ObservacionesDiger,
        EstaEnSol          = f.EstaEnSol,
        SolTramo           = f.SolTramo,

        // El monto viaja al campo de la TGR porque es el único de la ficha, y la promoción lo
        // vuelve a leer de ahí para rearmar el texto del costo.
        TgrMonto = f.CostoTexto,

        // El enlace, que es lo que bloquea la ficha (D-17).
        TramiteSigerId = f.Id
    };

    public static List<TramiteRequisito> Requisitos(int expedienteId, int indice, IEnumerable<RequisitoSiger> reqs) =>
        reqs.Where(r => !string.IsNullOrWhiteSpace(r.Requisito))
            .OrderBy(r => r.Numero)
            .Select((r, i) => new TramiteRequisito
            {
                ExpedienteId = expedienteId,
                TramiteIndex = indice,
                Orden = i,
                Requisito = r.Requisito
            })
            .ToList();

    public static List<ExpedienteTramiteEntregable> Entregables(
        int expedienteId, int indice, IEnumerable<EntregableSiger> items) =>
        items.Where(g => !string.IsNullOrWhiteSpace(g.Entregable))
             .OrderBy(g => g.Numero)
             .Select((g, i) => new ExpedienteTramiteEntregable
             {
                 ExpedienteId = expedienteId,
                 TramiteIndex = indice,
                 Orden = i,
                 Entregable = g.Entregable,
                 Formato = g.Formato,
                 Presentacion = g.Presentacion
             })
             .ToList();

    public static List<ExpedienteTramiteLugar> Lugares(
        int expedienteId, int indice, IEnumerable<LugarAtencionSiger> items) =>
        items.Where(l => !string.IsNullOrWhiteSpace(l.Lugar))
             .OrderBy(l => l.Numero)
             .Select((l, i) => new ExpedienteTramiteLugar
             {
                 ExpedienteId = expedienteId,
                 TramiteIndex = indice,
                 Orden = i,
                 Lugar = l.Lugar,
                 Ciudad = l.Ciudad,
                 Direccion = l.Direccion,
                 Telefonos = l.Telefonos
             })
             .ToList();
}
