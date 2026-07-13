using FluentValidation;

namespace Diger.TramitesEstado.Application.Unidades.Commands;

public sealed record CrearUnidadCommand(string Id, string AreaId, string Nombre, string? Descripcion, string? NombreCorto, string? LogoUrl)
    : IRequest<string>;

public sealed class CrearUnidadCommandHandler(IUnidadRepository repo, IUnitOfWork uow)
    : IRequestHandler<CrearUnidadCommand, string>
{
    public async Task<string> Handle(CrearUnidadCommand cmd, CancellationToken ct)
    {
        if (await repo.ExisteNombreAsync(cmd.Nombre, cmd.AreaId, null, ct))
            throw new DomainException($"Ya existe una unidad con el nombre '{cmd.Nombre}' en el área especificada.");

        if (await repo.GetByIdAsync(cmd.Id, ct) != null)
            throw new DomainException($"El ID '{cmd.Id}' ya está en uso.");

        var unidad = Unidad.Crear(cmd.Id, cmd.AreaId, cmd.Nombre, cmd.Descripcion, cmd.NombreCorto, cmd.LogoUrl);
        await repo.AddAsync(unidad, ct);
        await uow.SaveChangesAsync(ct);
        return unidad.Id;
    }
}

public sealed class CrearUnidadCommandValidator : AbstractValidator<CrearUnidadCommand>
{
    public CrearUnidadCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Matches(@"^[A-Z0-9]+$").MaximumLength(20);
        RuleFor(x => x.AreaId).NotEmpty();
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
    }
}
