namespace Diger.TramitesEstado.Domain.Common;

// ── Base entity ───────────────────────────────────────────────────────────
public abstract class BaseEntity<TId>
{
    public TId Id { get; protected set; } = default!;

    private readonly List<INotification> _domainEvents = [];
    public IReadOnlyCollection<INotification> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(INotification ev) => _domainEvents.Add(ev);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public abstract class BaseEntity : BaseEntity<int> { }

// ── Auditable base ────────────────────────────────────────────────────────
public abstract class BaseAuditableEntity<TId> : BaseEntity<TId>
{
    public DateTime  CreatedAt { get; set; }
    public string?   CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string?   UpdatedBy { get; set; }
}

public abstract class BaseAuditableEntity : BaseAuditableEntity<int> { }

// ── Domain events ─────────────────────────────────────────────────────────
public record ExpedienteCreatedEvent(int ExpedienteId, string Codigo, string Institucion) : INotification;
public record ExpedienteUpdatedEvent(int ExpedienteId)                                     : INotification;
public record ExpedienteDeletedEvent(int ExpedienteId, string Codigo)                      : INotification;
public record ReunionCreatedEvent(int ReunionId, string Titulo)                            : INotification;
public record ReunionUpdatedEvent(int ReunionId)                                           : INotification;
public record TicketCreatedEvent(int TicketId, string Numero, string Titulo)               : INotification;
public record TicketEstadoCambiadoEvent(int TicketId, string Numero, string Estado)        : INotification;

// ── Domain exception ──────────────────────────────────────────────────────
public sealed class DomainException(string message) : Exception(message);
