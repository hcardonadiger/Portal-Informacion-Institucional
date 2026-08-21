namespace Diger.TramitesEstado.Application.Siger.Promocion;

/// <summary>Publicado es consecuencia del estado de la ficha, y de nada más: aprobada o
/// completa se publica (D-02).</summary>
/// <remarks>
/// <para>
/// <b>Hasta el 20-08-2026 exigía además que la ficha estuviera completa</b>, y eso tenía
/// un efecto que nadie quería: el primer guardado que no llenara la ficha entera en el
/// mismo paso la despublicaba. Un técnico llenando por tandas —la categoría de treinta
/// fichas, luego la modalidad— veía el catálogo vaciarse mientras trabajaba. En producción
/// había 49 fichas publicadas y ninguna cumplía la regla: la primera edición de cualquiera
/// de ellas la habría borrado del portal del ciudadano.
/// </para>
/// <para>
/// <b>Se separó por decisión del usuario (P-09, opción 1):</b> una ficha incompleta se
/// queda publicada y HondurasÁgil enseña un guion donde falta el dato. Al ciudadano le
/// sirve más saber que el trámite existe y quién lo atiende, que no encontrarlo.
/// </para>
/// <para>
/// La completitud <b>no desaparece, deja de censurar</b>: FichaPublicaCompletitud sigue
/// calculando qué falta, el editor y el listado lo siguen avisando al técnico, y la API
/// pública lo sigue publicando en el campo FichaCompleta. Lo único que cambió es que ya no
/// decide si el ciudadano puede ver el trámite.
/// </para>
/// <para>
/// <b>Por qué vive acá y no en la página del editor:</b> la promoción de un trámite de
/// expediente a ficha SIGER necesita exactamente esta misma regla. Dos copias de una regla
/// que decide qué ve el ciudadano acabarían discrepando, y la discrepancia se vería en el
/// portal público. Es el mismo motivo por el que <c>FichaPublicaCompletitud</c> vive en esta
/// capa y no en cada pantalla que la consulta.
/// </para>
/// </remarks>
public static class ReglaPublicacion
{
    /// <summary>Estado con el que nace una ficha promovida desde un expediente: no se publica
    /// hasta que alguien la apruebe. Promover y publicar son dos actos distintos.</summary>
    public const string Registrado = "Registrado";
    public const string Aprobado   = "Aprobado";
    public const string Completo   = "Completo";

    public static bool SePublica(string? estadoSiger) =>
        estadoSiger is Aprobado or Completo;
}
