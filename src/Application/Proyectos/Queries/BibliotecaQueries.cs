using Diger.TramitesEstado.Application.Proyectos.Common;

namespace Diger.TramitesEstado.Application.Proyectos.Queries;

/// <summary>Una fila de la biblioteca: el documento con el proyecto del que cuelga.</summary>
public sealed record DocumentoBibliotecaDto(
    int      DocumentoId,
    int      VersionId,
    int      ProyectoId,
    string   ProyectoCodigo,
    string   ProyectoNombre,
    string?  ProyectoResponsable,
    int      CategoriaId,
    string   Categoria,
    int      CategoriaOrden,
    string   Titulo,
    string?  Descripcion,
    string   ArchivoNombre,
    long     ArchivoTamano,
    int      Numero,
    int      TotalVersiones,
    string   SubidoPor,
    DateTime SubidoEn)
{
    public string TamanoLegible => ArchivoTamano switch
    {
        < 1024        => $"{ArchivoTamano} B",
        < 1024 * 1024 => $"{ArchivoTamano / 1024d:0.#} KB",
        _             => $"{ArchivoTamano / (1024d * 1024d):0.#} MB"
    };

    public bool FueCorregido => TotalVersiones > 1;
}

/// <summary>Una opción de filtro con su conteo. El número es lo que evita elegir un filtro que
/// deja la pantalla vacía.</summary>
public sealed record FacetaDto(int Id, string Nombre, int Cantidad);

public sealed record BibliotecaDto(
    IReadOnlyList<DocumentoBibliotecaDto> Documentos,
    IReadOnlyList<FacetaDto>              Categorias,
    IReadOnlyList<FacetaDto>              Proyectos,

    /// <summary>Total sin filtrar, para poder decir «12 de 340».</summary>
    int TotalSinFiltrar);

/// <summary>
/// La biblioteca: la documentación de <b>todos los proyectos que la persona puede ver</b>.
///
/// <para><b>El aislamiento no se programa acá.</b> La consulta arranca en
/// <c>ProyectoDocumentos</c>, que lleva el filtro que hereda del proyecto, y ese filtro cubre las
/// dos excepciones —responsable e interesado— igual que en la ficha. Escribir una comprobación de
/// alcance en esta pantalla habría sido una segunda copia de la misma regla, y la copia es lo que
/// se desincroniza.</para>
///
/// <para>Las facetas se calculan sobre el conjunto <b>ya acotado por alcance pero antes</b> de
/// aplicar los filtros de la pantalla: si se calcularan después, elegir una categoría haría
/// desaparecer las demás del selector y no habría forma de volver.</para>
/// </summary>
public sealed record GetBibliotecaQuery(
    int?      CategoriaId = null,
    int?      ProyectoId  = null,
    string?   Buscar      = null,
    DateOnly? Desde       = null,
    DateOnly? Hasta       = null) : IRequest<BibliotecaDto>;

public sealed class GetBibliotecaQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetBibliotecaQuery, BibliotecaDto>
{
    public async Task<BibliotecaDto> Handle(GetBibliotecaQuery q, CancellationToken ct)
    {
        // Un solo viaje por las tres tablas, cruzadas en memoria. El portafolio es de decenas de
        // proyectos y la documentación crece despacio; cuando deje de serlo, esto se pagina.
        var documentos = await ctx.ProyectoDocumentos.AsNoTracking()
            .Include(d => d.Versiones)
            .ToListAsync(ct);

        if (documentos.Count == 0)
            return new BibliotecaDto([], [], [], 0);

        var proyectoIds = documentos.Select(d => d.ProyectoId).Distinct().ToList();

        // Los proyectos se traen por su propia consulta —filtrada— y no por un Join dentro de la
        // anterior: un Join habría descartado el Include de versiones, que es la trampa que ya
        // apareció en el tablero.
        var proyectos = await ctx.Proyectos.AsNoTracking()
            .Where(p => proyectoIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Codigo, p.Nombre, p.Responsable })
            .ToDictionaryAsync(p => p.Id, ct);

        var categorias = await ctx.CategoriasDocumento.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);

        var filas = documentos
            // Un documento cuyo proyecto no se pudo traer quedó fuera del alcance por el camino:
            // se descarta en vez de mostrarse sin proyecto.
            .Where(d => proyectos.ContainsKey(d.ProyectoId))
            .Select(d =>
            {
                var vigente  = d.Versiones.MaxBy(v => v.Numero);
                var proyecto = proyectos[d.ProyectoId];
                var cat      = categorias.GetValueOrDefault(d.CategoriaId);

                return vigente is null ? null : new DocumentoBibliotecaDto(
                    d.Id, vigente.Id, proyecto.Id, proyecto.Codigo, proyecto.Nombre, proyecto.Responsable,
                    d.CategoriaId, cat?.Nombre ?? "Sin categoría", cat?.Orden ?? int.MaxValue,
                    d.Titulo, d.Descripcion,
                    vigente.ArchivoNombre, vigente.ArchivoTamano,
                    vigente.Numero, d.Versiones.Count,
                    vigente.SubidoPor, vigente.SubidoEn);
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        // Facetas sobre lo visible sin filtrar. Ver la nota de la clase.
        var facetasCategoria = filas
            .GroupBy(f => (f.CategoriaId, f.Categoria, f.CategoriaOrden))
            .Select(g => new FacetaDto(g.Key.CategoriaId, g.Key.Categoria, g.Count()))
            .OrderBy(f => filas.First(x => x.CategoriaId == f.Id).CategoriaOrden)
            .ToList();

        var facetasProyecto = filas
            .GroupBy(f => (f.ProyectoId, f.ProyectoCodigo, f.ProyectoNombre))
            .Select(g => new FacetaDto(g.Key.ProyectoId, $"{g.Key.ProyectoCodigo} — {g.Key.ProyectoNombre}", g.Count()))
            .OrderBy(f => f.Nombre)
            .ToList();

        var total = filas.Count;

        if (q.CategoriaId is { } cid) filas = filas.Where(f => f.CategoriaId == cid).ToList();
        if (q.ProyectoId  is { } pid) filas = filas.Where(f => f.ProyectoId == pid).ToList();

        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            // Busca en título, descripción y nombre de archivo: quien busca «convenio SRECI» no
            // sabe cuál de los tres lo lleva.
            var texto = q.Buscar.Trim();
            filas = filas.Where(f =>
                f.Titulo.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || (f.Descripcion ?? "").Contains(texto, StringComparison.OrdinalIgnoreCase)
                || f.ArchivoNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Las fechas se comparan en local: quien filtra «hasta el 26 de agosto» piensa en su día,
        // no en UTC. SubidoEn se guarda en UTC — el mismo criterio de la bitácora.
        if (q.Desde is { } desde)
            filas = filas.Where(f => DateOnly.FromDateTime(f.SubidoEn.ToLocalTime()) >= desde).ToList();
        if (q.Hasta is { } hasta)
            filas = filas.Where(f => DateOnly.FromDateTime(f.SubidoEn.ToLocalTime()) <= hasta).ToList();

        filas = filas
            .OrderByDescending(f => f.SubidoEn)
            .ThenBy(f => f.Titulo)
            .ToList();

        return new BibliotecaDto(filas, facetasCategoria, facetasProyecto, total);
    }
}
