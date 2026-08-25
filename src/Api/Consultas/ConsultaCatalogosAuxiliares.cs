namespace Diger.TramitesEstado.Api.Consultas;

/// <summary>
/// Las dos listas con que se navega el catálogo: categorías e instituciones. Las dos traen
/// cuántos trámites <b>publicados</b> tiene cada una, que es el dato que permite no pintar
/// filtros que no van a devolver nada.
/// </summary>
public sealed class ConsultaCatalogosAuxiliares(ApiDbContext db)
{
    /// <summary>Las categorías activas, con su conteo — GET /api/v1/categorias.</summary>
    public async Task<IReadOnlyList<CategoriaPublicaDto>> CategoriasAsync(CancellationToken ct)
    {
        var conteos = await db.Fichas
            .Where(t => t.Publicado && t.CategoriaId != null)
            .GroupBy(t => t.CategoriaId!.Value)
            .Select(g => new { CategoriaId = g.Key, Conteo = g.Count() })
            .ToDictionaryAsync(x => x.CategoriaId, x => x.Conteo, ct);

        var categorias = await db.Categorias
            .Where(c => c.Activo)
            .OrderBy(c => c.Orden)
            .ToListAsync(ct);

        return categorias.Select(c => new CategoriaPublicaDto(
            c.Id, c.Nombre, c.Icono, c.Orden, conteos.TryGetValue(c.Id, out var n) ? n : 0))
            .ToList();
    }

    /// <summary>Las instituciones activas, con contacto y conteo — GET /api/v1/instituciones.</summary>
    /// <remarks>
    /// En PortalDigital esta tabla lleva un filtro global por alcance institucional, pensado para
    /// un usuario con sesión, y su consulta pública tenía que llamar a <c>IgnoreQueryFilters()</c>
    /// para no depender de un valor por omisión. Acá no hace falta: el modelo de lectura de esta
    /// API no tiene ese filtro porque no tiene usuarios ni permisos que aplicar.
    /// </remarks>
    public async Task<IReadOnlyList<InstitucionPublicaDto>> InstitucionesAsync(CancellationToken ct)
    {
        var conteos = await db.Fichas
            .Where(t => t.Publicado && t.InstitucionId != null)
            .GroupBy(t => t.InstitucionId!)
            .Select(g => new { InstitucionId = g.Key, Conteo = g.Count() })
            .ToDictionaryAsync(x => x.InstitucionId, x => x.Conteo, ct);

        var instituciones = await db.Instituciones
            .Where(i => i.Activo)
            .OrderBy(i => i.Nombre)
            .Select(i => new { i.Id, i.Nombre, i.NombreCorto, i.LogoUrl, i.Telefono, i.SitioWeb, i.Direccion, i.Horario, i.Tipo })
            .ToListAsync(ct);

        return instituciones.Select(i => new InstitucionPublicaDto(
            i.Id, i.Nombre, i.NombreCorto, i.LogoUrl, i.Telefono, i.SitioWeb, i.Direccion, i.Horario, i.Tipo,
            conteos.TryGetValue(i.Id, out var c) ? c : 0))
            .ToList();
    }
}
