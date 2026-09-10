namespace Diger.TramitesEstado.Application.Proyectos.EventHandlers;

/// <summary>
/// Cierra el circuito de <see cref="ProyectoEstadoCambiadoEvent"/>, que hasta ahora se disparaba
/// sin que nadie lo escuchara: el cambio de estado no dejaba rastro ni avisaba a nadie.
///
/// <para>Hace dos cosas: deja la entrada de auditoría y avisa al responsable del proyecto. El
/// aviso se omite cuando el propio responsable es quien hizo el cambio —notificarle su propia
/// acción es ruido— y cuando el proyecto no tiene responsable asignado.</para>
/// </summary>
public sealed class ProyectoEstadoCambiadoEventHandler(IApplicationDbContext ctx)
    : INotificationHandler<ProyectoEstadoCambiadoEvent>
{
    public async Task Handle(ProyectoEstadoCambiadoEvent e, CancellationToken ct)
    {
        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            e.ProyectoId, TipoEventoProyecto.CambioEstado,
            $"Estado: {e.EstadoAnterior} → {e.EstadoNuevo}.", e.Actor));

        var proyecto = await ctx.Proyectos
            .Where(p => p.Id == e.ProyectoId)
            .Select(p => new { p.ResponsableId, p.Responsable })
            .FirstOrDefaultAsync(ct);

        if (proyecto?.ResponsableId is { } destinatario
            && !string.Equals(proyecto.Responsable, e.Actor, StringComparison.OrdinalIgnoreCase))
        {
            ctx.Notificaciones.Add(Notificacion.Crear(
                destinatario,
                TipoNotificacion.ProyectoCambioEstado,
                $"{e.Codigo} pasó a «{e.EstadoNuevo}»",
                $"/Proyectos/Editor/{e.ProyectoId}"));
        }

        await ctx.SaveChangesAsync(ct);
    }
}
