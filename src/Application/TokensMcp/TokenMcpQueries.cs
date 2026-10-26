namespace Diger.TramitesEstado.Application.TokensMcp;

/// <summary>Lo que se puede volver a mostrar de un token. El valor completo NO está acá
/// a propósito: existe una sola vez, en la respuesta que lo generó.</summary>
public sealed record TokenMcpDto(
    Guid      Id,
    string    Nombre,
    string    Prefijo,
    string    InstitucionId,
    string?   AreaId,
    string?   UnidadId,
    string    Rol,
    DateTime? CreadoUtc,
    DateTime? ExpiraUtc,
    DateTime? UltimoUsoUtc,
    DateTime? RevocadoUtc)
{
    public bool EstaVigente => RevocadoUtc is null && (ExpiraUtc is null || ExpiraUtc > DateTime.UtcNow);

    public string Estado =>
        RevocadoUtc is not null                         ? "Revocado"
        : ExpiraUtc is { } e && e <= DateTime.UtcNow     ? "Vencido"
        :                                                 "Activo";
}

public sealed record TokenMcpAdminDto(TokenMcpDto Token, string UsuarioNombre, string UsuarioCorreo);

/// <summary>Los tokens del usuario en sesión.</summary>
public sealed record GetMisTokensMcpQuery : IRequest<IReadOnlyList<TokenMcpDto>>;

public sealed class GetMisTokensMcpQueryHandler(IApplicationDbContext ctx, ICurrentUserService currentUser)
    : IRequestHandler<GetMisTokensMcpQuery, IReadOnlyList<TokenMcpDto>>
{
    public async Task<IReadOnlyList<TokenMcpDto>> Handle(GetMisTokensMcpQuery q, CancellationToken ct)
    {
        var usuarioId = currentUser.UserId;
        if (usuarioId is null) return [];

        // El filtro por usuario es explícito y no puede faltar: TokensAccesoMcp no lleva
        // query filter global (lo lee la autenticación, antes de que exista identidad),
        // así que acá no hay red debajo.
        return await (from t in ctx.TokensAccesoMcp.AsNoTracking()
                      where t.UsuarioId == usuarioId.Value
                      join a in ctx.AsignacionesUsuario.AsNoTracking() on t.AsignacionId equals a.Id into ja
                      from a in ja.DefaultIfEmpty()
                      orderby t.CreatedAt descending
                      select new TokenMcpDto(
                          t.Id,
                          t.Nombre,
                          t.Prefijo,
                          a != null ? a.InstitucionId : "—",
                          a != null ? a.AreaId        : null,
                          a != null ? a.UnidadId      : null,
                          a != null ? a.Rol           : "—",
                          t.CreatedAt == default ? null : t.CreatedAt,
                          t.ExpiraUtc,
                          t.UltimoUsoUtc,
                          t.RevocadoUtc))
                     .ToListAsync(ct);
    }
}

/// <summary>Todos los tokens del portal, para que un administrador pueda revocar el de
/// alguien más — un token filtrado o el de un funcionario que ya no está.</summary>
public sealed record GetTokensMcpAdminQuery : IRequest<IReadOnlyList<TokenMcpAdminDto>>;

public sealed class GetTokensMcpAdminQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetTokensMcpAdminQuery, IReadOnlyList<TokenMcpAdminDto>>
{
    public async Task<IReadOnlyList<TokenMcpAdminDto>> Handle(GetTokensMcpAdminQuery q, CancellationToken ct)
    {
        // IgnoreQueryFilters sobre Usuarios: un token de una cuenta ya dada de baja es
        // justo el que más urge ver y revocar, y el filtro de borrado lógico lo escondería.
        var usuarios = await ctx.Usuarios.IgnoreQueryFilters().AsNoTracking()
            .Select(u => new { u.Id, u.Nombre, u.Correo })
            .ToListAsync(ct);

        var porId = usuarios.ToDictionary(u => u.Id);

        var tokens = await (from t in ctx.TokensAccesoMcp.AsNoTracking()
                            join a in ctx.AsignacionesUsuario.AsNoTracking() on t.AsignacionId equals a.Id into ja
                            from a in ja.DefaultIfEmpty()
                            orderby t.CreatedAt descending
                            select new
                            {
                                Dto = new TokenMcpDto(
                                    t.Id, t.Nombre, t.Prefijo,
                                    a != null ? a.InstitucionId : "—",
                                    a != null ? a.AreaId        : null,
                                    a != null ? a.UnidadId      : null,
                                    a != null ? a.Rol           : "—",
                                    t.CreatedAt == default ? null : t.CreatedAt, t.ExpiraUtc, t.UltimoUsoUtc, t.RevocadoUtc),
                                t.UsuarioId
                            })
                           .ToListAsync(ct);

        return tokens
            .Select(x => new TokenMcpAdminDto(
                x.Dto,
                porId.TryGetValue(x.UsuarioId, out var u) ? u.Nombre : "—",
                porId.TryGetValue(x.UsuarioId, out var u2) ? u2.Correo : "—"))
            .ToList();
    }
}

/// <summary>Las asignaciones del usuario en sesión, para elegir a cuál se fija un token.</summary>
public sealed record OpcionAsignacionDto(Guid Id, string InstitucionId, string? AreaId, string? UnidadId, string Rol)
{
    public string Etiqueta =>
        string.Join(" · ", new[] { InstitucionId, AreaId, UnidadId }
            .Where(s => !string.IsNullOrWhiteSpace(s))) + $" — {Rol}";
}

public sealed record GetMisAsignacionesQuery : IRequest<IReadOnlyList<OpcionAsignacionDto>>;

public sealed class GetMisAsignacionesQueryHandler(IApplicationDbContext ctx, ICurrentUserService currentUser)
    : IRequestHandler<GetMisAsignacionesQuery, IReadOnlyList<OpcionAsignacionDto>>
{
    public async Task<IReadOnlyList<OpcionAsignacionDto>> Handle(GetMisAsignacionesQuery q, CancellationToken ct)
    {
        var usuarioId = currentUser.UserId;
        if (usuarioId is null) return [];

        return await ctx.AsignacionesUsuario.AsNoTracking()
            .Where(a => a.UsuarioId == usuarioId.Value)
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
            .Select(a => new OpcionAsignacionDto(a.Id, a.InstitucionId, a.AreaId, a.UnidadId, a.Rol))
            .ToListAsync(ct);
    }
}
