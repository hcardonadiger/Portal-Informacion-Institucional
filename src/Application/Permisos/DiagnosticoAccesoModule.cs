namespace Diger.TramitesEstado.Application.Permisos;

public sealed record AsignacionDiagnosticoDto(
    string InstitucionId, string? AreaId, string? UnidadId, string RolId);

/// <summary>Un rol que el usuario tiene por alguna asignación, con sus capacidades y las
/// claves que la matriz le concede hoy.</summary>
public sealed record RolEfectivoDto(
    string RolId, string Nombre, bool ExisteEnCatalogo,
    NivelAlcance NivelAlcance,
    bool EsAdministrador, bool EsSoloLectura, bool EsSupervisor, bool EsTecnicoSoporte,
    IReadOnlyList<string> Claves);

public sealed record DiagnosticoAccesoDto(
    Guid UsuarioId, string Nombre, string Correo, bool Activo,
    IReadOnlyList<AsignacionDiagnosticoDto> Asignaciones,
    IReadOnlyList<RolEfectivoDto> Roles);

/// <summary>
/// Responde "por qué este usuario no puede hacer X": junta en una sola vista el rol de cada
/// asignación, el alcance de datos que ese rol otorga, sus capacidades y las claves que hoy
/// tiene concedidas. Todo el dato ya existía repartido entre Usuarios, AsignacionesUsuario,
/// Roles y RolPermisos; lo que faltaba era poder verlo junto sin consultar la base a mano.
/// </summary>
public sealed record GetDiagnosticoAccesoQuery(Guid UsuarioId) : IRequest<DiagnosticoAccesoDto>;

public sealed class GetDiagnosticoAccesoQueryHandler(IApplicationDbContext ctx, IRolCatalogo catalogo)
    : IRequestHandler<GetDiagnosticoAccesoQuery, DiagnosticoAccesoDto>
{
    public async Task<DiagnosticoAccesoDto> Handle(GetDiagnosticoAccesoQuery q, CancellationToken ct)
    {
        var usuario = await ctx.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == q.UsuarioId, ct)
            ?? throw new NotFoundException(nameof(Usuario), q.UsuarioId);

        var asignaciones = await ctx.AsignacionesUsuario
            .AsNoTracking()
            .Where(a => a.UsuarioId == q.UsuarioId)
            .Select(a => new AsignacionDiagnosticoDto(a.InstitucionId, a.AreaId, a.UnidadId, a.Rol))
            .ToListAsync(ct);

        var rolesIds = asignaciones
            .Select(a => a.RolId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var grants = await ctx.RolPermisos
            .AsNoTracking()
            .Where(p => rolesIds.Contains(p.RolId))
            .ToListAsync(ct);

        var roles = rolesIds.Select(rolId =>
        {
            var claves = grants
                .Where(g => string.Equals(g.RolId, rolId, StringComparison.OrdinalIgnoreCase))
                .Select(g => g.PermisoClave)
                .OrderBy(c => c)
                .ToList();

            // Un rol asignado que ya no está en el catálogo (borrado o desactivado) es
            // justamente uno de los casos que esta pantalla tiene que hacer visible: el
            // usuario queda sin capacidades y sin alcance, y desde la ficha no se nota.
            var info = catalogo.Obtener(rolId);

            return info is null
                ? new RolEfectivoDto(rolId, rolId, false, NivelAlcance.Unidad, false, false, false, false, claves)
                : new RolEfectivoDto(
                    info.Codigo, info.Nombre, true, info.NivelAlcance,
                    info.EsAdministrador, info.EsSoloLectura, info.EsSupervisor, info.EsTecnicoSoporte,
                    claves);
        }).ToList();

        return new DiagnosticoAccesoDto(
            usuario.Id, usuario.Nombre, usuario.Correo, usuario.Activo, asignaciones, roles);
    }
}
