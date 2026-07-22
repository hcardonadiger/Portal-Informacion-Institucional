using Diger.TramitesEstado.Application.Notificaciones;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diger.TramitesEstado.Infrastructure.Notifications;

/// <summary>Genera notificaciones de sistema: compromisos vencidos y reuniones del día siguiente.</summary>
public sealed class RecordatorioBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<RecordatorioBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Esperar 2 minutos al arranque para no competir con las migraciones EF del startup
        await Task.Delay(TimeSpan.FromMinutes(2), ct);

        while (!ct.IsCancellationRequested)
        {
            try { await ProcesarAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error en RecordatorioBackgroundService");
            }
            await Task.Delay(TimeSpan.FromHours(4), ct);
        }
    }

    private async Task ProcesarAsync(CancellationToken ct)
    {
        await using var scope   = scopeFactory.CreateAsyncScope();
        var ctx      = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var notifSvc = scope.ServiceProvider.GetRequiredService<INotificacionService>();

        var hoyUtc    = DateTime.UtcNow.Date;
        var mañana    = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var hoy       = DateOnly.FromDateTime(DateTime.UtcNow);

        // ── Pre-carga de notificaciones ya enviadas hoy (evita duplicados) ──
        var enviadas = await ctx.Notificaciones
            .Where(n => n.FechaCreacion >= hoyUtc
                     && (n.Tipo == TipoNotificacion.ReuniónMañana || n.Tipo == TipoNotificacion.CompromisoVencido))
            .Select(n => new { n.DestinatarioId, n.Tipo, n.Url })
            .ToListAsync(ct);

        bool YaEnviada(Guid uid, TipoNotificacion tipo, string? url) =>
            enviadas.Any(e => e.DestinatarioId == uid && e.Tipo == tipo && e.Url == url);

        // ── Reuniones de mañana → notificar al creador ───────────────────────
        var reunionesMañana = await ctx.Reuniones
            .Where(r => r.Fecha == mañana && r.CreadoPorId.HasValue)
            .Select(r => new { r.Id, r.Titulo, r.CreadoPorId })
            .Take(50)
            .ToListAsync(ct);

        foreach (var r in reunionesMañana)
        {
            var uid = r.CreadoPorId!.Value;
            var url = $"/Reuniones/Editor/{r.Id}";
            if (!YaEnviada(uid, TipoNotificacion.ReuniónMañana, url))
                await notifSvc.CrearYGuardarAsync(uid, TipoNotificacion.ReuniónMañana,
                    $"Reunión mañana: {r.Titulo}", url, ct);
        }

        // ── Compromisos vencidos sin cumplir → notificar por nombre (best-effort) ─
        var acuerdosVencidos = await ctx.Acuerdos
            .Where(a => (a.Estado == EstadoCompromiso.Pendiente || a.Estado == EstadoCompromiso.EnProgreso)
                     && a.Plazo.HasValue && a.Plazo < hoy
                     && a.Responsable != null && a.Responsable != "")
            .Select(a => new { a.Id, a.ReunionId, a.Responsable, a.Compromiso })
            .Take(60)
            .ToListAsync(ct);

        if (acuerdosVencidos.Count > 0)
        {
            var nombres = acuerdosVencidos.Select(a => a.Responsable!).Distinct().ToList();
            var usuarios = await ctx.Usuarios
                .Where(u => nombres.Contains(u.Nombre) && u.Activo)
                .Select(u => new { u.Id, u.Nombre })
                .ToListAsync(ct);

            var mapaUsuario = usuarios.ToDictionary(u => u.Nombre, u => u.Id);

            foreach (var a in acuerdosVencidos)
            {
                if (!mapaUsuario.TryGetValue(a.Responsable!, out var uid)) continue;
                var url  = $"/Reuniones/Compromisos?reunion={a.ReunionId}";
                var tit  = $"Compromiso vencido: {a.Compromiso[..Math.Min(80, a.Compromiso.Length)]}";
                if (!YaEnviada(uid, TipoNotificacion.CompromisoVencido, url))
                    await notifSvc.CrearYGuardarAsync(uid, TipoNotificacion.CompromisoVencido, tit, url, ct);
            }
        }

        logger.LogInformation("RecordatorioBackgroundService: {Reuniones} reuniones, {Compromisos} compromisos procesados.",
            reunionesMañana.Count, acuerdosVencidos.Count);
    }
}
