using Diger.TramitesEstado.Application.Siger.Promocion.Commands.PasarASiger;
using Diger.TramitesEstado.Application.Siger.Promocion.Queries.GetVistaPreviaPase;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Diger.TramitesEstado.Application.Instituciones.Commands.CrearInstitucion;
using Diger.TramitesEstado.Application.Tickets.Common;
using Diger.TramitesEstado.Application.Tickets.Queries.GetUsuariosAsignables;
using Diger.TramitesEstado.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.Expedientes;

[Authorize]
// Toda la página es de edición (incluidos los OnGet* que alimentan sus buscadores por AJAX):
// para consultar un expediente sin modificarlo está Resumen.cshtml, que pide Expedientes.Ver.
[Permission("Expedientes", AccionModulo.Editar, "Crear y editar expedientes")]
public sealed class EditorModel(
    ISender sender, IInstitucionRepository institucionRepo,
    ICurrentUserService currentUser, IWebHostEnvironment env,
    IApplicationDbContext db, AccesoModulosService acceso) : PageModel
{
    public int?    ExpId   { get; private set; }
    public string  Codigo  { get; private set; } = "";
    public string? ExpJson { get; private set; }   // OriginalExpedienteDto serializado (edición)
    public List<string> Plantillas { get; private set; } = [];

    /// <summary>El catálogo de categorías, para el desplegable de la ficha pública (Fase 8).
    /// Es la misma tabla que usa la ficha SIGER: dos catálogos distintos harían que promover un
    /// trámite cambiara su categoría.</summary>
    public List<CategoriaTramite> Categorias { get; private set; } = [];

    /// <summary>Si quien mira puede además escribir en el inventario SIGER. Sin esto el botón de
    /// «Pasar a SIGER» se ofrecería a quien el servidor va a rechazar.</summary>
    public bool PuedeSiger { get; private set; }

    /// <summary>Cada ficha enlazada al expediente, con su código y si nació acá o vino de SIGER.
    /// La clave es el id como texto porque viaja a JavaScript como objeto.</summary>
    public Dictionary<string, FichaEnlazadaVm> FichasEnlazadas { get; private set; } = [];
    public IReadOnlyList<UsuarioAsignableDto> Usuarios { get; private set; } = [];

    public bool EsContraparte { get; private set; }
    public bool EstaEntregaVencida { get; private set; }
    public bool EstaBloqueadoContraparte { get; private set; }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>Distingue al equipo DIGER de la contraparte institucional: enciende el modo
    /// edición completo y habilita crear un expediente desde cero. Antes era el nombre del rol;
    /// ahora es la clave que el servidor exige en esta misma página.</summary>
    public bool EsAdmin { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken ct)
    {
        EsAdmin    = await acceso.PuedeEditarAsync("Expedientes", ct);
        Plantillas = await sender.Send(new Diger.TramitesEstado.Application.Expedientes.Plantillas.GetNombresPlantillasActivasQuery(), ct);
        Usuarios   = await sender.Send(new GetUsuariosAsignablesQuery(), ct);
        Categorias = await db.CategoriasTramite.AsNoTracking().Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync(ct);
        PuedeSiger = await acceso.PuedeEditarAsync("Siger", ct);
        if (id is null && !EsAdmin)
            return Forbid();

        if (id is null) return Page();

        try
        {
            var detalle = await sender.Send(new GetExpedienteByIdQuery(id.Value), ct);
            ExpId   = detalle.Id;
            Codigo  = detalle.Codigo;

            if (detalle.Datos.ContraparteUsuarioId.HasValue && detalle.Datos.ContraparteUsuarioId == currentUser.UserId)
            {
                EsContraparte = true;
                var hoy = DateOnly.FromDateTime(DateTime.Today);
                EstaEntregaVencida = detalle.Datos.FechaLimiteEntrega.HasValue && detalle.Datos.FechaLimiteEntrega.Value < hoy;
                EstaBloqueadoContraparte = EstaEntregaVencida ||
                    (detalle.Datos.EstadoExpediente != EstadoExpediente.EnExploracion && detalle.Datos.EstadoExpediente != EstadoExpediente.EnLevantamiento);
            }

            if (!EsAdmin && !EsContraparte)
                return Forbid();

            // ── De dónde salió cada ficha enlazada (Fase 11) ──────────────
            //
            // Un trámite enlazado puede venir de dos sitios opuestos: se trajo del inventario de
            // SIGER, o la ficha nació acá y se promovió. La pantalla los pintaba iguales, y no lo
            // son: en el primero DIGER está modelando algo que ya existía; en el segundo está
            // publicando algo que no existía. Confundirlos lleva a buscar en SIGER una ficha que
            // nunca estuvo ahí.
            //
            // La diferencia la da IdSiger: vacío significa que la ficha no existe en SIGER.
            var enlazados = detalle.Datos.Tramites
                .Where(t => t.TramiteSigerId is not null)
                .Select(t => t.TramiteSigerId!.Value)
                .Distinct().ToList();

            if (enlazados.Count > 0)
            {
                FichasEnlazadas = await db.TramitesSiger.AsNoTracking()
                    .Where(f => enlazados.Contains(f.Id))
                    .Select(f => new { f.Id, f.Codigo, EsPromovida = f.IdSiger == null })
                    .ToDictionaryAsync(f => f.Id.ToString(),
                                       f => new FichaEnlazadaVm(f.Codigo, f.EsPromovida), ct);
            }

            var original = OriginalShapeMapper.FromInput(detalle.Datos);
            ExpJson = JsonSerializer.Serialize(original, JsonOpts);
            return Page();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Autocompletado de contactos por institución (consumido por expediente.js).</summary>
    public async Task<IActionResult> OnGetContactosAsync(string? institucion, CancellationToken ct)
        => new JsonResult(await sender.Send(new GetContactosPorInstitucionQuery(institucion ?? ""), ct));

    /// <summary>Busca una plantilla de Marco Legal/Requisitos por nombre exacto de trámite (copiado automático en el wizard).</summary>
    public async Task<IActionResult> OnGetPlantillaAsync(string? nombre, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return new JsonResult(null);
        var plantilla = await sender.Send(new GetPlantillaPorNombreQuery(nombre), ct);
        return new JsonResult(plantilla);
    }


    // ── Pasar a SIGER (Fase 9) ────────────────────────────────────────────
    //
    // Los dos manejadores trabajan sobre lo GUARDADO, no sobre lo que hay en pantalla. El editor
    // es un formulario que vive en el navegador y no manda nada hasta que alguien guarda; leer de
    // la base es lo único honesto, y por eso el diálogo lo dice con todas sus letras.
    //
    // Piden permiso de edición sobre SIGER además del de expedientes que cubre la página entera:
    // pasar un trámite crea o sobrescribe una ficha del inventario, y poder modelar un expediente
    // no es lo mismo que poder escribir en el catálogo que ve el ciudadano.

    public async Task<IActionResult> OnGetVistaPreviaPaseAsync(int id, int tramiteIndex, CancellationToken ct)
    {
        if (!await acceso.PuedeEditarAsync("Siger", ct)) return Forbid();

        try
        {
            var previa = await sender.Send(new GetVistaPreviaPaseQuery(id, tramiteIndex), ct);
            return new JsonResult(previa);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostPasarASigerAsync(int id, int tramiteIndex, CancellationToken ct)
    {
        if (!await acceso.PuedeEditarAsync("Siger", ct)) return Forbid();

        try
        {
            var r = await sender.Send(new PasarASigerCommand(id, tramiteIndex), ct);
            return new JsonResult(new { ok = true, r.TramiteSigerId, r.Codigo, r.FueCreada, r.VersionArchivada });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (DomainException ex)
        {
            return new JsonResult(new { ok = false, error = ex.Message });
        }
    }
    public async Task<IActionResult> OnGetBuscarSigerAsync(string? q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return new JsonResult(Array.Empty<object>());

        var term = q.Trim();
        var results = await db.TramitesSiger.AsNoTracking()
            .Where(t => t.Nombre.Contains(term) || t.Codigo.Contains(term) || (t.Sigla != null && t.Sigla.Contains(term)))
            .OrderBy(t => t.Nombre)
            .Take(20)
            .Select(t => new
            {
                t.Id, t.Codigo, t.Nombre, t.Institucion, t.Sigla,
                t.Descripcion, t.Objetivo, t.DirigidoA, t.Dependencia
            })
            .ToListAsync(ct);

        return new JsonResult(results);
    }

    public async Task<IActionResult> OnGetDetalleSigerAsync([FromQuery] int sigerId, CancellationToken ct)
    {
        var t = await db.TramitesSiger.AsNoTracking()
            .Include(x => x.Pasos.Where(p => p.NumeroPaso > 0).OrderBy(p => p.NumeroPaso))
            .Include(x => x.Requisitos.OrderBy(r => r.Numero))
            .Include(x => x.Entregables.OrderBy(e => e.Numero))
            .Include(x => x.LugaresAtencion.OrderBy(l => l.Numero))
            .Include(x => x.Enlaces.OrderBy(e => e.Numero))
            .FirstOrDefaultAsync(x => x.Id == sigerId, ct);

        if (t is null) return NotFound();

        return new JsonResult(new
        {
            t.Id, t.Codigo, t.Nombre, t.Institucion, t.Sigla, t.Dependencia,
            t.Descripcion, t.Objetivo, t.DirigidoA, t.DisponibleEnLinea,
            t.EnlacePrincipal, t.VigenciaDocumento, t.Temporalidad,
            pasos = t.Pasos.Select(p => new
            {
                p.NumeroPaso, p.Descripcion, p.LugarDependencia,
                p.SalidaResultado, p.TiempoRegistrado
            }),
            requisitos = t.Requisitos.Select(r => new
            {
                r.Numero, r.Requisito, r.Tipo, r.DocumentoSoporte, r.Formato
            }),
            entregables = t.Entregables.Select(e => new
            {
                e.Numero, e.Entregable, e.Formato, e.Presentacion
            }),
            lugares = t.LugaresAtencion.Select(l => new
            {
                l.Numero, l.Lugar, l.Ciudad, l.Direccion, l.Telefonos
            }),
            enlaces = t.Enlaces.Select(e => new { e.Numero, e.Url, e.Tipo })
        });
    }

    /// <summary>Sube un documento de "Documentación solicitada" y devuelve su URL (consumido por expediente.js).</summary>
    public async Task<IActionResult> OnPostSubirDocumentoAsync(IFormFile archivo, CancellationToken ct)
    {
        if (!EsAdmin)
            return Forbid();

        var guardados = await AdjuntoStorage.GuardarAsync([archivo], env, ct, carpeta: "expedientes");
        return new JsonResult(new { url = guardados.FirstOrDefault()?.Url });
    }

    public async Task<IActionResult> OnPostAsync(int? id, [FromBody] OriginalExpedienteDto datos, CancellationToken ct)
    {
        var esContraparte = false;
        if (id.HasValue)
        {
            var exp = await sender.Send(new GetExpedienteByIdQuery(id.Value), ct);
            if (exp.Datos.ContraparteUsuarioId.HasValue && exp.Datos.ContraparteUsuarioId == currentUser.UserId)
            {
                esContraparte = true;
                var hoy = DateOnly.FromDateTime(DateTime.Today);
                if (exp.Datos.FechaLimiteEntrega.HasValue && exp.Datos.FechaLimiteEntrega.Value < hoy)
                    return new JsonResult(new { error = "El plazo de entrega ha vencido. El expediente se encuentra bloqueado." }) { StatusCode = 400 };
            }
        }

        if (!EsAdmin && !esContraparte)
            return Forbid();

        // Resolver la institución (el editor envía el nombre)
        var instituciones = await institucionRepo.GetAllActivasAsync(ct);
        var inst = instituciones.FirstOrDefault(i =>
            string.Equals(i.Nombre, datos.Inst?.Trim(), StringComparison.OrdinalIgnoreCase));
        var institucionId = inst?.Id ?? string.Empty;

        // Opción "Otra" del editor: la institución escrita a mano no está en el catálogo.
        // Sólo se da de alta si el usuario lo pidió explícitamente con la casilla, porque
        // el catálogo es compartido y un error de tipeo quedaría permanente.
        if (inst is null && datos.InstRegistrar && EsAdmin && !string.IsNullOrWhiteSpace(datos.Inst))
        {
            var nombre = datos.Inst.Trim();
            var nuevoId = NormalizarInstitucionId(nombre);
            if (string.IsNullOrWhiteSpace(nuevoId))
                return new JsonResult(new { error = $"No se puede derivar un identificador del nombre '{nombre}'. Debe contener al menos una letra o un número." }) { StatusCode = 400 };

            try
            {
                institucionId = await sender.Send(new CrearInstitucionCommand(nuevoId, nombre), ct);
            }
            catch (DomainException ex)
            {
                // Nombre ya tomado (p. ej. por una institución inactiva, que no aparece en el catálogo activo).
                return new JsonResult(new { error = ex.Message }) { StatusCode = 400 };
            }
        }

        var input = OriginalShapeMapper.ToInput(datos, institucionId);

        int expedienteId;
        if (id is null)
            expedienteId = await sender.Send(new CrearExpedienteCommand(input), ct);
        else
        {
            await sender.Send(new ActualizarExpedienteCommand(id.Value, input), ct);
            expedienteId = id.Value;
        }

        return new JsonResult(new { id = expedienteId });
    }

    /// <summary>
    /// Deriva el Id de catálogo a partir del nombre. El dominio sólo acepta A-Z y 0-9,
    /// así que hay que quitar los acentos primero: <c>char.IsLetterOrDigit</c> acepta
    /// 'Í'/'Ó' y la creación fallaría con nombres como "SECRETARÍA…".
    /// Misma derivación que usa <c>ContactoFeeder</c> al dar de alta instituciones.
    /// </summary>
    private static string NormalizarInstitucionId(string nombre)
    {
        var desc = nombre.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(desc.Length);
        foreach (var ch in desc)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9') sb.Append(ch);
        }
        return sb.ToString();
    }

    public async Task<IActionResult> OnPostEnviarRecordatorioAsync(int id, string? mensaje, CancellationToken ct)
    {
        try
        {
            await sender.Send(new Diger.TramitesEstado.Application.Notificaciones.Commands.EnviarRecordatorioManual.EnviarRecordatorioExpedienteCommand(id, mensaje), ct);
            TempData["SuccessMsg"] = "Notificación de recordatorio enviada exitosamente.";
            return RedirectToPage(new { id });
        }
        catch (Exception ex)
        {
            TempData["ErrorMsg"] = ex.Message;
            return RedirectToPage(new { id });
        }
    }
}

/// <summary>Lo que la pantalla necesita saber de una ficha SIGER enlazada a un trámite.</summary>
/// <param name="EsPromovida">Cierto si la ficha nació en este portal y no existe en el inventario
/// de SIGER. Es lo que distingue las dos insignias.</param>
public sealed record FichaEnlazadaVm(string Codigo, bool EsPromovida);
