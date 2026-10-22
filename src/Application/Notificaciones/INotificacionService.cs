namespace Diger.TramitesEstado.Application.Notificaciones;

/// <param name="Url">Adónde lleva: al elemento si es uno solo; a <c>/Notificaciones/{id}</c> si son varios.</param>
/// <param name="Fecha">Cuándo entró el último elemento.</param>
public record NotificacionDto(
    int              Id,
    TipoNotificacion Tipo,
    string           Titulo,
    string?          Url,
    bool             Leida,
    DateTime         Fecha,
    int              Cantidad);

/// <summary>
/// La única puerta de los avisos. Aplica los interruptores del Panel de control (campana, correo,
/// agrupar), evita repetir los avisos diarios y anota en la bandeja lo que tiene que salir por correo.
/// </summary>
public interface INotificacionService
{
    /// <summary>Anota el aviso en el contexto EF; quien llama hace SaveChanges con su operación.</summary>
    /// <param name="clave">Qué identifica al elemento. Por defecto, la URL.</param>
    /// <returns>Si el aviso llegó a la campana (false si el módulo la tiene apagada o ya se avisó hoy).</returns>
    Task<bool> EncolarAsync(Guid destinatarioId, TipoNotificacion tipo, string titulo,
                            string? url = null, string? clave = null, CancellationToken ct = default);

    /// <summary>Igual que <see cref="EncolarAsync"/>, y guarda. Para el revisor y el chat, que no tienen operación propia.</summary>
    Task<bool> CrearYGuardarAsync(Guid destinatarioId, TipoNotificacion tipo, string titulo,
                                  string? url = null, string? clave = null, CancellationToken ct = default);

    Task<IReadOnlyList<NotificacionDto>> GetNoLeidasAsync(Guid usuarioId, int max = 8, CancellationToken ct = default);
    Task<int>                            ConteoNoLeidasAsync(Guid usuarioId, CancellationToken ct = default);
    Task                                 MarcarLeidaAsync(int id, Guid usuarioId, CancellationToken ct = default);
    Task                                 MarcarTodasLeidasAsync(Guid usuarioId, CancellationToken ct = default);
}
