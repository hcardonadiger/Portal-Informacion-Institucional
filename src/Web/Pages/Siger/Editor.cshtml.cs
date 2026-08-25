using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Pages.Siger;

// Antes era [Authorize(Roles = nameof(RolUsuario.Administrador))], que comparaba contra el
// código literal del rol: un rol personalizado con capacidad de administrador quedaba fuera.
[Authorize]
[Permission("Siger", AccionModulo.Editar, "Crear y editar fichas SIGER")]
public sealed class EditorModel(IApplicationDbContext ctx, IOptions<SolOptions> sol) : PageModel
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

    /// <summary>
    /// El prefijo fijo que se enseña pegado al campo del tramo (D-13):
    /// <c>https://sol.gob.hn/CONSUCOOP/</c>. Sin esto la persona escribe a ciegas y no tiene
    /// forma de saber qué dirección va a producir lo que teclea.
    /// </summary>
    public string PrefijoSol { get; private set; } = string.Empty;

    /// <summary>
    /// Arma el prefijo para la institución de la ficha. Si la institución no está en el catálogo
    /// se usa su propia sigla, que es exactamente lo que <c>RutaSolEfectiva</c> devolvería: así
    /// la pantalla enseña lo mismo que la API va a componer, y no una versión optimista.
    /// </summary>
    private async Task<string> PrefijoAsync(string? institucionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(institucionId))
            return DireccionSol.Prefijo(sol.Value.UrlBase, null);

        var ruta = await ctx.Instituciones.AsNoTracking()
            .Where(i => i.Id == institucionId)
            .Select(i => i.RutaSol ?? i.Id)
            .FirstOrDefaultAsync(ct);

        return DireccionSol.Prefijo(sol.Value.UrlBase, ruta ?? institucionId);
    }

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
                SolUrl = t.SolUrl, SolTramo = t.SolTramo, CostoTexto = t.CostoTexto, CostoEsGratuito = t.CostoEsGratuito,
                TiempoTexto = t.TiempoTexto, EsPopular = t.EsPopular
            };
            PublicadoActual = t.Publicado;
            Faltantes = FichaPublicaCompletitud.CamposFaltantes(
                t.CategoriaId, t.Modalidad, t.TiempoTexto, t.CostoEsGratuito, t.EstaEnSol, t.SolUrl, t.SolTramo);
            UltimaRevision = t.UpdatedAt ?? t.UltimaModificacion ?? t.CreatedAt;
            PrefijoSol = await PrefijoAsync(t.InstitucionId ?? t.Sigla, ct);
        }
        else
        {
            // Ficha nueva: todavía no hay institución elegida, así que se enseña el host solo.
            PrefijoSol = DireccionSol.Prefijo(sol.Value.UrlBase, null);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Categorias = await ctx.CategoriasTramite.AsNoTracking().Where(c => c.Activo).OrderBy(c => c.Orden).ToListAsync(ct);

        PrefijoSol = await PrefijoAsync(Form.Sigla, ct);

        // ── El enlace a SOL (D-13, D-14) ──────────────────────────────────────
        //
        // Ya no se captura la dirección completa: se captura el tramo final y la dirección se
        // compone con la ruta de la institución.
        //
        // La URL heredada se lee de la base y NO del formulario, aunque el formulario la enseñe.
        // Es un campo que la persona no puede editar, y confiar en que vuelva intacta en el POST
        // significaría borrarla en cada guardado si algún día deja de pintarse como campo oculto.
        // Un dato que no se edita se lee de donde vive.
        var heredadaActual = Form.Id == 0
            ? null
            : await ctx.TramitesSiger.AsNoTracking()
                .Where(x => x.Id == Form.Id).Select(x => x.SolUrl).FirstOrDefaultAsync(ct);

        var heredadaQueQueda = Form.QuitarSolHeredada ? null : heredadaActual;
        var tramo = DireccionSol.Normalizar(Form.SolTramo);

        // Pegar la dirección entera es el error más probable de quien ya conocía este campo
        // cuando pedía una URL completa. Decírselo con esas palabras vale más que el mensaje
        // genérico de forma, que le haría pensar que escribió mal un carácter.
        if (!string.IsNullOrWhiteSpace(Form.SolTramo) && Form.SolTramo.Contains("://"))
            ModelState.AddModelError("Form.SolTramo",
                "Escriba solo el tramo final, no la dirección completa: lo que va delante ya lo pone la institución.");
        else if (!DireccionSol.EsSegmentoValido(tramo))
            ModelState.AddModelError("Form.SolTramo",
                "El tramo solo puede llevar letras, números, guiones (-) y guiones bajos (_), " +
                "separando niveles con barra (/). Sin espacios ni tildes.");

        if (Form.EstaEnSol && tramo is null && string.IsNullOrWhiteSpace(heredadaQueQueda))
            ModelState.AddModelError("Form.SolTramo",
                "Si el trámite está en SOL, el tramo del enlace es obligatorio.");

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
                SolTramo = tramo, CostoTexto = Form.CostoTexto, CostoEsGratuito = Form.CostoEsGratuito,
                TiempoTexto = Form.TiempoTexto, EsPopular = Form.EsPopular,
                // Una ficha nueva nunca arrastra URL heredada: nace con tramo o sin nada.
                SolVerificadoEl = tramo is null ? null : DateTime.UtcNow
            };
            // Una ficha nace sin publicar, sea cual sea su estado. Promover y publicar son actos
            // distintos: el segundo se hace a mano en Siger/Publicacion (D-08, D-10).
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

            // Solo se sella al cambiar de verdad la dirección — no en cada guardado. Cuenta
            // tanto capturar o corregir el tramo como quitar la URL heredada: las dos cosas
            // cambian a dónde va el ciudadano.
            var cambioLaDireccion =
                !string.Equals(entity.SolTramo, tramo, StringComparison.Ordinal) ||
                !string.Equals(entity.SolUrl, heredadaQueQueda, StringComparison.Ordinal);

            if (cambioLaDireccion)
                entity.SolVerificadoEl = tramo is null && string.IsNullOrWhiteSpace(heredadaQueQueda)
                    ? null
                    : DateTime.UtcNow;

            entity.SolTramo = tramo;
            entity.SolUrl   = string.IsNullOrWhiteSpace(heredadaQueQueda) ? null : heredadaQueQueda;

            // Editar una ficha NO cambia si está publicada. Cuando este renglón la recalculaba,
            // corregir una tilde podía sacarla del portal del ciudadano —o meterla— sin que
            // nadie lo hubiera pedido. Publicar se decide en Siger/Publicacion (D-10).

            await ctx.SaveChangesAsync(ct);
            TempData["SuccessMsg"] = "Tramite actualizado.";
            return RedirectToPage("/Siger/Detalle", new { id = entity.Id });
        }
    }

}

public sealed class TramiteSigerForm
{
    public int Id { get; set; }
    /// <summary>Vacío en una ficha creada desde un expediente: no existe en SIGER.</summary>
    public int? IdSiger { get; set; }
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

    /// <summary>La URL completa heredada. <b>No se edita</b> (D-14): se enseña para que se sepa
    /// que está, y se puede quitar con <see cref="QuitarSolHeredada"/>.</summary>
    public string? SolUrl { get; set; }

    /// <summary>El tramo final que se captura hoy. Lo que va delante lo pone la institución.</summary>
    public string? SolTramo { get; set; }

    /// <summary>Marcar borra la URL heredada. Existe porque sin esto una dirección mal cargada
    /// —hay una apuntando a google.com, publicada— no se puede corregir desde el producto.</summary>
    public bool    QuitarSolHeredada { get; set; }
    public string? CostoTexto { get; set; }
    public bool?   CostoEsGratuito { get; set; }
    public string? TiempoTexto { get; set; }
    public bool    EsPopular { get; set; }
}
