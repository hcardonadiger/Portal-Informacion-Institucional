namespace Diger.TramitesEstado.Application.Reuniones.Queries.GetCompromisoDetalle;

public sealed record ComentarioCompromisoDto(
    int      Id,
    string?  Comentario,
    string?  ArchivoNombre,
    string?  ArchivoUrl,
    long?    ArchivoTamano,
    string   CreadoPor,
    string?  CreadoPorRol,
    DateTime CreadoEl);

public sealed record CompromisoDetalleDto(
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
    IReadOnlyList<ComentarioCompromisoDto> Comentarios);

public sealed record GetCompromisoDetalleQuery(int Id) : IRequest<CompromisoDetalleDto>;

public sealed class GetCompromisoDetalleQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetCompromisoDetalleQuery, CompromisoDetalleDto>
{
    public async Task<CompromisoDetalleDto> Handle(GetCompromisoDetalleQuery request, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var item = await (
            from a in ctx.Acuerdos.AsNoTracking().Include(x => x.Comentarios)
            join r in ctx.Reuniones on a.ReunionId equals r.Id
            where a.Id == request.Id
            select new { a, r }
        ).FirstOrDefaultAsync(ct);

        if (item is null)
            throw new NotFoundException(nameof(AcuerdoReunion), request.Id);

        var acuerdo = item.a;
        var reunion = item.r;

        var comentarios = acuerdo.Comentarios
            .OrderByDescending(c => c.CreadoEl)
            .Select(c => new ComentarioCompromisoDto(
                c.Id, c.Comentario, c.ArchivoNombre, c.ArchivoUrl, c.ArchivoTamano,
                c.CreadoPor, c.CreadoPorRol, c.CreadoEl))
            .ToList();

        var vencido = acuerdo.Plazo != null && acuerdo.Plazo < hoy &&
            (acuerdo.Estado == EstadoCompromiso.Pendiente || acuerdo.Estado == EstadoCompromiso.EnProgreso || acuerdo.Estado == EstadoCompromiso.Reprogramado);

        return new CompromisoDetalleDto(
            acuerdo.Id, reunion.Id, reunion.Titulo, reunion.Fecha, reunion.InstitucionId, reunion.Institucion,
            acuerdo.Compromiso, acuerdo.Responsable, acuerdo.Plazo, acuerdo.Estado,
            acuerdo.FechaCumplimiento, acuerdo.NotaSeguimiento, vencido,
            acuerdo.SeguimientoActualizadoEl, acuerdo.SeguimientoActualizadoPor,
            comentarios);
    }
}
