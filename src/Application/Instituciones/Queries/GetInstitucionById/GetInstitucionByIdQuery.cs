using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Instituciones.Queries.GetInstitucionById;

public sealed record InstitucionDetailDto(
    string Id, string Nombre, bool Activo, int NumTramites,
    int Expedientes, int Tickets, int TicketsAbiertos, int Reuniones, int Contactos, int UsuariosAsignados,
    string? LogoUrl = null, string? NombreCorto = null, string? Color = null, string? Descripcion = null,
    string? RutaSol = null, string RutaSolEfectiva = "");

public sealed record GetInstitucionByIdQuery(string Id) : IRequest<InstitucionDetailDto>;

public sealed class GetInstitucionByIdQueryHandler(IInstitucionRepository repo, IApplicationDbContext ctx)
    : IRequestHandler<GetInstitucionByIdQuery, InstitucionDetailDto>
{
    public async Task<InstitucionDetailDto> Handle(GetInstitucionByIdQuery q, CancellationToken ct)
    {
        var inst = await repo.GetByIdAsync(q.Id, ct)
            ?? throw new NotFoundException(nameof(Institucion), q.Id);

        // Conteos de relaciones (IgnoreQueryFilters: el catálogo es de Administrador / vista global).
        var id = q.Id;
        var numTramites     = await ctx.Expedientes.IgnoreQueryFilters().Where(e => e.InstitucionId == id).SelectMany(e => e.Tramites).CountAsync(ct);
        var expedientes     = await ctx.Expedientes.IgnoreQueryFilters().CountAsync(e => e.InstitucionId == id, ct);
        var tickets         = await ctx.Tickets.IgnoreQueryFilters().CountAsync(t => t.InstitucionId == id, ct);
        var ticketsAbiertos = await ctx.Tickets.IgnoreQueryFilters().CountAsync(t => t.InstitucionId == id &&
            (t.Estado == EstadoTicket.Abierto || t.Estado == EstadoTicket.EnProgreso), ct);
        var reuniones       = await ctx.Reuniones.IgnoreQueryFilters().CountAsync(r => r.InstitucionId == id, ct);
        var contactos       = await ctx.Contactos.IgnoreQueryFilters().CountAsync(c => c.InstitucionId == id, ct);
        var usuarios        = await ctx.AsignacionesUsuario.CountAsync(u => u.InstitucionId == id, ct);

        return new InstitucionDetailDto(
            inst.Id, inst.Nombre, inst.Activo, numTramites,
            expedientes, tickets, ticketsAbiertos, reuniones, contactos, usuarios,
            inst.LogoUrl, inst.NombreCorto, inst.Color, inst.Descripcion,
            inst.RutaSol, inst.RutaSolEfectiva);
    }
}
