namespace Diger.TramitesEstado.Application.Siger.Importacion;

/// <summary>
/// El expediente donde aterrizan las fichas importadas de SIGER cuando nadie eligió un destino
/// (D-06): uno por institución, «Trámites Importados de SIGER».
/// </summary>
/// <remarks>
/// <para>
/// <b>No es un levantamiento y no debe parecerlo</b> (D-21). Se marca con
/// <see cref="OrigenExternoId"/> para poder excluirlo de los listados, los conteos y los tableros
/// del módulo de expedientes. Sin esa marca aparecería en todas partes como si alguien hubiera
/// abierto un levantamiento real —inflando las cifras de trabajo en curso— y además quedaría
/// atrapado para siempre en <c>EnExploracion</c>, porque <c>CambiarEstado</c> solo admite avanzar
/// de una etapa a la siguiente y un bucket nunca se «levanta».
/// </para>
/// <para>
/// <b>La identidad es el <c>OrigenExternoId</c>, no el código.</b> El código se deriva de la
/// llave de la institución y hay llaves de 43 caracteres contra una columna de 40, así que se
/// recorta; el <c>OrigenExternoId</c> cabe entero y es lo que se busca para no crear dos buckets
/// de la misma institución.
/// </para>
/// </remarks>
public static class BucketImportacion
{
    private const string Prefijo = "SIGER-IMPORT:";

    /// <summary>Cuánto mide como máximo la columna <c>Expediente.Codigo</c>.</summary>
    private const int LargoMaximoCodigo = 40;

    public const string NombreAnalista = "Importación automática";

    /// <summary>La marca que identifica al bucket de una institución. Es la llave real.</summary>
    public static string OrigenExternoId(string institucionId) =>
        Prefijo + institucionId.Trim().ToUpperInvariant();

    /// <summary>Si un expediente es un bucket de importación y no un levantamiento.</summary>
    public static bool EsBucket(string? origenExternoId) =>
        origenExternoId is not null &&
        origenExternoId.StartsWith(Prefijo, StringComparison.Ordinal);

    /// <summary>
    /// El código que se le pone al bucket. Se recorta a lo que cabe en la columna; si dos
    /// instituciones compartieran los primeros caracteres, quien crea el bucket resuelve el
    /// choque —la unicidad de verdad la da el <c>OrigenExternoId</c>—.
    /// </summary>
    public static string CodigoSugerido(string institucionId)
    {
        var codigo = "SIGER-" + institucionId.Trim().ToUpperInvariant();
        return codigo.Length <= LargoMaximoCodigo ? codigo : codigo[..LargoMaximoCodigo];
    }

    /// <summary>
    /// Quita los buckets de importación de una consulta de expedientes (D-21).
    ///
    /// Existe como extensión y no como condición escrita a mano en cada consulta porque son siete
    /// los sitios que listan o cuentan expedientes —el listado, cuatro tableros, el calendario y
    /// el seguimiento— y siete copias de la misma condición acaban discrepando: bastaría con que
    /// una se olvidara para que las cifras de trabajo en curso incluyeran contenedores que nadie
    /// abrió.
    ///
    /// <b>No se puso como filtro global del modelo</b> a propósito: eso escondería el bucket
    /// también de su propio editor, y el bucket sí se abre para trabajar los trámites importados.
    /// </summary>
    public static IQueryable<Expediente> SinBuckets(this IQueryable<Expediente> q) =>
        q.Where(e => e.OrigenExternoId == null || !e.OrigenExternoId.StartsWith(Prefijo));
}
