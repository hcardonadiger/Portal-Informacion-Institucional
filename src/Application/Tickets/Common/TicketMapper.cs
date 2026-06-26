namespace Diger.TramitesEstado.Application.Tickets.Common;

public static class TicketMapper
{
    /// <summary>Aplica los escalares editables del formulario. Los snapshots de institución/expediente
    /// y la asignación/estado se establecen en los handlers (requieren resolución/transiciones).</summary>
    public static void Aplicar(Ticket t, TicketFormDto d)
    {
        t.EstablecerTitulo(d.Titulo);
        t.Descripcion = string.IsNullOrWhiteSpace(d.Descripcion) ? null : d.Descripcion.Trim();
        t.Categoria   = d.Categoria;
        t.Prioridad   = d.Prioridad;
        t.InstitucionId = d.InstitucionId;
        t.ExpedienteId  = d.ExpedienteId;
        t.ReportanteNombre   = d.ReportanteNombre?.Trim();
        t.ReportanteCorreo   = d.ReportanteCorreo?.Trim().ToLowerInvariant();
        t.ReportanteTelefono = d.ReportanteTelefono?.Trim();
    }

    public static TicketFormDto ToForm(Ticket t) => new()
    {
        Titulo = t.Titulo, Descripcion = t.Descripcion, Categoria = t.Categoria, Prioridad = t.Prioridad,
        InstitucionId = t.InstitucionId, ExpedienteId = t.ExpedienteId,
        ReportanteNombre = t.ReportanteNombre, ReportanteCorreo = t.ReportanteCorreo, ReportanteTelefono = t.ReportanteTelefono
    };

    public static TicketDetailDto ToDetail(Ticket t) => new(
        t.Id, t.Numero, t.Titulo, t.Descripcion, t.Categoria, t.Prioridad, t.Estado,
        t.InstitucionId, t.Institucion, t.ExpedienteId, t.ExpedienteCodigo,
        t.ReportanteNombre, t.ReportanteCorreo, t.ReportanteTelefono,
        t.AsignadoAId, t.AsignadoA, t.FechaResolucion, t.NotaResolucion,
        t.CreatedAt, t.CreatedBy,
        t.Comentarios.OrderBy(c => c.Fecha).Select(c =>
            new TicketComentarioDto(c.Tipo, c.Autor, c.Texto, c.Fecha)).ToList());
}
