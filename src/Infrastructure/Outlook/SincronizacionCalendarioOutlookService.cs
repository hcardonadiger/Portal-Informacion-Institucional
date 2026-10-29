using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Infrastructure.Outlook;

/// <summary>
/// Cada 15 minutos trae el calendario de Outlook de cada usuario con conexión vigente (spec
/// 2026-10-29). Un scope por usuario: un fallo —Graph caído, conexión vencida— se registra y
/// sigue con el siguiente. Sin TenantId/ClientId/ClientSecret no hace nada.
/// </summary>
public sealed class SincronizacionCalendarioOutlookService(
    IServiceScopeFactory scopes,
    IOptions<MicrosoftGraphOptions> opciones,
    ILogger<SincronizacionCalendarioOutlookService> log) : BackgroundService
{
    internal static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);
    // Como RecordatorioBackgroundService: no competir con las migraciones del arranque.
    private static readonly TimeSpan EsperaInicial = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!opciones.Value.EstaCompleta)
        {
            log.LogInformation("Outlook no está configurado: no se sincronizan calendarios.");
            return;
        }

        try
        {
            await Task.Delay(EsperaInicial, ct);
            while (!ct.IsCancellationRequested)
            {
                try { await CicloAsync(ct); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Falló el ciclo de sincronización de calendarios");
                }
                await Task.Delay(Intervalo, ct);
            }
        }
        catch (OperationCanceledException) { /* apagado del host */ }
    }

    /// <summary>Una vuelta completa. Pública para probarla sin esperar al intervalo.</summary>
    public async Task CicloAsync(CancellationToken ct)
    {
        List<Guid> usuarios;
        await using (var scope = scopes.CreateAsyncScope())
        {
            usuarios = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().ConexionesMicrosoft
                .AsNoTracking().Where(c => !c.RequiereReconexion).Select(c => c.Id).ToListAsync(ct);
        }

        foreach (var usuarioId in usuarios)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var r = await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new SincronizarCalendarioOutlookCommand(usuarioId), ct);
                if (r.Creadas + r.Actualizadas + r.Canceladas > 0)
                    log.LogInformation("Calendario de {UsuarioId}: {Creadas} nuevas, {Actualizadas} actualizadas, {Canceladas} canceladas",
                        usuarioId, r.Creadas, r.Actualizadas, r.Canceladas);
            }
            catch (OutlookConexionVencidaException)
            {
                log.LogInformation("Calendario de {UsuarioId}: la conexión venció; se salta hasta que reconecte", usuarioId);
            }
            catch (OutlookNoDisponibleException ex)
            {
                log.LogWarning("Calendario de {UsuarioId}: Microsoft no respondió ({Motivo}); se reintenta en el próximo ciclo",
                    usuarioId, ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Calendario de {UsuarioId}: error inesperado", usuarioId);
            }
        }
    }
}
