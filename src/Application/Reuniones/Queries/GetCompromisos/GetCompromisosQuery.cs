namespace Diger.TramitesEstado.Application.Reuniones.Queries.GetCompromisos;

public sealed record CompromisoListItemDto(
    int              Id,
    int              ReunionId,
    string           ReunionTitulo,
    DateOnly?        ReunionFecha,
    string?          InstitucionId,
    string?          Institucion,
    string           Compromiso,
    string?          Responsable,
    DateOnly?        Plazo,
    EstadoCompromiso Estado,
    DateOnly?        FechaCumplimiento,
    string?          NotaSeguimiento,
    bool             Vencido,
    DateTime?        ActualizadoEl,
    string?          ActualizadoPor,
    int              NumComentarios = 0,
    int              NumArchivos = 0);

public sealed record CompromisosResumen(
    int Total, int Pendientes, int EnProgreso, int Cumplidos,
    int Reprogramados, int Cancelados, int Vencidos, int EnRevision = 0);

public sealed record CompromisosResult(
    PagedResult<CompromisoListItemDto> Pagina,
    CompromisosResumen                 Resumen,
    IReadOnlyList<string>              Responsables);

public sealed record GetCompromisosQuery(
    string?          Q             = null,
    EstadoCompromiso? Estado       = null,
    string?          InstitucionId = null,
    string?          Responsable   = null,
    bool             SoloVencidos  = false,
    int?             Page          = null,
    int?             Size          = null) : IRequest<CompromisosResult>;

public sealed class GetCompromisosQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetCompromisosQuery, CompromisosResult>
{
    public async Task<CompromisosResult> Handle(GetCompromisosQuery query, CancellationToken ct)
    {
        var (q, page, size) = Paginacion.Normalizar(query.Q, query.Page, query.Size);
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        // El join contra Reuniones (filtradas por alcance) restringe los acuerdos a reuniones visibles.
        var alcance =
            from a in ctx.Acuerdos.AsNoTracking()
            join r in ctx.Reuniones on a.ReunionId equals r.Id
            select new { a, r };

        // ── Resumen (sobre el alcance completo, independiente de los filtros activos) ──
        var soloAcuerdos = alcance.Select(x => x.a);
        var resumen = new CompromisosResumen(
            Total:         await soloAcuerdos.CountAsync(ct),
            Pendientes:    await soloAcuerdos.CountAsync(a => a.Estado == EstadoCompromiso.Pendiente, ct),
            EnProgreso:    await soloAcuerdos.CountAsync(a => a.Estado == EstadoCompromiso.EnProgreso, ct),
            Cumplidos:     await soloAcuerdos.CountAsync(a => a.Estado == EstadoCompromiso.Cumplido, ct),
            Reprogramados: await soloAcuerdos.CountAsync(a => a.Estado == EstadoCompromiso.Reprogramado, ct),
            Cancelados:    await soloAcuerdos.CountAsync(a => a.Estado == EstadoCompromiso.Cancelado, ct),
            Vencidos:      await soloAcuerdos.CountAsync(a =>
                a.Plazo != null && a.Plazo < hoy &&
                (a.Estado == EstadoCompromiso.Pendiente || a.Estado == EstadoCompromiso.EnProgreso || a.Estado == EstadoCompromiso.Reprogramado), ct),
            EnRevision:    await soloAcuerdos.CountAsync(a => a.Estado == EstadoCompromiso.EnRevision, ct));

        var responsables = await soloAcuerdos
            .Where(a => a.Responsable != null && a.Responsable != "")
            .Select(a => a.Responsable!).Distinct().OrderBy(x => x).Take(200).ToListAsync(ct);

        // ── Filtros ──
        var filtrada = alcance;
        if (query.Estado is { } est)
            filtrada = filtrada.Where(x => x.a.Estado == est);
        if (query.InstitucionId is { } inst)
            filtrada = filtrada.Where(x => x.r.InstitucionId == inst);
        if (!string.IsNullOrWhiteSpace(query.Responsable))
        {
            var resp = query.Responsable.Trim();
            filtrada = filtrada.Where(x => x.a.Responsable != null && x.a.Responsable.Contains(resp));
        }
        if (query.SoloVencidos)
            filtrada = filtrada.Where(x =>
                x.a.Plazo != null && x.a.Plazo < hoy &&
                (x.a.Estado == EstadoCompromiso.Pendiente || x.a.Estado == EstadoCompromiso.EnProgreso || x.a.Estado == EstadoCompromiso.Reprogramado));
        if (q is not null)
            filtrada = filtrada.Where(x =>
                x.a.Compromiso.Contains(q) ||
                (x.a.Responsable != null && x.a.Responsable.Contains(q)) ||
                x.r.Titulo.Contains(q));

        var total = await filtrada.CountAsync(ct);

        var items = await filtrada
            // Abiertos primero; dentro de ellos, vencidos arriba y por plazo más próximo.
            .OrderBy(x => x.a.Estado == EstadoCompromiso.Cumplido || x.a.Estado == EstadoCompromiso.Cancelado)
            .ThenByDescending(x => x.a.Plazo != null && x.a.Plazo < hoy)
            .ThenBy(x => x.a.Plazo == null)
            .ThenBy(x => x.a.Plazo)
            .Skip((page - 1) * size).Take(size)
            .Select(x => new CompromisoListItemDto(
                x.a.Id, x.r.Id, x.r.Titulo, x.r.Fecha, x.r.InstitucionId, x.r.Institucion,
                x.a.Compromiso, x.a.Responsable, x.a.Plazo, x.a.Estado,
                x.a.FechaCumplimiento, x.a.NotaSeguimiento,
                x.a.Plazo != null && x.a.Plazo < hoy &&
                    (x.a.Estado == EstadoCompromiso.Pendiente || x.a.Estado == EstadoCompromiso.EnProgreso || x.a.Estado == EstadoCompromiso.Reprogramado),
                x.a.SeguimientoActualizadoEl, x.a.SeguimientoActualizadoPor,
                x.a.Comentarios.Count(),
                x.a.Comentarios.Count(c => c.ArchivoUrl != null && c.ArchivoUrl != "")))
            .ToListAsync(ct);

        var pagina = new PagedResult<CompromisoListItemDto>(items, total, page, size);
        return new CompromisosResult(pagina, resumen, responsables);
    }
}
