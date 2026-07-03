namespace Diger.TramitesEstado.Application.Contactos.Queries.GetContactos;

public sealed record ContactoDto(
    int Id, string Nombre, int InstitucionId, string Institucion, string? Cargo,
    string? Correo, string? Telefono, string? Notas, OrigenContacto Origen);

public sealed record GetContactosQuery(string? Buscar = null, string? Institucion = null)
    : IRequest<IReadOnlyList<ContactoDto>>;

public sealed class GetContactosQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetContactosQuery, IReadOnlyList<ContactoDto>>
{
    public async Task<IReadOnlyList<ContactoDto>> Handle(GetContactosQuery q, CancellationToken ct)
    {
        var query = ctx.Contactos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Institucion))
            query = query.Where(c => c.Institucion == q.Institucion);

        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var b = q.Buscar.Trim();
            query = query.Where(c =>
                c.Nombre.Contains(b) || (c.Correo != null && c.Correo.Contains(b)) ||
                (c.Cargo != null && c.Cargo.Contains(b)) || c.Institucion.Contains(b));
        }

        return await query
            .OrderBy(c => c.Institucion).ThenBy(c => c.Nombre)
            .Select(c => new ContactoDto(
                c.Id, c.Nombre, c.InstitucionId, c.Institucion, c.Cargo, c.Correo, c.Telefono, c.Notas, c.Origen))
            .ToListAsync(ct);
    }
}
