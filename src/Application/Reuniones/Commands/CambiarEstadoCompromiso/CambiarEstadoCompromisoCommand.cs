namespace Diger.TramitesEstado.Application.Reuniones.Commands.CambiarEstadoCompromiso;

public sealed record CambiarEstadoCompromisoCommand(
    int              CompromisoId,
    EstadoCompromiso NuevoEstado,
    string?          Nota = null) : IRequest;

public sealed class CambiarEstadoCompromisoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser) : IRequestHandler<CambiarEstadoCompromisoCommand>
{
    public async Task Handle(CambiarEstadoCompromisoCommand request, CancellationToken ct)
    {
        var acuerdo = await ctx.Acuerdos
            .FirstOrDefaultAsync(x => x.Id == request.CompromisoId, ct);

        if (acuerdo is null)
            throw new NotFoundException(nameof(AcuerdoReunion), request.CompromisoId);

        var esAdmin = currentUser.Rol == nameof(RolUsuario.Administrador);

        // Regla: Si está Cumplido/Aceptado, nadie salvo Admin puede destrabar/cambiar el estado.
        if (acuerdo.Estado == EstadoCompromiso.Cumplido && !esAdmin)
            throw new DomainException("El compromiso está Aceptado / Cumplido. Solo un Administrador puede cambiar su estado.");

        // Regla: Los no-admins no pueden auto-aprobar a Cumplido.
        if (request.NuevoEstado == EstadoCompromiso.Cumplido && !esAdmin)
            throw new DomainException("Solo los Administradores pueden marcar un compromiso como Aceptado / Cumplido.");

        var actor = !string.IsNullOrWhiteSpace(currentUser.Nombre)
            ? $"{currentUser.Nombre} ({currentUser.Rol})"
            : currentUser.Correo ?? "Usuario";

        acuerdo.ActualizarSeguimiento(
            request.NuevoEstado,
            acuerdo.FechaCumplimiento,
            request.Nota ?? acuerdo.NotaSeguimiento,
            actor);

        // Si se cambia el estado con una nota descriptiva, también se registra en la línea de tiempo
        if (!string.IsNullOrWhiteSpace(request.Nota))
        {
            ctx.ComentariosCompromisos.Add(new ComentarioCompromiso
            {
                AcuerdoReunionId = acuerdo.Id,
                Comentario = $"[Cambio de estado a {request.NuevoEstado}]: {request.Nota.Trim()}",
                CreadoPor = actor,
                CreadoPorRol = currentUser.Rol,
                CreadoEl = DateTime.UtcNow
            });
        }

        await ctx.SaveChangesAsync(ct);
    }
}
