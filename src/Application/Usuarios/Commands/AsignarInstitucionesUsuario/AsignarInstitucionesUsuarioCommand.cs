using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Proyectos.Services;

namespace Diger.TramitesEstado.Application.Usuarios.Commands.AsignarInstitucionesUsuario;

using Diger.TramitesEstado.Application.Usuarios.Common;

public sealed record AsignarJerarquiaUsuarioCommand(Guid UsuarioId, string Rol, IReadOnlyList<AsignacionDto> Asignaciones)
    : IRequest<Unit>;

public sealed class AsignarJerarquiaUsuarioCommandHandler(
    IUsuarioRepository repo, IUnitOfWork uow, IApplicationDbContext ctx, IRolCatalogo catalogo,
    IInteresadosAutomaticosSync sync)
    : IRequestHandler<AsignarJerarquiaUsuarioCommand, Unit>
{
    public async Task<Unit> Handle(AsignarJerarquiaUsuarioCommand cmd, CancellationToken ct)
    {
        _ = await repo.GetByIdAsync(cmd.UsuarioId, ct)
            ?? throw new NotFoundException(nameof(Usuario), cmd.UsuarioId);

        // Quitarle la condición de administrador al último que la tiene dejaría al portal sin
        // nadie capaz de volver a asignarla. Cuenta como quitársela tanto cambiarle el rol
        // como dejarlo sin asignaciones (sin asignación no hay rol; ver AutenticarUsuarioQuery).
        var seguiraSiendoAdmin =
            (cmd.Asignaciones?.Count ?? 0) > 0 &&
            catalogo.Obtener(cmd.Rol)?.EsAdministrador == true;

        if (!seguiraSiendoAdmin &&
            await AdministradoresInvariante.TieneRolAdministradorAsync(ctx, catalogo, cmd.UsuarioId, ct))
        {
            await AdministradoresInvariante.ValidarNoEsElUltimoAdministradorAsync(ctx, catalogo, cmd.UsuarioId, ct);
        }

        await repo.ReemplazarAsignacionesAsync(cmd.UsuarioId, cmd.Rol, cmd.Asignaciones ?? [], ct);
        await uow.SaveChangesAsync(ct);

        await sync.SincronizarUsuarioAsync(cmd.UsuarioId, ct);

        return Unit.Value;
    }
}
