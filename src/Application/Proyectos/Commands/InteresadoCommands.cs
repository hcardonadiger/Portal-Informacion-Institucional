using Diger.TramitesEstado.Application.Proyectos.Common;

namespace Diger.TramitesEstado.Application.Proyectos.Commands;

// ── Agregar interesado ────────────────────────────────────────────────────
/// <summary>
/// Suma a un usuario del portal como interesado del proyecto.
///
/// <para>El nombre y el correo no viajan en el comando: se leen del usuario. Dejarlos entrar desde
/// afuera permitiría registrar a alguien con el nombre de otro, y el nombre es lo que después se
/// lee en la bitácora y en el listado.</para>
/// </summary>
public sealed record AgregarInteresadoCommand(
    int              ProyectoId,
    Guid             UsuarioId,
    RolInteresado    Rol,
    NivelCualitativo Influencia  = NivelCualitativo.Media,
    string?          Institucion = null,
    string?          Cargo       = null,
    string?          Notas       = null) : IRequest<int>;

public sealed class AgregarInteresadoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<AgregarInteresadoCommand, int>
{
    public async Task<int> Handle(AgregarInteresadoCommand cmd, CancellationToken ct)
    {
        var existe = await ctx.Proyectos.AnyAsync(p => p.Id == cmd.ProyectoId, ct);
        if (!existe) throw new NotFoundException(nameof(Proyecto), cmd.ProyectoId);

        // Usuarios no lleva filtro de alcance (la administración de usuarios es global), así que
        // esta búsqueda ve a cualquiera del portal — que es lo que se quiere: se puede sumar como
        // interesado a alguien de otra institución, y de hecho es el caso interesante.
        var usuario = await ctx.Usuarios
            .FirstOrDefaultAsync(u => u.Id == cmd.UsuarioId, ct)
            ?? throw new NotFoundException(nameof(Usuario), cmd.UsuarioId);

        if (!usuario.Activo)
            throw new DomainException(
                $"«{usuario.Nombre}» está inactivo en el portal y no puede quedar como interesado.");

        var repetido = await ctx.ProyectoInteresados
            .AnyAsync(i => i.ProyectoId == cmd.ProyectoId && i.UsuarioId == cmd.UsuarioId, ct);
        if (repetido)
            throw new DomainException($"«{usuario.Nombre}» ya figura como interesado de este proyecto.");

        var actor = currentUser.Nombre ?? "—";
        var interesado = InteresadoProyecto.Crear(
            cmd.ProyectoId, usuario.Id, usuario.Nombre, cmd.Rol, actor, cmd.Influencia,
            usuario.Correo, cmd.Institucion, cmd.Cargo, cmd.Notas);

        ctx.ProyectoInteresados.Add(interesado);

        // Se deja constancia de que este registro además abre el proyecto: es un cambio de acceso,
        // no solo una anotación de gestión.
        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            cmd.ProyectoId, TipoEventoProyecto.Interesado,
            $"Interesado agregado: {interesado.Nombre} ({interesado.Rol}" +
            (interesado.Institucion is { } i ? $", {i}" : "") + "). Puede ver el proyecto.", actor));

        await ctx.SaveChangesAsync(ct);
        return interesado.Id;
    }
}

// ── Actualizar interesado ─────────────────────────────────────────────────
/// <summary>Cambia el papel del interesado. La persona no se cambia acá: para eso se quita y se
/// agrega, que es lo que refleja lo que realmente pasa con el acceso.</summary>
public sealed record ActualizarInteresadoCommand(
    int              InteresadoId,
    RolInteresado    Rol,
    NivelCualitativo Influencia,
    string?          Institucion,
    string?          Cargo,
    string?          Notas) : IRequest<Unit>;

public sealed class ActualizarInteresadoCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<ActualizarInteresadoCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarInteresadoCommand cmd, CancellationToken ct)
    {
        var interesado = await ctx.ProyectoInteresados
            .FirstOrDefaultAsync(i => i.Id == cmd.InteresadoId, ct)
            ?? throw new NotFoundException(nameof(InteresadoProyecto), cmd.InteresadoId);

        interesado.Actualizar(cmd.Rol, cmd.Influencia, cmd.Institucion, cmd.Cargo, cmd.Notas);

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Quitar interesado ─────────────────────────────────────────────────────
public sealed record QuitarInteresadoCommand(int InteresadoId) : IRequest<Unit>;

public sealed class QuitarInteresadoCommandHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<QuitarInteresadoCommand, Unit>
{
    public async Task<Unit> Handle(QuitarInteresadoCommand cmd, CancellationToken ct)
    {
        var interesado = await ctx.ProyectoInteresados
            .FirstOrDefaultAsync(i => i.Id == cmd.InteresadoId, ct)
            ?? throw new NotFoundException(nameof(InteresadoProyecto), cmd.InteresadoId);

        // Quitar a un interesado le quita el acceso al proyecto, salvo que lo alcance por su
        // propio ámbito o porque sea el responsable. Vale la pena que la bitácora lo diga.
        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            interesado.ProyectoId, TipoEventoProyecto.Interesado,
            $"Interesado quitado: {interesado.Nombre}. Pierde el acceso que le daba este registro.",
            currentUser.Nombre ?? "—"));

        ctx.ProyectoInteresados.Remove(interesado);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
