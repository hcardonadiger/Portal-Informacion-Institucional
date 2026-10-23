namespace Diger.TramitesEstado.Application.Reuniones.Memoria;

/// <summary>
/// Escribe en la reunión la ayuda memoria que la IA devolvió como JSON (lo usa el MCP). Las reglas
/// de qué se lee del JSON son las de <see cref="MemoriaDesdeTranscripcion"/>; aquí solo se decide
/// si se puede escribir. Lanza <see cref="NotFoundException"/> o <see cref="DomainException"/>.
/// </summary>
/// <remarks>
/// Todo o nada: si algo impide escribir, no se toca ningún campo. Sin <c>Sobreescribir</c>, un
/// campo que ya tiene un valor distinto (o acuerdos ya capturados) bloquea, el mismo criterio que
/// el editor. Los acuerdos con seguimiento no se reemplazan nunca, ni sobreescribiendo: el
/// seguimiento es trabajo de una persona que una transcripción no puede rehacer.
/// </remarks>
public sealed record LlenarMemoriaReunionCommand(int ReunionId, string Json, bool Sobreescribir) : IRequest<Unit>;

public sealed class LlenarMemoriaReunionCommandHandler(
    IReunionRepository repo,
    IApplicationDbContext ctx,
    IUnitOfWork uow)
    : IRequestHandler<LlenarMemoriaReunionCommand, Unit>
{
    // El texto del editor, con «sobreescribir» en vez del nombre de la casilla: quien lo lee es el MCP.
    public const string HayCamposConInformacion =
        "Ya hay campos con información. Use «sobreescribir» para borrar los campos existentes.";

    public const string AcuerdosEnSeguimiento =
        "La reunión ya tiene acuerdos en seguimiento; no se reemplazan desde una transcripción. Agregue los nuevos desde el editor.";

    public async Task<Unit> Handle(LlenarMemoriaReunionCommand cmd, CancellationToken ct)
    {
        var r = await repo.GetByIdWithDetailsAsync(cmd.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), cmd.ReunionId);

        var p = MemoriaDesdeTranscripcion.Interpretar(
            cmd.Json, MemoriaDesdeTranscripcion.EsCapacitacion(r.Tipo), r.EncuestaActiva);

        var reemplazaAcuerdos = p.Acuerdos.Count > 0 && r.Acuerdos.Count > 0;

        // El seguimiento se revisa antes que el choque: pedir «sobreescribir» cuando tampoco
        // serviría solo manda a la persona a un segundo rechazo.
        if (reemplazaAcuerdos && await AlgunoConSeguimientoAsync(r.Acuerdos, ct))
            throw new DomainException(AcuerdosEnSeguimiento);

        if (!cmd.Sobreescribir && (reemplazaAcuerdos || HayChoque(r, p)))
            throw new DomainException(HayCamposConInformacion);

        Aplicar(r, p);
        r.MarcarActualizada();
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private async Task<bool> AlgunoConSeguimientoAsync(IReadOnlyCollection<AcuerdoReunion> acuerdos, CancellationToken ct)
    {
        if (acuerdos.Any(a => a.Estado != EstadoCompromiso.Pendiente
                              || a.FechaCumplimiento is not null
                              || !string.IsNullOrWhiteSpace(a.NotaSeguimiento)))
            return true;

        // Los comentarios no vienen con la reunión: se preguntan aparte.
        var ids = acuerdos.Select(a => a.Id).ToList();
        return await ctx.ComentariosCompromisos.AnyAsync(c => ids.Contains(c.AcuerdoReunionId), ct);
    }

    private static bool HayChoque(Reunion r, MemoriaPropuesta p) =>
        Choca(r.ObjetivoAgenda, p.Objetivo)
        || Choca(r.DocsRecursos, p.Documentos)
        || Choca(r.ValDiger, p.ValidadorDiger)
        || Choca(r.ValInst, p.ValidadorInstitucion)
        || Choca(r.Compromisos, p.Compromisos)
        || Choca(r.Convocados, p.Convocados)
        || Choca(r.NumAsistentes, p.NumAsistentes)
        || Choca(r.PctAsistencia, p.PctAsistencia)
        || Choca(r.Desarrollo, p.Desarrollo);

    /// <summary>Choca si el JSON trae valor, el campo ya tiene uno no vacío y son distintos sin espacios a los lados.</summary>
    private static bool Choca(string? actual, string? nuevo) =>
        nuevo is not null && !string.IsNullOrWhiteSpace(actual) && actual.Trim() != nuevo.Trim();

    private static bool Choca(int? actual, int? nuevo) =>
        nuevo is not null && actual is not null && actual != nuevo;

    private static void Aplicar(Reunion r, MemoriaPropuesta p)
    {
        // Un null en la propuesta es «no viene»: nunca borra lo que hay.
        if (p.Objetivo is not null)             r.ObjetivoAgenda = p.Objetivo.Trim();
        if (p.Desarrollo is not null)           r.Desarrollo     = p.Desarrollo.Trim();
        if (p.Documentos is not null)           r.DocsRecursos   = p.Documentos.Trim();
        if (p.ValidadorDiger is not null)       r.ValDiger       = p.ValidadorDiger.Trim();
        if (p.ValidadorInstitucion is not null) r.ValInst        = p.ValidadorInstitucion.Trim();
        if (p.Compromisos is not null)          r.Compromisos    = p.Compromisos.Trim();
        if (p.Convocados is not null)           r.Convocados     = p.Convocados;
        if (p.NumAsistentes is not null)        r.NumAsistentes  = p.NumAsistentes;
        if (p.PctAsistencia is not null)        r.PctAsistencia  = p.PctAsistencia;

        if (p.Acuerdos.Count > 0)
            r.ReemplazarAcuerdos(p.Acuerdos.Select(a => new AcuerdoReunion
            {
                Compromiso = a.Compromiso.Trim(),
                // Texto tal como lo nombra la transcripción; casarlo con el directorio es de una persona.
                Responsable = string.IsNullOrWhiteSpace(a.Responsable) ? null : a.Responsable.Trim(),
                ResponsableContactoId = null,
                Plazo = a.Plazo,
            }));
    }
}
