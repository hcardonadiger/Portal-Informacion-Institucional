namespace Diger.TramitesEstado.Application.Contactos.Queries.GetContactosPorInstitucion;

public sealed record ContactoLookupDto(string Nombre, string? Cargo, string? Correo, string? Telefono);

public sealed record GetContactosPorInstitucionQuery(string Institucion)
    : IRequest<IReadOnlyList<ContactoLookupDto>>;

public sealed class GetContactosPorInstitucionQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetContactosPorInstitucionQuery, IReadOnlyList<ContactoLookupDto>>
{
    public async Task<IReadOnlyList<ContactoLookupDto>> Handle(
        GetContactosPorInstitucionQuery q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.Institucion)) return [];

        return await ctx.Contactos
            .AsNoTracking()
            .Where(c => c.Institucion == q.Institucion)
            .OrderBy(c => c.Nombre)
            .Select(c => new ContactoLookupDto(c.Nombre, c.Cargo, c.Correo, c.Telefono))
            .ToListAsync(ct);
    }
}
