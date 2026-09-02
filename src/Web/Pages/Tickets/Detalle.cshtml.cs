using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Infrastructure.Security;

namespace Diger.TramitesEstado.Web.Pages.Tickets;

[Authorize]
// Ver el detalle basta con Tickets.Ver; tomar, liberar, comentar o cambiar el estado son
// mutaciones y piden Tickets.Editar (ver los overrides por handler más abajo).
[Permission("Tickets", AccionModulo.Ver, "Ver tickets")]
public sealed class DetalleModel(ISender sender, ICurrentUserService currentUser, IWebHostEnvironment env, AccesoModulosService acceso) : PageModel
{
    public TicketDetailDto Ticket { get; private set; } = default!;
    public string? Error { get; set; }

    /// <summary>Antes era "el rol se llama Administrador"; ahora es la clave concreta que el
    /// servidor va a exigir en los handlers de mutación, así el botón y el gateo coinciden.</summary>
    public bool EsAdmin { get; private set; }

    public bool PuedeGestionar => EsAdmin;
    public bool EsAsignado => Ticket.AsignadoAId is Guid a && a == currentUser.UserId;
    public bool PuedeAtender => EsAdmin;

    public bool SinResponsable => Ticket.AsignadoAId is null;
    public bool PuedeTomar => SinResponsable && EsAdmin;
    public bool PuedeLiberar => !SinResponsable && EsAdmin;

    /// <summary>Los proyectos a los que contribuye este ticket, más los que se le pueden vincular.
    /// Ver la nota de <c>GetProyectosDeTicketQuery</c> sobre los que quedan fuera de alcance.</summary>
    public ProyectosVinculadosDto Proyectos { get; private set; } = new([], 0, []);

    /// <summary>Vincular pide <b>Proyectos.Editar</b>, no Tickets.Editar: la acción escribe en la
    /// ficha y en la bitácora del proyecto. Quien atiende tickets no necesariamente puede eso.</summary>
    public bool PuedeVincular { get; private set; }

    [BindProperty] public int     VinculoProyectoId { get; set; }
    [BindProperty] public string? VinculoNota       { get; set; }
    [BindProperty] public int     VinculoId         { get; set; }

