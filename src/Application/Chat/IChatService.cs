namespace Diger.TramitesEstado.Application.Chat;

// ── DTOs ─────────────────────────────────────────────────────────────────
public record ChatMensajeDto(
    int      Id,
    int      SesionId,
    string   Texto,
    bool     EsDelTecnico,
    bool     EsSistema,
    string   AutorNombre,
    DateTime Enviado,
    bool     Leido);

public record ChatSesionDto(
    int        Id,
    Guid       UsuarioId,
    string     UsuarioNombre,
    Guid?      TecnicoId,
    string?    TecnicoNombre,
    int?       TemaId,
    string?    TemaNombre,
    int?       TicketId,
    ChatEstado Estado,
    DateTime   Inicio,
    DateTime?  Cierre,
    int        MensajesNoLeidos);

public record ChatSesionDetalleDto(
    ChatSesionDto              Sesion,
    IReadOnlyList<ChatMensajeDto> Mensajes);

/// <summary>Candidato a recibir un chat de la cola. <see cref="DelTema"/>: tiene asignado el tema del chat.</summary>
public record TecnicoAsignableDto(Guid Id, string Nombre, bool DelTema);

// ── Interfaz del servicio ────────────────────────────────────────────────
public interface IChatService
{
    /// <summary>Crea una sesión en estado EnCola y persiste.</summary>
    Task<ChatSesion> IniciarSesionAsync(Guid usuarioId, string usuarioNombre, int? temaId, CancellationToken ct = default);

    /// <summary>
    /// El técnico toma la sesión y la activa. Si ya la atiende otro, solo procede con
    /// <paramref name="puedeReasignar"/> (alcance Global); si no, lanza DomainException.
    /// Devuelve el nombre de quien la atendía cuando hubo reasignación.
    /// </summary>
    Task<string?> TomarSesionAsync(int sesionId, Guid tecnicoId, string tecnicoNombre, bool puedeReasignar, CancellationToken ct = default);

    /// <summary>
    /// Un supervisor reparte un chat de la cola a <paramref name="tecnicoId"/>. Solo chats en cola
    /// y solo técnicos activos con un rol que atiende soporte; si no, DomainException.
    /// Devuelve el nombre del técnico, para el mensaje de sistema y la notificación.
    /// </summary>
    Task<string> AsignarDesdeColaAsync(int sesionId, Guid tecnicoId, CancellationToken ct = default);

    /// <summary>¿Es <paramref name="tecnicoId"/> quien atiende esta sesión, y sigue abierta?
    /// Solo ese técnico responde o resuelve; los demás, incluido el Global, la leen.</summary>
    Task<bool> AtiendeSesionAsync(int sesionId, Guid tecnicoId, CancellationToken ct = default);

    /// <summary>Agrega un mensaje a la sesión y lo persiste.</summary>
    Task<ChatMensajeDto> EnviarMensajeAsync(int sesionId, string texto, bool esDelTecnico, bool esSistema, string autorNombre, CancellationToken ct = default);

    /// <summary>Marca como leídos los mensajes de la parte contraria.</summary>
    Task MarcarLeidosAsync(int sesionId, bool lectoPorTecnico, CancellationToken ct = default);

    /// <summary>Cierra la sesión con el estado indicado.</summary>
    Task CerrarSesionAsync(int sesionId, ChatEstado estado = ChatEstado.Resuelto, CancellationToken ct = default);

    /// <summary>Registra la calificación del usuario (1-5).</summary>
    Task CalificarAsync(int sesionId, byte puntuacion, CancellationToken ct = default);

    /// <summary>Vincula un ticket creado a esta sesión de chat.</summary>
    Task VincularTicketAsync(int sesionId, int ticketId, CancellationToken ct = default);

    // ── Queries ──────────────────────────────────────────────────────────
    Task<ChatSesionDetalleDto?>           GetDetalleAsync(int sesionId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatSesionDto>>    GetSesionesTecnicoAsync(Guid tecnicoId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatSesionDto>>    GetColaAsync(int? temaId = null, CancellationToken ct = default);

    // Supervisión (alcance Global): lo que la bandeja de un técnico no muestra.
    Task<IReadOnlyList<ChatSesionDto>>    GetActivasDeOtrosAsync(Guid tecnicoId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatSesionDto>>    GetCerradasRecientesAsync(int maximo = 50, CancellationToken ct = default);
    Task<int>                             ContarColaAsync(CancellationToken ct = default);

    /// <summary>Técnicos activos a quienes se puede asignar un chat: los del tema primero, luego el resto.</summary>
    Task<IReadOnlyList<TecnicoAsignableDto>> GetTecnicosAsignablesAsync(int? temaId, CancellationToken ct = default);
    Task<ChatSesionDto?>                  GetSesionActivaUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
}
