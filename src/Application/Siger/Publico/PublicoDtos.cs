namespace Diger.TramitesEstado.Application.Siger.Publico;

/// <summary>Catálogo cerrado de modalidad para la ficha pública. "Digital" = Virtual o Hibrido —
/// ver <see cref="EsDigital"/>. Un enum C# no se usa a propósito (ver M-04 del plan: TramiteSiger
/// se mantiene anémico, y el catálogo cerrado ya está protegido por CHECK en la base).</summary>
public static class ModalidadPublica
{
    public const string Virtual    = "Virtual";
    public const string Presencial = "Presencial";
    public const string Hibrido    = "Hibrido";

    /// <summary>Un trámite Híbrido cuenta como "se puede hacer en línea" igual que uno Virtual —
    /// filtrar solo por Virtual subestima el conteo de trámites digitales.</summary>
    public static bool EsDigital(string? modalidad) => modalidad is Virtual or Hibrido;
}

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

/// <summary>Regla única de "ficha completa" (informativa en el resumen, real en el filtro
/// ?soloFichasCompletas=true) — un solo lugar para no desincronizar el resumen del filtro.</summary>
public static class FichaPublicaCompletitud
{
    public static bool Evaluar(int? categoriaId, string? modalidad, string? tiempoTexto,
        bool? costoEsGratuito, bool estaEnSol, string? solUrl) =>
        categoriaId is not null && modalidad is not null && tiempoTexto is not null &&
        costoEsGratuito is not null && (!estaEnSol || solUrl is not null);
}
