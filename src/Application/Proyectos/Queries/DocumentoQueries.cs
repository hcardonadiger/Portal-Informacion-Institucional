using Diger.TramitesEstado.Application.Proyectos.Common;

namespace Diger.TramitesEstado.Application.Proyectos.Queries;

// ── Documentos de un proyecto ─────────────────────────────────────────────
/// <summary>
/// El repositorio documental de un proyecto, agrupado por categoría en la vista.
///
/// <para>No hace falta filtrar por alcance acá: <c>ProyectoDocumentos</c> lleva su propio filtro,
/// que hereda el del proyecto. Pedir los documentos de un proyecto ajeno devuelve vacío, no un
/// error — igual que pedir el proyecto.</para>
/// </summary>
public sealed record GetDocumentosProyectoQuery(int ProyectoId) : IRequest<IReadOnlyList<DocumentoProyectoDto>>;

public sealed class GetDocumentosProyectoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetDocumentosProyectoQuery, IReadOnlyList<DocumentoProyectoDto>>
{
    public async Task<IReadOnlyList<DocumentoProyectoDto>> Handle(
        GetDocumentosProyectoQuery query, CancellationToken ct)
    {
        // Dos colecciones no: solo Versiones. El nombre de la categoría se resuelve con un join
        // porque el catálogo es chico y traerlo entero sale más barato que un Include por fila.
        var documentos = await ctx.ProyectoDocumentos.AsNoTracking()
            .Where(d => d.ProyectoId == query.ProyectoId)
            .Include(d => d.Versiones)
            .ToListAsync(ct);

        if (documentos.Count == 0) return [];

        var categorias = await ctx.CategoriasDocumento.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, ct);

        return documentos
            .Select(d => new DocumentoProyectoDto(
                d.Id,
                d.CategoriaId,
                categorias.TryGetValue(d.CategoriaId, out var cat) ? cat.Nombre : "Sin categoría",
                d.Titulo,
                d.Descripcion,
                d.Versiones
                    .OrderByDescending(v => v.Numero)
                    .Select(v => new VersionDocumentoDto(
                        v.Id, v.Numero, v.ArchivoNombre, v.ArchivoTamano,
                        v.Sha256, v.Notas, v.SubidoPor, v.SubidoEn))
                    .ToList()))
            // El orden de la categoría manda; dentro de ella, lo último que se movió primero.
            .OrderBy(d => categorias.TryGetValue(d.CategoriaId, out var cat) ? cat.Orden : int.MaxValue)
            .ThenByDescending(d => d.ActualizadoEn)
            .ThenBy(d => d.Titulo)
            .ToList();
    }
}

// ── Catálogo de categorías ────────────────────────────────────────────────
/// <summary>
/// Las categorías del catálogo.
/// </summary>
/// <param name="SoloActivas">Para el selector de un documento nuevo: una categoría desactivada no
/// se ofrece, pero los documentos que ya la tienen la conservan. La administración las pide todas.</param>
public sealed record GetCategoriasDocumentoQuery(bool SoloActivas = true)
    : IRequest<IReadOnlyList<CategoriaDocumentoDto>>;

public sealed class GetCategoriasDocumentoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetCategoriasDocumentoQuery, IReadOnlyList<CategoriaDocumentoDto>>
{
    public async Task<IReadOnlyList<CategoriaDocumentoDto>> Handle(
        GetCategoriasDocumentoQuery query, CancellationToken ct)
    {
        var categorias = await ctx.CategoriasDocumento.AsNoTracking()
            .Where(c => !query.SoloActivas || c.Activa)
            .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
            .ToListAsync(ct);

        // El conteo de uso sale de una sola agregación, no de una consulta por categoría. Cuenta
        // solo lo que el usuario puede ver, que es lo correcto: es el número que sostiene el aviso
        // de «no la desactives, está en uso» en SU pantalla.
        var enUso = await ctx.ProyectoDocumentos.AsNoTracking()
            .GroupBy(d => d.CategoriaId)
            .Select(g => new { CategoriaId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.CategoriaId, x => x.Total, ct);

        return categorias
            .Select(c => new CategoriaDocumentoDto(
                c.Id, c.Nombre, c.Descripcion, c.Orden, c.Activa,
                enUso.GetValueOrDefault(c.Id)))
            .ToList();
    }
}

// ── Descarga ──────────────────────────────────────────────────────────────
/// <summary>
/// Metadatos de una versión para servirla.
///
/// <para>La consulta arranca en <c>ProyectoDocumentoVersiones</c>, que lleva su ancla al documento
/// y por él al proyecto: pedir la versión de un documento ajeno devuelve null, sin necesidad de
/// comprobar nada a mano en el handler.</para>
/// </summary>
public sealed record GetDescargaDocumentoQuery(int VersionId) : IRequest<DescargaDocumentoDto?>;

public sealed class GetDescargaDocumentoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetDescargaDocumentoQuery, DescargaDocumentoDto?>
{
    public async Task<DescargaDocumentoDto?> Handle(GetDescargaDocumentoQuery query, CancellationToken ct) =>
        await ctx.ProyectoDocumentoVersiones.AsNoTracking()
            .Where(v => v.Id == query.VersionId)
            .Join(ctx.ProyectoDocumentos.AsNoTracking(), v => v.DocumentoId, d => d.Id, (v, d) => new { v, d })
            .Select(x => new DescargaDocumentoDto(x.d.ProyectoId, x.v.ArchivoNombre, x.v.ArchivoUrl))
            .FirstOrDefaultAsync(ct);
}
