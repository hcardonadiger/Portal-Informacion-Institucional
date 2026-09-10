using Diger.TramitesEstado.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Application.Recursos.Queries.GetRecursos;

public sealed record RecursoDto(
    int      Id,
    string   Titulo,
    string?  Descripcion,
    string   Categoria,
    string   ArchivoNombre,
    string   ArchivoUrl,
    long     ArchivoTamano,
    int      DescargasCount,
    DateTime CreatedAt,
    string?  CreatedBy);

public sealed record GetRecursosQuery(string? Q = null, string? Categoria = null) : IRequest<IReadOnlyList<RecursoDto>>;

public sealed class GetRecursosQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetRecursosQuery, IReadOnlyList<RecursoDto>>
{
    public async Task<IReadOnlyList<RecursoDto>> Handle(GetRecursosQuery query, CancellationToken ct)
    {
        var q = ctx.Recursos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Categoria) && !query.Categoria.Equals("Todos", StringComparison.OrdinalIgnoreCase))
        {
            var cat = query.Categoria.Trim();
            q = q.Where(r => r.Categoria == cat);
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var search = query.Q.Trim().ToLower();
            q = q.Where(r => r.Titulo.ToLower().Contains(search) ||
                             (r.Descripcion != null && r.Descripcion.ToLower().Contains(search)) ||
                             r.ArchivoNombre.ToLower().Contains(search));
        }

        return await q
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RecursoDto(
                r.Id,
                r.Titulo,
                r.Descripcion,
                r.Categoria,
                r.ArchivoNombre,
                r.ArchivoUrl,
                r.ArchivoTamano,
                r.DescargasCount,
                r.CreatedAt,
                r.CreatedBy
            ))
            .ToListAsync(ct);
    }
}
