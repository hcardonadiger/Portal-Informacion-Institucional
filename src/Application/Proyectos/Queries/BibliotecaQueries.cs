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
    DateTime SubidoEn,

    /// <summary>Descargas del documento entero, todas sus versiones sumadas. Acá se cuenta por
    /// documento y no por versión —al revés que en la ficha— porque la pregunta que responde esta
    /// pantalla es «¿esta documentación la está usando alguien?», no cuál de las copias se bajó.</summary>
    int      Descargas = 0,

    /// <summary>Quién se llevó la última copia y cuándo. Null si nadie la ha descargado.</summary>
    string?  UltimaDescargaPor = null,
    DateTime? UltimaDescargaEn = null)
{
    public string TamanoLegible => ArchivoTamano switch
    {
        < 1024        => $"{ArchivoTamano} B",
        < 1024 * 1024 => $"{ArchivoTamano / 1024d:0.#} KB",
        _             => $"{ArchivoTamano / (1024d * 1024d):0.#} MB"
    };

    public bool FueCorregido => TotalVersiones > 1;

    /// <summary>Extensión en minúsculas, sin punto. Se deriva del nombre y no se guarda: es lo que
    /// permite filtrar «solo los PDF» sin una columna nueva.</summary>
    public string Extension
    {
        get
        {
            var i = ArchivoNombre.LastIndexOf('.');
            return i < 0 || i == ArchivoNombre.Length - 1
                ? ""
                : ArchivoNombre[(i + 1)..].ToLowerInvariant();
        }
    }

    /// <summary>Días desde la última versión. El convenio que quedó en borrador hace ocho meses no
    /// se distingue del que está al día si solo se mira la fecha.</summary>
    public int DiasSinActualizar => (int)(DateTime.UtcNow - SubidoEn).TotalDays;
}

/// <summary>Una opción de filtro con su conteo. El número es lo que evita elegir un filtro que
/// deja la pantalla vacía.</summary>
public sealed record FacetaDto(int Id, string Nombre, int Cantidad);

/// <summary>Lo mismo para lo que no tiene Id: personas y extensiones son texto, no catálogo.</summary>
public sealed record FacetaTextoDto(string Valor, int Cantidad);

