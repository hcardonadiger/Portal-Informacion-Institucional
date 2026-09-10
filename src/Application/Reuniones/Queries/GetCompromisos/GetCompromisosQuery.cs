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
    int              NumArchivos = 0,
    bool             ProximoAVencer = false);

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
    public async Task<CompromisosResult> Handle(GetCompromisosQuery request, CancellationToken ct)
    {
        var (q, page, size) = Paginacion.Normalizar(request.Q, request.Page, request.Size);
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var tresDias = hoy.AddDays(3);

        var baseQuery = ctx.Acuerdos
            .Join(ctx.Reuniones,
                  a => a.ReunionId, r => r.Id,
                  (a, r) => new { a, r });

        var resumen = new CompromisosResumen(
            Total:         await baseQuery.CountAsync(ct),
            Pendientes:    await baseQuery.CountAsync(x => x.a.Estado == EstadoCompromiso.Pendiente, ct),
            EnProgreso:    await baseQuery.CountAsync(x => x.a.Estado == EstadoCompromiso.EnProgreso, ct),
            Cumplidos:     await baseQuery.CountAsync(x => x.a.Estado == EstadoCompromiso.Cumplido, ct),
            Reprogramados: await baseQuery.CountAsync(x => x.a.Estado == EstadoCompromiso.Reprogramado, ct),
            Cancelados:    await baseQuery.CountAsync(x => x.a.Estado == EstadoCompromiso.Cancelado, ct),
            Vencidos:      await baseQuery.CountAsync(x => x.a.Plazo != null && x.a.Plazo < hoy &&
                (x.a.Estado == EstadoCompromiso.Pendiente || x.a.Estado == EstadoCompromiso.EnProgreso || x.a.Estado == EstadoCompromiso.Reprogramado), ct),
            EnRevision:    await baseQuery.CountAsync(x => x.a.Estado == EstadoCompromiso.EnRevision, ct));

        var responsables = await baseQuery
            .Where(x => x.a.Responsable != null && x.a.Responsable != "")
            .Select(x => x.a.Responsable!)
            .Distinct().OrderBy(x => x).ToListAsync(ct);

        var filtrada = baseQuery;

        if (request.Estado.HasValue)
            filtrada = filtrada.Where(x => x.a.Estado == request.Estado.Value);

        if (!string.IsNullOrWhiteSpace(request.InstitucionId))
            filtrada = filtrada.Where(x => x.r.InstitucionId == request.InstitucionId);

        if (!string.IsNullOrWhiteSpace(request.Responsable))
            filtrada = filtrada.Where(x => x.a.Responsable == request.Responsable);

        if (request.SoloVencidos)
            filtrada = filtrada.Where(x => x.a.Plazo != null && x.a.Plazo < hoy &&
                (x.a.Estado == EstadoCompromiso.Pendiente || x.a.Estado == EstadoCompromiso.EnProgreso || x.a.Estado == EstadoCompromiso.Reprogramado));

        if (q is not null)
            filtrada = filtrada.Where(x =>
                x.a.Compromiso.Contains(q) ||
                (x.a.Responsable != null && x.a.Responsable.Contains(q)) ||
                x.r.Titulo.Contains(q));

        var total = await filtrada.CountAsync(ct);

        var items = await filtrada
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
                x.a.Comentarios.Count(c => c.ArchivoUrl != null && c.ArchivoUrl != ""),
                x.a.Plazo != null && x.a.Plazo >= hoy && x.a.Plazo <= tresDias &&
                    (x.a.Estado == EstadoCompromiso.Pendiente || x.a.Estado == EstadoCompromiso.EnProgreso || x.a.Estado == EstadoCompromiso.Reprogramado)))
            .ToListAsync(ct);

        var pagina = new PagedResult<CompromisoListItemDto>(items, total, page, size);
        return new CompromisosResult(pagina, resumen, responsables);
    }
}
