using Diger.TramitesEstado.Application.Reuniones.Outlook;

namespace Diger.TramitesEstado.Application.CuentaMicrosoft;

public sealed record MiConexionOutlookDto(bool Configurada, bool Conectada, string? Upn, DateTime? ConectadaEl, bool RequiereReconexion);

/// <summary>Arma la URL de Microsoft. No escribe nada: el estado y el verificador los guarda la página.</summary>
public sealed record IniciarConexionOutlookQuery(string UrlRetorno) : IRequest<InicioConexionDto>;

public sealed class IniciarConexionOutlookQueryHandler(IConexionMicrosoft conexion)
    : IRequestHandler<IniciarConexionOutlookQuery, InicioConexionDto>
{
    public async Task<InicioConexionDto> Handle(IniciarConexionOutlookQuery q, CancellationToken ct)
    {
        try
        {
            return await conexion.IniciarAsync(q.UrlRetorno, ct);
        }
        catch (OutlookNoDisponibleException ex)
        {
            throw new DomainException($"No se pudo conectar Outlook: {ex.Message}");
        }
    }
}

public sealed record CompletarConexionOutlookCommand(string Codigo, string Verificador, string UrlRetorno) : IRequest<Unit>;

public sealed class CompletarConexionOutlookCommandHandler(IConexionMicrosoft conexion, ICurrentUserService currentUser)
    : IRequestHandler<CompletarConexionOutlookCommand, Unit>
{
    public async Task<Unit> Handle(CompletarConexionOutlookCommand cmd, CancellationToken ct)
    {
        var usuarioId = currentUser.UserId
            ?? throw new DomainException("No hay una sesión de usuario para conectar Outlook.");
        try
        {
            await conexion.CompletarAsync(usuarioId, cmd.Codigo, cmd.Verificador, cmd.UrlRetorno, ct);
        }
        catch (OutlookNoDisponibleException ex)
        {
            throw new DomainException($"No se pudo conectar Outlook: {ex.Message}");
        }
        return Unit.Value;
    }
}

/// <summary>Borra la conexión propia. Las citas ya enviadas siguen en Outlook.</summary>
public sealed record DesconectarOutlookCommand : IRequest<Unit>;

public sealed class DesconectarOutlookCommandHandler(IApplicationDbContext ctx, ICurrentUserService currentUser, IUnitOfWork uow)
    : IRequestHandler<DesconectarOutlookCommand, Unit>
{
    public async Task<Unit> Handle(DesconectarOutlookCommand cmd, CancellationToken ct)
    {
        if (currentUser.UserId is not { } uid) return Unit.Value;
        var fila = await ctx.ConexionesMicrosoft.FirstOrDefaultAsync(c => c.Id == uid, ct);
        if (fila is null) return Unit.Value;
        ctx.ConexionesMicrosoft.Remove(fila);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed record GetMiConexionOutlookQuery : IRequest<MiConexionOutlookDto>;

public sealed class GetMiConexionOutlookQueryHandler(IApplicationDbContext ctx, ICurrentUserService currentUser, IConexionMicrosoft conexion)
    : IRequestHandler<GetMiConexionOutlookQuery, MiConexionOutlookDto>
{
    public async Task<MiConexionOutlookDto> Handle(GetMiConexionOutlookQuery q, CancellationToken ct)
    {
        var fila = currentUser.UserId is { } uid
            ? await ctx.ConexionesMicrosoft.AsNoTracking().Where(c => c.Id == uid)
                .Select(c => new { c.Upn, c.ConectadaEl, c.RequiereReconexion }).FirstOrDefaultAsync(ct)
            : null;

        return new MiConexionOutlookDto(conexion.EstaConfigurada, fila is not null, fila?.Upn, fila?.ConectadaEl, fila?.RequiereReconexion ?? false);
    }
}
