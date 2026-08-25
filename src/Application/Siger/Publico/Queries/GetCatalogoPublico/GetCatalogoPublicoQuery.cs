namespace Diger.TramitesEstado.Application.Siger.Publico.Queries.GetCatalogoPublico;

/// <summary>Catálogo público paginado — GET /api/v1/tramites.</summary>
public sealed record GetCatalogoPublicoQuery(
    string? Busqueda = null,
    int?    Categoria = null,
    string? Institucion = null,
    string? Modalidad = null,
    bool    SoloGratuitos = false,
    bool    SoloEnSol = false,
    bool    SoloFichasCompletas = false,
    string? Orden = null,
    int     Pagina = 1,
    int     Tamano = 20) : IRequest<CatalogoPublicoDto>;

public sealed class GetCatalogoPublicoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetCatalogoPublicoQuery, CatalogoPublicoDto>
{
    public async Task<CatalogoPublicoDto> Handle(GetCatalogoPublicoQuery q, CancellationToken ct)
    {
        var pagina = q.Pagina < 1 ? 1 : q.Pagina;
        var tamano = q.Tamano is < 1 or > 100 ? 20 : q.Tamano;

        // Sin excepción: es la única condición que hace público al catálogo.
        var query = ctx.TramitesSiger.AsNoTracking().Where(t => t.Publicado);

        if (!string.IsNullOrWhiteSpace(q.Busqueda))
        {
            var b = q.Busqueda.Trim();
            query = query.Where(t =>
                t.Nombre.Contains(b) ||
                (t.Descripcion != null && t.Descripcion.Contains(b)) ||
                (t.Objetivo != null && t.Objetivo.Contains(b)));
        }

        if (q.Categoria is { } categoriaId)
            query = query.Where(t => t.CategoriaId == categoriaId);

        if (!string.IsNullOrWhiteSpace(q.Institucion))
            query = query.Where(t => t.InstitucionId == q.Institucion);

        if (!string.IsNullOrWhiteSpace(q.Modalidad))
        {
            // modalidad=Virtual también trae los híbridos (ModalidadPublica.EsDigital).
            query = q.Modalidad == ModalidadPublica.Virtual
                ? query.Where(t => t.Modalidad == ModalidadPublica.Virtual || t.Modalidad == ModalidadPublica.Hibrido)
                : query.Where(t => t.Modalidad == q.Modalidad);
        }

        if (q.SoloGratuitos)
            query = query.Where(t => t.CostoEsGratuito == true);

        if (q.SoloEnSol)
            query = query.Where(t => t.EstaEnSol);

        if (q.SoloFichasCompletas)
            query = query.Where(t =>
                t.CategoriaId != null && t.Modalidad != null && t.TiempoTexto != null &&
                t.CostoEsGratuito != null && (!t.EstaEnSol || t.SolUrl != null || t.SolTramo != null));

        var total = await query.CountAsync(ct);

        query = q.Orden switch
        {
            "nombre" => query.OrderBy(t => t.Nombre),
            _        => query.OrderByDescending(t => t.EsPopular).ThenBy(t => t.Nombre)
        };

        var filas = await query
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .Select(t => new
            {
                t.Codigo, t.Nombre, t.InstitucionId, t.Institucion,
                t.CategoriaId, t.Modalidad, t.EsPopular,
                t.CostoEsGratuito, t.CostoTexto, t.TiempoTexto, t.EstaEnSol, t.SolUrl, t.SolTramo
            })
            .ToListAsync(ct);

        var categoriaIds = filas.Where(f => f.CategoriaId != null).Select(f => f.CategoriaId!.Value).Distinct().ToList();
        var categoriaNombres = await ctx.CategoriasTramite.AsNoTracking()
            .Where(c => categoriaIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nombre, ct);

        var items = filas.Select(f => new TramiteResumenPublicoDto(
            f.Codigo, f.Nombre, f.InstitucionId ?? "", f.Institucion,
            f.CategoriaId,
            f.CategoriaId is { } cid && categoriaNombres.TryGetValue(cid, out var nombre) ? nombre : null,
            f.Modalidad, f.EsPopular, f.CostoEsGratuito, f.CostoTexto, f.TiempoTexto, f.EstaEnSol,
            FichaPublicaCompletitud.Evaluar(f.CategoriaId, f.Modalidad, f.TiempoTexto, f.CostoEsGratuito, f.EstaEnSol, f.SolUrl, f.SolTramo)))
            .ToList();

        return new CatalogoPublicoDto(items, total, pagina, tamano);
    }
}
