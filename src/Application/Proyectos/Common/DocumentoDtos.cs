namespace Diger.TramitesEstado.Application.Proyectos.Common;

/// <summary>Una categoría del catálogo, como la ve el selector y la administración.</summary>
public sealed record CategoriaDocumentoDto(
    int     Id,
    string  Nombre,
    string? Descripcion,
    int     Orden,
    bool    Activa,

    /// <summary>Cuántos documentos vivos la usan. Es lo que decide si se puede desactivar sin
    /// dejar documentación colgando de una etiqueta que ya no se ofrece.</summary>
    int     EnUso);

/// <summary>
/// Una versión del archivo.
///
/// <para><b>No expone la ruta del archivo</b>, con el mismo criterio que
/// <see cref="AvanceProyectoDto"/>: la descarga pasa siempre por el handler autenticado, que la
/// resuelve por el Id. Desde que <c>/uploads</c> dejó de servirse como estático, filtrar la ruta
/// no bastaría para bajar el archivo — pero exponerla seguiría siendo decirle a la vista algo que
/// no le hace falta.</para>
/// </summary>
public sealed record VersionDocumentoDto(
    int      Id,
    int      Numero,
    string   ArchivoNombre,
    long     ArchivoTamano,
    string   Sha256,
    string?  Notas,
    string   SubidoPor,
    DateTime SubidoEn,

    /// <summary>Cuántas veces se descargó <b>esta versión</b>, no el documento. Es lo que permite
    /// distinguir que el convenio se bajó cuatro veces en su v1 y ninguna desde que se corrigió.</summary>
    int      Descargas = 0,

    /// <summary>Quién la descargó por última vez y cuándo. Null si nadie la ha bajado — que es
    /// distinto de cero descargas por no estar registrando: la tabla existe desde el 31-08-2026,
    /// así que todo lo anterior a esa fecha figura sin descargas aunque se hubiera bajado.</summary>
    string?  UltimaDescargaPor = null,
    DateTime? UltimaDescargaEn = null)
{
    /// <summary>Tamaño legible. La vista no debería estar dividiendo entre 1024.</summary>
    public string TamanoLegible => ArchivoTamano switch
    {
        < 1024               => $"{ArchivoTamano} B",
        < 1024 * 1024        => $"{ArchivoTamano / 1024d:0.#} KB",
        _                    => $"{ArchivoTamano / (1024d * 1024d):0.#} MB"
    };

    /// <summary>Las primeras cifras de la huella, que es lo único que se muestra: sirve para
    /// comparar dos versiones de un vistazo sin llenar la pantalla con 64 caracteres.</summary>
    public string HuellaCorta => Sha256.Length >= 12 ? Sha256[..12] : Sha256;
}

/// <summary>
/// Un documento del repositorio, con su historial completo.
/// </summary>
public sealed record DocumentoProyectoDto(
    int      Id,
    int      CategoriaId,
    string   Categoria,
    string   Titulo,
    string?  Descripcion,
    IReadOnlyList<VersionDocumentoDto> Versiones)
{
    /// <summary>La versión que rige: la de mayor número. Null solo si el documento quedó sin
    /// archivo, que el alta no permite pero un script podría dejar.</summary>
    public VersionDocumentoDto? Vigente => Versiones.MaxBy(v => v.Numero);

    public int TotalVersiones => Versiones.Count;

    /// <summary>Tiene historial: hubo al menos una corrección. La ficha lo señala porque un
    /// documento corregido se lee distinto de uno que nunca se tocó.</summary>
    public bool FueCorregido => Versiones.Count > 1;

    public DateTime? ActualizadoEn => Vigente?.SubidoEn;

    /// <summary>Descargas de todas las versiones sumadas: cuánto se ha usado el documento a lo
    /// largo de su vida, frente al conteo por versión que lleva cada una.</summary>
    public int TotalDescargas => Versiones.Sum(v => v.Descargas);

    /// <summary>Alguien volvió a subir un archivo idéntico al anterior: mismo contenido, versión
    /// nueva. No es un error —puede ser un renombrado deliberado— pero conviene que se vea.</summary>
    public bool UltimaVersionRepiteContenido
    {
        get
        {
            if (Versiones.Count < 2) return false;
            var orden = Versiones.OrderByDescending(v => v.Numero).ToList();
            return orden[0].Sha256 == orden[1].Sha256;
        }
    }
}

/// <summary>Metadatos que necesita el handler de descarga. Incluye el proyecto para poder volver
/// a su ficha si el archivo físico ya no está. Mismo contrato que <see cref="EvidenciaAvanceDto"/>.</summary>
public sealed record DescargaDocumentoDto(
    int    ProyectoId,
    string ArchivoNombre,
    string ArchivoUrl);
