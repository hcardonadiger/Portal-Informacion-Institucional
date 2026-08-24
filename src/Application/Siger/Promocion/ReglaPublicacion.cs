namespace Diger.TramitesEstado.Application.Siger.Promocion;

/// <summary>
/// Cuándo el estado de una ficha SIGER <b>sugiere</b> que ya se puede publicar en HondurasÁgil.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta regla ya no decide nada; aconseja.</b> Hasta la Fase 4 era lo que ponía y quitaba la
/// bandera <c>Publicado</c> en cada guardado, y eso tenía dos consecuencias malas. La primera:
/// nadie elegía qué ve el ciudadano —lo elegía un campo de estado administrativo—. La segunda,
/// peor: la bandera solo se recalculaba al editar, así que había 303 fichas en Aprobado o
/// Completo y solo 50 publicadas, y las otras 253 no iban a corregirse nunca porque nadie las
/// iba a volver a editar.
/// </para>
/// <para>
/// Desde D-10 la publicación es <b>manual y no se bloquea</b>: quien administra PortalDigital
/// marca qué se publica, y esta regla solo alimenta el aviso de la pantalla y la lista de
/// candidatas. Un aviso informa; un bloqueo se impondría sobre el criterio de quien sí conoce
/// el trámite.
/// </para>
/// <para>
/// <b>Por qué vive acá y no en la página.</b> Tanto la pantalla de publicación como la promoción
/// de un trámite de expediente necesitan la misma respuesta. Dos copias de una regla que habla
/// de lo que ve el ciudadano acabarían discrepando, y la discrepancia se vería en el portal
/// público. Ya pasó: antes de la Fase 1 había tres copias escritas a mano.
/// </para>
/// </remarks>
public static class ReglaPublicacion
{
    /// <summary>Estado con el que nace una ficha promovida desde un expediente: no se publica
    /// hasta que alguien lo decida. Promover y publicar son dos actos distintos.</summary>
    public const string Registrado = "Registrado";
    public const string Aprobado   = "Aprobado";
    public const string Completo   = "Completo";

    /// <summary>
    /// Cierto cuando el estado administrativo de la ficha no da motivo para dudar de publicarla.
    /// No es permiso ni impedimento: es lo que separa a las candidatas del resto y lo que decide
    /// si la pantalla muestra un aviso junto a la fila.
    /// </summary>
    public static bool EstadoListoParaPublicar(string? estadoSiger) =>
        estadoSiger is Aprobado or Completo;
}
