namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Perfil técnico disponible/requerido para la plataforma SOL.</summary>
public sealed class InfraPerfil : BaseEntity
{
    public int     ExpedienteId { get; set; }
    public string  Perfil       { get; set; } = default!;
    public string? Nombre       { get; set; }
    public string? Correo       { get; set; }
}

/// <summary>Condición operativa del data center marcada como cumplida.</summary>
public sealed class InfraCondicion : BaseEntity
{
    public int    ExpedienteId { get; set; }
    public string Condicion    { get; set; } = default!;
}

/// <summary>Cumplimiento de un requerimiento mínimo de infraestructura.</summary>
public sealed class InfraChecklistItem : BaseEntity
{
    public int         ExpedienteId { get; set; }
    public int         Orden        { get; set; }
    public string      Grupo        { get; set; } = default!;
    public string      Requisito    { get; set; } = default!;
    public InfraStatus Status       { get; set; } = InfraStatus.Pendiente;
    public string?     Obs          { get; set; }
}
