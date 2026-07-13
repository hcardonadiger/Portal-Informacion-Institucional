using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Expedientes.Plantillas;

// ── Crear plantilla ──────────────────────────────────────────────────────────
public sealed record CrearPlantillaCommand(
    string Nombre, List<PlantillaLegalInput> Legal, List<PlantillaRequisitoInput> Requisitos) : IRequest<int>;

public sealed class CrearPlantillaCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<CrearPlantillaCommand, int>
{
    public async Task<int> Handle(CrearPlantillaCommand cmd, CancellationToken ct)
    {
        var nombre = cmd.Nombre.Trim();
        if (await ctx.PlantillasTramite.AnyAsync(p => p.Nombre == nombre, ct))
            throw new DomainException($"Ya existe una plantilla llamada «{nombre}».");

        var plantilla = PlantillaTramite.Crear(nombre);
        AplicarHijos(plantilla, cmd.Legal, cmd.Requisitos);
        ctx.PlantillasTramite.Add(plantilla);
        await ctx.SaveChangesAsync(ct);
        return plantilla.Id;
    }

    internal static void AplicarHijos(PlantillaTramite plantilla, List<PlantillaLegalInput> legal, List<PlantillaRequisitoInput> requisitos)
    {
        plantilla.LimpiarHijos();
        var ordenL = 0;
        foreach (var l in legal.Where(x => !string.IsNullOrWhiteSpace(x.Instrumento)))
            plantilla.Agregar(new PlantillaFundamentoLegal
            {
                Orden = ordenL++, Instrumento = l.Instrumento.Trim(),
                Articulos = l.Articulos?.Trim(), Obs = l.Obs?.Trim()
            });

        var ordenR = 0;
        foreach (var r in requisitos.Where(x => !string.IsNullOrWhiteSpace(x.Requisito)))
            plantilla.Agregar(new PlantillaRequisito
            {
                Orden = ordenR++, Requisito = r.Requisito.Trim(), Obs = r.Obs?.Trim()
            });
    }
}

// ── Actualizar plantilla ─────────────────────────────────────────────────────
public sealed record ActualizarPlantillaCommand(
    int Id, string Nombre, bool Activa, List<PlantillaLegalInput> Legal, List<PlantillaRequisitoInput> Requisitos) : IRequest<Unit>;

public sealed class ActualizarPlantillaCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<ActualizarPlantillaCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarPlantillaCommand cmd, CancellationToken ct)
    {
        var plantilla = await ctx.PlantillasTramite
            .Include(p => p.Legal).Include(p => p.Requisitos)
            .FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(PlantillaTramite), cmd.Id);

        var nombre = cmd.Nombre.Trim();
        if (await ctx.PlantillasTramite.AnyAsync(p => p.Nombre == nombre && p.Id != cmd.Id, ct))
            throw new DomainException($"Ya existe una plantilla llamada «{nombre}».");

        plantilla.Actualizar(nombre, cmd.Activa);
        CrearPlantillaCommandHandler.AplicarHijos(plantilla, cmd.Legal, cmd.Requisitos);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Eliminar plantilla ───────────────────────────────────────────────────────
public sealed record EliminarPlantillaCommand(int Id) : IRequest<Unit>;

public sealed class EliminarPlantillaCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<EliminarPlantillaCommand, Unit>
{
    public async Task<Unit> Handle(EliminarPlantillaCommand cmd, CancellationToken ct)
    {
        var plantilla = await ctx.PlantillasTramite.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
            ?? throw new NotFoundException(nameof(PlantillaTramite), cmd.Id);

        ctx.PlantillasTramite.Remove(plantilla);
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class CrearPlantillaCommandValidator : AbstractValidator<CrearPlantillaCommand>
{
    public CrearPlantillaCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre de la plantilla es obligatorio.").MaximumLength(300);
    }
}

public sealed class ActualizarPlantillaCommandValidator : AbstractValidator<ActualizarPlantillaCommand>
{
    public ActualizarPlantillaCommandValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre de la plantilla es obligatorio.").MaximumLength(300);
    }
}
