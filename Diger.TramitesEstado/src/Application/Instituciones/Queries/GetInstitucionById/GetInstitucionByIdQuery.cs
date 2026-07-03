using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Instituciones.Queries.GetInstitucionById;

public sealed record InstitucionDetailDto(
    int Id, string Nombre, bool Activo, IReadOnlyList<string> Tramites,
    int Expedientes, int Tickets, int TicketsAbiertos, int Reuniones, int Contactos, int UsuariosAsignados);

public sealed record GetInstitucionByIdQuery(int Id) : IRequest<InstitucionDetailDto>;

public sealed class GetInstitucionByIdQueryHandler(IInstitucionRepository repo, IApplicationDbContext ctx)
    : IRequestHandler<GetInstitucionByIdQuery, InstitucionDetailDto>
{
    public async Task<InstitucionDetailDto> Handle(GetInstitucionByIdQuery q, CancellationToken ct)
    {
        var inst = await repo.GetByIdWithTramitesAsync(q.Id, ct)
            ?? throw new NotFoundException(nameof(Institucion), q.Id);

        // Conteos de relaciones (IgnoreQueryFilters: el catálogo es de Administrador / vista global).
        var id = q.Id;
        var expedientes     = await ctx.Expedientes.IgnoreQueryFilters().CountAsync(e => e.InstitucionId == id, ct);
        var tickets         = await ctx.Tickets.IgnoreQueryFilters().CountAsync(t => t.InstitucionId == id, ct);
        var ticketsAbiertos = await ctx.Tickets.IgnoreQueryFilters().CountAsync(t => t.InstitucionId == id &&
            (t.Estado == EstadoTicket.Abierto || t.Estado == EstadoTicket.EnProgreso), ct);
        var reuniones       = await ctx.Reuniones.IgnoreQueryFilters().CountAsync(r => r.InstitucionId == id, ct);
        var contactos       = await ctx.Contactos.IgnoreQueryFilters().CountAsync(c => c.InstitucionId == id, ct);
        var usuarios        = await ctx.UsuarioInstituciones.CountAsync(u => u.InstitucionId == id, ct);

        return new InstitucionDetailDto(
            inst.Id, inst.Nombre, inst.Activo,
            inst.Tramites.OrderBy(t => t.Orden).Select(t => t.Nombre).ToList(),
            expedientes, tickets, ticketsAbiertos, reuniones, contactos, usuarios);
    }
}
