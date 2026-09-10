namespace Diger.TramitesEstado.Application.Siger.Publico.Queries.GetTramitePublico;

/// <summary>Ficha completa de un trámite público — GET /api/v1/tramites/{codigo}.
/// Null si no existe o no está publicado: una API pública no distingue las dos cosas.</summary>
public sealed record GetTramitePublicoQuery(string Codigo) : IRequest<TramiteDetallePublicoDto?>;

public sealed class GetTramitePublicoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetTramitePublicoQuery, TramiteDetallePublicoDto?>
{
    public async Task<TramiteDetallePublicoDto?> Handle(GetTramitePublicoQuery q, CancellationToken ct)
    {
        var t = await ctx.TramitesSiger.AsNoTracking()
            .Include(x => x.Pasos)
            .Include(x => x.Requisitos)
            .Include(x => x.Entregables)
            .Include(x => x.LugaresAtencion)
            .Include(x => x.Enlaces)
            .FirstOrDefaultAsync(x => x.Codigo == q.Codigo && x.Publicado, ct);

        if (t is null) return null;

        string? categoriaNombre = t.CategoriaId is { } cid
            ? await ctx.CategoriasTramite.AsNoTracking().Where(c => c.Id == cid).Select(c => c.Nombre).FirstOrDefaultAsync(ct)
            : null;

        // M-05: la fecha que se publica al ciudadano es la que sella el sistema, nunca el
        // campo editable del formulario (UltimaModificacion).
        var ultimaRevision = t.UpdatedAt ?? t.UltimaModificacion ?? t.CreatedAt;

        return new TramiteDetallePublicoDto(
            t.Codigo, t.Nombre, t.InstitucionId ?? "", t.Institucion,
            t.CategoriaId, categoriaNombre, t.Modalidad, t.EsPopular,
            t.CostoEsGratuito, t.CostoTexto, t.TiempoTexto, t.EstaEnSol,
            FichaPublicaCompletitud.Evaluar(t.CategoriaId, t.Modalidad, t.TiempoTexto, t.CostoEsGratuito, t.EstaEnSol, t.SolUrl),
            t.Descripcion, t.Objetivo, t.DirigidoA, t.VigenciaDocumento, t.Temporalidad,
            t.SolUrl, t.SolVerificadoEl, ultimaRevision, t.EnlacePrincipal,
            t.Pasos.OrderBy(p => p.NumeroPaso)
                .Select(p => new PasoPublicoDto(p.NumeroPaso, p.Titulo, p.Descripcion, p.Modalidad, p.LugarDependencia, p.SalidaResultado, p.TiempoRegistrado))
                .ToList(),
            t.Requisitos.OrderBy(r => r.Numero)
                .Select(r => new RequisitoPublicoDto(r.Numero, r.Requisito, r.Tipo, r.DocumentoSoporte, r.Formato))
                .ToList(),
            t.Entregables.OrderBy(e => e.Numero)
                .Select(e => new EntregablePublicoDto(e.Numero, e.Entregable, e.Formato, e.Presentacion))
                .ToList(),
            t.LugaresAtencion.OrderBy(l => l.Numero)
                .Select(l => new LugarAtencionPublicoDto(l.Numero, l.Lugar, l.Ciudad, l.Direccion, l.Telefonos))
                .ToList(),
            t.Enlaces.OrderBy(e => e.Numero)
                .Select(e => new EnlacePublicoDto(e.Numero, e.Url, e.Tipo))
                .ToList());
    }
}