    private async Task<bool> CargarAsync(int id, CancellationToken ct)
    {
        try { Ticket = await sender.Send(new GetTicketByIdQuery(id), ct); }
        catch (NotFoundException) { return false; }
        EsAdmin = await acceso.PuedeEditarAsync("Tickets", ct);

        // Los vínculos se cargan acá y no solo en OnGetAsync porque los handlers de POST también
        // devuelven Page() cuando fallan —comentar vacío, por ejemplo— y esa página tiene que
        // mostrar los proyectos que el ticket ya tenía, no una lista vacía que parece un dato.
        Proyectos     = await sender.Send(new GetProyectosDeTicketQuery(id), ct);
        PuedeVincular = await acceso.PuedeEditarAsync("Proyectos", ct);
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
        => await CargarAsync(id, ct) ? Page() : NotFound();

    /// <summary>
    /// Sirve un adjunto del ticket.
    ///
    /// <para>Pasa por <c>CargarAsync</c> a propósito: es la consulta que ya aplica el alcance, así
    /// que quien no puede abrir el ticket tampoco baja su archivo. Antes el enlace apuntaba directo
    /// a <c>/uploads/tickets/…</c>, que se servía sin sesión — ver <see cref="ArchivosProtegidos"/>.</para>
    /// </summary>
    public async Task<IActionResult> OnGetAdjuntoAsync(int id, int adjuntoId, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();

        var adjunto = Ticket.Adjuntos.FirstOrDefault(a => a.Id == adjuntoId);
        if (adjunto is null) return NotFound();

        var ruta = ArchivosProtegidos.Resolver(env, adjunto.Url);
        if (ruta is null) return NotFound();

        return PhysicalFile(ruta, ArchivosProtegidos.TipoContenido(adjunto.Nombre), adjunto.Nombre);
    }

    [Permission("Tickets", AccionModulo.Editar, "Crear y editar tickets")]
    public async Task<IActionResult> OnPostCambiarEstadoAsync(int id, EstadoTicket estado, string? nota, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();
        if (!PuedeAtender) return Forbid();
        try
        {
            await sender.Send(new CambiarEstadoTicketCommand(id, estado, nota), ct);
            TempData["SuccessMsg"] = "Estado actualizado.";
            return RedirectToPage(new { id });
        }
        catch (DomainException ex) { Error = ex.Message; return Page(); }
    }

    [Permission("Tickets", AccionModulo.Editar, "Crear y editar tickets")]
    public async Task<IActionResult> OnPostTomarAsync(int id, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();
        if (!PuedeTomar) return Forbid();
        if (!SinResponsable)
        {
            TempData["SuccessMsg"] = $"El ticket ya lo tomó {Ticket.AsignadoA}.";
            return RedirectToPage(new { id });
        }
        await sender.Send(new AsignarTicketCommand(id, currentUser.UserId), ct);
        TempData["SuccessMsg"] = "Tomaste el ticket. Ahora eres el responsable del seguimiento.";
        return RedirectToPage(new { id });
    }

    [Permission("Tickets", AccionModulo.Editar, "Crear y editar tickets")]
    public async Task<IActionResult> OnPostLiberarAsync(int id, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();
        if (!PuedeLiberar) return Forbid();
        await sender.Send(new AsignarTicketCommand(id, null), ct);
        TempData["SuccessMsg"] = "Ticket liberado. Queda disponible para que lo tome un responsable.";
        return RedirectToPage(new { id });
    }

    [Permission("Tickets", AccionModulo.Editar, "Crear y editar tickets")]
    public async Task<IActionResult> OnPostComentarAsync(int id, string texto, List<IFormFile>? archivos, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();
        if (!HttpContext.CanMutate()) return Forbid();
        if (string.IsNullOrWhiteSpace(texto))
        {
            Error = "El comentario no puede estar vacío.";
            return Page();
        }
        try
        {
            var adjuntos = await AdjuntoStorage.GuardarAsync(archivos, env, ct);
            await sender.Send(new AgregarComentarioTicketCommand(id, texto, adjuntos), ct);
            TempData["SuccessMsg"] = "Comentario agregado.";
            return RedirectToPage(new { id });
        }
        catch (DomainException ex) { Error = ex.Message; return Page(); }
    }

    // ── Vínculo con proyectos ─────────────────────────────────────
    // Se vincula desde acá y no solo desde la ficha del proyecto porque este es el momento en que
    // se sabe: quien atiende el ticket es quien reconoce que lo pedido es trabajo del proyecto.
    // La misma relación se gestiona desde los dos extremos, y en los dos exige Proyectos.Editar:
    // la acción escribe en la ficha y en la bitácora del proyecto, no en el ticket.

    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostVincularProyectoAsync(int id, CancellationToken ct)
    {
        try
        {
            await sender.Send(new VincularTicketCommand(VinculoProyectoId, id, VinculoNota), ct);
            TempData["SuccessMsg"] = "Ticket vinculado al proyecto.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { TempData["ErrorMsg"] = "Ese proyecto no existe o no está a su alcance."; }

        return RedirectToPage(new { id });
    }

    [Permission("Proyectos", AccionModulo.Editar, "Editar proyectos")]
    public async Task<IActionResult> OnPostDesvincularProyectoAsync(int id, int proyectoId, CancellationToken ct)
    {
        try
        {
            await sender.Send(new QuitarVinculoTicketCommand(proyectoId, VinculoId), ct);
            TempData["SuccessMsg"] = "Ticket desvinculado del proyecto.";
        }
        catch (DomainException ex) { TempData["ErrorMsg"] = ex.Message; }
        catch (NotFoundException)  { return NotFound(); }

        return RedirectToPage(new { id });
    }

    [Permission("Tickets", AccionModulo.Editar, "Crear y editar tickets")]
    public async Task<IActionResult> OnPostEnviarRecordatorioAsync(int id, string? mensaje, CancellationToken ct)
    {
        if (!await CargarAsync(id, ct)) return NotFound();
        try
        {
            await sender.Send(new Diger.TramitesEstado.Application.Notificaciones.Commands.EnviarRecordatorioManual.EnviarRecordatorioTicketCommand(id, mensaje), ct);
            TempData["SuccessMsg"] = "Notificación de recordatorio enviada exitosamente.";
            return RedirectToPage(new { id });
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return Page();
        }
    }
}
