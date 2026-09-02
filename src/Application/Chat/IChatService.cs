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

public record TecnicoDisponibleDto(Guid Id, string Nombre, int ChatsActivos);

// ── Interfaz del servicio ────────────────────────────────────────────────
public interface IChatService
{
    /// <summary>Crea una sesión en estado EnCola y persiste.</summary>
    Task<ChatSesion> IniciarSesionAsync(Guid usuarioId, string usuarioNombre, int? temaId, CancellationToken ct = default);

    /// <summary>Asigna un técnico a la sesión y la activa.</summary>
    Task AsignarTecnicoAsync(int sesionId, Guid tecnicoId, string tecnicoNombre, CancellationToken ct = default);

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
    Task<IReadOnlyList<TecnicoDisponibleDto>> GetTecnicosDisponiblesAsync(int temaId, CancellationToken ct = default);
    Task<ChatSesionDto?>                  GetSesionActivaUsuarioAsync(Guid usuarioId, CancellationToken ct = default);
}
