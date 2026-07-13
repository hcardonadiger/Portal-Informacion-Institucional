using FluentValidation;

namespace Diger.TramitesEstado.Application.Areas.Commands;

public sealed record CrearAreaCommand(string Id, string InstitucionId, string Nombre, string? Descripcion, string? NombreCorto, string? LogoUrl)
    : IRequest<string>;

public sealed class CrearAreaCommandHandler(IAreaRepository repo, IUnitOfWork uow)
    : IRequestHandler<CrearAreaCommand, string>
{
    public async Task<string> Handle(CrearAreaCommand cmd, CancellationToken ct)
    {
        if (await repo.ExisteNombreAsync(cmd.Nombre, cmd.InstitucionId, null, ct))
            throw new DomainException($"Ya existe un área con el nombre '{cmd.Nombre}' en la institución especificada.");

        if (await repo.GetByIdAsync(cmd.Id, ct) != null)
            throw new DomainException($"El ID '{cmd.Id}' ya está en uso.");

        var area = Area.Crear(cmd.Id, cmd.InstitucionId, cmd.Nombre, cmd.Descripcion, cmd.NombreCorto, cmd.LogoUrl);
        await repo.AddAsync(area, ct);
        await uow.SaveChangesAsync(ct);
        return area.Id;
    }
}

public sealed class CrearAreaCommandValidator : AbstractValidator<CrearAreaCommand>
{
    public CrearAreaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().Matches(@"^[A-Z0-9]+$").MaximumLength(20);
        RuleFor(x => x.InstitucionId).NotEmpty();
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
    }
}
