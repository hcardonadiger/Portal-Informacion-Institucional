using FluentValidation;

namespace Diger.TramitesEstado.Application.Reuniones.Commands.EnlazarReuniones;

/// <summary>
/// Enlaza dos reuniones en un mismo hilo. Si ninguna tiene hilo, genera uno nuevo;
/// si solo una lo tiene, la otra se une; si ambas pertenecen a hilos distintos, se fusionan
/// (todas las de un hilo pasan al otro). Devuelve el HiloId resultante.
/// </summary>
public sealed record EnlazarReunionesCommand(int ReunionId, int OtraReunionId) : IRequest<Guid>;

public sealed class EnlazarReunionesCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<EnlazarReunionesCommand, Guid>
{
    public async Task<Guid> Handle(EnlazarReunionesCommand cmd, CancellationToken ct)
    {
        if (cmd.ReunionId == cmd.OtraReunionId)
            throw new DomainException("No se puede enlazar una reunión consigo misma.");

        var a = await ctx.Reuniones.FirstOrDefaultAsync(r => r.Id == cmd.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), cmd.ReunionId);
        var b = await ctx.Reuniones.FirstOrDefaultAsync(r => r.Id == cmd.OtraReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), cmd.OtraReunionId);

        Guid hiloId;
        if (a.HiloId is null && b.HiloId is null)
        {
            hiloId = Guid.NewGuid();
            a.EnlazarAHilo(hiloId);
            b.EnlazarAHilo(hiloId);
        }
        else if (a.HiloId is not null && b.HiloId is null)
        {
            hiloId = a.HiloId.Value;
            b.EnlazarAHilo(hiloId);
        }
        else if (a.HiloId is null && b.HiloId is not null)
        {
            hiloId = b.HiloId.Value;
            a.EnlazarAHilo(hiloId);
        }
        else
        {
            // Ambas ya tienen hilo: fusionar el de b dentro del de a.
            hiloId = a.HiloId!.Value;
            var otroHilo = b.HiloId!.Value;
            if (hiloId != otroHilo)
            {
                var miembros = await ctx.Reuniones.Where(r => r.HiloId == otroHilo).ToListAsync(ct);
                foreach (var m in miembros) m.EnlazarAHilo(hiloId);
            }
        }

        await ctx.SaveChangesAsync(ct);
        return hiloId;
    }
}

public sealed class EnlazarReunionesCommandValidator : AbstractValidator<EnlazarReunionesCommand>
{
    public EnlazarReunionesCommandValidator()
    {
        RuleFor(x => x.ReunionId).GreaterThan(0);
        RuleFor(x => x.OtraReunionId).GreaterThan(0);
    }
}
