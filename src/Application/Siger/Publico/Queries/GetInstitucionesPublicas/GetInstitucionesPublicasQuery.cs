namespace Diger.TramitesEstado.Application.Siger.Publico.Queries.GetInstitucionesPublicas;

/// <summary>Instituciones con contacto y conteo de trámites publicados — GET /api/v1/instituciones.</summary>
public sealed record GetInstitucionesPublicasQuery : IRequest<IReadOnlyList<InstitucionPublicaDto>>;

public sealed class GetInstitucionesPublicasQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetInstitucionesPublicasQuery, IReadOnlyList<InstitucionPublicaDto>>
{
    public async Task<IReadOnlyList<InstitucionPublicaDto>> Handle(GetInstitucionesPublicasQuery q, CancellationToken ct)
    {
        // M-08: Institucion sí tiene filtro de alcance institucional (RLS) — sin usuario
        // autenticado el alcance por defecto es global y ya vería todo, pero una API pública
        // no debe depender de ese valor por defecto. Explícito, con la prueba correspondiente
        // (GetInstitucionesPublicasQueryTests) verificando que sin usuario devuelve filas.
        var conteos = await ctx.TramitesSiger.AsNoTracking()
            .Where(t => t.Publicado && t.InstitucionId != null)
            .GroupBy(t => t.InstitucionId!)
            .Select(g => new { InstitucionId = g.Key, Conteo = g.Count() })
            .ToDictionaryAsync(x => x.InstitucionId, x => x.Conteo, ct);

        var instituciones = await ctx.Instituciones.AsNoTracking()
            .IgnoreQueryFilters()
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
