
namespace Diger.TramitesEstado.Application.Reuniones.Outlook;

/// <summary>Primer envío de la reunión al calendario del usuario actual, que pasa a ser su
/// organizador en Outlook. Cualquier problema vuelve como DomainException con el motivo.</summary>
public sealed record EnviarReunionAOutlookCommand(int ReunionId, bool ConTeams, string UrlFicha) : IRequest<Unit>;

public sealed class EnviarReunionAOutlookCommandHandler(
    IApplicationDbContext ctx,
    IReunionRepository repo,
    ICurrentUserService currentUser,
    IOutlookCalendario outlook,
    IUnitOfWork uow)
    : IRequestHandler<EnviarReunionAOutlookCommand, Unit>
{
    public async Task<Unit> Handle(EnviarReunionAOutlookCommand cmd, CancellationToken ct)
    {
        var usuarioId = currentUser.UserId
            ?? throw new DomainException("No hay una sesión de usuario para enviar a Outlook.");

        var conexion = await ctx.ConexionesMicrosoft.AsNoTracking().FirstOrDefaultAsync(c => c.Id == usuarioId, ct)
            ?? throw new DomainException("Conecte su Outlook desde «Mi perfil» antes de enviar reuniones.");
        if (conexion.RequiereReconexion)
            throw new DomainException("Su conexión de Outlook venció. Reconéctela desde «Mi perfil» y vuelva a enviar.");

        var r = await repo.GetByIdWithDetailsAsync(cmd.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), cmd.ReunionId);

        var transaccion = r.PrepararEnvioOutlook(cmd.ConTeams);
        var evento = EventoOutlookBuilder.Construir(r, conexion.Upn, cmd.UrlFicha); // rechaza antes de tocar Graph

        // La transacción se guarda ANTES de llamar a Graph: si Graph crea la cita y lo que falla
        // es guardar después, el reintento manda el mismo transactionId y Graph no la duplica.
        await uow.SaveChangesAsync(ct);

        ResultadoOutlook res;
        try
        {
            res = await outlook.CrearAsync(usuarioId, evento, transaccion, ct);
        }
        catch (OutlookConexionVencidaException)
        {
            throw new DomainException("Su conexión de Outlook venció. Reconéctela desde «Mi perfil» y vuelva a enviar.");
        }
        catch (OutlookNoDisponibleException ex)
        {
            throw new DomainException($"Outlook no respondió ({ex.Message}). Puede volver a intentarlo: no se duplicará la cita.");
        }

        r.RegistrarEnvioOutlook(res.EventId, usuarioId, res.EnlaceTeams, evento.Huella(), DateTime.UtcNow);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>Después de guardar: si la cita cambió para los invitados, se actualiza en Outlook con
/// la conexión del ORGANIZADOR. Nunca lanza por Outlook: un fallo queda como pendiente.</summary>
public sealed record SincronizarReunionOutlookCommand(int ReunionId, string UrlFicha) : IRequest<Unit>;

public sealed class SincronizarReunionOutlookCommandHandler(
    IApplicationDbContext ctx,
    IReunionRepository repo,
    IOutlookCalendario outlook,
    IUnitOfWork uow)
    : IRequestHandler<SincronizarReunionOutlookCommand, Unit>
{
    public async Task<Unit> Handle(SincronizarReunionOutlookCommand cmd, CancellationToken ct)
    {
        var r = await repo.GetByIdWithDetailsAsync(cmd.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), cmd.ReunionId);
        if (!r.EstaEnOutlook) return Unit.Value;

        var organizadorId = r.OutlookOrganizadorId!.Value;
        var conexion = await ctx.ConexionesMicrosoft.AsNoTracking().FirstOrDefaultAsync(c => c.Id == organizadorId, ct);

        EventoOutlook evento;
        try
        {
            evento = EventoOutlookBuilder.Construir(r, conexion?.Upn, cmd.UrlFicha);
        }
        catch (DomainException ex)
        {
            r.MarcarOutlookPendiente($"No se actualizó en Outlook: {ex.Message}");
            await uow.SaveChangesAsync(ct);
            return Unit.Value;
        }

        var huella = evento.Huella();
        if (huella == r.OutlookHuella && !r.OutlookPendiente) return Unit.Value;

        try
        {
            if (conexion is null || conexion.RequiereReconexion)
                throw new OutlookConexionVencidaException("sin conexión vigente");

            var res = await outlook.ActualizarAsync(organizadorId, r.OutlookEventId!, evento, ct);
            r.RegistrarSincronizacionOutlook(res.EnlaceTeams, huella, DateTime.UtcNow);
        }
        catch (OutlookConexionVencidaException)
        {
            var nombre = await ctx.Usuarios.AsNoTracking().Where(u => u.Id == organizadorId)
                .Select(u => u.Nombre).FirstOrDefaultAsync(ct) ?? "quien la envió";
            r.MarcarOutlookPendiente($"La conexión de Outlook de {nombre} venció; debe reconectarse desde su perfil.");
        }
        catch (OutlookNoDisponibleException ex)
        {
            r.MarcarOutlookPendiente($"No se pudo actualizar en Outlook: {ex.Message}");
        }

        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>Antes de borrar la reunión. Si no puede cancelar lanza DomainException con el aviso;
/// quien llama decide borrar igual (el portal no bloquea un borrado por Microsoft).</summary>
public sealed record CancelarReunionOutlookCommand(int ReunionId) : IRequest<Unit>;

public sealed class CancelarReunionOutlookCommandHandler(IReunionRepository repo, IOutlookCalendario outlook)
    : IRequestHandler<CancelarReunionOutlookCommand, Unit>
{
    public async Task<Unit> Handle(CancelarReunionOutlookCommand cmd, CancellationToken ct)
    {
        var r = await repo.GetByIdAsync(cmd.ReunionId, ct);
        if (r is null || !r.EstaEnOutlook) return Unit.Value;

        try
        {
            await outlook.CancelarAsync(r.OutlookOrganizadorId!.Value, r.OutlookEventId!,
                $"La reunión se canceló en {Marca.Nombre}.", ct);
        }
        catch (Exception ex) when (ex is OutlookConexionVencidaException or OutlookNoDisponibleException)
        {
            throw new DomainException("No se pudo cancelar la cita en Outlook: cancélela a mano desde el calendario del organizador.");
        }

        return Unit.Value;
    }
}
