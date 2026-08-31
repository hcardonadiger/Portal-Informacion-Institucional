namespace Diger.TramitesEstado.Application.Proyectos.Commands;

/// <summary>
/// Vincula una reunión a un proyecto.
///
/// <para><b>Los dos extremos se cargan por su consulta normal</b>, y por lo tanto por su filtro de
/// alcance. No es un trámite: es la autorización. Sin cargar la reunión, alguien podría colgar del
/// proyecto una reunión que no puede ni abrir pasando su Id en el formulario —y con eso averiguar
/// que existe, que es justo lo que el filtro impide—.</para>
/// </summary>
public sealed record VincularReunionCommand(int ProyectoId, int ReunionId, string? Nota) : IRequest<int>;

public sealed class VincularReunionCommandHandler(
    IApplicationDbContext ctx, ICurrentUserService currentUser)
    : IRequestHandler<VincularReunionCommand, int>
{
    public async Task<int> Handle(VincularReunionCommand cmd, CancellationToken ct)
    {
        var proyecto = await ctx.Proyectos.FirstOrDefaultAsync(p => p.Id == cmd.ProyectoId, ct)
            ?? throw new NotFoundException(nameof(Proyecto), cmd.ProyectoId);

        if (proyecto.Estado is EstadoProyecto.Cerrado or EstadoProyecto.Cancelado)
            throw new DomainException(
                $"El proyecto está «{proyecto.Estado}»: sus vínculos quedan como están.");

        var reunion = await ctx.Reuniones.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == cmd.ReunionId, ct)
            ?? throw new DomainException("Esa reunión no existe o no está a su alcance.");

        var yaEsta = await ctx.ProyectoReuniones
            .AnyAsync(x => x.ProyectoId == cmd.ProyectoId && x.ReunionId == cmd.ReunionId, ct);

        if (yaEsta)
            throw new DomainException($"«{reunion.Titulo}» ya está vinculada a este proyecto.");

        var actor   = currentUser.Nombre ?? "—";
        var vinculo = ProyectoReunion.Crear(cmd.ProyectoId, cmd.ReunionId, actor, cmd.Nota);
        ctx.ProyectoReuniones.Add(vinculo);

        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            cmd.ProyectoId, TipoEventoProyecto.ModificacionFicha,
            $"reunión vinculada: «{reunion.Titulo}»", actor));

        await ctx.SaveChangesAsync(ct);
        return vinculo.Id;
    }
}

/// <summary>Vincula un expediente. Mismas reglas que la reunión.</summary>
public sealed record VincularExpedienteCommand(int ProyectoId, int ExpedienteId, string? Nota) : IRequest<int>;

public sealed class VincularExpedienteCommandHandler(
    IApplicationDbContext ctx, ICurrentUserService currentUser)
    : IRequestHandler<VincularExpedienteCommand, int>
{
    public async Task<int> Handle(VincularExpedienteCommand cmd, CancellationToken ct)
    {
        var proyecto = await ctx.Proyectos.FirstOrDefaultAsync(p => p.Id == cmd.ProyectoId, ct)
            ?? throw new NotFoundException(nameof(Proyecto), cmd.ProyectoId);

        if (proyecto.Estado is EstadoProyecto.Cerrado or EstadoProyecto.Cancelado)
            throw new DomainException(
                $"El proyecto está «{proyecto.Estado}»: sus vínculos quedan como están.");

        var expediente = await ctx.Expedientes.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == cmd.ExpedienteId, ct)
            ?? throw new DomainException("Ese expediente no existe o no está a su alcance.");

        var yaEsta = await ctx.ProyectoExpedientes
            .AnyAsync(x => x.ProyectoId == cmd.ProyectoId && x.ExpedienteId == cmd.ExpedienteId, ct);

        if (yaEsta)
            throw new DomainException($"El expediente {expediente.Codigo} ya está vinculado a este proyecto.");

        var actor   = currentUser.Nombre ?? "—";
        var vinculo = ProyectoExpediente.Crear(cmd.ProyectoId, cmd.ExpedienteId, actor, cmd.Nota);
        ctx.ProyectoExpedientes.Add(vinculo);

        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            cmd.ProyectoId, TipoEventoProyecto.ModificacionFicha,
            $"expediente vinculado: {expediente.Codigo}", actor));

        await ctx.SaveChangesAsync(ct);
        return vinculo.Id;
    }
}

