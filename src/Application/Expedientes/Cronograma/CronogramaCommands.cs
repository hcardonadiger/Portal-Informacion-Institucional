using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Expedientes.Seguimiento;
using FluentValidation;

namespace Diger.TramitesEstado.Application.Expedientes.Cronograma;

public sealed record GuardarEtapaCronogramaCommand(
    int       ExpedienteId,
    int       TramiteIndex,
    string    EtapaNum,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    DateOnly? FechaRealFin,
    string?   Responsable,
    string?   Observacion
) : IRequest<Unit>;

public sealed class GuardarEtapaCronogramaCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<GuardarEtapaCronogramaCommand, Unit>
{
    public async Task<Unit> Handle(GuardarEtapaCronogramaCommand cmd, CancellationToken ct)
    {
        if (!MetodologiaDigitalizacion.EtapaExiste(cmd.EtapaNum))
            throw new DomainException($"Etapa «{cmd.EtapaNum}» no existe en la metodología.");

        if (!await ctx.Expedientes.AnyAsync(e => e.Id == cmd.ExpedienteId, ct))
            throw new NotFoundException(nameof(Expediente), cmd.ExpedienteId);

        var fila = await ctx.EtapaCronogramas.FirstOrDefaultAsync(
            c => c.ExpedienteId == cmd.ExpedienteId
              && c.TramiteIndex  == cmd.TramiteIndex
              && c.EtapaNum     == cmd.EtapaNum, ct);

        if (fila is null)
            ctx.EtapaCronogramas.Add(new ExpedienteEtapaCronograma
            {
                ExpedienteId = cmd.ExpedienteId, TramiteIndex = cmd.TramiteIndex,
                EtapaNum    = cmd.EtapaNum,      FechaInicio  = cmd.FechaInicio,
                FechaFin    = cmd.FechaFin,      FechaRealFin = cmd.FechaRealFin,
                Responsable = cmd.Responsable,   Observacion  = cmd.Observacion,
            });
        else
        {
            fila.FechaInicio  = cmd.FechaInicio;
            fila.FechaFin     = cmd.FechaFin;
            fila.FechaRealFin = cmd.FechaRealFin;
            fila.Responsable  = cmd.Responsable;
            fila.Observacion  = cmd.Observacion;
        }

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class GuardarEtapaCronogramaCommandValidator : AbstractValidator<GuardarEtapaCronogramaCommand>
{
    public GuardarEtapaCronogramaCommandValidator()
    {
        RuleFor(x => x.ExpedienteId).GreaterThan(0);
        RuleFor(x => x.TramiteIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EtapaNum).NotEmpty().MaximumLength(5);
        RuleFor(x => x.Responsable).MaximumLength(150);
        RuleFor(x => x.Observacion).MaximumLength(1000);
    }
}

// ── Guardado centralizado: aplica la etapa a TODOS los trámites ───────────
public sealed record GuardarEtapaCronogramaTodosCommand(
    int       ExpedienteId,
    string    EtapaNum,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    DateOnly? FechaRealFin,
    string?   Responsable,
    string?   Observacion
) : IRequest<Unit>;

public sealed class GuardarEtapaCronogramaTodosCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<GuardarEtapaCronogramaTodosCommand, Unit>
{
    public async Task<Unit> Handle(GuardarEtapaCronogramaTodosCommand cmd, CancellationToken ct)
    {
        if (!MetodologiaDigitalizacion.EtapaExiste(cmd.EtapaNum))
            throw new DomainException($"Etapa «{cmd.EtapaNum}» no existe en la metodología.");

        if (!await ctx.Expedientes.AnyAsync(e => e.Id == cmd.ExpedienteId, ct))
            throw new NotFoundException(nameof(Expediente), cmd.ExpedienteId);

        var indices = await ctx.Tramites
            .Where(t => t.ExpedienteId == cmd.ExpedienteId)
            .Select(t => t.TramiteIndex)
            .ToListAsync(ct);

        if (indices.Count == 0)
            throw new DomainException("El expediente no tiene trámites registrados.");

        var existentes = await ctx.EtapaCronogramas
            .Where(c => c.ExpedienteId == cmd.ExpedienteId && c.EtapaNum == cmd.EtapaNum)
            .ToListAsync(ct);
        var porTramite = existentes.ToDictionary(f => f.TramiteIndex);

        foreach (var idx in indices)
        {
            if (porTramite.TryGetValue(idx, out var fila))
            {
                fila.FechaInicio  = cmd.FechaInicio;
                fila.FechaFin     = cmd.FechaFin;
                fila.FechaRealFin = cmd.FechaRealFin;
                fila.Responsable  = cmd.Responsable;
                fila.Observacion  = cmd.Observacion;
            }
            else
                ctx.EtapaCronogramas.Add(new ExpedienteEtapaCronograma
                {
                    ExpedienteId = cmd.ExpedienteId, TramiteIndex = idx,
                    EtapaNum    = cmd.EtapaNum,      FechaInicio  = cmd.FechaInicio,
                    FechaFin    = cmd.FechaFin,      FechaRealFin = cmd.FechaRealFin,
                    Responsable = cmd.Responsable,   Observacion  = cmd.Observacion,
                });
        }

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class GuardarEtapaCronogramaTodosCommandValidator : AbstractValidator<GuardarEtapaCronogramaTodosCommand>
{
    public GuardarEtapaCronogramaTodosCommandValidator()
    {
        RuleFor(x => x.ExpedienteId).GreaterThan(0);
        RuleFor(x => x.EtapaNum).NotEmpty().MaximumLength(5);
        RuleFor(x => x.Responsable).MaximumLength(150);
        RuleFor(x => x.Observacion).MaximumLength(1000);
    }
}
