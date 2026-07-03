using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Expedientes.Seguimiento;

// ── Actualizar el estado (0/1/2) de un sub-paso de un trámite ─────────────
public sealed record ActualizarSubEtapaCommand(int ExpedienteId, int TramiteIndex, string SubId, int Estado) : IRequest<Unit>;

public sealed class ActualizarSubEtapaCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<ActualizarSubEtapaCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarSubEtapaCommand cmd, CancellationToken ct)
    {
        if (!MetodologiaDigitalizacion.SubExiste(cmd.SubId))
            throw new DomainException($"Sub-paso «{cmd.SubId}» no existe en la metodología.");

        // Alcance: el expediente debe ser visible.
        if (!await ctx.Expedientes.AnyAsync(e => e.Id == cmd.ExpedienteId, ct))
            throw new NotFoundException(nameof(Expediente), cmd.ExpedienteId);

        var fila = await ctx.ExpedienteEtapaAvances.FirstOrDefaultAsync(
            a => a.ExpedienteId == cmd.ExpedienteId && a.TramiteIndex == cmd.TramiteIndex && a.SubId == cmd.SubId, ct);

        if (cmd.Estado <= 0)
        {
            if (fila is not null) ctx.ExpedienteEtapaAvances.Remove(fila); // disperso: no se guarda "Pendiente"
        }
        else if (fila is null)
            ctx.ExpedienteEtapaAvances.Add(new ExpedienteEtapaAvance
            {
                ExpedienteId = cmd.ExpedienteId, TramiteIndex = cmd.TramiteIndex, SubId = cmd.SubId, Estado = cmd.Estado
            });
        else
            fila.Estado = cmd.Estado;

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class ActualizarSubEtapaCommandValidator : AbstractValidator<ActualizarSubEtapaCommand>
{
    public ActualizarSubEtapaCommandValidator()
    {
        RuleFor(x => x.ExpedienteId).GreaterThan(0);
        RuleFor(x => x.TramiteIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SubId).NotEmpty();
        RuleFor(x => x.Estado).InclusiveBetween(0, 2);
    }
}

// ── Marcar si una etapa especial (toggle) aplica o no, por trámite ────────
public sealed record CambiarAplicaEtapaCommand(int ExpedienteId, int TramiteIndex, string EtapaNum, bool Aplica) : IRequest<Unit>;

public sealed class CambiarAplicaEtapaCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<CambiarAplicaEtapaCommand, Unit>
{
    public async Task<Unit> Handle(CambiarAplicaEtapaCommand cmd, CancellationToken ct)
    {
        if (!MetodologiaDigitalizacion.EtapaEsToggle(cmd.EtapaNum))
            throw new DomainException($"La etapa «{cmd.EtapaNum}» no admite marcarse como no aplicable.");

        if (!await ctx.Expedientes.AnyAsync(e => e.Id == cmd.ExpedienteId, ct))
            throw new NotFoundException(nameof(Expediente), cmd.ExpedienteId);

        var clave = "APLICA:" + cmd.EtapaNum;
        var fila = await ctx.ExpedienteEtapaAvances.FirstOrDefaultAsync(
            a => a.ExpedienteId == cmd.ExpedienteId && a.TramiteIndex == cmd.TramiteIndex && a.SubId == clave, ct);

        if (cmd.Aplica) // aplica es el valor por defecto → no se guarda fila
        {
            if (fila is not null) ctx.ExpedienteEtapaAvances.Remove(fila);
        }
        else if (fila is null)
            ctx.ExpedienteEtapaAvances.Add(new ExpedienteEtapaAvance
            {
                ExpedienteId = cmd.ExpedienteId, TramiteIndex = cmd.TramiteIndex, SubId = clave, Estado = 0
            });
        else
            fila.Estado = 0;

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
