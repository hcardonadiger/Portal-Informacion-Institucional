using Diger.TramitesEstado.Application.Tickets.Common;

namespace Diger.TramitesEstado.Application.Tickets.Queries.GetUsuariosAsignables;

/// <summary>Usuarios internos activos a los que se puede asignar un ticket.</summary>
/// <param name="Modulo">
/// Si se indica y el módulo está limitado en <c>ModuloAmbitos</c> (p. ej. Expedientes → DITRA),
/// solo vuelven los usuarios con alguna asignación dentro de ese ámbito. Un módulo sin filas no
/// filtra nada, igual que en <c>PermissionCache</c>: abierto por defecto.
/// </param>
public sealed record GetUsuariosAsignablesQuery(string? Modulo = null) : IRequest<IReadOnlyList<UsuarioAsignableDto>>;

public sealed class GetUsuariosAsignablesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetUsuariosAsignablesQuery, IReadOnlyList<UsuarioAsignableDto>>
{
    public async Task<IReadOnlyList<UsuarioAsignableDto>> Handle(GetUsuariosAsignablesQuery q, CancellationToken ct)
    {
        var usuarios = await ctx.Usuarios
            .AsNoTracking()
            .Where(u => u.Activo)
            .OrderBy(u => u.Nombre)
            .Select(u => new UsuarioAsignableDto(u.Id, u.Nombre,
                ctx.AsignacionesUsuario.Where(a => a.UsuarioId == u.Id).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).Select(a => a.Rol).FirstOrDefault() ?? "Empleado"))
            .ToListAsync(ct);

        if (string.IsNullOrWhiteSpace(q.Modulo)) return usuarios;

        // Restringir el padre restringe al hijo: "Siger.Conciliacion" mira también "Siger".
        var modulos = new List<string>();
        for (var m = q.Modulo.Trim(); m.Length > 0; m = m.Contains('.') ? m[..m.LastIndexOf('.')] : "")
            modulos.Add(m);

        var concesiones = await ctx.ModuloAmbitos
            .AsNoTracking()
            .Where(a => modulos.Contains(a.Modulo))
            .ToListAsync(ct);
        if (concesiones.Count == 0) return usuarios;

        var asignaciones = await ctx.AsignacionesUsuario
            .AsNoTracking()
            .Select(a => new { a.UsuarioId, a.InstitucionId, a.AreaId, a.UnidadId })
            .ToListAsync(ct);

        // Cada módulo restringido de la cadena debe alcanzar a alguna asignación del usuario.
        var porModulo = concesiones.GroupBy(c => c.Modulo, StringComparer.OrdinalIgnoreCase).ToList();
        var habilitados = asignaciones
            .GroupBy(a => a.UsuarioId)
            .Where(g => porModulo.All(m => g.Any(a => m.Any(c => c.Alcanza(a.InstitucionId, a.AreaId, a.UnidadId)))))
            .Select(g => g.Key)
            .ToHashSet();

        return usuarios.Where(u => habilitados.Contains(u.Id)).ToList();
    }
}
