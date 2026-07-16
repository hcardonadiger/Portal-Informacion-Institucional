using FluentValidation;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Application.Expedientes.Common;

namespace Diger.TramitesEstado.Application.Expedientes.Commands.CrearExpediente;

public sealed record CrearExpedienteCommand(ExpedienteInputDto Datos, string? OrigenExternoId = null)
    : IRequest<int>;

public sealed class CrearExpedienteCommandHandler(
    IExpedienteRepository repo,
    ICurrentUserService currentUser,
    IUnitOfWork uow)
    : IRequestHandler<CrearExpedienteCommand, int>
{
    public async Task<int> Handle(CrearExpedienteCommand cmd, CancellationToken ct)
    {
        var d = cmd.Datos;
        if (!currentUser.PuedeAccederInstitucion(d.InstitucionId))
            throw new DomainException("Debe seleccionar una institución dentro de su alcance asignado.");
        var codigo = await GenerarCodigoAsync(d.Institucion, ct);

        var exp = Expediente.Crear(codigo, d.InstitucionId, null, null, d.Institucion, d.Analista);
        ExpedienteMapper.Aplicar(exp, d);
        exp.OrigenExternoId = cmd.OrigenExternoId;

        await repo.AddAsync(exp, ct);
        await uow.SaveChangesAsync(ct);
        return exp.Id;
    }

    private async Task<string> GenerarCodigoAsync(string institucion, CancellationToken ct)
    {
        var abbr = Regex.Replace(institucion ?? "", "[^A-Za-z]", "").ToUpperInvariant();
        abbr = abbr.Length >= 4 ? abbr[..4] : abbr.PadRight(Math.Max(abbr.Length, 1), 'X');
        var year = DateTime.UtcNow.Year;
        var prefijo = $"EXP-{abbr}-{year}-";

        var seq = await repo.CountByInstitucionPrefixAsync(prefijo, ct) + 1;
        string codigo;
        do
        {
            codigo = $"{prefijo}{seq:D2}";
            seq++;
        } while (await repo.CodigoExisteAsync(codigo, ct));

        return codigo;
    }
}

public sealed class CrearExpedienteCommandValidator : AbstractValidator<CrearExpedienteCommand>
{
    public CrearExpedienteCommandValidator()
    {
        RuleFor(x => x.Datos.InstitucionId).NotEmpty().WithMessage("Seleccione una institución.");
        RuleFor(x => x.Datos.Institucion).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Datos.Analista).NotEmpty().MaximumLength(150)
            .WithMessage("Indique el analista responsable.");
        RuleFor(x => x.Datos.Tramites)
            .Must(t => t != null && t.Any(x => !string.IsNullOrWhiteSpace(x.NombreTramite)))
            .WithMessage("Registre al menos un trámite.");
    }
}
