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
    public IReadOnlyList<CategoriaTramite> Categorias { get; private set; } = [];

    /// <summary>Solo informativo: recalculado en cada carga con la misma regla que usa la API
    /// pública (FichaPublicaCompletitud), para que el editor nunca contradiga lo que se publica.</summary>
    public IReadOnlyList<string> Faltantes { get; private set; } = [];
    public bool FichaCompleta => Faltantes.Count == 0;
    public bool PublicadoActual { get; private set; }
    public DateTime? UltimaRevision { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken ct)
    {
        Categorias = await ctx.CategoriasTramite.AsNoTracking().Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync(ct);

        if (id is not null)
        {
            var t = await ctx.TramitesSiger.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (t is null) return NotFound();
            Form = new TramiteSigerForm
            {
                Id = t.Id, IdSiger = t.IdSiger, Codigo = t.Codigo, Nombre = t.Nombre,
                Institucion = t.Institucion, Sigla = t.Sigla, Dependencia = t.Dependencia,
                Descripcion = t.Descripcion, Objetivo = t.Objetivo, DirigidoA = t.DirigidoA,
                EstadoSiger = t.EstadoSiger,
                DisponibleEnLinea = t.DisponibleEnLinea, EnPlanDigitalizacion = t.EnPlanDigitalizacion,
                VigenciaDocumento = t.VigenciaDocumento, Temporalidad = t.Temporalidad,
                DiagramaUrl = t.DiagramaUrl, EnlacePrincipal = t.EnlacePrincipal,
                ObservacionesDiger = t.ObservacionesDiger, FechaIngreso = t.FechaIngreso,
                CategoriaId = t.CategoriaId, Modalidad = t.Modalidad, EstaEnSol = t.EstaEnSol,
                SolUrl = t.SolUrl, CostoTexto = t.CostoTexto, CostoEsGratuito = t.CostoEsGratuito,
                TiempoTexto = t.TiempoTexto, EsPopular = t.EsPopular
            };
            PublicadoActual = t.Publicado;
            Faltantes = FichaPublicaCompletitud.CamposFaltantes(
                t.CategoriaId, t.Modalidad, t.TiempoTexto, t.CostoEsGratuito, t.EstaEnSol, t.SolUrl);
            UltimaRevision = t.UpdatedAt ?? t.UltimaModificacion ?? t.CreatedAt;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Categorias = await ctx.CategoriasTramite.AsNoTracking().Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(Form.SolUrl) && !(Form.SolUrl.StartsWith("http://") || Form.SolUrl.StartsWith("https://")))
            ModelState.AddModelError("Form.SolUrl", "El enlace a SOL debe ser una URL absoluta (http:// o https://).");
        if (Form.EstaEnSol && string.IsNullOrWhiteSpace(Form.SolUrl))
            ModelState.AddModelError("Form.SolUrl", "Si el trámite está en SOL, el enlace es obligatorio.");

        if (!ModelState.IsValid) return Page();

        if (Form.Id == 0)
        {
            var entity = new TramiteSiger
            {
                IdSiger = Form.IdSiger, Codigo = Form.Codigo!, Nombre = Form.Nombre!,
                Institucion = Form.Institucion!, Sigla = Form.Sigla, Dependencia = Form.Dependencia,
                Descripcion = Form.Descripcion, Objetivo = Form.Objetivo, DirigidoA = Form.DirigidoA,
                EstadoSiger = Form.EstadoSiger,
                DisponibleEnLinea = Form.DisponibleEnLinea, EnPlanDigitalizacion = Form.EnPlanDigitalizacion,
                VigenciaDocumento = Form.VigenciaDocumento, Temporalidad = Form.Temporalidad,
                DiagramaUrl = Form.DiagramaUrl, EnlacePrincipal = Form.EnlacePrincipal,
                ObservacionesDiger = Form.ObservacionesDiger, FechaIngreso = Form.FechaIngreso,
                CategoriaId = Form.CategoriaId, Modalidad = Form.Modalidad, EstaEnSol = Form.EstaEnSol,
                SolUrl = Form.SolUrl, CostoTexto = Form.CostoTexto, CostoEsGratuito = Form.CostoEsGratuito,
                TiempoTexto = Form.TiempoTexto, EsPopular = Form.EsPopular,
                SolVerificadoEl = string.IsNullOrWhiteSpace(Form.SolUrl) ? null : DateTime.UtcNow
            };
            entity.Publicado = CalcularPublicado(entity);
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
            entity.DisponibleEnLinea = Form.DisponibleEnLinea;
            entity.EnPlanDigitalizacion = Form.EnPlanDigitalizacion;
            entity.VigenciaDocumento = Form.VigenciaDocumento;
            entity.Temporalidad = Form.Temporalidad;
            entity.DiagramaUrl = Form.DiagramaUrl;
            entity.EnlacePrincipal = Form.EnlacePrincipal;
            entity.ObservacionesDiger = Form.ObservacionesDiger;
            entity.FechaIngreso = Form.FechaIngreso;
            entity.CategoriaId = Form.CategoriaId;
            entity.Modalidad = Form.Modalidad;
            entity.EstaEnSol = Form.EstaEnSol;
            entity.CostoTexto = Form.CostoTexto;
            entity.CostoEsGratuito = Form.CostoEsGratuito;
            entity.TiempoTexto = Form.TiempoTexto;
            entity.EsPopular = Form.EsPopular;

            // Solo se sella al cambiar el texto de la URL — no en cada guardado.
            if (!string.Equals(entity.SolUrl, Form.SolUrl, StringComparison.Ordinal))
                entity.SolVerificadoEl = string.IsNullOrWhiteSpace(Form.SolUrl) ? null : DateTime.UtcNow;
            entity.SolUrl = Form.SolUrl;

            entity.Publicado = CalcularPublicado(entity);

            await ctx.SaveChangesAsync(ct);
            TempData["SuccessMsg"] = "Tramite actualizado.";
            return RedirectToPage("/Siger/Detalle", new { id = entity.Id });
        }
    }

    /// <summary>Publicado es consecuencia del estado de la ficha, y de nada más: aprobada o
    /// completa se publica (D-02).</summary>
    /// <remarks>
    /// <para>
    /// <b>Hasta el 20-08-2026 exigía además que la ficha estuviera completa</b>, y eso tenía
    /// un efecto que nadie quería: el primer guardado que no llenara la ficha entera en el
    /// mismo paso la despublicaba. Un técnico llenando por tandas —la categoría de treinta
    /// fichas, luego la modalidad— veía el catálogo vaciarse mientras trabajaba. En producción
    /// había 49 fichas publicadas y ninguna cumplía la regla: la primera edición de cualquiera
    /// de ellas la habría borrado del portal del ciudadano.
    /// </para>
    /// <para>
    /// <b>Se separó por decisión del usuario (P-09, opción 1):</b> una ficha incompleta se
    /// queda publicada y HondurasÁgil enseña un guion donde falta el dato. Al ciudadano le
    /// sirve más saber que el trámite existe y quién lo atiende, que no encontrarlo.
    /// </para>
    /// <para>
    /// La completitud <b>no desaparece, deja de censurar</b>: FichaPublicaCompletitud sigue
    /// calculando qué falta, el editor y el listado lo siguen avisando al técnico, y la API
    /// pública lo sigue publicando en el campo FichaCompleta. Lo único que cambió es que ya no
    /// decide si el ciudadano puede ver el trámite.
    /// </para>
    /// </remarks>
    private static bool CalcularPublicado(TramiteSiger t) =>
        t.EstadoSiger is "Aprobado" or "Completo";
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
    public bool DisponibleEnLinea { get; set; }
    public bool EnPlanDigitalizacion { get; set; }
    public string? VigenciaDocumento { get; set; }
    public string? Temporalidad { get; set; }
    public string? DiagramaUrl { get; set; }
    public string? EnlacePrincipal { get; set; }
    public string? ObservacionesDiger { get; set; }
    public DateTime? FechaIngreso { get; set; }

    // ── Ficha pública (Ventanilla Digital) ──────────────────────────────────
    public int?    CategoriaId { get; set; }
    public string? Modalidad { get; set; }
    public bool    EstaEnSol { get; set; }
    public string? SolUrl { get; set; }
    public string? CostoTexto { get; set; }
    public bool?   CostoEsGratuito { get; set; }
    public string? TiempoTexto { get; set; }
    public bool    EsPopular { get; set; }
}
