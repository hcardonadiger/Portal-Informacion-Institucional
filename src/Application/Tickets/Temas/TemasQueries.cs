using Diger.TramitesEstado.Application.Tickets.Common;

namespace Diger.TramitesEstado.Application.Tickets.Temas;

/// <summary>Fila del catálogo de temas para administración (incluye categoría y uso).</summary>
public sealed record TemaAdminDto(
    int Id, string Nombre, int HorasResolucion, bool Activo,
    int? CategoriaId, string? Categoria, int Tickets, int Especialistas);

// ── Lista de temas para administración ────────────────────────────────────
public sealed record GetTemasQuery(bool SoloActivos = false) : IRequest<IReadOnlyList<TemaAdminDto>>;

public sealed class GetTemasQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetTemasQuery, IReadOnlyList<TemaAdminDto>>
{
    public async Task<IReadOnlyList<TemaAdminDto>> Handle(GetTemasQuery q, CancellationToken ct)
    {
        var temas = await ctx.TemasTicket
            .Where(t => !q.SoloActivos || t.Activo)
            .OrderBy(t => t.CategoriaRef != null ? t.CategoriaRef.Nombre : "zzz").ThenBy(t => t.Nombre)
            .Select(t => new
            {
                t.Id, t.Nombre, t.HorasResolucion, t.Activo, t.CategoriaId,
                Categoria = t.CategoriaRef != null ? t.CategoriaRef.Nombre : null,
                Tickets = ctx.Tickets.IgnoreQueryFilters().Count(k => k.TemaId == t.Id),
                Especialistas = ctx.UsuarioTemas.Count(u => u.TemaId == t.Id)
            })
            .ToListAsync(ct);

        return temas.Select(t => new TemaAdminDto(t.Id, t.Nombre, t.HorasResolucion, t.Activo,
            t.CategoriaId, t.Categoria, t.Tickets, t.Especialistas)).ToList();
    }
}

// ── Temas activos para selectores (creación de ticket, asignación) ─────────────
public sealed record GetTemasActivosQuery : IRequest<IReadOnlyList<TemaOpcionDto>>, ICacheableQuery
{
    public string CacheKey => "temas-activos";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(60);
}

public sealed class GetTemasActivosQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetTemasActivosQuery, IReadOnlyList<TemaOpcionDto>>
{
    public async Task<IReadOnlyList<TemaOpcionDto>> Handle(GetTemasActivosQuery q, CancellationToken ct) =>
        await ctx.TemasTicket
            .Where(t => t.Activo)
            .OrderBy(t => t.CategoriaRef != null ? t.CategoriaRef.Nombre : "zzz").ThenBy(t => t.Nombre)
            .Select(t => new TemaOpcionDto(t.Id, t.Nombre, t.HorasResolucion, t.Activo,
                t.CategoriaId, t.CategoriaRef != null ? t.CategoriaRef.Nombre : null))
            .ToListAsync(ct);
}
