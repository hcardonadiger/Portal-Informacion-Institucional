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

        // Lo decide la capacidad EsAdministrador del rol (tabla Roles), no que el rol se
        // llame "Administrador": un rol personalizado con esa capacidad tiene que poder
        // destrabar un compromiso igual que el rol base.
        var esAdmin = currentUser.EsGlobal;

        // Regla: Si está Cumplido/Aceptado, nadie salvo un administrador puede destrabarlo.
        if (acuerdo.Estado == EstadoCompromiso.Cumplido && !esAdmin)
            throw new DomainException("El compromiso está Aceptado / Cumplido. Solo un rol con capacidad de administrador puede cambiar su estado.");

        // Regla: quien no es administrador no puede auto-aprobar a Cumplido.
        if (request.NuevoEstado == EstadoCompromiso.Cumplido && !esAdmin)
            throw new DomainException("Solo un rol con capacidad de administrador puede marcar un compromiso como Aceptado / Cumplido.");

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
