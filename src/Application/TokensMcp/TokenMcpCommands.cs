using FluentValidation;

namespace Diger.TramitesEstado.Application.TokensMcp;

/// <summary>La única vez que el token completo existe fuera de la cabeza de quien lo pide.
/// No se guarda, no se vuelve a mostrar y no debe ir a un log.</summary>
public sealed record TokenGeneradoDto(Guid Id, string Token, string Prefijo);

public sealed record GenerarTokenMcpCommand(string Nombre, Guid? AsignacionId, int? DiasVigencia)
    : IRequest<TokenGeneradoDto>;

public sealed class GenerarTokenMcpCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser,
    ITokenMcpHasher hasher,
    IUnitOfWork uow)
    : IRequestHandler<GenerarTokenMcpCommand, TokenGeneradoDto>
{
    public async Task<TokenGeneradoDto> Handle(GenerarTokenMcpCommand cmd, CancellationToken ct)
    {
        var usuarioId = currentUser.UserId
            ?? throw new DomainException("No hay una sesión de usuario para emitir el token.");

        var asignaciones = await ctx.AsignacionesUsuario.AsNoTracking()
            .Where(a => a.UsuarioId == usuarioId)
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
            .ToListAsync(ct);

        if (asignaciones.Count == 0)
            throw new DomainException(
                "Su cuenta no tiene ninguna asignación, así que un token no tendría institución ni rol con el cual actuar. Pídale a un administrador que se la configure.");

        // El contexto se fija acá porque un token no tiene dónde cambiarlo después: no hay
        // selector de institución en una cabecera HTTP.
        var asignacion = cmd.AsignacionId is { } pedida
            ? asignaciones.FirstOrDefault(a => a.Id == pedida)
              ?? throw new DomainException("La asignación elegida no es suya.")
            : asignaciones.Count == 1
                ? asignaciones[0]
                : throw new DomainException("Tiene más de una asignación: elija con cuál debe actuar el token.");

        DateTime? expira = cmd.DiasVigencia is { } dias and > 0
            ? DateTime.UtcNow.AddDays(dias)
            : null;

        var (token, prefijo, hash) = hasher.Generar();

        var entidad = TokenAccesoMcp.Crear(usuarioId, asignacion.Id, cmd.Nombre, prefijo, hash, expira);

        ctx.TokensAccesoMcp.Add(entidad);
        await uow.SaveChangesAsync(ct);

        return new TokenGeneradoDto(entidad.Id, token, prefijo);
    }
}

public sealed class GenerarTokenMcpCommandValidator : AbstractValidator<GenerarTokenMcpCommand>
{
    public GenerarTokenMcpCommandValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("Póngale un nombre para reconocerlo después (ej. «Claude Code — laptop»).")
            .MaximumLength(120);

        RuleFor(x => x.DiasVigencia)
            .InclusiveBetween(1, 3650)
            .When(x => x.DiasVigencia is not null)
            .WithMessage("La vigencia debe estar entre 1 y 3650 días.");
    }
}

/// <summary>Revoca un token propio.</summary>
public sealed record RevocarTokenMcpCommand(Guid TokenId) : IRequest<Unit>;

public sealed class RevocarTokenMcpCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser,
    IUnitOfWork uow)
    : IRequestHandler<RevocarTokenMcpCommand, Unit>
{
    public async Task<Unit> Handle(RevocarTokenMcpCommand cmd, CancellationToken ct)
    {
        var usuarioId = currentUser.UserId
            ?? throw new DomainException("No hay una sesión de usuario.");

        var token = await ctx.TokensAccesoMcp.FirstOrDefaultAsync(t => t.Id == cmd.TokenId, ct)
            ?? throw new NotFoundException(nameof(TokenAccesoMcp), cmd.TokenId);

        // Mismo mensaje que si no existiera: a quien prueba con Ids ajenos no se le confirma
        // cuáles existen.
        if (token.UsuarioId != usuarioId)
            throw new NotFoundException(nameof(TokenAccesoMcp), cmd.TokenId);

        token.Revocar();
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>Revoca cualquier token. Lo gatea el permiso de la página de administración
/// (<c>TokensMcp.Admin.Eliminar</c>), no este handler.</summary>
public sealed record RevocarTokenMcpComoAdminCommand(Guid TokenId) : IRequest<Unit>;

public sealed class RevocarTokenMcpComoAdminCommandHandler(IApplicationDbContext ctx, IUnitOfWork uow)
    : IRequestHandler<RevocarTokenMcpComoAdminCommand, Unit>
{
    public async Task<Unit> Handle(RevocarTokenMcpComoAdminCommand cmd, CancellationToken ct)
    {
        var token = await ctx.TokensAccesoMcp.FirstOrDefaultAsync(t => t.Id == cmd.TokenId, ct)
            ?? throw new NotFoundException(nameof(TokenAccesoMcp), cmd.TokenId);

        token.Revocar();
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
