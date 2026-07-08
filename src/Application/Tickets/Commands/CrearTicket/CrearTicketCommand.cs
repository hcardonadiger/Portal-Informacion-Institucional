using FluentValidation;
using Diger.TramitesEstado.Application.Tickets.Common;

namespace Diger.TramitesEstado.Application.Tickets.Commands.CrearTicket;

public sealed record CrearTicketCommand(TicketFormDto Datos, IReadOnlyList<AdjuntoInput>? Adjuntos = null) : IRequest<int>;

public sealed class CrearTicketCommandHandler(
    ITicketRepository repo,
    IInstitucionRepository institucionRepo,
    IExpedienteRepository expedienteRepo,
    ICurrentUserService currentUser,
    IApplicationDbContext ctx,
    IUnitOfWork uow)
    : IRequestHandler<CrearTicketCommand, int>
{
    public async Task<int> Handle(CrearTicketCommand cmd, CancellationToken ct)
    {
        var d = cmd.Datos;
        // Coherencia institución-expediente: si se vincula un expediente, la institución se deriva de él.
        var exp = d.ExpedienteId is int expId ? await expedienteRepo.GetByIdAsync(expId, ct) : null;
        var institucionId = exp?.InstitucionId ?? d.InstitucionId;

        if (!currentUser.PuedeAccederInstitucion(institucionId))
            throw new DomainException("Debe seleccionar una institución dentro de su alcance asignado.");

        var numero = await GenerarNumeroAsync(ct);
        var t = Ticket.Crear(numero, d.Titulo);
        await NormalizarTemaOtroAsync(d, ct);
        TicketMapper.Aplicar(t, d);
        t.EstablecerCreador(currentUser.UserId, currentUser.Nombre ?? currentUser.Correo);
        // "Reportado por" se obtiene del usuario que registra el ticket.
        t.EstablecerReportante(currentUser.Nombre ?? currentUser.Correo, currentUser.Correo);

        t.InstitucionId    = institucionId;
        t.Institucion      = exp?.Institucion
            ?? (institucionId is int iid ? (await institucionRepo.GetByIdAsync(iid, ct))?.Nombre : null);
        t.ExpedienteId     = exp?.Id;
        t.ExpedienteCodigo = exp?.Codigo;

        // Trámites afectados: solo los del catálogo de la institución del ticket (integridad).
        if (institucionId is int itid && d.TramiteIds.Count > 0)
        {
            var catalogo = await institucionRepo.GetTramitesAsync(itid, ct);
            t.EstablecerTramites(catalogo
                .Where(td => d.TramiteIds.Contains(td.Id))
                .Select(td => ((int?)td.Id, td.Nombre)));
        }

        // Archivos adjuntos subidos al crear (ComentarioId = null).
        if (cmd.Adjuntos is { Count: > 0 })
            foreach (var a in cmd.Adjuntos)
                t.AgregarAdjunto(a.Nombre, a.Url, a.Tamano);

        await repo.AddAsync(t, ct);
        await uow.SaveChangesAsync(ct);
        return t.Id;
    }

    private async Task<string> GenerarNumeroAsync(CancellationToken ct)
    {
        var prefijo = $"TCK-{DateTime.UtcNow.Year}-";
        var seq = await repo.CountByNumeroPrefixAsync(prefijo, ct) + 1;
        string numero;
        do { numero = $"{prefijo}{seq:D4}"; seq++; }
        while (await repo.NumeroExisteAsync(numero, ct));
        return numero;
    }

    private async Task NormalizarTemaOtroAsync(TicketFormDto d, CancellationToken ct)
    {
        if (d.TemaId is not int temaId)
        {
            d.TemaOtro = null;
            return;
        }

        var nombreTema = await ctx.TemasTicket
            .AsNoTracking()
            .Where(t => t.Id == temaId)
            .Select(t => t.Nombre)
            .FirstOrDefaultAsync(ct);

        if (!string.Equals(nombreTema?.Trim(), "Otro", StringComparison.OrdinalIgnoreCase))
        {
            d.TemaOtro = null;
            return;
        }

        d.TemaOtro = string.IsNullOrWhiteSpace(d.TemaOtro) ? null : d.TemaOtro.Trim();
        if (d.TemaOtro is null)
            throw new DomainException("Debe indicar cual es el otro tema.");
    }
}

public sealed class CrearTicketCommandValidator : AbstractValidator<CrearTicketCommand>
{
    public CrearTicketCommandValidator()
    {
        RuleFor(x => x.Datos.Titulo).NotEmpty().WithMessage("El título es obligatorio.").MaximumLength(200);
        RuleFor(x => x.Datos.Descripcion).MaximumLength(4000);
        RuleFor(x => x.Datos.TemaOtro).MaximumLength(200);
    }
}
