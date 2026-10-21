using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Usuarios.Common;

namespace Diger.TramitesEstado.Application.Usuarios.Commands.EliminarUsuario;

/// <summary>
/// Borrado <b>lógico</b> de un usuario: la fila se conserva y deja de verse.
///
/// <para><b>Por qué no se borra de verdad.</b> Quince columnas repartidas en once tablas guardan
/// el GUID de un usuario <b>sin clave foránea</b> — <c>Proyectos.ResponsableId</c>,
/// <c>Expedientes.AnalistaId</c>, <c>ProyectoInteresados.UsuarioId</c> y compañía—. Un DELETE las
/// dejaría apuntando a un fantasma en silencio: por ejemplo, un proyecto cuyo responsable ya no
/// existe no admite corregir su bitácora nunca más, porque la guarda de propiedad compara contra
/// un usuario inexistente. Y encima el DELETE fallaría en seco contra <c>Tickets.CreadoPorId</c>,
/// que es NO ACTION.</para>
///
/// <para><b>Dónde se aplica el ocultamiento.</b> En el filtro global de <c>AppDbContext</c>, no en
/// la consulta de la lista. Así el usuario desaparece también del login, de los selectores de
/// responsable y de cualquier consulta futura que nadie se acuerde de filtrar. Ocultarlo solo de
/// la lista sería un maquillaje: seguiría entrando al portal con su contraseña.</para>
///
/// <para><b>Sin clave otorgable.</b> Se exige la capacidad <c>EsAdministrador</c> del rol
/// —expuesta como <c>EsGlobal</c>— y no un permiso de la matriz: se pidió expresamente que no se
/// pudiera delegar.</para>
/// </summary>
public sealed record EliminarUsuarioCommand(Guid Id) : IRequest<Unit>;

public sealed class EliminarUsuarioCommandHandler(
    IApplicationDbContext ctx, IUnitOfWork uow, ICurrentUserService currentUser, IRolCatalogo catalogo)
    : IRequestHandler<EliminarUsuarioCommand, Unit>
{
    public async Task<Unit> Handle(EliminarUsuarioCommand cmd, CancellationToken ct)
    {
        if (!currentUser.EsGlobal)
            throw new DomainException("Eliminar usuarios está reservado a los administradores del portal.");

        // Antes de buscarlo: cerrarse la puerta desde adentro deja al portal sin ese administrador
        // y sin nadie que pueda deshacerlo desde la interfaz, porque el eliminado tampoco entra.
        if (currentUser.UserId == cmd.Id)
            throw new DomainException("No puede eliminarse a sí mismo.");

        var usuario = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Usuario), cmd.Id);

        // Mismo invariante que ya protege a la desactivación: eliminar al último administrador
        // deja el portal sin nadie capaz de administrarlo, y la única salida sería la base a mano.
        if (await AdministradoresInvariante.TieneRolAdministradorAsync(ctx, catalogo, cmd.Id, ct))
            await AdministradoresInvariante.ValidarNoEsElUltimoAdministradorAsync(ctx, catalogo, cmd.Id, ct);

        usuario.Eliminar();
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>Deshace un <see cref="EliminarUsuarioCommand"/>. Existe porque, sin él, un borrado por
/// error solo se arreglaría tocando la base de datos: la fila sobrevive pero nada en el portal la
/// alcanza. Lo alimenta la casilla «Mostrar eliminados» de la lista de usuarios.</summary>
public sealed record RestaurarUsuarioCommand(Guid Id) : IRequest<Unit>;

public sealed class RestaurarUsuarioCommandHandler(
    IApplicationDbContext ctx, IUnitOfWork uow, ICurrentUserService currentUser)
    : IRequestHandler<RestaurarUsuarioCommand, Unit>
{
    public async Task<Unit> Handle(RestaurarUsuarioCommand cmd, CancellationToken ct)
    {
        if (!currentUser.EsGlobal)
            throw new DomainException("Restaurar usuarios está reservado a los administradores del portal.");

        // IgnoreQueryFilters es obligatorio acá: el filtro global es justamente lo que esconde a
        // quien se quiere restaurar, así que sin esto la consulta nunca lo encontraría.
        var usuario = await ctx.Usuarios.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Usuario), cmd.Id);

        usuario.Restaurar();
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
