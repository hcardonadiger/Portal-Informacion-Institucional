namespace Diger.TramitesEstado.Application.Usuarios.Queries.AutenticarUsuario;

/// <summary>
/// La identidad que el servidor MCP le da a una petición autenticada por token: el mismo
/// <see cref="UsuarioAuthDto"/> que produce el login, más cuál de sus asignaciones es la
/// activa (la que el token fijó al emitirse).
/// </summary>
public sealed record IdentidadTokenDto(UsuarioAuthDto Usuario, AsignacionAuthDto Activa);

/// <summary>
/// Resuelve la identidad de un token ya verificado.
///
/// Se consulta **fresco en cada petición**, a propósito: el token guarda a quién
/// representa, no qué puede hacer. Así, dar de baja al usuario o cambiarle el rol surte
/// efecto en la siguiente llamada, en vez de quedar congelado en una credencial emitida
/// hace meses.
/// </summary>
public sealed record ResolverIdentidadPorTokenQuery(Guid UsuarioId, Guid AsignacionId)
    : IRequest<IdentidadTokenDto?>;

public sealed class ResolverIdentidadPorTokenQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<ResolverIdentidadPorTokenQuery, IdentidadTokenDto?>
{
    public async Task<IdentidadTokenDto?> Handle(ResolverIdentidadPorTokenQuery q, CancellationToken ct)
    {
        // IgnoreQueryFilters porque acá todavía no hay identidad: el filtro de borrado
        // lógico escondería la cuenta dada de baja, y lo que hace falta es *verla* para
        // poder rechazarla explícitamente en vez de tratarla como inexistente.
        var usuario = await ctx.Usuarios.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == q.UsuarioId, ct);

        if (usuario is null || !usuario.Activo || usuario.IsDeleted)
            return null;

        var asignacionesEntity = await ctx.AsignacionesUsuario.AsNoTracking()
            .Where(a => a.UsuarioId == usuario.Id)
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
            .ToListAsync(ct);

        var activaEntity = asignacionesEntity.FirstOrDefault(a => a.Id == q.AsignacionId);

        // La asignación que el token fijó ya no existe: el contexto con el que se emitió
        // desapareció y no hay forma de elegir otro sin inventarle un alcance. Falla cerrado.
        if (activaEntity is null)
            return null;

        var asignaciones = asignacionesEntity
            .Select(a => new AsignacionAuthDto(a.InstitucionId, a.AreaId, a.UnidadId, a.Rol))
            .ToList();

        var activa = new AsignacionAuthDto(
            activaEntity.InstitucionId, activaEntity.AreaId, activaEntity.UnidadId, activaEntity.Rol);

        var usuarioDto = new UsuarioAuthDto(
            usuario.Id, usuario.Nombre, usuario.Correo, activa.Rol, asignaciones);

        return new IdentidadTokenDto(usuarioDto, activa);
    }
}
