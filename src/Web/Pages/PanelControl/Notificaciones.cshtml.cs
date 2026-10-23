using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.PanelControl.Notificaciones;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.PanelControl;

/// <summary>
/// Panel de control → Notificaciones. Un rechazo vuelve a la misma página con lo escrito (no
/// redirige): un redirect borraba el formulario y había que teclearlo todo de nuevo. La contraseña
/// es la excepción: nunca se vuelve a pintar.
/// </summary>
[Authorize]
[Permission(Clave, AccionModulo.Ver, "Ver la configuración de notificaciones del Panel de control")]
public sealed class NotificacionesModel(ISender sender, AccesoModulosService acceso) : PageModel
{
    public const string Clave = "PanelControl.Notificaciones";

    public PanelNotificacionesDto Panel { get; private set; } = default!;
    public bool PuedeEditar { get; private set; }

    [TempData] public string? Aviso { get; set; }
    public string? Error { get; private set; }
    public string? ErrorCorreo { get; private set; }
    public ResultadoPruebaCorreo? Prueba { get; private set; }

    [BindProperty] public int Horas   { get; set; }
    [BindProperty] public int Minutos { get; set; }
    [BindProperty] public List<ModuloForm> Modulos { get; set; } = [];
    [BindProperty] public CorreoForm Correo { get; set; } = new();

    public sealed class ModuloForm
    {
        public ModuloNotificacion Modulo  { get; set; }
        public bool               Campana { get; set; }
        public bool               Correo  { get; set; }
        public bool               Agrupar { get; set; }
    }

    public sealed class CorreoForm
    {
        public string  Host            { get; set; } = "";
        public int     Puerto          { get; set; } = 587;
        public bool    Ssl             { get; set; } = true;
        public string  Usuario         { get; set; } = "";
        public string? Password        { get; set; }
        public bool    QuitarPassword  { get; set; }
        public string  Remitente       { get; set; } = "";
        public string? NombreRemitente { get; set; }
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await CargarAsync(ct);
        LlenarDesdePanel(revision: true, modulos: true, correo: true);
    }

    [Permission(Clave, AccionModulo.Editar, "Cambiar la configuración de notificaciones y enviar el correo de prueba")]
    public async Task<IActionResult> OnPostRevisionAsync(CancellationToken ct)
    {
        try
        {
            await sender.Send(new GuardarRevisionCommand(Horas, Minutos), ct);
            Aviso = "Revisión automática guardada. Se aplica desde ahora.";
            return RedirectToPage();
        }
        catch (Exception ex) when (ex is DomainException or FluentValidation.ValidationException)
        {
            Error = Mensaje(ex);
            await CargarAsync(ct);
            LlenarDesdePanel(revision: false, modulos: true, correo: true);
            return Page();
        }
    }

    [Permission(Clave, AccionModulo.Editar, "Cambiar la configuración de notificaciones y enviar el correo de prueba")]
    public async Task<IActionResult> OnPostModulosAsync(CancellationToken ct)
    {
        await sender.Send(new GuardarModulosCommand(
            Modulos.Select(m => new InterruptoresInput(m.Modulo, m.Campana, m.Correo, m.Agrupar)).ToList()), ct);
        Aviso = "Módulos guardados.";
        return RedirectToPage();
    }

    [Permission(Clave, AccionModulo.Editar, "Cambiar la configuración de notificaciones y enviar el correo de prueba")]
    public async Task<IActionResult> OnPostCorreoAsync(CancellationToken ct)
    {
        try
        {
            await sender.Send(new GuardarCorreoCommand(Correo.Host, Correo.Puerto, Correo.Ssl, Correo.Usuario,
                Correo.Password, Correo.QuitarPassword, Correo.Remitente, Correo.NombreRemitente), ct);
            Aviso = "Correo saliente guardado. Use «Enviar correo de prueba» para comprobarlo.";
            return RedirectToPage();
        }
        catch (Exception ex) when (ex is DomainException or FluentValidation.ValidationException)
        {
            ErrorCorreo = Mensaje(ex);
            Correo.Password = null; // la contraseña no se vuelve a pintar, ni siquiera la recién escrita
            await CargarAsync(ct);
            LlenarDesdePanel(revision: true, modulos: true, correo: false);
            return Page();
        }
    }

    [Permission(Clave, AccionModulo.Editar, "Cambiar la configuración de notificaciones y enviar el correo de prueba")]
    public async Task<IActionResult> OnPostProbarAsync(CancellationToken ct)
    {
        try { Prueba = await sender.Send(new EnviarCorreoPruebaCommand(), ct); }
        catch (DomainException ex) { ErrorCorreo = ex.Message; }

        await CargarAsync(ct);
        LlenarDesdePanel(revision: true, modulos: true, correo: true);
        return Page();
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        Panel       = await sender.Send(new GetPanelNotificacionesQuery(), ct);
        PuedeEditar = await acceso.PuedeClaveAsync($"{Clave}.Editar", ct);
    }

    private void LlenarDesdePanel(bool revision, bool modulos, bool correo)
    {
        if (revision) { Horas = Panel.IntervaloMinutos / 60; Minutos = Panel.IntervaloMinutos % 60; }
        if (modulos)
            Modulos = Panel.Modulos.Select(m => new ModuloForm
                { Modulo = m.Modulo, Campana = m.Campana, Correo = m.Correo, Agrupar = m.Agrupar }).ToList();
        if (correo)
            Correo = new CorreoForm
            {
                Host = Panel.Correo.Host, Puerto = Panel.Correo.Puerto, Ssl = Panel.Correo.Ssl,
                Usuario = Panel.Correo.Usuario, Remitente = Panel.Correo.Remitente,
                NombreRemitente = Panel.Correo.NombreRemitente,
            };
    }

    private static string Mensaje(Exception ex) => ex is FluentValidation.ValidationException v
        ? string.Join(" ", v.Errors.Select(e => e.ErrorMessage).Distinct())
        : ex.Message;
}
