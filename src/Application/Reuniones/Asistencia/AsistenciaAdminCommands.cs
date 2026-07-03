using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Reuniones.Asistencia;

// ── Abrir / cerrar el registro público ────────────────────────────────────
public sealed record CambiarRegistroAsistenciaCommand(int ReunionId, bool Abrir) : IRequest<Unit>;

public sealed class CambiarRegistroAsistenciaCommandHandler(IReunionRepository repo, IUnitOfWork uow)
    : IRequestHandler<CambiarRegistroAsistenciaCommand, Unit>
{
    public async Task<Unit> Handle(CambiarRegistroAsistenciaCommand cmd, CancellationToken ct)
    {
        var r = await repo.GetByIdAsync(cmd.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), cmd.ReunionId);
        r.RegistroAbierto = cmd.Abrir;
        repo.Update(r);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Regenerar el token (invalida el enlace/QR anterior) ────────────────────
public sealed record RegenerarTokenAsistenciaCommand(int ReunionId) : IRequest<Unit>;

public sealed class RegenerarTokenAsistenciaCommandHandler(IReunionRepository repo, IUnitOfWork uow)
    : IRequestHandler<RegenerarTokenAsistenciaCommand, Unit>
{
    public async Task<Unit> Handle(RegenerarTokenAsistenciaCommand cmd, CancellationToken ct)
    {
        var r = await repo.GetByIdAsync(cmd.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), cmd.ReunionId);
        r.RegenerarToken();
        repo.Update(r);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Eliminar un asistente puntual de la lista en vivo ──────────────────────
public sealed record EliminarAsistenteCommand(int ReunionId, int AsistenteId) : IRequest<Unit>;

public sealed class EliminarAsistenteCommandHandler(IReunionRepository repo, IUnitOfWork uow)
    : IRequestHandler<EliminarAsistenteCommand, Unit>
{
    public async Task<Unit> Handle(EliminarAsistenteCommand cmd, CancellationToken ct)
    {
        var r = await repo.GetByIdWithDetailsAsync(cmd.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), cmd.ReunionId);
        r.EliminarAsistente(cmd.AsistenteId);
        repo.Update(r);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Agregar asistentes desde el directorio de contactos ────────────────────
public sealed record AgregarAsistentesDirectorioCommand(int ReunionId, IReadOnlyList<int> ContactoIds) : IRequest<int>;

public sealed class AgregarAsistentesDirectorioCommandHandler(
    IReunionRepository repo, IApplicationDbContext ctx, IUnitOfWork uow)
    : IRequestHandler<AgregarAsistentesDirectorioCommand, int>
{
    public async Task<int> Handle(AgregarAsistentesDirectorioCommand cmd, CancellationToken ct)
    {
        var r = await repo.GetByIdWithDetailsAsync(cmd.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), cmd.ReunionId);

        if (cmd.ContactoIds.Count == 0) return 0;

        var ids = cmd.ContactoIds.ToList();
        var contactos = await ctx.Contactos.Where(c => ids.Contains(c.Id)).ToListAsync(ct);

        var correosExistentes = r.Asistentes
            .Where(a => a.Correo != null).Select(a => a.Correo!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var agregados = 0;
        foreach (var c in contactos)
        {
            // Salta duplicados por correo.
            if (c.Correo != null && correosExistentes.Contains(c.Correo)) continue;
            r.Agregar(new Asistente
            {
                Nombre = c.Nombre, Cargo = c.Cargo, Institucion = c.Institucion,
                Correo = c.Correo?.ToLowerInvariant(), Telefono = c.Telefono
            });
            if (c.Correo != null) correosExistentes.Add(c.Correo);
            agregados++;
        }

        if (agregados > 0)
        {
            r.NumAsistentes = r.Asistentes.Count;
            repo.Update(r);
            await uow.SaveChangesAsync(ct);
        }
        return agregados;
    }
}
