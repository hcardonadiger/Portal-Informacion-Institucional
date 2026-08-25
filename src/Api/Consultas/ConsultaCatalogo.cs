namespace Diger.TramitesEstado.Api.Consultas;

/// <summary>Lo que se puede pedir del catálogo — GET /api/v1/tramites.</summary>
public sealed record CatalogoFiltros(
    string? Busqueda = null,
    int?    Categoria = null,
    string? Institucion = null,
    string? Modalidad = null,
    bool    SoloGratuitos = false,
    bool    SoloEnSol = false,
    bool    SoloFichasCompletas = false,
    string? Orden = null,
    int     Pagina = 1,
    int     Tamano = 20);

public sealed class ConsultaCatalogo(ApiDbContext db)
{
    public async Task<CatalogoPublicoDto> EjecutarAsync(CatalogoFiltros f, CancellationToken ct)
    {
        var pagina = f.Pagina < 1 ? 1 : f.Pagina;
        var tamano = f.Tamano is < 1 or > 100 ? 20 : f.Tamano;

        // Sin excepción: es la única condición que hace público al catálogo.
        var query = db.Fichas.Where(t => t.Publicado);

        if (!string.IsNullOrWhiteSpace(f.Busqueda))
        {
            var b = f.Busqueda.Trim();
            query = query.Where(t =>
                t.Nombre.Contains(b) ||
                (t.Descripcion != null && t.Descripcion.Contains(b)) ||
                (t.Objetivo != null && t.Objetivo.Contains(b)));
        }

        if (f.Categoria is { } categoriaId)
            query = query.Where(t => t.CategoriaId == categoriaId);

        if (!string.IsNullOrWhiteSpace(f.Institucion))
            query = query.Where(t => t.InstitucionId == f.Institucion);

        if (!string.IsNullOrWhiteSpace(f.Modalidad))
        {
            // modalidad=Virtual también trae los híbridos (ModalidadPublica.EsDigital).
            query = f.Modalidad == ModalidadPublica.Virtual
                ? query.Where(t => t.Modalidad == ModalidadPublica.Virtual || t.Modalidad == ModalidadPublica.Hibrido)
                : query.Where(t => t.Modalidad == f.Modalidad);
        }

        if (f.SoloGratuitos)
            query = query.Where(t => t.CostoEsGratuito == true);

        if (f.SoloEnSol)
            query = query.Where(t => t.EstaEnSol);

        // Antes acá había una copia en SQL de la regla de completitud de PortalDigital, con su
        // gemela en C# unas líneas más abajo, y las dos tenían que decir lo mismo. Hoy es una
        // columna: PortalDigital decide qué está completo, esta API solo lo filtra.
        if (f.SoloFichasCompletas)
            query = query.Where(t => t.FichaCompleta);

        var total = await query.CountAsync(ct);

        query = f.Orden switch
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
                t.CostoEsGratuito, t.CostoTexto, t.TiempoTexto, t.EstaEnSol, t.FichaCompleta
            })
            .ToListAsync(ct);

        var categoriaIds = filas.Where(f2 => f2.CategoriaId != null).Select(f2 => f2.CategoriaId!.Value).Distinct().ToList();
        var categoriaNombres = await db.Categorias
            .Where(c => categoriaIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nombre, ct);

        var items = filas.Select(x => new TramiteResumenPublicoDto(
            x.Codigo, x.Nombre, x.InstitucionId ?? "", x.Institucion,
            x.CategoriaId,
            x.CategoriaId is { } cid && categoriaNombres.TryGetValue(cid, out var nombre) ? nombre : null,
            x.Modalidad, x.EsPopular, x.CostoEsGratuito, x.CostoTexto, x.TiempoTexto, x.EstaEnSol,
            x.FichaCompleta))
            .ToList();

        return new CatalogoPublicoDto(items, total, pagina, tamano);
    }
}
