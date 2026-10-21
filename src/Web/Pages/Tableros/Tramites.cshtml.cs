using Diger.TramitesEstado.Application.Dashboards.Queries.GetTramitesSeguimiento;
using Diger.TramitesEstado.Application.Expedientes.Commands.AgregarNotaSeguimiento;
using Diger.TramitesEstado.Application.Expedientes.Queries.GetBitacoraExpediente;
using Diger.TramitesEstado.Application.Expedientes.Queries.GetNotasSeguimiento;
using Diger.TramitesEstado.Application.Expedientes.Seguimiento;

namespace Diger.TramitesEstado.Web.Pages.Tableros;

[Authorize]
[Permission("Tableros", AccionModulo.Ver, "Ver tableros")]
public sealed class TramitesModel(ISender sender) : PageModel
{
    public TramitesSeguimientoDto Data { get; private set; } = default!;

    /// <summary>Id de institución seleccionado en el filtro (vacío = todas).</summary>
    public string? InstitucionId { get; private set; }
    public BandaAvance? Banda { get; private set; }
    public DateOnly? Desde { get; private set; }
    public DateOnly? Hasta { get; private set; }
    public EstadoTramite? Estado { get; private set; }

    /// <summary>Acción seleccionada en el filtro; null = todas. Puede traer el centinela
    /// <see cref="FiltroAccion.SinClasificar"/>.</summary>
    public string? Accion { get; private set; }

    /// <summary>Instituciones con algún expediente vigente, para el desplegable — no el catálogo
    /// completo (ver GetTramitesSeguimientoQueryHandler).</summary>
    public IReadOnlyList<InstitucionOpcionDto> Instituciones { get; private set; } = [];

    public async Task OnGetAsync(string? institucion, string? banda, DateOnly? desde, DateOnly? hasta, string? estado,
        string? accion, CancellationToken ct)
    {
        InstitucionId = string.IsNullOrWhiteSpace(institucion) ? null : institucion;
        Banda = Enum.TryParse<BandaAvance>(banda, ignoreCase: true, out var b) ? b : null;
        Desde = desde;
        Hasta = hasta;
        Estado = Enum.TryParse<EstadoTramite>(estado, ignoreCase: true, out var est) ? est : null;
        Accion = string.IsNullOrWhiteSpace(accion) ? null : accion;

        Data = await sender.Send(new GetTramitesSeguimientoQuery(InstitucionId, Banda, Desde, Hasta, Estado, Accion), ct);
        Instituciones = Data.Instituciones;
    }

    /// <summary>Bitácora completa de un expediente (la consume el modal de notas).</summary>
    /// <remarks>La consulta ya valida el alcance institucional y devuelve vacío si no aplica.</remarks>
    public async Task<IActionResult> OnGetNotasAsync(int expedienteId, CancellationToken ct)
    {
        var notas = await sender.Send(new GetNotasSeguimientoQuery(expedienteId), ct);
        return new JsonResult(notas.Select(n => new
        {
            texto = n.Texto,
            autor = n.CreadoPor,
            fecha = n.CreadoEl.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
        }));
    }

    /// <summary>Bitácora de auditoría de un expediente (la consume el modal de bitácora).</summary>
    /// <remarks>La consulta ya valida el alcance institucional y devuelve vacío si no aplica.</remarks>
    public async Task<IActionResult> OnGetBitacoraAsync(int expedienteId, CancellationToken ct)
    {
        var entradas = await sender.Send(new GetBitacoraExpedienteQuery(expedienteId), ct);
        return new JsonResult(entradas.Select(b => new
        {
            tipo = TipoLabel(b.Tipo),
            detalle = b.Detalle,
            actor = b.Actor,
            fecha = b.Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
        }));
    }

    private static string TipoLabel(TipoEventoBitacora t) => t switch
    {
        TipoEventoBitacora.CambioEstado => "Cambio de estado",
        TipoEventoBitacora.ModificacionHijos => "Trámites/documentos",
        TipoEventoBitacora.AvanceMetodologico => "Avance metodológico",
        TipoEventoBitacora.Validacion => "Validación",
        _ => t.ToString(),
    };

    // Escribe una nota de seguimiento sobre el expediente: es una mutación del expediente,
    // no del tablero, así que pide el permiso del módulo dueño del dato.
    [Permission("Expedientes", AccionModulo.Editar, "Crear y editar expedientes")]
    public async Task<IActionResult> OnPostNotaAsync(int expedienteId, string? texto, CancellationToken ct)
    {
        try
        {
            await sender.Send(new AgregarNotaSeguimientoCommand(expedienteId, texto ?? ""), ct);
            TempData["SuccessMsg"] = "Nota de seguimiento agregada.";
        }
        // La validación del comando llega como FluentValidation.ValidationException
        // desde ValidationBehavior, no como DomainException.
        catch (Exception ex) when (ex is DomainException or NotFoundException or FluentValidation.ValidationException)
        {
            TempData["ErrorMsg"] = ex.Message;
        }

        // Se conservan los filtros para no devolver al usuario a la lista completa.
        return RedirectToPage(new
        {
            institucion = Request.Query["institucion"].ToString(),
            banda       = Request.Query["banda"].ToString(),
            desde       = Request.Query["desde"].ToString(),
            hasta       = Request.Query["hasta"].ToString(),
            estado      = Request.Query["estado"].ToString(),
            accion      = Request.Query["accion"].ToString(),
        });
    }
}
