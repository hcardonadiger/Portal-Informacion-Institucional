namespace Diger.TramitesEstado.Application.Siger.Historial;

/// <summary>
/// La forma congelada de una ficha SIGER dentro de una <see cref="FotoTramiteSiger"/>.
/// </summary>
/// <remarks>
/// Es una copia deliberada de <see cref="TramiteSiger"/>, no una referencia a ella. Esa
/// duplicación es el punto: el archivo tiene que conservar la forma que tenía el día que se tomó,
/// así que este contrato **no se edita** cuando la entidad cambie. Si mañana la ficha gana un
/// campo, se agrega acá también —los documentos viejos simplemente lo traerán vacío— pero nunca
/// se quita ni se renombra nada, o las fotos ya guardadas dejarían de leerse.
/// </remarks>
public sealed record FichaFoto(
    int       Id,
    int?      IdSiger,
    string    Codigo,
    string    Nombre,
    string    Institucion,
    string?   Sigla,
    string?   Dependencia,
    string?   Descripcion,
    string?   Objetivo,
    string?   DirigidoA,
    string?   EstadoSiger,
    bool      Publicado,
    bool      DisponibleEnLinea,
    bool      EnPlanDigitalizacion,
    string?   VigenciaDocumento,
    string?   Temporalidad,
    string?   DiagramaUrl,
    string?   EnlacePrincipal,
    string?   ObservacionesDiger,
    DateTime? FechaIngreso,
    DateTime? UltimaModificacion,
    string?   InstitucionId,
    bool      EstaEnSol,
    string?   SolUrl,
    DateTime? SolVerificadoEl,
    int?      CategoriaId,
    string?   CostoTexto,
    bool?     CostoEsGratuito,
    string?   TiempoTexto,
    string?   Modalidad,
    bool      EsPopular,
    IReadOnlyList<PasoFoto>       Pasos,
    IReadOnlyList<RequisitoFoto>  Requisitos,
    IReadOnlyList<EntregableFoto> Entregables,
    IReadOnlyList<LugarFoto>      LugaresAtencion,
    IReadOnlyList<EnlaceFoto>     Enlaces,
    IReadOnlyList<TareaFoto>      TareasDigitalizacion);

public sealed record PasoFoto(
    int NumeroPaso, string Descripcion, string? LugarDependencia,
    string? SalidaResultado, string? TiempoRegistrado, string? Titulo, string? Modalidad);

public sealed record RequisitoFoto(
    int Numero, string Requisito, string? Tipo, string? DocumentoSoporte, string? Formato);

public sealed record EntregableFoto(
    int Numero, string Entregable, string? Formato, string? Presentacion);

public sealed record LugarFoto(
    int Numero, string Lugar, string? Ciudad, string? Direccion, string? Telefonos);

public sealed record EnlaceFoto(int Numero, string Url, string? Tipo);

public sealed record TareaFoto(
    int NumeroTarea, string Descripcion, string? Estado, DateTime? FechaCumplimiento);