/// <summary>
/// Quita un vínculo.
///
/// <para>Se borra la fila y no se marca como inactiva: el vínculo no es un hecho histórico sino
/// una afirmación sobre el presente —«esta reunión trata de este proyecto»—. Que quede constancia
/// de haberla quitado es trabajo de la bitácora, y ahí queda.</para>
///
/// <para>Se pide el proyecto además del vínculo y se comprueba que coincidan: el filtro ya impide
/// tocar el de otro, pero esto ataja un formulario viejo que mande el vínculo de OTRO proyecto que
/// la persona sí puede ver.</para>
/// </summary>
public sealed record QuitarVinculoReunionCommand(int ProyectoId, int VinculoId) : IRequest<Unit>;

public sealed class QuitarVinculoReunionCommandHandler(
    IApplicationDbContext ctx, ICurrentUserService currentUser)
    : IRequestHandler<QuitarVinculoReunionCommand, Unit>
{
    public async Task<Unit> Handle(QuitarVinculoReunionCommand cmd, CancellationToken ct)
    {
        var vinculo = await ctx.ProyectoReuniones.FirstOrDefaultAsync(x => x.Id == cmd.VinculoId, ct)
            ?? throw new NotFoundException(nameof(ProyectoReunion), cmd.VinculoId);

        if (vinculo.ProyectoId != cmd.ProyectoId)
            throw new DomainException("Ese vínculo no pertenece a este proyecto.");

        // El título se busca sin filtro a propósito: se está quitando el vínculo, y la bitácora
        // tiene que poder nombrar qué se quitó aunque la reunión esté fuera del alcance de quien
        // lo quita. Es un nombre que ya estaba a la vista en la ficha.
        var titulo = await ctx.Reuniones.AsNoTracking().IgnoreQueryFilters()
            .Where(r => r.Id == vinculo.ReunionId).Select(r => r.Titulo).FirstOrDefaultAsync(ct);

        ctx.ProyectoReuniones.Remove(vinculo);

        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            cmd.ProyectoId, TipoEventoProyecto.ModificacionFicha,
            $"reunión desvinculada: «{titulo ?? "(eliminada)"}»", currentUser.Nombre ?? "—"));

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>Quita el vínculo de un expediente. Mismas reglas.</summary>
public sealed record QuitarVinculoExpedienteCommand(int ProyectoId, int VinculoId) : IRequest<Unit>;

public sealed class QuitarVinculoExpedienteCommandHandler(
    IApplicationDbContext ctx, ICurrentUserService currentUser)
    : IRequestHandler<QuitarVinculoExpedienteCommand, Unit>
{
    public async Task<Unit> Handle(QuitarVinculoExpedienteCommand cmd, CancellationToken ct)
    {
        var vinculo = await ctx.ProyectoExpedientes.FirstOrDefaultAsync(x => x.Id == cmd.VinculoId, ct)
            ?? throw new NotFoundException(nameof(ProyectoExpediente), cmd.VinculoId);

        if (vinculo.ProyectoId != cmd.ProyectoId)
            throw new DomainException("Ese vínculo no pertenece a este proyecto.");

        var codigo = await ctx.Expedientes.AsNoTracking().IgnoreQueryFilters()
            .Where(e => e.Id == vinculo.ExpedienteId).Select(e => e.Codigo).FirstOrDefaultAsync(ct);

        ctx.ProyectoExpedientes.Remove(vinculo);

        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            cmd.ProyectoId, TipoEventoProyecto.ModificacionFicha,
            $"expediente desvinculado: {codigo ?? "(eliminado)"}", currentUser.Nombre ?? "—"));

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
