using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Infrastructure.Email;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Infrastructure.Notifications;

/// <summary>
/// Lee los ajustes del Panel de control con una caché de un minuto. Sin fila en la base rige
/// appsettings (<c>Notificaciones:IntervaloHoras</c> y la sección <c>Smtp</c>), así que el día del
/// despliegue nada deja de funcionar aunque nadie haya abierto el panel.
/// </summary>
public sealed class AjustesNotificacionesProvider(
    IApplicationDbContext ctx,
    IMemoryCache cache,
    IProtectorSecretos protector,
    IOptions<NotificacionesOptions> notificaciones,
    IOptions<SmtpSettings> smtp) : IAjustesNotificaciones
{
    internal const string ClaveCache = "PanelControl.Notificaciones.Ajustes";
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(1);

    private sealed record Instantanea(AjustesVigentes Ajustes, SmtpVigente Smtp);

    public async Task<AjustesVigentes> ObtenerAsync(CancellationToken ct = default) => (await CargarAsync(ct)).Ajustes;

    public async Task<SmtpVigente> SmtpAsync(CancellationToken ct = default) => (await CargarAsync(ct)).Smtp;

    public void Invalidar() => cache.Remove(ClaveCache);

    private async Task<Instantanea> CargarAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(ClaveCache, out Instantanea? guardada) && guardada is not null)
            return guardada;

        var fila = await ctx.AjustesNotificaciones.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == AjustesNotificaciones.IdUnico, ct);
        var filas = await ctx.InterruptoresNotificacion.AsNoTracking().ToListAsync(ct);

        var modulos = Enum.GetValues<ModuloNotificacion>().ToDictionary(m => m, m =>
            filas.FirstOrDefault(f => f.Modulo == m) is { } f
                ? new InterruptoresModulo(f.CampanaActiva, f.CorreoActivo && CatalogoNotificaciones.ModuloAdmiteCorreo(m), f.Agrupar)
                : InterruptoresModulo.PorDefecto);

        var intervalo = fila?.IntervaloMinutos
            ?? Math.Clamp(notificaciones.Value.IntervaloHoras * 60, AjustesNotificaciones.MinimoMinutos, AjustesNotificaciones.MaximoMinutos);

        var instantanea = new Instantanea(new AjustesVigentes(intervalo, modulos, fila?.UltimaRevisionUtc), Smtp(fila));
        cache.Set(ClaveCache, instantanea, Vigencia);
        return instantanea;
    }

    private SmtpVigente Smtp(AjustesNotificaciones? fila)
    {
        var s = smtp.Value;
        if (fila is not { SmtpConfigurado: true })
            return new SmtpVigente(s.Host, s.Port, s.EnableSsl, s.Username, s.Password, s.FromEmail, s.FromName,
                s.TimeoutSegundos, DesdePanel: false, HayPassword: !string.IsNullOrEmpty(s.Password), PasswordIlegible: false);

        var password = fila.SmtpPasswordCifrada is { } cifrada ? protector.Descifrar(cifrada) : "";
        return new SmtpVigente(
            fila.SmtpHost ?? "", fila.SmtpPuerto, fila.SmtpSsl, fila.SmtpUsuario ?? "", password ?? "",
            fila.SmtpRemitente ?? "", fila.SmtpNombreRemitente ?? Marca.Nombre, s.TimeoutSegundos,
            DesdePanel: true, HayPassword: fila.SmtpPasswordCifrada is not null, PasswordIlegible: password is null);
    }
}
