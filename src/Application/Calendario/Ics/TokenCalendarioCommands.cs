namespace Diger.TramitesEstado.Application.Calendario.Ics;

/// <summary>
/// Devuelve el token de suscripción de la persona conectada, creándolo la primera vez.
///
/// <para>Es idempotente: pedir el enlace dos veces devuelve el mismo, para no invalidar el que ya
/// esté pegado en el calendario de alguien. El usuario sale del contexto y no de la petición, igual
/// que en el resto de las pantallas de autoservicio.</para>
/// </summary>
public sealed record ObtenerTokenCalendarioCommand : IRequest<Guid>;

/// <summary>Emite un token nuevo e invalida el anterior. Es lo que se usa cuando un enlace se
/// compartió de más.</summary>
public sealed record RegenerarTokenCalendarioCommand : IRequest<Guid>;

/// <summary>Lee el token sin crearlo. Devuelve null si la persona todavía no pidió el enlace, que
/// es lo que le permite al perfil ofrecer el botón en vez de mostrar una URL que nadie pidió.</summary>
public sealed record ObtenerTokenCalendarioQuery : IRequest<Guid?>;

public sealed class ObtenerTokenCalendarioQueryHandler(
    IApplicationDbContext ctx, ICurrentUserService currentUser)
    : IRequestHandler<ObtenerTokenCalendarioQuery, Guid?>
{
    public async Task<Guid?> Handle(ObtenerTokenCalendarioQuery _, CancellationToken ct)
    {
        if (currentUser.UserId is not { } id) return null;

        return await ctx.Usuarios.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => u.CalendarioToken)
            .FirstOrDefaultAsync(ct);
    }
}

public sealed class TokenCalendarioHandlers(IApplicationDbContext ctx, ICurrentUserService currentUser)
    : IRequestHandler<ObtenerTokenCalendarioCommand, Guid>,
      IRequestHandler<RegenerarTokenCalendarioCommand, Guid>
{
    public Task<Guid> Handle(ObtenerTokenCalendarioCommand _, CancellationToken ct) =>
        Aplicar(u => u.AsegurarTokenCalendario(), ct);

    public Task<Guid> Handle(RegenerarTokenCalendarioCommand _, CancellationToken ct) =>
        Aplicar(u => u.RegenerarTokenCalendario(), ct);

    private async Task<Guid> Aplicar(Func<Usuario, Guid> accion, CancellationToken ct)
    {
        var id = currentUser.UserId
            ?? throw new DomainException("No hay una sesión activa.");

        var usuario = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(nameof(Usuario), id);

        var token = accion(usuario);
        await ctx.SaveChangesAsync(ct);
        return token;
    }
}
