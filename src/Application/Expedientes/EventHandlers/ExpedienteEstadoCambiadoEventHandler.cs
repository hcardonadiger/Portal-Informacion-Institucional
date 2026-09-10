namespace Diger.TramitesEstado.Application.Expedientes.EventHandlers;

public sealed class ExpedienteEstadoCambiadoEventHandler(IApplicationDbContext ctx)
    : INotificationHandler<ExpedienteEstadoCambiadoEvent>
{
    public async Task Handle(ExpedienteEstadoCambiadoEvent e, CancellationToken ct)
    {
        ctx.BitacorasExpediente.Add(BitacoraExpediente.Crear(
            e.ExpedienteId, TipoEventoBitacora.CambioEstado,
            $"Estado: {e.EstadoAnterior} → {e.EstadoNuevo}.", e.Actor));
        await ctx.SaveChangesAsync(ct);
    }
}
