namespace Diger.TramitesEstado.Application.Common.Interfaces;

public interface IExpedienteRepository
{
    Task<Expediente?>               GetByIdAsync(int id, CancellationToken ct = default);
    Task<Expediente?>               GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Expediente>> GetAllAsync(CancellationToken ct = default);
    Task<int>                       CountByInstitucionPrefixAsync(string prefijo, CancellationToken ct = default);
    Task<bool>                      CodigoExisteAsync(string codigo, CancellationToken ct = default);
    Task<HashSet<string>>           GetOrigenExternoIdsAsync(CancellationToken ct = default);
    Task                            AddAsync(Expediente expediente, CancellationToken ct = default);
    void                            Update(Expediente expediente);
    void                            Delete(Expediente expediente);
}

public interface IInstitucionRepository
{
    Task<IReadOnlyList<Institucion>>       GetAllActivasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Institucion>>       GetAllAsync(CancellationToken ct = default);
    Task<Institucion?>                     GetByIdAsync(int id, CancellationToken ct = default);
    Task<Institucion?>                     GetByIdWithTramitesAsync(int id, CancellationToken ct = default);
    Task<Institucion?>                     GetByNombreAsync(string nombre, CancellationToken ct = default);
    Task<IReadOnlyList<TramiteDefinicion>> GetTramitesAsync(int institucionId, CancellationToken ct = default);
    Task<bool>                             ExisteNombreAsync(string nombre, int? exceptoId = null, CancellationToken ct = default);
    Task<bool>                             TieneExpedientesAsync(int institucionId, CancellationToken ct = default);
    Task                                   AddAsync(Institucion institucion, CancellationToken ct = default);
    void                                   Update(Institucion institucion);
    void                                   Delete(Institucion institucion);
}

public interface IContactoRepository
{
    Task<Contacto?>               GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool>                    ExisteCorreoAsync(string correo, CancellationToken ct = default);
    Task                          AddAsync(Contacto contacto, CancellationToken ct = default);
    void                          Update(Contacto contacto);
    void                          Delete(Contacto contacto);
}

public interface IReunionRepository
{
    Task<Reunion?>               GetByIdAsync(int id, CancellationToken ct = default);
    Task<Reunion?>               GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
    Task<HashSet<string>>        GetOrigenExternoIdsAsync(CancellationToken ct = default);
    Task                         AddAsync(Reunion reunion, CancellationToken ct = default);
    void                         Update(Reunion reunion);
    void                         Delete(Reunion reunion);
}

public interface ITicketRepository
{
    Task<Ticket?>               GetByIdAsync(int id, CancellationToken ct = default);
    Task<Ticket?>               GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
    Task<int>                   CountByNumeroPrefixAsync(string prefijo, CancellationToken ct = default);
    Task<bool>                  NumeroExisteAsync(string numero, CancellationToken ct = default);
    Task                        AddAsync(Ticket ticket, CancellationToken ct = default);
    void                        Update(Ticket ticket);
    void                        Delete(Ticket ticket);
}

public interface IUsuarioRepository
{
    Task<Usuario?>               GetByIdAsync(int id, CancellationToken ct = default);
    Task<Usuario?>               GetByCorreoAsync(string correo, CancellationToken ct = default);
    Task<IReadOnlyList<Usuario>> GetByRolAsync(RolUsuario rol, bool soloActivos = true, CancellationToken ct = default);
    Task<IReadOnlyList<Usuario>> GetAllAsync(CancellationToken ct = default);
    Task<bool>                   ExisteCorreoAsync(string correo, int? exceptoId = null, CancellationToken ct = default);
    Task                         AddAsync(Usuario usuario, CancellationToken ct = default);
    void                         Update(Usuario usuario);

    // ── Alcance institucional ──────────────────────────────────────────────
    Task<IReadOnlyList<int>>     GetInstitucionIdsAsync(int usuarioId, CancellationToken ct = default);
    Task                         ReemplazarInstitucionesAsync(int usuarioId, IEnumerable<int> institucionIds, CancellationToken ct = default);
}

// ── Servicios de aplicación ───────────────────────────────────────────────
public interface ICurrentUserService
{
    int?        UserId   { get; }
    string?     Nombre   { get; }
    string?     Correo   { get; }
    RolUsuario? Rol      { get; }
    bool        IsAuthenticated { get; }

    /// <summary>Acceso global (Administrador, o procesos del sistema sin usuario).</summary>
    bool EsGlobal { get; }
    /// <summary>Instituciones a las que el usuario tiene alcance (vacío para usuarios sin asignación).</summary>
    IReadOnlyCollection<int> InstitucionesAsignadas { get; }
    /// <summary>True si el usuario puede acceder a un registro de la institución dada.</summary>
    bool PuedeAccederInstitucion(int? institucionId);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool   Verify(string password, string hash);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IApplicationDbContext
{
    DbSet<Institucion>              Instituciones { get; }
    DbSet<Usuario>                  Usuarios      { get; }
    DbSet<Contacto>                 Contactos     { get; }
    DbSet<Reunion>                  Reuniones     { get; }
    DbSet<Asistente>                Asistentes    { get; }
    DbSet<AcuerdoReunion>           Acuerdos      { get; }
    DbSet<Expediente>               Expedientes   { get; }
    DbSet<ExpedienteTramite>        Tramites      { get; }
    DbSet<TramiteRequisito>         Requisitos    { get; }
    DbSet<FlujoNodo>                Flujos        { get; }
    DbSet<FundamentoLegal>          Legal         { get; }
    DbSet<DocumentoSolicitado>      DocsSolicitados { get; }
    DbSet<DocumentoInterno>         DocsInternos    { get; }
    DbSet<InfraPerfil>              Perfiles      { get; }
    DbSet<InfraCondicion>           Condiciones   { get; }
    DbSet<InfraChecklistItem>       ChecklistInfra { get; }
    DbSet<ExpedienteSeccionEstado>  Secciones     { get; }
    DbSet<Ticket>                   Tickets       { get; }
    DbSet<TicketComentario>         TicketComentarios { get; }
    DbSet<UsuarioInstitucion>       UsuarioInstituciones { get; }
    Task<int> SaveChangesAsync(CancellationToken ct);
}
