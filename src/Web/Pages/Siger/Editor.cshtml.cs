using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.Siger;

// Antes era [Authorize(Roles = nameof(RolUsuario.Administrador))], que comparaba contra el
// código literal del rol: un rol personalizado con capacidad de administrador quedaba fuera.
[Authorize]
[Permission("Siger", AccionModulo.Editar, "Crear y editar fichas SIGER")]
public sealed class EditorModel(IApplicationDbContext ctx) : PageModel
{
    [BindProperty] public TramiteSigerForm Form { get; set; } = new();
    public bool EsNuevo => Form.Id == 0;

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken ct)
    {
        if (id is not null)
        {
            var t = await ctx.TramitesSiger.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (t is null) return NotFound();
            Form = new TramiteSigerForm
            {
                Id = t.Id, IdSiger = t.IdSiger, Codigo = t.Codigo, Nombre = t.Nombre,
                Institucion = t.Institucion, Sigla = t.Sigla, Dependencia = t.Dependencia,
                Descripcion = t.Descripcion, Objetivo = t.Objetivo, DirigidoA = t.DirigidoA,
                EstadoSiger = t.EstadoSiger, Publicado = t.Publicado,
                DisponibleEnLinea = t.DisponibleEnLinea, EnPlanDigitalizacion = t.EnPlanDigitalizacion,
                VigenciaDocumento = t.VigenciaDocumento, Temporalidad = t.Temporalidad,
                DiagramaUrl = t.DiagramaUrl, EnlacePrincipal = t.EnlacePrincipal,
                ObservacionesDiger = t.ObservacionesDiger,
                FechaIngreso = t.FechaIngreso, UltimaModificacion = t.UltimaModificacion
            };
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();

        if (Form.Id == 0)
        {
            var entity = new TramiteSiger
            {
                IdSiger = Form.IdSiger, Codigo = Form.Codigo!, Nombre = Form.Nombre!,
                Institucion = Form.Institucion!, Sigla = Form.Sigla, Dependencia = Form.Dependencia,
                Descripcion = Form.Descripcion, Objetivo = Form.Objetivo, DirigidoA = Form.DirigidoA,
                EstadoSiger = Form.EstadoSiger, Publicado = Form.Publicado,
                DisponibleEnLinea = Form.DisponibleEnLinea, EnPlanDigitalizacion = Form.EnPlanDigitalizacion,
                VigenciaDocumento = Form.VigenciaDocumento, Temporalidad = Form.Temporalidad,
                DiagramaUrl = Form.DiagramaUrl, EnlacePrincipal = Form.EnlacePrincipal,
                ObservacionesDiger = Form.ObservacionesDiger,
                FechaIngreso = Form.FechaIngreso, UltimaModificacion = Form.UltimaModificacion
            };
            ctx.TramitesSiger.Add(entity);
            await ctx.SaveChangesAsync(ct);
            TempData["SuccessMsg"] = "Tramite creado.";
            return RedirectToPage("/Siger/Detalle", new { id = entity.Id });
        }
        else
        {
            var entity = await ctx.TramitesSiger.FindAsync([Form.Id], ct);
            if (entity is null) return NotFound();

            entity.IdSiger = Form.IdSiger;
            entity.Codigo = Form.Codigo!;
            entity.Nombre = Form.Nombre!;
            entity.Institucion = Form.Institucion!;
            entity.Sigla = Form.Sigla;
            entity.Dependencia = Form.Dependencia;
            entity.Descripcion = Form.Descripcion;
            entity.Objetivo = Form.Objetivo;
            entity.DirigidoA = Form.DirigidoA;
            entity.EstadoSiger = Form.EstadoSiger;
            entity.Publicado = Form.Publicado;
            entity.DisponibleEnLinea = Form.DisponibleEnLinea;
            entity.EnPlanDigitalizacion = Form.EnPlanDigitalizacion;
            entity.VigenciaDocumento = Form.VigenciaDocumento;
            entity.Temporalidad = Form.Temporalidad;
            entity.DiagramaUrl = Form.DiagramaUrl;
            entity.EnlacePrincipal = Form.EnlacePrincipal;
            entity.ObservacionesDiger = Form.ObservacionesDiger;
            entity.FechaIngreso = Form.FechaIngreso;
            entity.UltimaModificacion = Form.UltimaModificacion;

            await ctx.SaveChangesAsync(ct);
            TempData["SuccessMsg"] = "Tramite actualizado.";
            return RedirectToPage("/Siger/Detalle", new { id = entity.Id });
        }
    }
}

public sealed class TramiteSigerForm
{
    public int Id { get; set; }
    public int IdSiger { get; set; }
    public string? Codigo { get; set; }
    public string? Nombre { get; set; }
    public string? Institucion { get; set; }
    public string? Sigla { get; set; }
    public string? Dependencia { get; set; }
    public string? Descripcion { get; set; }
    public string? Objetivo { get; set; }
    public string? DirigidoA { get; set; }
    public string? EstadoSiger { get; set; }
    public bool Publicado { get; set; }
    public bool DisponibleEnLinea { get; set; }
    public bool EnPlanDigitalizacion { get; set; }
    public string? VigenciaDocumento { get; set; }
    public string? Temporalidad { get; set; }
    public string? DiagramaUrl { get; set; }
    public string? EnlacePrincipal { get; set; }
    public string? ObservacionesDiger { get; set; }
    public DateTime? FechaIngreso { get; set; }
    public DateTime? UltimaModificacion { get; set; }
}