public sealed record BibliotecaDto(
    IReadOnlyList<DocumentoBibliotecaDto> Documentos,
    IReadOnlyList<FacetaDto>              Categorias,
    IReadOnlyList<FacetaDto>              Proyectos,

    /// <summary>Total sin filtrar, para poder decir «12 de 340».</summary>
    int TotalSinFiltrar,

    /// <summary>Quiénes han cargado o actualizado documentación. Es la faceta que pidió
    /// coordinación: «qué mantiene cada persona».</summary>
    IReadOnlyList<FacetaTextoDto> Responsables = null!,
    IReadOnlyList<FacetaTextoDto> Tipos        = null!)
{
    public IReadOnlyList<FacetaTextoDto> Responsables { get; init; } = Responsables ?? [];
    public IReadOnlyList<FacetaTextoDto> Tipos        { get; init; } = Tipos        ?? [];
}

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
    DateOnly? Hasta       = null,

    /// <summary>Quién subió la versión vigente. Coincide con el nombre guardado como copia, no con
    /// el usuario: si la persona cambió de nombre, el histórico conserva el que tenía.</summary>
    string?   SubidoPor   = null,

    /// <summary>Extensión sin punto, en minúsculas.</summary>
    string?   Tipo        = null,

    /// <summary>Solo documentos con historial. Son los que se negocian, frente a los que se
    /// archivan una vez y no se tocan más.</summary>
    bool      SoloConHistorial = false,

    /// <summary>Solo los que llevan sin actualizarse al menos estos días.</summary>
    int?      SinActualizarDias = null) : IRequest<BibliotecaDto>;

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

        // ── Descargas por documento ────────────────────────────────────────────
        // Dos consultas planas contra el índice (VersionId, FechaHora). Ni JOIN ni subconsultas
        // correlacionadas: el mapa versión → documento ya está en memoria por el Include de arriba,
        // así que la suma por documento se hace acá y no en SQL.
        //
        // La primera versión proyectaba tres subconsultas por fila, cada una reevaluando la cadena
        // de EXISTS versión → documento → proyecto con el filtro de alcance entero: 180–300 ms con
        // cuatro documentos. Sobre la biblioteca, que barre TODO el portafolio, eso no aguanta.
        //
        // `IgnoreQueryFilters` es seguro acá: `versionPorDocumento` sale de `documentos`, que ya
        // vino filtrado por alcance. Los ids son de documentos que esta persona puede ver.
        var versionPorDocumento = documentos
            .SelectMany(d => d.Versiones.Select(v => (VersionId: v.Id, DocumentoId: d.Id)))
            .ToDictionary(x => x.VersionId, x => x.DocumentoId);

        var versionIds = versionPorDocumento.Keys.ToList();

        var porVersion = await ctx.ProyectoDocumentoDescargas.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => versionIds.Contains(x.VersionId))
            .GroupBy(x => x.VersionId)
            .Select(g => new
            {
                VersionId = g.Key,
                Total     = g.Count(),
                UltimaEn  = g.Max(x => x.FechaHora)
            })
            .ToListAsync(ct);

        var marcas = porVersion.Select(a => a.UltimaEn).ToList();

        var ultimos = marcas.Count == 0
            ? []
            : await ctx.ProyectoDocumentoDescargas.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(x => versionIds.Contains(x.VersionId) && marcas.Contains(x.FechaHora))
                .Select(x => new { x.VersionId, x.Usuario, x.FechaHora })
                .ToListAsync(ct);

        // Roll-up a documento: sus versiones sumadas, y la última de todas ellas.
        var descargas = porVersion
            .GroupBy(a => versionPorDocumento[a.VersionId])
            .ToDictionary(g => g.Key, g =>
            {
                var ultima = g.MaxBy(a => a.UltimaEn)!;
                return new
                {
                    Total     = g.Sum(a => a.Total),
                    UltimaEn  = (DateTime?)ultima.UltimaEn,
                    UltimaPor = ultimos.FirstOrDefault(u =>
                        u.VersionId == ultima.VersionId && u.FechaHora == ultima.UltimaEn)?.Usuario
                };
            });

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
                    vigente.SubidoPor, vigente.SubidoEn,
                    descargas.TryGetValue(d.Id, out var dsc) ? dsc.Total : 0,
                    descargas.TryGetValue(d.Id, out var dsc2) ? dsc2.UltimaPor : null,
                    descargas.TryGetValue(d.Id, out var dsc3) ? dsc3.UltimaEn : null);
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

        var facetasResponsable = filas
            .GroupBy(f => f.SubidoPor)
            .Select(g => new FacetaTextoDto(g.Key, g.Count()))
            .OrderBy(f => f.Valor)
            .ToList();

        var facetasTipo = filas
            .Where(f => f.Extension.Length > 0)
            .GroupBy(f => f.Extension)
            .Select(g => new FacetaTextoDto(g.Key, g.Count()))
            .OrderByDescending(f => f.Cantidad).ThenBy(f => f.Valor)
            .ToList();

        var total = filas.Count;

        if (q.CategoriaId is { } cid) filas = filas.Where(f => f.CategoriaId == cid).ToList();
        if (q.ProyectoId  is { } pid) filas = filas.Where(f => f.ProyectoId == pid).ToList();

        if (!string.IsNullOrWhiteSpace(q.SubidoPor))
            filas = filas.Where(f => f.SubidoPor == q.SubidoPor).ToList();

        if (!string.IsNullOrWhiteSpace(q.Tipo))
            filas = filas.Where(f => f.Extension == q.Tipo.ToLowerInvariant()).ToList();

        if (q.SoloConHistorial)
            filas = filas.Where(f => f.FueCorregido).ToList();

        if (q.SinActualizarDias is { } dias)
            filas = filas.Where(f => f.DiasSinActualizar >= dias).ToList();

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

        return new BibliotecaDto(filas, facetasCategoria, facetasProyecto, total,
                                 facetasResponsable, facetasTipo);
    }
}
