using Diger.TramitesEstado.Application.Siger.Bloqueo;
using Diger.TramitesEstado.Application.Siger.Importacion;
using Diger.TramitesEstado.Application.Siger.Importacion.Commands.ImportarFicha;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.Siger;

[Authorize]
[Permission("Siger", AccionModulo.Ver, "Ver el inventario SIGER")]
public sealed class DetalleModel(IApplicationDbContext ctx, ISender sender) : PageModel
{
    public TramiteSiger Tramite { get; private set; } = default!;
    public List<ExpedienteVinculadoRow> ExpedientesVinculados { get; private set; } = [];

    /// <summary>Qué le falta a esta ficha para poder publicarse. Vacía = completa.</summary>
    public IReadOnlyList<string> Faltantes { get; private set; } = [];


    /// <summary>Dónde se edita esta ficha (D-17).</summary>
    public BloqueoFichaDto Bloqueo { get; private set; } = new(false, null, null, null, false);

    /// <summary>
    /// Expedientes de la misma institución a los que se puede traer esta ficha (D-06).
    /// </summary>
    /// <remarks>
    /// Solo de la misma institución: importar una ficha de ADUANAS a un expediente de SALUD
    /// produciría un trámite que dice pertenecer a dos instituciones, y el pase de vuelta le
    /// cambiaría la institución a la ficha sin que nadie lo pidiera. Los buckets de importación
    /// no salen en la lista porque tienen su propia opción, que no obliga a saber su código.
    /// </remarks>
    public List<(int Id, string Codigo)> ExpedientesDestino { get; private set; } = [];

    public string? ErrorImportacion { get; private set; }

    /// <summary>
    /// Trae la ficha a un expediente y la deja enlazada —y con eso bloqueada (D-17)—.
    /// </summary>
    /// <remarks>
    /// Pide permiso de edición sobre expedientes además del de ver SIGER que cubre la página:
    /// importar crea un trámite dentro de un expediente, y poder consultar el inventario no es lo
    /// mismo que poder escribir en el trabajo de levantamiento de otra persona.
    /// </remarks>
    [Permission("Expedientes", AccionModulo.Editar, "Traer fichas SIGER a un expediente")]
    public async Task<IActionResult> OnPostImportarAsync(int id, int? expedienteId, CancellationToken ct)
    {
        try
        {
            var r = await sender.Send(new ImportarFichaCommand(id, expedienteId), ct);

            TempData["SuccessMsg"] = r.EnBucket
                ? $"Ficha traída al contenedor de importados {r.ExpedienteCodigo}" +
                  (r.BucketCreado ? ", que se creó ahora." : ".")
                : $"Ficha traída al expediente {r.ExpedienteCodigo}.";

            return RedirectToPage("/Expedientes/Editor", new { id = r.ExpedienteId });
        }
        catch (DomainException ex)
        {
            TempData["ErrorMsg"] = ex.Message;
            return RedirectToPage("/Siger/Detalle", new { id });
        }
    }
    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var t = await ctx.TramitesSiger.AsNoTracking()
            .Include(x => x.Pasos.OrderBy(p => p.NumeroPaso))
            .Include(x => x.Requisitos.OrderBy(r => r.Numero))
            .Include(x => x.Entregables.OrderBy(e => e.Numero))
            .Include(x => x.LugaresAtencion.OrderBy(l => l.Numero))
            .Include(x => x.Enlaces.OrderBy(e => e.Numero))
            .Include(x => x.TareasDigitalizacion.OrderBy(d => d.NumeroTarea))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (t is null) return NotFound();
        Tramite = t;

        Faltantes = FichaPublicaCompletitud.CamposFaltantes(
            t.CategoriaId, t.Modalidad, t.TiempoTexto, t.CostoEsGratuito, t.EstaEnSol, t.SolUrl, t.SolTramo);

        Bloqueo = await sender.Send(new GetBloqueoFichaQuery(id), ct);

        if (!Bloqueo.Bloqueada && !string.IsNullOrWhiteSpace(t.InstitucionId))
        {
            ExpedientesDestino = (await ctx.Expedientes.AsNoTracking().SinBuckets()
                .Where(e => e.InstitucionId == t.InstitucionId)
                .OrderByDescending(e => e.Id)
                .Select(e => new { e.Id, e.Codigo })
                .Take(50)
                .ToListAsync(ct))
                .Select(x => (x.Id, x.Codigo)).ToList();
        }

        var raw = await ctx.Tramites.AsNoTracking()
            .Where(et => et.TramiteSigerId == id)
            .Join(ctx.Expedientes, et => et.ExpedienteId, e => e.Id,
                (et, e) => new { e.Id, e.Codigo, e.Institucion, et.NombreTramite })
            .ToListAsync(ct);
        ExpedientesVinculados = raw.Select(x =>
            new ExpedienteVinculadoRow(x.Id, x.Codigo, x.Institucion, x.NombreTramite)).ToList();

        return Page();
    }

    [Permission("Siger", AccionModulo.Eliminar, "Eliminar fichas SIGER")]
    public async Task<IActionResult> OnPostEliminarAsync(int id, CancellationToken ct)
    {
        // El chequeo de rol por nombre que había acá lo sustituye el [Permission] de arriba,
        // que PermissionPageFilter resuelve contra la matriz antes de entrar al handler.
        var t = await ctx.TramitesSiger.FindAsync([id], ct);
        if (t is null) return NotFound();
        ctx.TramitesSiger.Remove(t);

        Faltantes = FichaPublicaCompletitud.CamposFaltantes(
            t.CategoriaId, t.Modalidad, t.TiempoTexto, t.CostoEsGratuito, t.EstaEnSol, t.SolUrl, t.SolTramo);
        await ctx.SaveChangesAsync(ct);
        TempData["SuccessMsg"] = "Tramite eliminado.";
        return RedirectToPage("/Siger/Index");
    }
}

public sealed record ExpedienteVinculadoRow(int Id, string Codigo, string Institucion, string NombreTramite);
