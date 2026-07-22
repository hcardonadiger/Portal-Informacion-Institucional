namespace Diger.TramitesEstado.Application.Reuniones.Commands.AgregarComentarioCompromiso;

public sealed record AgregarComentarioCompromisoCommand(
    int     CompromisoId,
    string? Comentario,
    string? ArchivoNombre,
    string? ArchivoUrl,
    long?   ArchivoTamano,
    bool    AutoEnviar = false) : IRequest<int>;

public sealed class AgregarComentarioCompromisoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser) : IRequestHandler<AgregarComentarioCompromisoCommand, int>
{
    public async Task<int> Handle(AgregarComentarioCompromisoCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Comentario) && string.IsNullOrWhiteSpace(request.ArchivoUrl))
            throw new DomainException("Debe ingresar un comentario o adjuntar un archivo.");

        var acuerdo = await ctx.Acuerdos
            .FirstOrDefaultAsync(x => x.Id == request.CompromisoId, ct);

        if (acuerdo is null)
            throw new NotFoundException(nameof(AcuerdoReunion), request.CompromisoId);

        var esAdmin = currentUser.Rol == nameof(RolUsuario.Administrador);

        if (acuerdo.Estado == EstadoCompromiso.Cumplido)
            throw new DomainException("El compromiso se encuentra Aceptado / Cumplido. No se permiten comentarios ni archivos a menos que un Administrador cambie primero su estado.");

        if (!esAdmin && acuerdo.Estado == EstadoCompromiso.EnRevision)
            throw new DomainException("El compromiso está En Revisión. Cambie el estado a 'En Proceso' si necesita agregar más comentarios o archivos.");

        var actor = !string.IsNullOrWhiteSpace(currentUser.Nombre)
            ? $"{currentUser.Nombre} ({currentUser.Rol})"
            : currentUser.Correo ?? "Usuario";

        var entidad = new ComentarioCompromiso
        {
            AcuerdoReunionId = acuerdo.Id,
            Comentario = string.IsNullOrWhiteSpace(request.Comentario) ? null : request.Comentario.Trim(),
            ArchivoNombre = request.ArchivoNombre,
            ArchivoUrl = request.ArchivoUrl,
            ArchivoTamano = request.ArchivoTamano,
            CreadoPor = actor,
            CreadoPorRol = currentUser.Rol,
            CreadoEl = DateTime.UtcNow
        };

        ctx.ComentariosCompromisos.Add(entidad);

        if (request.AutoEnviar && !esAdmin && acuerdo.Estado != EstadoCompromiso.Cumplido)
        {
            acuerdo.Estado = EstadoCompromiso.EnRevision;
            acuerdo.SeguimientoActualizadoPor = actor;
            acuerdo.SeguimientoActualizadoEl = DateTime.UtcNow;
        }

        await ctx.SaveChangesAsync(ct);
        return entidad.Id;
    }
}
