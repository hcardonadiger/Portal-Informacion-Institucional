namespace Diger.TramitesEstado.Application.Notificaciones;

public record NotificacionDto(
    int             Id,
    TipoNotificacion Tipo,
    string           Titulo,
    string?          Url,
    bool             Leida,
    DateTime         FechaCreacion);

public interface INotificacionService
{
    /// <summary>Encola la notificación en el contexto EF — el caller hace SaveChanges junto con su operación principal.</summary>
    void Encolar(Guid destinatarioId, TipoNotificacion tipo, string titulo, string? url = null);

    /// <summary>Crea y persiste de forma autónoma (usado desde el hosted service con su propio scope).</summary>
    Task CrearYGuardarAsync(Guid destinatarioId, TipoNotificacion tipo, string titulo, string? url = null, CancellationToken ct = default);

    Task<IReadOnlyList<NotificacionDto>> GetNoLeidasAsync(Guid usuarioId, int max = 8, CancellationToken ct = default);
    Task<int>                            ConteoNoLeidasAsync(Guid usuarioId, CancellationToken ct = default);
    Task                                 MarcarLeidaAsync(int id, Guid usuarioId, CancellationToken ct = default);
    Task                                 MarcarTodasLeidasAsync(Guid usuarioId, CancellationToken ct = default);
}
