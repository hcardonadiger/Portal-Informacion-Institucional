namespace Diger.TramitesEstado.Application.Reuniones.Outlook;

public sealed record EstadoOutlookDto(
    bool Configurado,
    bool UsuarioConectado,
    bool UsuarioRequiereReconexion,
    bool EnOutlook,
    bool ConTeams,
    string? EnlaceTeams,
    bool Pendiente,
    string? Error,
    DateTime? SincronizadaEl,
    string? Organizador);

public sealed record GetEstadoOutlookQuery(int ReunionId) : IRequest<EstadoOutlookDto>;

public sealed class GetEstadoOutlookQueryHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser,
    IConexionMicrosoft conexiones)
    : IRequestHandler<GetEstadoOutlookQuery, EstadoOutlookDto>
{
    public async Task<EstadoOutlookDto> Handle(GetEstadoOutlookQuery q, CancellationToken ct)
    {
        // Pasa por el filtro de alcance: quien no ve la reunión no ve su estado.
        var r = await ctx.Reuniones.AsNoTracking()
            .Where(x => x.Id == q.ReunionId)
            .Select(x => new
            {
                x.OutlookEventId, x.OutlookConTeams, x.OutlookEnlaceTeams, x.OutlookPendiente,
                x.OutlookError, x.OutlookSincronizadaEl, x.OutlookOrganizadorId
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Reunion), q.ReunionId);

        var mia = currentUser.UserId is { } uid
            ? await ctx.ConexionesMicrosoft.AsNoTracking().Where(c => c.Id == uid)
                .Select(c => new { c.RequiereReconexion }).FirstOrDefaultAsync(ct)
            : null;

        var organizador = r.OutlookOrganizadorId is { } oid
            ? await ctx.Usuarios.AsNoTracking().Where(u => u.Id == oid).Select(u => u.Nombre).FirstOrDefaultAsync(ct)
            : null;

        return new EstadoOutlookDto(
            Configurado:               conexiones.EstaConfigurada,
            UsuarioConectado:          mia is not null,
            UsuarioRequiereReconexion: mia?.RequiereReconexion ?? false,
            EnOutlook:                 r.OutlookEventId is not null,
            ConTeams:                  r.OutlookConTeams,
            EnlaceTeams:               r.OutlookEnlaceTeams,
            Pendiente:                 r.OutlookPendiente,
            Error:                     r.OutlookError,
            SincronizadaEl:            r.OutlookSincronizadaEl,
            Organizador:               organizador);
    }
}
