using Diger.TramitesEstado.Application.Notificaciones;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Infrastructure.Notifications;

public sealed class NotificacionService(
    IApplicationDbContext ctx,
    IUnitOfWork uow,
    IAjustesNotificaciones ajustes,
    TimeProvider reloj,
    IOptions<NotificacionesOptions> opciones) : INotificacionService
{
    private readonly TimeZoneInfo _zona = CalendarioLocal.Resolver(opciones.Value.ZonaHoraria).Zona;

    public async Task<bool> EncolarAsync(Guid destinatarioId, TipoNotificacion tipo, string titulo,
                                         string? url = null, string? clave = null, CancellationToken ct = default)
    {
        var def     = CatalogoNotificaciones.De(tipo);
        var inter   = (await ajustes.ObtenerAsync(ct)).De(def.Modulo);
        var ahora   = reloj.GetUtcNow().UtcDateTime;
        var claveEf = Notificacion.Recortar(clave ?? url ?? titulo, NotificacionElemento.LargoClave);

        // Solo los del revisor diario: el resto (un chat, un ticket asignado) puede volver a pasar
        // el mismo día y tiene que llegar.
        if (def.Diario && await YaAvisadoHoyAsync(destinatarioId, tipo, claveEf, ct))
            return false;

        var enCampana = false;
        if (inter.Campana)
        {
            await AnotarEnCampanaAsync(destinatarioId, tipo, titulo, url, claveEf, def, inter.Agrupar, ahora, ct);
            enCampana = true;
        }

        if (inter.Correo && def.AdmiteCorreo && !def.CorreoInmediato)
            ctx.CorreosPendientes.Add(CorreoPendiente.Crear(destinatarioId, def.Modulo, tipo, titulo, url, claveEf, ahora));

        return enCampana;
    }

    public async Task<bool> CrearYGuardarAsync(Guid destinatarioId, TipoNotificacion tipo, string titulo,
                                               string? url = null, string? clave = null, CancellationToken ct = default)
    {
        var llego = await EncolarAsync(destinatarioId, tipo, titulo, url, clave, ct);
        await uow.SaveChangesAsync(ct);
        return llego;
    }

    private async Task AnotarEnCampanaAsync(Guid destinatarioId, TipoNotificacion tipo, string titulo, string? url,
                                            string clave, DefinicionNotificacion def, bool agrupar, DateTime ahora,
                                            CancellationToken ct)
    {
        if (agrupar)
        {
            // Primero lo que todavía no se guardó: dos avisos del mismo tipo en una sola operación
            // tienen que caer en el mismo grupo.
            var abierta = ctx.Notificaciones.Local
                              .FirstOrDefault(n => n.DestinatarioId == destinatarioId && n.Tipo == tipo && !n.Leida)
                          ?? await ctx.Notificaciones
                              .Include(n => n.Elementos)
                              .Where(n => n.DestinatarioId == destinatarioId && n.Tipo == tipo && !n.Leida)
                              .OrderByDescending(n => n.FechaActualizacion)
                              .FirstOrDefaultAsync(ct);

            if (abierta is not null)
            {
                abierta.Sumar(clave, titulo, url, def.Plural, ahora);
                return;
            }
        }

        ctx.Notificaciones.Add(Notificacion.Crear(destinatarioId, tipo, titulo, url, clave, ahora));
    }

    private async Task<bool> YaAvisadoHoyAsync(Guid destinatarioId, TipoNotificacion tipo, string clave, CancellationToken ct)
    {
        var inicio = CalendarioLocal.Dia(reloj.GetUtcNow(), _zona).InicioDeHoyUtc;

        var enCampana = await ctx.NotificacionElementos.AnyAsync(e =>
            e.Clave == clave && e.FechaUtc >= inicio &&
            ctx.Notificaciones.Any(n => n.Id == e.NotificacionId && n.DestinatarioId == destinatarioId && n.Tipo == tipo), ct);
        if (enCampana) return true;

        // Con la campana apagada y el correo encendido no hay elemento: lo dice la bandeja.
        return await ctx.CorreosPendientes.AnyAsync(c =>
            c.DestinatarioId == destinatarioId && c.Tipo == tipo && c.Clave == clave && c.CreadoUtc >= inicio, ct);
    }

    public async Task<IReadOnlyList<NotificacionDto>> GetNoLeidasAsync(Guid usuarioId, int max = 8, CancellationToken ct = default)
        => await ctx.Notificaciones
            .AsNoTracking()
            .Where(n => n.DestinatarioId == usuarioId && !n.Leida)
            .OrderByDescending(n => n.FechaActualizacion)
            .Take(max)
            .Select(n => new NotificacionDto(n.Id, n.Tipo, n.Titulo,
                n.Cantidad > 1 ? "/Notificaciones/" + n.Id : n.Url,
                n.Leida, n.FechaActualizacion, n.Cantidad))
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
