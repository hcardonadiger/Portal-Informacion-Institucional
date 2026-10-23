using Diger.TramitesEstado.Application.Notificaciones;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Infrastructure.Notifications;

/// <summary>Genera notificaciones de sistema: compromisos vencidos, reuniones del día siguiente y etapas de cronograma.</summary>
public sealed class RecordatorioBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificacionesOptions> options,
    TimeProvider reloj,
    SenalRevision senal,
    ILogger<RecordatorioBackgroundService> logger) : BackgroundService
{
    // «Hoy» y «mañana» son los de la zona configurada (Notificaciones:ZonaHoraria), no los de UTC.
    private readonly (TimeZoneInfo Zona, bool Encontrada) _zona = CalendarioLocal.Resolver(options.Value.ZonaHoraria);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_zona.Encontrada)
            logger.LogWarning(
                "Notificaciones:ZonaHoraria «{Zona}» no existe en este servidor; los recordatorios usan UTC-6 fijo.",
                options.Value.ZonaHoraria);

        // La cancelación es el apagado normal del host: se captura aquí para que no
        // escape como excepción no controlada (el depurador rompería en cada Task.Delay).
        try { await EjecutarBucleAsync(ct); }
        catch (OperationCanceledException) { /* apagado solicitado */ }
    }

    private async Task EjecutarBucleAsync(CancellationToken ct)
    {
        // Esperar al arranque para no competir con las migraciones EF del startup. Guardar el panel
        // durante esa espera la corta, igual que la de cada vuelta.
        await senal.EsperarAsync(TimeSpan.FromMinutes(options.Value.DelayInicialMinutos), ct);

        while (!ct.IsCancellationRequested)
        {
            try { await ProcesarAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error en RecordatorioBackgroundService");
            }

            // El intervalo se lee en cada vuelta: lo cambia el Panel de control sin reiniciar.
            await senal.EsperarAsync(TimeSpan.FromMinutes(await IntervaloAsync(ct)), ct);
        }
    }

    private async Task<int> IntervaloAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return (await scope.ServiceProvider.GetRequiredService<IAjustesNotificaciones>().ObtenerAsync(ct)).IntervaloMinutos;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudo leer el intervalo de revisión; se usa el de por defecto.");
            return AjustesNotificaciones.IntervaloPorDefecto;
        }
    }

    /// <summary>Una corrida completa. La llama el bucle; es pública para poder probarla con un
    /// reloj fijo sin esperar al intervalo.</summary>
    public async Task ProcesarAsync(CancellationToken ct)
    {
        await using var scope   = scopeFactory.CreateAsyncScope();
        var ctx      = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var notifSvc = scope.ServiceProvider.GetRequiredService<INotificacionService>();

        // Plazos y fechas de reunión se escriben en hora local. «Ya se avisó hoy» ya no se mide
        // aquí: lo controla NotificacionService elemento por elemento.
        var (hoy, mañana, _) = CalendarioLocal.Dia(reloj.GetUtcNow(), _zona.Zona);

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
            await notifSvc.CrearYGuardarAsync(uid, TipoNotificacion.ReuniónMañana,
                $"Reunión mañana: {r.Titulo}", url, ct: ct);
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
                await notifSvc.CrearYGuardarAsync(uid, TipoNotificacion.CompromisoVencido, tit, url, ct: ct);
            }
        }

        // ── Compromisos próximos a vencer (≤ 3 días) ──────────────────────────
        var tresDiasCompromiso = hoy.AddDays(3);
        var acuerdosProximos = await ctx.Acuerdos
            .Where(a => (a.Estado == EstadoCompromiso.Pendiente || a.Estado == EstadoCompromiso.EnProgreso)
                     && a.Plazo.HasValue && a.Plazo >= hoy && a.Plazo <= tresDiasCompromiso
                     && a.Responsable != null && a.Responsable != "")
            .Select(a => new { a.Id, a.ReunionId, a.Responsable, a.Compromiso, a.Plazo })
            .Take(60)
            .ToListAsync(ct);

        if (acuerdosProximos.Count > 0)
        {
            var nombresP = acuerdosProximos.Select(a => a.Responsable!).Distinct().ToList();
            var usuariosP = await ctx.Usuarios
                .Where(u => nombresP.Contains(u.Nombre) && u.Activo)
                .Select(u => new { u.Id, u.Nombre })
                .ToListAsync(ct);

            var mapaUsuarioP = usuariosP.ToDictionary(u => u.Nombre, u => u.Id);

            foreach (var a in acuerdosProximos)
            {
                if (!mapaUsuarioP.TryGetValue(a.Responsable!, out var uid)) continue;
                var url  = $"/Reuniones/Compromisos?reunion={a.ReunionId}";
                var tit  = $"Compromiso próximo a vencer ({a.Plazo:dd/MM}): {a.Compromiso[..Math.Min(80, a.Compromiso.Length)]}";
                await notifSvc.CrearYGuardarAsync(uid, TipoNotificacion.CompromisoProximo, tit, url, ct: ct);
            }
        }

        // ── Etapas de cronograma vencidas / próximas → notificar por nombre ──
        var tresDias = hoy.AddDays(3);

        var etapasVencidas = await ctx.EtapaCronogramas
            .Where(e => e.FechaFin < hoy && e.FechaRealFin == null)
            .Join(ctx.Expedientes.IgnoreQueryFilters(),
                  e => e.ExpedienteId, x => x.Id,
                  (e, x) => new { e.ExpedienteId, e.TramiteIndex, e.EtapaNum, e.Responsable, x.Codigo })
            .Take(60)
            .ToListAsync(ct);

        var etapasProximas = await ctx.EtapaCronogramas
            .Where(e => e.FechaFin >= hoy && e.FechaFin <= tresDias && e.FechaRealFin == null)
            .Join(ctx.Expedientes.IgnoreQueryFilters(),
                  e => e.ExpedienteId, x => x.Id,
                  (e, x) => new { e.ExpedienteId, e.TramiteIndex, e.EtapaNum, e.Responsable, x.Codigo })
            .Take(60)
            .ToListAsync(ct);

        var nombresEtapas = etapasVencidas
            .Concat(etapasProximas)
            .Where(e => e.Responsable is not null and not "")
            .Select(e => e.Responsable!)
            .Distinct()
            .ToList();

        if (nombresEtapas.Count > 0)
        {
            var usuariosCrono = await ctx.Usuarios
                .Where(u => nombresEtapas.Contains(u.Nombre) && u.Activo)
                .Select(u => new { u.Id, u.Nombre })
                .ToListAsync(ct);

            var mapaCrono = usuariosCrono.ToDictionary(u => u.Nombre, u => u.Id);

            foreach (var e in etapasVencidas)
            {
                if (e.Responsable is null || !mapaCrono.TryGetValue(e.Responsable, out var uid)) continue;
                var url = $"/Expedientes/Cronograma/{e.ExpedienteId}?t={e.TramiteIndex}";
                var tit = $"Etapa {e.EtapaNum} vencida – {e.Codigo}";
                await notifSvc.CrearYGuardarAsync(uid, TipoNotificacion.EtapaCronogramaVencida, tit, url, ct: ct);
            }

            foreach (var e in etapasProximas)
            {
                if (e.Responsable is null || !mapaCrono.TryGetValue(e.Responsable, out var uid)) continue;
                var url = $"/Expedientes/Cronograma/{e.ExpedienteId}?t={e.TramiteIndex}";
                var tit = $"Etapa {e.EtapaNum} próxima a vencer – {e.Codigo}";
                await notifSvc.CrearYGuardarAsync(uid, TipoNotificacion.EtapaCronogramaProxima, tit, url, ct: ct);
            }
        }

        logger.LogInformation(
            "RecordatorioBackgroundService: {Reuniones} reuniones, {Compromisos} compromisos, {Vencidas} etapas vencidas, {Proximas} etapas próximas procesadas.",
            reunionesMañana.Count, acuerdosVencidos.Count, etapasVencidas.Count, etapasProximas.Count);

        // ── Última revisión (la muestra el panel) y correo de la bandeja ──────
        var ajustes = scope.ServiceProvider.GetRequiredService<IAjustesNotificaciones>();
        var fila = await ctx.AjustesNotificaciones.FirstOrDefaultAsync(a => a.Id == AjustesNotificaciones.IdUnico, ct);
        if (fila is null)
        {
            // Sin fila nace con el intervalo vigente (el de appsettings), no con el de por defecto:
            // si no, la primera revisión cambiaría en silencio un intervalo que nadie tocó.
            fila = AjustesNotificaciones.CrearPorDefecto((await ajustes.ObtenerAsync(ct)).IntervaloMinutos);
            ctx.AjustesNotificaciones.Add(fila);
        }
        fila.MarcarRevision(reloj.GetUtcNow().UtcDateTime);
        await ctx.SaveChangesAsync(ct);
        ajustes.Invalidar();

        await scope.ServiceProvider.GetRequiredService<IDespachadorCorreos>().DespacharAsync(ct);
    }
}
