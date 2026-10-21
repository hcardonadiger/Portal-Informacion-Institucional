using Diger.TramitesEstado.Application.Tickets.Common;

namespace Diger.TramitesEstado.Application.Tickets.Queries.GetOperadoresSoporte;

/// <summary>
/// Operadores de soporte a los que se puede asignar un ticket.
///
/// <para>Preferimos los <b>especialistas del tema</b> (<see cref="Domain.Entities.UsuarioTema"/>):
/// son quienes el administrador declaró competentes para ese tipo de incidente. Si el tema no tiene
/// especialistas (o no se indica tema), caemos a <b>todos los usuarios con la capacidad de rol
/// <c>EsTecnicoSoporte</c></b>, para no dejar la lista vacía y bloquear la asignación.</para>
/// </summary>
public sealed record GetOperadoresSoporteQuery(int? TemaId) : IRequest<IReadOnlyList<UsuarioAsignableDto>>;

public sealed class GetOperadoresSoporteQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetOperadoresSoporteQuery, IReadOnlyList<UsuarioAsignableDto>>
{
    public async Task<IReadOnlyList<UsuarioAsignableDto>> Handle(GetOperadoresSoporteQuery q, CancellationToken ct)
    {
        // Ids candidatos: especialistas del tema; si no hay, los técnicos de soporte.
        HashSet<Guid> ids = [];

        if (q.TemaId is int temaId)
            ids = (await ctx.UsuarioTemas.AsNoTracking()
                    .Where(u => u.TemaId == temaId)
                    .Select(u => u.UsuarioId)
                    .ToListAsync(ct))
                .ToHashSet();

        if (ids.Count == 0)
            ids = (await (
                    from a in ctx.AsignacionesUsuario.AsNoTracking()
                    join r in ctx.Roles.AsNoTracking() on a.Rol equals r.Id
                    where r.EsTecnicoSoporte && r.Activo
                    select a.UsuarioId)
                .Distinct()
                .ToListAsync(ct))
                .ToHashSet();

        if (ids.Count == 0) return [];

        return await ctx.Usuarios.AsNoTracking()
            .Where(u => u.Activo && ids.Contains(u.Id))
            .OrderBy(u => u.Nombre)
            .Select(u => new UsuarioAsignableDto(u.Id, u.Nombre,
                ctx.AsignacionesUsuario.Where(a => a.UsuarioId == u.Id)
                   .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
                   .Select(a => a.Rol).FirstOrDefault() ?? "Empleado"))
            .ToListAsync(ct);
    }
}
