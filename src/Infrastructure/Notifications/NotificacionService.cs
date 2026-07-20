using Diger.TramitesEstado.Application.Notificaciones;

namespace Diger.TramitesEstado.Infrastructure.Notifications;

public sealed class NotificacionService(IApplicationDbContext ctx, IUnitOfWork uow) : INotificacionService
{
    public void Encolar(Guid destinatarioId, TipoNotificacion tipo, string titulo, string? url = null)
        => ctx.Notificaciones.Add(Notificacion.Crear(destinatarioId, tipo, titulo, url));

    public async Task CrearYGuardarAsync(Guid destinatarioId, TipoNotificacion tipo, string titulo, string? url = null, CancellationToken ct = default)
    {
        ctx.Notificaciones.Add(Notificacion.Crear(destinatarioId, tipo, titulo, url));
        await uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NotificacionDto>> GetNoLeidasAsync(Guid usuarioId, int max = 8, CancellationToken ct = default)
        => await ctx.Notificaciones
            .Where(n => n.DestinatarioId == usuarioId && !n.Leida)
            .OrderByDescending(n => n.FechaCreacion)
            .Take(max)
            .Select(n => new NotificacionDto(n.Id, n.Tipo, n.Titulo, n.Url, n.Leida, n.FechaCreacion))
            .ToListAsync(ct);

    public async Task<int> ConteoNoLeidasAsync(Guid usuarioId, CancellationToken ct = default)
        => await ctx.Notificaciones.CountAsync(n => n.DestinatarioId == usuarioId && !n.Leida, ct);

    public async Task MarcarLeidaAsync(int id, Guid usuarioId, CancellationToken ct = default)
    {
        var n = await ctx.Notificaciones.FirstOrDefaultAsync(x => x.Id == id && x.DestinatarioId == usuarioId, ct);
        if (n is null) return;
        n.MarcarLeida();
        await uow.SaveChangesAsync(ct);
    }

    public async Task MarcarTodasLeidasAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var pendientes = await ctx.Notificaciones
            .Where(x => x.DestinatarioId == usuarioId && !x.Leida)
            .ToListAsync(ct);
        foreach (var n in pendientes) n.MarcarLeida();
        if (pendientes.Count > 0) await uow.SaveChangesAsync(ct);
    }
}
