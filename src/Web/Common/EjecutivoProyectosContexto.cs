using Diger.TramitesEstado.Application.Areas.Queries;
using Diger.TramitesEstado.Application.Proyectos.Categorias;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Qué cartera muestra el tablero ejecutivo de proyectos: área y categoría ya validadas, con sus
/// nombres y sus opciones. Unidad y responsable no van aquí: se eligen dentro de la cartera y sus
/// opciones salen de la propia consulta del tablero (<c>GetEjecutivoProyectosQuery</c>).
///
/// <para>Lo comparten <c>/Tableros/Ejecutivo</c> y la banda del Inicio, que tienen que mostrar la
/// misma cartera por defecto: si cada una resolviera el área a su manera, el resumen del Inicio y
/// el tablero al que enlaza dirían números distintos.</para>
/// </summary>
public sealed record EjecutivoProyectosContexto(
    string? AreaId,
    string? AreaNombre,
    bool    PuedeElegirArea,
    IReadOnlyList<AreaListItemDto>     Areas,
    int?    CategoriaId,
    string? CategoriaNombre,
    IReadOnlyList<OpcionCategoriaDto>  Categorias,
    string  Titulo)
{
    /// <summary>Clave de configuración del área predeterminada. Es configuración por despliegue,
    /// no esquema: cada institución pone la suya.</summary>
    public const string ClaveAreaPredeterminada = "Tableros:EjecutivoProyectos:AreaPredeterminada";

    /// <summary>
    /// Resuelve la cartera. Orden del área por defecto (especificación, §9.4): (1) el área activa
    /// si el rol tiene alcance Área o Unidad —para ellos el parámetro se ignora—; (2) la
    /// configurada; (3) la primera activa por nombre; (4) «todas» solo si quien puede elegir la pide
    /// explícitamente con <c>Area=</c>.
    ///
    /// <para>Todo parámetro que no pertenece a lo visible se descarta <b>antes</b> de consultar, como
    /// en <c>/Tableros/Proyectos</c>: aplicado, daría un tablero vacío sin explicación. No es una
    /// fuga: la consulta interseca sobre lo que ya recortó el alcance.</para>
    /// </summary>
    /// <param name="areaExplicita">El parámetro <c>Area</c> vino en la URL, aunque sea vacío.</param>
    /// <param name="categoriaPedida">Categoría del catálogo. Una que no existe se descarta: se ve
    /// la cartera entera.</param>
    /// <param name="conOpciones">Cargar las opciones. La banda del Inicio no las necesita.</param>
    public static async Task<EjecutivoProyectosContexto> ResolverAsync(
        ISender sender, ICurrentUserService usuario, IConfiguration config, InstitucionOptions institucion,
        string? areaPedida, bool areaExplicita, int? categoriaPedida,
        bool conOpciones, CancellationToken ct)
    {
        var puedeElegir = usuario.EsGlobal
            || usuario.NivelAlcance is NivelAlcance.Institucion or NivelAlcance.Global;

        // Acotadas a la institución activa: para un rol global el filtro de Area se cortocircuita
        // y sin argumento llegarían las áreas de todas las instituciones (ver /Tableros/Proyectos).
        var areas = await sender.Send(new GetAreasQuery(usuario.ActiveInstitucionId), ct);
        bool Existe(string? id) => !string.IsNullOrEmpty(id) && areas.Any(a => a.Id == id);

        string? Predeterminada()
        {
            var configurada = config[ClaveAreaPredeterminada];
            if (Existe(configurada)) return configurada;
            return areas.Where(a => a.Activo).OrderBy(a => a.Nombre).Select(a => a.Id).FirstOrDefault();
        }

        string? areaId;
        if (!puedeElegir)
            areaId = Existe(usuario.ActiveAreaId) ? usuario.ActiveAreaId : Predeterminada();
        else if (Existe(areaPedida))
            areaId = areaPedida;
        else if (areaExplicita && string.IsNullOrEmpty(areaPedida))
            areaId = null;   // «todas», elegido a propósito
        else
            areaId = Predeterminada();

        var areaNombre = areas.FirstOrDefault(a => a.Id == areaId)?.Nombre;

        // Las «Q», en el orden del catálogo. Una retirada solo aparece si ya venía elegida.
        IReadOnlyList<OpcionCategoriaDto> categorias = conOpciones || categoriaPedida is not null
            ? await sender.Send(new GetOpcionesCategoriaQuery(categoriaPedida), ct)
            : [];
        var categoria = categorias.FirstOrDefault(x => x.Id == categoriaPedida);

        var titulo = $"Proyectos de {areaNombre ?? institucion.NombreCorto}"
                   + (categoria is null ? "" : $" · {categoria.Nombre}");

        return new EjecutivoProyectosContexto(
            areaId, areaNombre, puedeElegir,
            conOpciones ? areas.Where(a => a.Activo || a.Id == areaId).ToList() : [],
            categoria?.Id, categoria?.Nombre,
            categorias,
            titulo);
    }
}
