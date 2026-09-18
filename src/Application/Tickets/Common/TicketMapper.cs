namespace Diger.TramitesEstado.Application.Tickets.Common;

public static class TicketMapper
{
    /// <summary>Aplica los escalares editables del formulario. Los snapshots de institución/expediente
    /// y la asignación/estado se establecen en los handlers (requieren resolución/transiciones).</summary>
    /// <param name="prioridadId">
    /// Ya resuelta contra el catálogo por el handler: el formulario puede traer 0 —«la que
    /// corresponda»— y decidirlo exige consultar la base, que es justo lo que un mapeador no hace.
    /// </param>
    public static void Aplicar(Ticket t, TicketFormDto d, int prioridadId)
    {
        t.EstablecerTitulo(d.Titulo);
        t.Descripcion = string.IsNullOrWhiteSpace(d.Descripcion) ? null : d.Descripcion.Trim();
        t.TemaId      = d.TemaId;
        t.TemaOtro    = string.IsNullOrWhiteSpace(d.TemaOtro) ? null : d.TemaOtro.Trim();
        t.PrioridadId = prioridadId;
        t.InstitucionId = d.InstitucionId;
        t.ExpedienteId  = d.ExpedienteId;
        // El reportante NO se toca aquí: se fija desde el usuario al crear y se conserva al editar.
    }

    public static TicketFormDto ToForm(Ticket t) => new()
    {
        Titulo = t.Titulo, Descripcion = t.Descripcion, TemaId = t.TemaId, TemaOtro = t.TemaOtro,
        PrioridadId = t.PrioridadId,
        InstitucionId = t.InstitucionId, ExpedienteId = t.ExpedienteId,
        TramiteIds = t.Tramites.Where(x => x.TramiteDefinicionId != null)
                               .Select(x => x.TramiteDefinicionId!.Value).ToList()
    };

    public static TicketDetailDto ToDetail(Ticket t) => new(
        t.Id, t.Numero, t.Titulo, t.Descripcion,
        t.InstitucionId, t.Institucion, t.ExpedienteId, t.ExpedienteCodigo,
        t.TemaId, t.TemaRef?.Nombre, t.TemaOtro, t.TemaRef?.HorasResolucion,
        // Los respaldos cubren al ticket cuya consulta no trajo la navegación: preferimos una
        // insignia gris sin nombre a una excepción de referencia nula al pintar la ficha.
        t.PrioridadId, t.PrioridadRef?.Nombre ?? "", t.PrioridadRef?.Color ?? ColorEtiqueta.Gris,
        t.Estado,
        t.ReportanteNombre, t.ReportanteCorreo, t.ReportanteTelefono,
        t.AsignadoAId, t.AsignadoA, t.FechaResolucion, t.NotaResolucion,
        t.CreatedAt, t.CreadoPor ?? t.CreatedBy,
        t.Tramites.Select(x => x.Tramite).OrderBy(x => x).ToList(),
        t.Adjuntos.OrderBy(a => a.Id)
            .Select(a => new AdjuntoDto(a.Id, a.ComentarioId, a.NombreArchivo, a.Url, a.Tamano)).ToList(),
        t.Comentarios.OrderBy(c => c.Fecha).Select(c =>
            new TicketComentarioDto(c.Id, c.Tipo, c.Autor, c.Texto, c.Fecha)).ToList());
}
