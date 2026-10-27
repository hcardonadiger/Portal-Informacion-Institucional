namespace Diger.TramitesEstado.Domain.Entities;

public sealed class ChatSesion : BaseEntity
{
    public Guid      UsuarioId     { get; private set; }
    public string    UsuarioNombre { get; private set; } = "";
    public Guid?     TecnicoId     { get; private set; }
    public string?   TecnicoNombre { get; private set; }
    public int?      TemaId        { get; private set; }
    public string?   TemaNombre    { get; private set; }
    public int?      TicketId      { get; private set; }
    public ChatEstado Estado       { get; private set; }
    public byte?     Calificacion  { get; private set; }
    public DateTime  Inicio        { get; private set; }
    public DateTime? Cierre        { get; private set; }

    private readonly List<ChatMensaje> _mensajes = [];
    public IReadOnlyCollection<ChatMensaje> Mensajes => _mensajes.AsReadOnly();

    private ChatSesion() { }

    public static ChatSesion Iniciar(Guid usuarioId, string usuarioNombre)
        => new()
        {
            UsuarioId     = usuarioId,
            UsuarioNombre = usuarioNombre,
            Estado        = ChatEstado.EnCola,
            Inicio        = DateTime.UtcNow,
        };

    public void AsignarTema(int temaId, string temaNombre)
    {
        TemaId     = temaId;
        TemaNombre = temaNombre;
    }

    public void AsignarTecnico(Guid tecnicoId, string tecnicoNombre)
    {
        TecnicoId     = tecnicoId;
        TecnicoNombre = tecnicoNombre;
        Estado        = ChatEstado.Activo;
    }

    public bool EstaAbierta => Estado is ChatEstado.EnCola or ChatEstado.Activo;

    /// <summary>
    /// Un técnico toma la conversación. De la cola la toma cualquiera; si ya la atiende otro,
    /// solo quien tiene alcance Global puede quitársela (<paramref name="puedeReasignar"/>).
    /// Devuelve el nombre de quien la atendía cuando hubo reasignación, para dejarlo dicho en el chat.
    /// </summary>
    public string? Tomar(Guid tecnicoId, string tecnicoNombre, bool puedeReasignar)
    {
        if (!EstaAbierta)
            throw new DomainException("La conversación ya está cerrada.");
        if (TecnicoId == tecnicoId)
            throw new DomainException("Ya atiende esta conversación.");
        if (TecnicoId.HasValue && !puedeReasignar)
            throw new DomainException("Esta conversación ya la atiende otro técnico.");

        var anterior = TecnicoId.HasValue ? TecnicoNombre : null;
        AsignarTecnico(tecnicoId, tecnicoNombre);
        return anterior;
    }

    public void VincularTicket(int ticketId) => TicketId = ticketId;

    public ChatMensaje AgregarMensaje(string texto, bool esDelTecnico, bool esSistema, string autorNombre)
    {
        var m = ChatMensaje.Crear(Id, texto, esDelTecnico, esSistema, autorNombre);
        _mensajes.Add(m);
        return m;
    }

    public void Cerrar(ChatEstado estadoCierre = ChatEstado.Resuelto)
    {
        Estado = estadoCierre;
        Cierre = DateTime.UtcNow;
    }

    public void Calificar(byte puntuacion)
    {
        if (puntuacion < 1 || puntuacion > 5) return;
        Calificacion = puntuacion;
    }
}
