using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Api.Consultas;

/// <summary>La ficha completa de un trámite — GET /api/v1/tramites/{codigo}.
/// Null si no existe o no está publicado: una API pública no distingue las dos cosas.</summary>
public sealed class ConsultaFicha(ApiDbContext db, IOptions<SolOptions> sol)
{
    public async Task<TramiteDetallePublicoDto?> EjecutarAsync(string codigo, CancellationToken ct)
    {
        // ── Por qué seis consultas y no cinco Include ──────────────────────────
        //
        // Traer las cinco colecciones hijas con Include en una sola consulta hace que SQL Server
        // devuelva el PRODUCTO CARTESIANO de todas ellas, y cada fila arrastra el trámite entero
        // (descripción, objetivo, dirigido a...).
        //
        // Medido el 17-08-2026 sobre datos reales:
        //     603-019 → 18 pasos x 3 requisitos x 1 entregable x 18 lugares x 2 enlaces
        //             = 1.944 filas para devolver 9 KB de JSON
        //             = 25 segundos de respuesta, reproducible
        //     603-023 → 2.520 filas, el peor del piloto
        //
        // Importa más de lo que parece: la sincronización de HondurasÁgil pide el detalle ficha
        // por ficha. A 25 segundos, sincronizar las 49 del piloto tardaría veinte minutos y el
        // cliente agotaría su tiempo de espera en cada una.
        //
        // Así, seis consultas devuelven 42 filas en total en lugar de 1.944.
        var t = await db.Fichas.FirstOrDefaultAsync(x => x.Codigo == codigo && x.Publicado, ct);

        if (t is null) return null;

        var pasos = await db.Pasos
            .Where(p => p.TramiteSigerId == t.Id)
            .OrderBy(p => p.NumeroPaso)
            .Select(p => new PasoPublicoDto(
                p.NumeroPaso, p.Titulo, p.Descripcion, p.Modalidad,
                p.LugarDependencia, p.SalidaResultado, p.TiempoRegistrado))
            .ToListAsync(ct);

        var requisitos = await db.Requisitos
            .Where(r => r.TramiteSigerId == t.Id)
            .OrderBy(r => r.Numero)
            .Select(r => new RequisitoPublicoDto(
                r.Numero, r.Requisito, r.Tipo, r.DocumentoSoporte, r.Formato))
            .ToListAsync(ct);

        var entregables = await db.Entregables
            .Where(e => e.TramiteSigerId == t.Id)
            .OrderBy(e => e.Numero)
            .Select(e => new EntregablePublicoDto(
                e.Numero, e.Entregable, e.Formato, e.Presentacion))
            .ToListAsync(ct);

        var lugares = await db.LugaresAtencion
            .Where(l => l.TramiteSigerId == t.Id)
            .OrderBy(l => l.Numero)
            .Select(l => new LugarAtencionPublicoDto(
                l.Numero, l.Lugar, l.Ciudad, l.Direccion, l.Telefonos))
            .ToListAsync(ct);

        var enlaces = await db.Enlaces
            .Where(e => e.TramiteSigerId == t.Id)
            .OrderBy(e => e.Numero)
            .Select(e => new EnlacePublicoDto(e.Numero, e.Url, e.Tipo))
            .ToListAsync(ct);

        string? categoriaNombre = t.CategoriaId is { } cid
            ? await db.Categorias.Where(c => c.Id == cid).Select(c => c.Nombre).FirstOrDefaultAsync(ct)
            : null;

        // La fecha que se publica al ciudadano es la que sella el sistema, nunca el campo
        // editable del formulario (UltimaModificacion).
        var ultimaRevision = t.UpdatedAt ?? t.UltimaModificacion ?? t.CreatedAt;

        // ── La dirección en SOL ───────────────────────────────────────────────
        //
        // Se consulta la institución SOLO cuando hay tramo que componer. Esta es la ruta que
        // HondurasÁgil llama una vez por trámite al sincronizar, donde una séptima consulta por
        // ficha se nota.
        //
        // RutaSol ?? Id: nula significa «nadie la corrigió», y entonces vale la sigla.
        string? rutaInstitucion = null;
        if (t.SolTramo is not null && t.InstitucionId is not null)
            rutaInstitucion = await db.Instituciones
                .Where(i => i.Id == t.InstitucionId)
                .Select(i => i.RutaSol ?? i.Id)
                .FirstOrDefaultAsync(ct);

        var solUrl = DireccionSol.Componer(sol.Value.UrlBase, rutaInstitucion, t.SolTramo, t.SolUrl);

        return new TramiteDetallePublicoDto(
            t.Codigo, t.Nombre, t.InstitucionId ?? "", t.Institucion,
            t.CategoriaId, categoriaNombre, t.Modalidad, t.EsPopular,
            t.CostoEsGratuito, t.CostoTexto, t.TiempoTexto, t.EstaEnSol,
            t.FichaCompleta,
            t.Descripcion, t.Objetivo, t.DirigidoA, t.VigenciaDocumento, t.Temporalidad,
            solUrl, t.SolVerificadoEl, ultimaRevision, t.EnlacePrincipal,
            pasos, requisitos, entregables, lugares, enlaces);
    }
}
