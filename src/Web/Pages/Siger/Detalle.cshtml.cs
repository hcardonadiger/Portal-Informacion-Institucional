using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.Siger;

[Authorize]
[Permission("Siger", AccionModulo.Ver, "Ver el inventario SIGER")]
public sealed class DetalleModel(IApplicationDbContext ctx) : PageModel
{
    public TramiteSiger Tramite { get; private set; } = default!;
    public List<ExpedienteVinculadoRow> ExpedientesVinculados { get; private set; } = [];

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
        await ctx.SaveChangesAsync(ct);
        TempData["SuccessMsg"] = "Tramite eliminado.";
        return RedirectToPage("/Siger/Index");
    }
}

public sealed record ExpedienteVinculadoRow(int Id, string Codigo, string Institucion, string NombreTramite);
