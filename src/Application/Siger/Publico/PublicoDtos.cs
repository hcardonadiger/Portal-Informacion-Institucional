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
    /// <summary>Qué le falta a la ficha para poder publicarse. Lista vacía = ficha completa.</summary>
    /// <remarks>
    /// Los nombres son los que el técnico ve en el editor, no los de las columnas: quien lee la
    /// alerta va a buscar el campo en la pantalla, no en la base. El costo se decide por
    /// <paramref name="costoEsGratuito"/> y no por el texto del monto, porque "es gratuito" ya es
    /// una respuesta completa aunque no haya monto que escribir.
    /// <para>
    /// La comparación es contra <c>null</c> y no contra cadena vacía a propósito: el filtro
    /// <c>?soloFichasCompletas=true</c> se resuelve en SQL, donde este método no se puede llamar,
    /// y tiene que decidir exactamente lo mismo. Si acá se apretara el criterio, el catálogo
    /// público mostraría fichas que esta alerta declara incompletas.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> CamposFaltantes(int? categoriaId, string? modalidad,
        string? tiempoTexto, bool? costoEsGratuito, bool estaEnSol, string? solUrl)
    {
        var faltantes = new List<string>(5);

        if (categoriaId is null)         faltantes.Add("categoría");
        if (modalidad is null)           faltantes.Add("modalidad");
        if (tiempoTexto is null)         faltantes.Add("tiempo");
        if (costoEsGratuito is null)     faltantes.Add("costo");
        if (estaEnSol && solUrl is null) faltantes.Add("enlace a SOL");

        return faltantes;
    }

    /// <summary>Una ficha está completa cuando no le falta nada. Definido sobre
    /// <see cref="CamposFaltantes"/> a propósito: el día que se agregue un campo obligatorio, la
    /// alerta que ve el técnico y el filtro que ve el ciudadano no pueden decir cosas distintas.</summary>
    public static bool Evaluar(int? categoriaId, string? modalidad, string? tiempoTexto,
        bool? costoEsGratuito, bool estaEnSol, string? solUrl) =>
        CamposFaltantes(categoriaId, modalidad, tiempoTexto, costoEsGratuito, estaEnSol, solUrl).Count == 0;

    /// <summary>Cómo se le dice al técnico qué falta. Vive junto a la regla y no en cada página
    /// para que el inventario, el detalle y el editor no acaben con tres redacciones distintas
    /// del mismo aviso.</summary>
    public static string Frase(IReadOnlyList<string> faltantes) =>
        faltantes.Count == 0
            ? "La ficha pública está completa."
            : $"Falta capturar: {string.Join(", ", faltantes)}.";
}
