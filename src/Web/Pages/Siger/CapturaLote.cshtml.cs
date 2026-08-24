using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.Siger;

/// <summary>Asignar categoría/modalidad/tiempo/costo a una selección de trámites filtrada por
/// institución, en una sola pantalla. El plan lo marca como obligatorio para la Fase 3: con un
/// formulario de uno en uno, cientos de fichas por piloto son meses de trabajo humano insufrible.</summary>
[Authorize]
[Permission("Siger", AccionModulo.Editar, "Captura por lotes de fichas SIGER")]
public sealed class CapturaLoteModel(IApplicationDbContext ctx) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? InstitucionId { get; set; }
    [BindProperty(SupportsGet = true)] public bool SoloIncompletas { get; set; }

    [BindProperty] public List<int> Seleccionados { get; set; } = [];
    [BindProperty] public int? CategoriaId { get; set; }
    [BindProperty] public string? Modalidad { get; set; }
    [BindProperty] public string? TiempoTexto { get; set; }
    [BindProperty] public string? CostoTexto { get; set; }
    [BindProperty] public string? CostoEsGratuito { get; set; }

    public IReadOnlyList<Institucion> Instituciones { get; private set; } = [];
    public IReadOnlyList<CategoriaTramite> Categorias { get; private set; } = [];
    public IReadOnlyList<FilaLoteDto> Filas { get; private set; } = [];

    public sealed record FilaLoteDto(
        int Id, string Codigo, string Nombre, string? CategoriaNombre, string? Modalidad,
        string? TiempoTexto, string? CostoTexto, bool? CostoEsGratuito, bool EstaEnSol, bool FichaCompleta);

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Seleccionados.Count > 0)
        {
            var entidades = await ctx.TramitesSiger.Where(t => Seleccionados.Contains(t.Id)).ToListAsync(ct);
            foreach (var t in entidades)
            {
                // Solo se toca lo que el operador explícitamente llenó — dejar un campo vacío
                // en el panel de lote NO borra lo que ya tenía cada trámite.
                if (CategoriaId is not null) t.CategoriaId = CategoriaId;
                if (!string.IsNullOrWhiteSpace(Modalidad)) t.Modalidad = Modalidad;
                if (!string.IsNullOrWhiteSpace(TiempoTexto)) t.TiempoTexto = TiempoTexto;
                if (!string.IsNullOrWhiteSpace(CostoTexto)) t.CostoTexto = CostoTexto;
                t.CostoEsGratuito = CostoEsGratuito switch
                {
                    "true" => true,
                    "false" => false,
                    _ => t.CostoEsGratuito
                };

                // La captura por lotes no toca la publicación: llenar campos y decidir qué ve el
                // ciudadano son cosas distintas, y esta pantalla existe para lo primero. Antes
                // recalculaba la bandera acá, que era donde más dolía —la pantalla de llenar por
                // tandas despublicando justo lo que el técnico acababa de mejorar.
            }
            await ctx.SaveChangesAsync(ct);
            TempData["SuccessMsg"] = $"{entidades.Count} trámite(s) actualizados.";
        }

        await CargarAsync(ct);
        return Page();
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        Instituciones = await ctx.Instituciones.AsNoTracking().Where(i => i.Activo).OrderBy(i => i.Nombre).ToListAsync(ct);
        Categorias = await ctx.CategoriasTramite.AsNoTracking().Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync(ct);
        var categoriaNombres = Categorias.ToDictionary(c => c.Id, c => c.Nombre);

        var q = ctx.TramitesSiger.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(InstitucionId))
            q = q.Where(t => t.InstitucionId == InstitucionId);

        var filas = await q.OrderBy(t => t.Nombre)
            .Select(t => new { t.Id, t.Codigo, t.Nombre, t.CategoriaId, t.Modalidad, t.TiempoTexto, t.CostoTexto, t.CostoEsGratuito, t.EstaEnSol, t.SolUrl })
            .ToListAsync(ct);

        var todas = filas.Select(f => new FilaLoteDto(
            f.Id, f.Codigo, f.Nombre,
            f.CategoriaId is { } cid && categoriaNombres.TryGetValue(cid, out var n) ? n : null,
            f.Modalidad, f.TiempoTexto, f.CostoTexto, f.CostoEsGratuito, f.EstaEnSol,
            FichaPublicaCompletitud.Evaluar(f.CategoriaId, f.Modalidad, f.TiempoTexto, f.CostoEsGratuito, f.EstaEnSol, f.SolUrl)));

        Filas = (SoloIncompletas ? todas.Where(f => !f.FichaCompleta) : todas).ToList();
    }
}
