using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Application.Tests.Notificaciones;

/// <summary>Ajustes fijos para las pruebas: por defecto, campana sí y correo no, como el despliegue.</summary>
internal sealed class AjustesDePrueba : IAjustesNotificaciones
{
    public Dictionary<ModuloNotificacion, InterruptoresModulo> Modulos { get; } =
        Enum.GetValues<ModuloNotificacion>().ToDictionary(m => m, _ => InterruptoresModulo.PorDefecto);

    public int         IntervaloMinutos { get; set; } = 240;
    public SmtpVigente Smtp             { get; set; } = ConSmtp("smtp.prueba.local", "no-reply@prueba.local");
    public int         Invalidaciones   { get; private set; }

    public Task<AjustesVigentes> ObtenerAsync(CancellationToken ct = default) =>
        Task.FromResult(new AjustesVigentes(IntervaloMinutos, Modulos, null));

    public Task<SmtpVigente> SmtpAsync(CancellationToken ct = default) => Task.FromResult(Smtp);

    public void Invalidar() => Invalidaciones++;

    public AjustesDePrueba Con(ModuloNotificacion modulo, bool campana = true, bool correo = false, bool agrupar = true)
    {
        Modulos[modulo] = new InterruptoresModulo(campana, correo, agrupar);
        return this;
    }

    /// <summary>Correo encendido donde se admite: lo que necesitan las pruebas de recordatorios.</summary>
    public static AjustesDePrueba CorreoEncendido()
    {
        var a = new AjustesDePrueba();
        foreach (var m in Enum.GetValues<ModuloNotificacion>())
            a.Modulos[m] = new InterruptoresModulo(true, CatalogoNotificaciones.ModuloAdmiteCorreo(m), true);
        return a;
    }

    public static SmtpVigente ConSmtp(string host, string usuario, string password = "x") =>
        new(host, 587, true, usuario, password, "no-reply@prueba.local", "Prueba", 15,
            DesdePanel: false, HayPassword: password.Length > 0, PasswordIlegible: false);
}

internal static class NotificacionesDePrueba
{
    public static Diger.TramitesEstado.Infrastructure.Notifications.NotificacionService Servicio(
        Diger.TramitesEstado.Infrastructure.Persistence.AppDbContext ctx,
        IAjustesNotificaciones? ajustes = null, TimeProvider? reloj = null, NotificacionesOptions? opciones = null) =>
        new(ctx, ctx, ajustes ?? new AjustesDePrueba(), reloj ?? TimeProvider.System,
            Microsoft.Extensions.Options.Options.Create(opciones ?? new NotificacionesOptions()));
}
