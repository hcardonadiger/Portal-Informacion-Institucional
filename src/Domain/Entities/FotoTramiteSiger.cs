namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Foto congelada de una ficha SIGER y de todas sus colecciones hijas, guardada como documento
/// JSON. La versión 0 es el inventario tal como llegó de SIGER, antes de que este portal
/// escribiera nada; las versiones siguientes las escribe cada pase desde un expediente.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué un documento y no tablas espejo.</b> Esto es un archivo, y un archivo tiene que
/// sobrevivir a los cambios de esquema que vengan. Si el original viviera en tablas con la misma
/// forma que <see cref="TramiteSiger"/>, cada columna que se agregue o se quite más adelante lo
/// iría deformando, y en unos años ya no diría lo que decía el día que se tomó. Un documento
/// congelado no se deforma.
/// </para>
/// <para>
/// <b>Por qué no hay llave foránea a la ficha.</b> Un archivo tiene que sobrevivir también a su
/// propio sujeto. Con cascada, borrar una ficha destruiría la única copia de su información
/// original —justo lo contrario de para lo que existe esta tabla—; y con restricción, el archivo
/// impediría borrar fichas. Por eso <see cref="TramiteSigerId"/> es solo una columna indexada, y
/// la fila lleva <see cref="Codigo"/> e <see cref="IdSiger"/> copiados para poder identificar la
/// ficha aunque ya no exista.
/// </para>
/// </remarks>
public sealed class FotoTramiteSiger : BaseAuditableEntity
{
    /// <summary>Ficha retratada. Sin llave foránea a propósito — ver las notas de la clase.</summary>
    public int TramiteSigerId { get; set; }

    /// <summary>0 = el original de SIGER. De 1 en adelante, cada pase desde un expediente.</summary>
    public int Version { get; set; }

    public string Origen { get; set; } = default!;

    /// <summary>Código de la ficha al momento de la foto. Copiado para que la fila siga siendo
    /// identificable aunque la ficha se borre.</summary>
    public string Codigo { get; set; } = default!;

    /// <summary>Identificador en SIGER al momento de la foto. Vacío si la ficha nació aquí.</summary>
    public int? IdSiger { get; set; }

    public DateTime CapturadaEl { get; set; }

    /// <summary>La ficha y sus seis colecciones hijas, serializadas.</summary>
    public string Contenido { get; set; } = default!;
}

/// <summary>De dónde salió una foto. Texto y no enum: se guarda dentro del archivo y tiene que
/// seguir leyéndose igual dentro de años, aunque el código cambie de nombres.</summary>
public static class OrigenFoto
{
    /// <summary>El inventario tal como llegó de SIGER, antes de la primera escritura del portal.</summary>
    public const string SigerOriginal = "SigerOriginal";

    /// <summary>Estado de la ficha justo antes de que un pase desde un expediente la sobrescriba.</summary>
    public const string PaseDesdeExpediente = "PaseDesdeExpediente";

    /// <summary>Número de versión reservado para el original. Ver <see cref="FotoTramiteSiger.Version"/>.</summary>
    public const int VersionOriginal = 0;
}
