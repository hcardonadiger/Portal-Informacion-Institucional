using FluentValidation;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Expedientes.Common;

namespace Diger.TramitesEstado.Application.Expedientes.Commands.ActualizarExpediente;

public sealed record ActualizarExpedienteCommand(int Id, ExpedienteInputDto Datos) : IRequest<Unit>;

public sealed class ActualizarExpedienteCommandHandler(
    IExpedienteRepository  repo,
    IApplicationDbContext  ctx,
    IUnitOfWork            uow)
    : IRequestHandler<ActualizarExpedienteCommand, Unit>
{
    public async Task<Unit> Handle(ActualizarExpedienteCommand cmd, CancellationToken ct)
    {
        var exp = await repo.GetByIdWithDetailsAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Expediente), cmd.Id);

        var datos = cmd.Datos;
        // Si viene un usuario del sistema, el snapshot del nombre se toma de su registro
        if (datos.AnalistaId.HasValue)
        {
            var nombre = await ctx.Usuarios
                .Where(u => u.Id == datos.AnalistaId.Value)
                .Select(u => u.Nombre)
                .FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException(nameof(Usuario), datos.AnalistaId.Value);
            datos = datos with { Analista = nombre };
        }

        var estadoAnterior = exp.EstadoExpediente;
        ExpedienteMapper.Aplicar(exp, datos);
        exp.MarcarActualizado();

        // Cuando el expediente cierra por primera vez, cumplir metas vinculadas
        if (cmd.Datos.EstadoExpediente == EstadoExpediente.Cerrado
            && estadoAnterior != EstadoExpediente.Cerrado)
        {
            var metas = await ctx.MetasTrabajo
                .Where(m => m.ExpedienteId == cmd.Id
                         && m.Estado != EstadoMeta.Cumplida
                         && m.Estado != EstadoMeta.Cancelada)
                .ToListAsync(ct);

            if (metas.Count > 0)
            {
                // Fecha real por trámite del cronograma; la global sirve de respaldo
                var fechasPorTramite = (await ctx.EtapaCronogramas
                        .Where(c => c.ExpedienteId == cmd.Id && c.FechaRealFin.HasValue)
                        .GroupBy(c => c.TramiteIndex)
                        .Select(g => new { g.Key, Fecha = g.Max(c => c.FechaRealFin!.Value) })
                        .ToListAsync(ct))
                    .ToDictionary(x => x.Key, x => x.Fecha);

                var fechaGlobal = fechasPorTramite.Count > 0
                    ? fechasPorTramite.Values.Max()
                    : DateOnly.FromDateTime(DateTime.UtcNow);

                foreach (var meta in metas)
                {
                    meta.Estado       = EstadoMeta.Cumplida;
                    meta.FechaRealFin = meta.ExpedienteTramiteIndex.HasValue
                        && fechasPorTramite.TryGetValue(meta.ExpedienteTramiteIndex.Value, out var f)
                            ? f
                            : fechaGlobal;
                }
            }
        }

        repo.Update(exp);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public sealed class ActualizarExpedienteCommandValidator : AbstractValidator<ActualizarExpedienteCommand>
{
    public ActualizarExpedienteCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Datos.Analista).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Datos.Tramites)
            .Must(t => t != null && t.Any(x => !string.IsNullOrWhiteSpace(x.NombreTramite)))
            .WithMessage("Registre al menos un trámite.");
    }
}
