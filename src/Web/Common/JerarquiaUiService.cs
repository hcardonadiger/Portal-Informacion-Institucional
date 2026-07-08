using Diger.TramitesEstado.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Common;

public sealed record OpcionJerarquia(string Id, string Nombre);

public sealed class JerarquiaUiService(ICurrentUserService currentUser, IApplicationDbContext ctx)
{
    public async Task<List<OpcionJerarquia>> GetAreasAsync(CancellationToken ct = default)
    {
        var inst = currentUser.ActiveInstitucionId;
        if (string.IsNullOrEmpty(inst)) return new();

        return await ctx.Areas
            .AsNoTracking()
            .Where(a => a.InstitucionId == inst)
            .OrderBy(a => a.Nombre)
            .Select(a => new OpcionJerarquia(a.Id, a.Nombre))
            .ToListAsync(ct);
    }

    public async Task<List<OpcionJerarquia>> GetUnidadesAsync(string? filterAreaId, CancellationToken ct = default)
    {
        var inst = currentUser.ActiveInstitucionId;
        if (string.IsNullOrEmpty(inst)) return new();

        var query = ctx.Unidades.AsNoTracking()
            .Join(ctx.Areas, u => u.AreaId, a => a.Id, (u, a) => new { Unidad = u, Area = a })
            .Where(x => x.Area.InstitucionId == inst);

        if (!string.IsNullOrEmpty(filterAreaId))
        {
            query = query.Where(x => x.Unidad.AreaId == filterAreaId);
        }

        return await query
            .OrderBy(x => x.Unidad.Nombre)
            .Select(x => new OpcionJerarquia(x.Unidad.Id, x.Unidad.Nombre))
            .ToListAsync(ct);
    }
}
