using FluentValidation;

namespace Diger.TramitesEstado.Application.Areas.Commands;

public sealed record ActualizarAreaCommand(string Id, string Nombre, string? Descripcion, string? NombreCorto, string? LogoUrl)
    : IRequest<Unit>;

public sealed class ActualizarAreaCommandHandler(IAreaRepository repo, IUnitOfWork uow)
    : IRequestHandler<ActualizarAreaCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarAreaCommand cmd, CancellationToken ct)
    {
        var area = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Area), cmd.Id);

        if (await repo.ExisteNombreAsync(cmd.Nombre, area.InstitucionId, cmd.Id, ct))
            throw new DomainException($"Ya existe otra área con el nombre '{cmd.Nombre}' en la institución.");

        area.Renombrar(cmd.Nombre);
        area.ActualizarDetalles(cmd.Descripcion, cmd.NombreCorto, cmd.LogoUrl);
        
        repo.Update(area);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class ActualizarAreaCommandValidator : AbstractValidator<ActualizarAreaCommand>
{
    public ActualizarAreaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
    }
}
