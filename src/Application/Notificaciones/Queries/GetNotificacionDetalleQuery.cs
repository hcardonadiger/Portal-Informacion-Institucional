namespace Diger.TramitesEstado.Application.Notificaciones.Queries;

public sealed record ElementoNotificacionDto(string Titulo, string? Url, DateTime FechaUtc);

public sealed record NotificacionDetalleDto(
    int Id, string Titulo, TipoNotificacion Tipo, bool Leida, DateTime Fecha,
    IReadOnlyList<ElementoNotificacionDto> Elementos);

/// <summary>Un aviso con sus elementos. Solo para su destinatario: el de otra persona es 404, no 403,
/// para no confirmar que existe.</summary>
public sealed record GetNotificacionDetalleQuery(int Id) : IRequest<NotificacionDetalleDto>;

public sealed class GetNotificacionDetalleQueryHandler(IApplicationDbContext ctx, ICurrentUserService usuario)
    : IRequestHandler<GetNotificacionDetalleQuery, NotificacionDetalleDto>
{
    public async Task<NotificacionDetalleDto> Handle(GetNotificacionDetalleQuery q, CancellationToken ct)
    {
        var uid = usuario.UserId ?? throw new NotFoundException(nameof(Notificacion), q.Id);

        var n = await ctx.Notificaciones.AsNoTracking()
            .Include(x => x.Elementos)
            .FirstOrDefaultAsync(x => x.Id == q.Id && x.DestinatarioId == uid, ct)
            ?? throw new NotFoundException(nameof(Notificacion), q.Id);

        return new NotificacionDetalleDto(n.Id, n.Titulo, n.Tipo, n.Leida, n.FechaActualizacion,
            n.Elementos.OrderByDescending(e => e.FechaUtc)
                       .Select(e => new ElementoNotificacionDto(e.Titulo, e.Url, e.FechaUtc)).ToList());
    }
}
