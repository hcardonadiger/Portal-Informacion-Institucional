using Diger.TramitesEstado.Application.Reuniones.Outlook;

namespace Diger.TramitesEstado.Application.Reuniones.Memoria;

/// <summary>
/// La transcripción de Teams de una reunión que el portal envió a Outlook con Teams (fase 2,
/// parte B). Usa la conexión del <b>organizador</b>, no la de quien pregunta: es el único al que
/// Graph le entrega la transcripción. Lanza <see cref="NotFoundException"/> o
/// <see cref="TranscripcionNoDisponibleException"/> con un mensaje para la persona.
/// </summary>
public sealed record GetTranscripcionReunionQuery(int ReunionId) : IRequest<TranscripcionTeams>;

public sealed class GetTranscripcionReunionQueryHandler(IApplicationDbContext ctx, ITranscripcionesTeams teams)
    : IRequestHandler<GetTranscripcionReunionQuery, TranscripcionTeams>
{
    public const string SinTeams =
        "La reunión no se envió a Outlook con Teams desde el portal, así que no hay reunión de Teams " +
        "de la cual sacar la transcripción. Pídale a la persona que la pegue.";

    public async Task<TranscripcionTeams> Handle(GetTranscripcionReunionQuery q, CancellationToken ct)
    {
        // Pasa por el filtro de alcance: quien no ve la reunión no ve su transcripción.
        var r = await ctx.Reuniones.AsNoTracking()
            .Where(x => x.Id == q.ReunionId)
            .Select(x => new { x.OutlookOrganizadorId, x.OutlookEnlaceTeams })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Reunion), q.ReunionId);

        if (r.OutlookOrganizadorId is not { } organizadorId || string.IsNullOrWhiteSpace(r.OutlookEnlaceTeams))
            throw new TranscripcionNoDisponibleException(SinTeams);

        try
        {
            return await teams.ObtenerUltimaAsync(organizadorId, r.OutlookEnlaceTeams, ct);
        }
        catch (OutlookConexionVencidaException)
        {
            var nombre = await ctx.Usuarios.AsNoTracking().Where(u => u.Id == organizadorId)
                .Select(u => u.Nombre).FirstOrDefaultAsync(ct) ?? "quien organizó la reunión";
            throw new TranscripcionNoDisponibleException(
                $"La conexión de Outlook de {nombre}, que organizó la reunión, venció: tiene que reconectarse desde su perfil.");
        }
        catch (OutlookNoDisponibleException ex)
        {
            throw new TranscripcionNoDisponibleException($"Microsoft no entregó la transcripción: {ex.Message}.");
        }
    }
}
