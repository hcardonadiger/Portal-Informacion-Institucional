namespace Diger.TramitesEstado.Api.Contrato;

// ─────────────────────────────────────────────────────────────────────────────
//  EL CONTRATO PÚBLICO — v1
//
//  Estos tipos SON la API. Sus nombres viajan a la especificación OpenAPI como
//  nombres de esquema, y HondurasÁgil está escrito contra ellos.
//
//  Cambiar un nombre, quitar un campo o volver obligatorio uno que era opcional
//  ROMPE al consumidor. Agregar un campo opcional, no. Esa es toda la regla, y por
//  eso el archivo vive acá y no junto a la base de datos: lo que se lee de las
//  tablas se puede reorganizar cuando convenga; esto no.
//
//  La comprobación que lo hace cumplir está en las pruebas: la especificación
//  generada tiene que salir idéntica a docs/api-v1/openapi-v1.yaml, y si no,
//  falla. No avisa: falla.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record TramiteResumenPublicoDto(
    string  Codigo,
    string  Nombre,
    string  InstitucionId,
    string  Institucion,
    int?    CategoriaId,
    string? CategoriaNombre,
    string? Modalidad,
    bool    EsPopular,
    bool?   CostoEsGratuito,
    string? CostoTexto,
    string? TiempoTexto,
    bool    EstaEnSol,
    bool    FichaCompleta);

public sealed record PasoPublicoDto(
    int Numero, string? Titulo, string Descripcion, string? Modalidad,
    string? LugarDependencia, string? SalidaResultado, string? TiempoRegistrado);

public sealed record RequisitoPublicoDto(
    int Numero, string Requisito, string? Tipo, string? DocumentoSoporte, string? Formato);

public sealed record EntregablePublicoDto(
    int Numero, string Entregable, string? Formato, string? Presentacion);

public sealed record LugarAtencionPublicoDto(
    int Numero, string Lugar, string? Ciudad, string? Direccion, string? Telefonos);

public sealed record EnlacePublicoDto(int Numero, string Url, string? Tipo);

public sealed record TramiteDetallePublicoDto(
    string  Codigo,
    string  Nombre,
    string  InstitucionId,
    string  Institucion,
    int?    CategoriaId,
    string? CategoriaNombre,
    string? Modalidad,
    bool    EsPopular,
    bool?   CostoEsGratuito,
    string? CostoTexto,
    string? TiempoTexto,
    bool    EstaEnSol,
    bool    FichaCompleta,
    string? Descripcion,
    string? Objetivo,
    string? DirigidoA,
    string? VigenciaDocumento,
    string? Temporalidad,
    string? SolUrl,
    DateTime? SolVerificadoEl,
    DateTime UltimaRevision,
    string? EnlacePrincipal,
    IReadOnlyList<PasoPublicoDto>          Pasos,
    IReadOnlyList<RequisitoPublicoDto>     Requisitos,
    IReadOnlyList<EntregablePublicoDto>    Entregables,
    IReadOnlyList<LugarAtencionPublicoDto> LugaresAtencion,
    IReadOnlyList<EnlacePublicoDto>        Enlaces);

public sealed record CatalogoPublicoDto(
    IReadOnlyList<TramiteResumenPublicoDto> Items, int Total, int Pagina, int Tamano);

public sealed record InstitucionPublicaDto(
    string Id, string Nombre, string? NombreCorto, string? LogoUrl,
    string? Telefono, string? SitioWeb, string? Direccion, string? Horario, string? Tipo,
    int ConteoTramitesPublicados);

public sealed record CategoriaPublicaDto(
    int Id, string Nombre, string? Icono, int Orden, int ConteoTramitesPublicados);

public sealed record CambiosPublicosDto(IReadOnlyList<string> Codigos, DateTime GeneradoEl);

public sealed record SaludPublicaDto(string Estado, bool BaseDeDatos, DateTime HoraServidor);

/// <summary>
/// Los tres valores que puede tomar <c>modalidad</c> en el filtro del catálogo.
/// </summary>
/// <remarks>
/// <para>
/// <b>No es «el catálogo de modalidades de PortalDigital» copiado acá.</b> Es lo que esta API
/// documenta que acepta, y de eso responde ella. Si PortalDigital agregara una cuarta modalidad,
/// esta API la devolvería sin cambios —el valor viaja tal cual desde la columna— y seguiría
/// filtrando por igualdad para cualquier texto que le manden. Lo único que hay acá es la
/// asimetría de <see cref="EsDigital"/>, que es una decisión del contrato público y está escrita
/// en la documentación de la ruta.
/// </para>
/// </remarks>
public static class ModalidadPublica
{
    public const string Virtual    = "Virtual";
    public const string Presencial = "Presencial";
    public const string Hibrido    = "Hibrido";

    /// <summary>Un trámite híbrido cuenta como «se puede hacer en línea» igual que uno virtual —
    /// filtrar solo por Virtual subestima cuántos trámites hay en línea.</summary>
    public static bool EsDigital(string? modalidad) => modalidad is Virtual or Hibrido;
}
