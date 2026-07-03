using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Instituciones.Commands.EliminarInstitucion;

public sealed record EliminarInstitucionCommand(int Id) : IRequest<Unit>;

public sealed class EliminarInstitucionCommandHandler(
    IInstitucionRepository repo,
    IApplicationDbContext ctx,
    IUnitOfWork uow)
    : IRequestHandler<EliminarInstitucionCommand, Unit>
{
    public async Task<Unit> Handle(EliminarInstitucionCommand cmd, CancellationToken ct)
    {
        var inst = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Institucion), cmd.Id);

        var id = cmd.Id;
        var motivos = new List<string>();
        if (await ctx.Expedientes.IgnoreQueryFilters().AnyAsync(e => e.InstitucionId == id, ct)) motivos.Add("expedientes");
        if (await ctx.Tickets.IgnoreQueryFilters().AnyAsync(t => t.InstitucionId == id, ct))     motivos.Add("tickets");
        if (await ctx.Reuniones.IgnoreQueryFilters().AnyAsync(r => r.InstitucionId == id, ct))    motivos.Add("reuniones");
        if (await ctx.Contactos.IgnoreQueryFilters().AnyAsync(c => c.InstitucionId == id, ct))    motivos.Add("contactos");
        if (await ctx.UsuarioInstituciones.AnyAsync(u => u.InstitucionId == id, ct))              motivos.Add("usuarios asignados");

        if (motivos.Count > 0)
            throw new DomainException(
                $"No se puede eliminar la institución porque tiene {string.Join(", ", motivos)} asociados. Desactívela en su lugar.");

        repo.Delete(inst);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
