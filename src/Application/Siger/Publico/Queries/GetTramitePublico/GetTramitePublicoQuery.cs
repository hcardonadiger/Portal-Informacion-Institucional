namespace Diger.TramitesEstado.Application.Siger.Publico.Queries.GetTramitePublico;

/// <summary>Ficha completa de un trámite público — GET /api/v1/tramites/{codigo}.
/// Null si no existe o no está publicado: una API pública no distingue las dos cosas.</summary>
public sealed record GetTramitePublicoQuery(string Codigo) : IRequest<TramiteDetallePublicoDto?>;

public sealed class GetTramitePublicoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetTramitePublicoQuery, TramiteDetallePublicoDto?>
{
    public async Task<TramiteDetallePublicoDto?> Handle(GetTramitePublicoQuery q, CancellationToken ct)
    {
        // ── Por qué seis consultas y no cinco Include ──────────────────────────
        //
        // La versión anterior traía las cinco colecciones hijas con Include en una sola
        // consulta. Eso hace que SQL Server devuelva el PRODUCTO CARTESIANO de todas ellas,
        // y cada fila arrastra el trámite entero (descripción, objetivo, dirigido a...).
        //
        // Medido el 17-08-2026 sobre datos reales:
        //     603-019 → 18 pasos x 3 requisitos x 1 entregable x 18 lugares x 2 enlaces
        //             = 1.944 filas para devolver 9 KB de JSON
        //             = 25 segundos de respuesta, reproducible
        //     603-023 → 2.520 filas, el peor del piloto
        //
        // Importa más de lo que parece: la sincronización de HondurasÁgil pide el detalle
        // ficha por ficha. A 25 segundos, sincronizar las 49 del piloto tardaría veinte
        // minutos y el cliente agotaría su tiempo de espera en cada una.
        //
        // Se resuelve con consultas separadas —una por colección— en vez de con
        // AsSplitQuery, por dos razones:
        //   1. AsSplitQuery vive en el ensamblado Relational, y esta capa no debe conocer
        //      el proveedor de base de datos.
        //   2. El proveedor en memoria de las pruebas no lo soporta.
        // Así, seis consultas devuelven 42 filas en total en lugar de 1.944.

        var t = await ctx.TramitesSiger.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Codigo == q.Codigo && x.Publicado, ct);

        if (t is null) return null;

        var pasos = await ctx.PasosSiger.AsNoTracking()
            .Where(p => p.TramiteSigerId == t.Id)
            .OrderBy(p => p.NumeroPaso)
            .Select(p => new PasoPublicoDto(
                p.NumeroPaso, p.Titulo, p.Descripcion, p.Modalidad,
                p.LugarDependencia, p.SalidaResultado, p.TiempoRegistrado))
            .ToListAsync(ct);

        var requisitos = await ctx.RequisitosSiger.AsNoTracking()
            .Where(r => r.TramiteSigerId == t.Id)
            .OrderBy(r => r.Numero)
            .Select(r => new RequisitoPublicoDto(
                r.Numero, r.Requisito, r.Tipo, r.DocumentoSoporte, r.Formato))
            .ToListAsync(ct);

        var entregables = await ctx.EntregablesSiger.AsNoTracking()
            .Where(e => e.TramiteSigerId == t.Id)
            .OrderBy(e => e.Numero)
            .Select(e => new EntregablePublicoDto(
                e.Numero, e.Entregable, e.Formato, e.Presentacion))
            .ToListAsync(ct);

        var lugares = await ctx.LugaresAtencionSiger.AsNoTracking()
            .Where(l => l.TramiteSigerId == t.Id)
            .OrderBy(l => l.Numero)
            .Select(l => new LugarAtencionPublicoDto(
                l.Numero, l.Lugar, l.Ciudad, l.Direccion, l.Telefonos))
            .ToListAsync(ct);

        var enlaces = await ctx.EnlacesSiger.AsNoTracking()
            .Where(e => e.TramiteSigerId == t.Id)
            .OrderBy(e => e.Numero)
            .Select(e => new EnlacePublicoDto(e.Numero, e.Url, e.Tipo))
            .ToListAsync(ct);

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
            pasos, requisitos, entregables, lugares, enlaces);
    }
}
