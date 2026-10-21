using FluentValidation;

namespace Diger.TramitesEstado.Application.Unidades.Commands;

public sealed record ActualizarUnidadCommand(string Id, string Nombre, bool Activo, string? Descripcion, string? NombreCorto, string? LogoUrl)
    : IRequest<Unit>;

public sealed class ActualizarUnidadCommandHandler(IUnidadRepository repo, IUnitOfWork uow)
    : IRequestHandler<ActualizarUnidadCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarUnidadCommand cmd, CancellationToken ct)
    {
        var unidad = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Unidad), cmd.Id);

        if (await repo.ExisteNombreAsync(cmd.Nombre, unidad.AreaId, cmd.Id, ct))
            throw new DomainException($"Ya existe otra unidad con el nombre '{cmd.Nombre}' en el área.");

        unidad.Renombrar(cmd.Nombre);
        unidad.ActualizarDetalles(cmd.Descripcion, cmd.NombreCorto, cmd.LogoUrl);
        
        if (cmd.Activo) unidad.Activar();
        else unidad.Desactivar();
        
        repo.Update(unidad);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class ActualizarUnidadCommandValidator : AbstractValidator<ActualizarUnidadCommand>
{
    public ActualizarUnidadCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
    }
}
