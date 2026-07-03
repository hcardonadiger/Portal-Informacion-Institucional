using FluentValidation;

namespace Diger.TramitesEstado.Application.Instituciones.Commands.CrearInstitucion;

public sealed record CrearInstitucionCommand(string Nombre, List<string> Tramites) : IRequest<int>;

public sealed class CrearInstitucionCommandHandler(
    IInstitucionRepository repo,
    IUnitOfWork uow)
    : IRequestHandler<CrearInstitucionCommand, int>
{
    public async Task<int> Handle(CrearInstitucionCommand cmd, CancellationToken ct)
    {
        if (await repo.ExisteNombreAsync(cmd.Nombre, null, ct))
            throw new DomainException($"Ya existe una institución con el nombre '{cmd.Nombre.Trim().ToUpper()}'.");

        var inst = Institucion.Crear(cmd.Nombre);
        AsignarTramites(inst, cmd.Tramites);

        await repo.AddAsync(inst, ct);
        await uow.SaveChangesAsync(ct);
        return inst.Id;
    }

    internal static void AsignarTramites(Institucion inst, List<string> tramites)
    {
        inst.LimpiarTramites();
        var orden = 0;
        foreach (var nombre in tramites.Where(t => !string.IsNullOrWhiteSpace(t)))
            inst.AgregarTramite(TramiteDefinicion.Crear(inst.Id, nombre, orden++));
    }
}

public sealed class CrearInstitucionCommandValidator : AbstractValidator<CrearInstitucionCommand>
{
    public CrearInstitucionCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(120);
        RuleForEach(x => x.Tramites).MaximumLength(400);
    }
}
