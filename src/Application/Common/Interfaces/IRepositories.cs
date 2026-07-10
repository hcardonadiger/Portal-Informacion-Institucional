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
    Task<Institucion?>                     GetByIdAsync(string id, CancellationToken ct = default);
    Task<Institucion?>                     GetByIdWithTramitesAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<Institucion>>       GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct = default);
    Task<Institucion?>                     GetByNombreAsync(string nombre, CancellationToken ct = default);
    Task<IReadOnlyList<TramiteDefinicion>> GetTramitesAsync(string institucionId, CancellationToken ct = default);
    Task<IReadOnlyList<TramiteDefinicion>> GetAllTramitesAsync(CancellationToken ct = default);
    Task<bool>                             ExisteNombreAsync(string nombre, string? exceptoId = null, CancellationToken ct = default);
    Task<bool>                             TieneExpedientesAsync(string institucionId, CancellationToken ct = default);
    Task                                   AddAsync(Institucion institucion, CancellationToken ct = default);
    void                                   Update(Institucion institucion);
    void                                   Delete(Institucion institucion);
}

public interface IAreaRepository
{
    Task<IReadOnlyList<Area>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Area>> GetByInstitucionAsync(string institucionId, CancellationToken ct = default);
    Task<Area?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<bool> ExisteNombreAsync(string nombre, string institucionId, string? exceptoId = null, CancellationToken ct = default);
    Task AddAsync(Area area, CancellationToken ct = default);
    void Update(Area area);
    void Delete(Area area);
}

public interface IUnidadRepository
{
    Task<IReadOnlyList<Unidad>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Unidad>> GetByAreaAsync(string areaId, CancellationToken ct = default);
    Task<Unidad?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<bool> ExisteNombreAsync(string nombre, string areaId, string? exceptoId = null, CancellationToken ct = default);
    Task AddAsync(Unidad unidad, CancellationToken ct = default);
    void Update(Unidad unidad);
    void Delete(Unidad unidad);
}

public interface IContactoRepository
{
    Task<Contacto?>               GetByIdAsync(int id, CancellationToken ct = default);
    /// <summary>Busca por correo ignorando el alcance institucional (uso desde flujos anónimos, p. ej. el auto-registro público).</summary>
    Task<Contacto?>               GetByCorreoAsync(string correo, CancellationToken ct = default);
    Task<bool>                    ExisteCorreoAsync(string correo, CancellationToken ct = default);
    Task                          AddAsync(Contacto contacto, CancellationToken ct = default);
    void                          Update(Contacto contacto);
    void                          Delete(Contacto contacto);
}

public interface IReunionRepository
{
    Task<Reunion?>               GetByIdAsync(int id, CancellationToken ct = default);
    Task<Reunion?>               GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
    /// <summary>Reunión por token público de auto-registro. Ignora el filtro de alcance (acceso anónimo).</summary>
    Task<Reunion?>               GetByTokenWithAsistentesAsync(Guid token, CancellationToken ct = default);
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
    Task<Usuario?>               GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Usuario?>               GetByCorreoAsync(string correo, CancellationToken ct = default);
    Task<Usuario?>               GetByCertificadoThumbprintAsync(string thumbprint, CancellationToken ct = default);
    Task<IReadOnlyList<Usuario>> GetByRolAsync(RolUsuario rol, bool soloActivos = true, CancellationToken ct = default);
    Task<IReadOnlyList<Usuario>> GetAllAsync(CancellationToken ct = default);
    Task<bool>                   ExisteCorreoAsync(string correo, Guid? exceptoId = null, CancellationToken ct = default);
    Task                         AddAsync(Usuario usuario, CancellationToken ct = default);
    void                         Update(Usuario usuario);

    // ── Alcance (Jerarquía) ──────────────────────────────────────────────
    Task<IReadOnlyList<Diger.TramitesEstado.Application.Usuarios.Common.AsignacionDto>> GetAsignacionesAsync(Guid usuarioId, CancellationToken ct = default);
    Task ReemplazarAsignacionesAsync(Guid usuarioId, string rol, IEnumerable<Diger.TramitesEstado.Application.Usuarios.Common.AsignacionDto> asignaciones, CancellationToken ct = default);

    // ── Temas de ticket que atiende (especialidad) ─────────────────────────
    Task<IReadOnlyList<int>> GetTemaIdsAsync(Guid usuarioId, CancellationToken ct = default);
    Task                     ReemplazarTemasAsync(Guid usuarioId, IEnumerable<int> temaIds, CancellationToken ct = default);
}

// ── Servicios de aplicación ───────────────────────────────────────────────
public interface ICurrentUserService
{
    Guid?       UserId   { get; }
    string?     Nombre   { get; }
    string?     Correo   { get; }
    string?     Rol      { get; }
    bool        IsAuthenticated { get; }

    /// <summary>Acceso global (Administrador, o procesos del sistema sin usuario).</summary>
    bool EsGlobal { get; }
    /// <summary>Contexto activo: Institución.</summary>
    string? ActiveInstitucionId { get; }
    /// <summary>Contexto activo: Área.</summary>
    string? ActiveAreaId { get; }
    /// <summary>Contexto activo: Unidad.</summary>
    string? ActiveUnidadId { get; }
    
    /// <summary>Instituciones a las que el usuario tiene alcance.</summary>
    IReadOnlyCollection<string> InstitucionesAsignadas { get; }

    /// <summary>True si el usuario puede acceder a un registro de la institución dada en el contexto actual.</summary>
    bool PuedeAccederInstitucion(string? institucionId);
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
    DbSet<ExpedienteEtapaAvance>    ExpedienteEtapaAvances { get; }
    DbSet<Ticket>                   Tickets       { get; }
    DbSet<TicketComentario>         TicketComentarios { get; }
    DbSet<CategoriaTicket>          CategoriasTicket { get; }
    DbSet<TemaTicket>               TemasTicket   { get; }
    DbSet<UsuarioTema>              UsuarioTemas  { get; }
    DbSet<RolModuloAcceso>          RolModuloAccesos { get; }
    DbSet<AsignacionUsuario>        AsignacionesUsuario { get; }
    DbSet<Area>                     Areas               { get; }
    DbSet<Unidad>                   Unidades            { get; }
    DbSet<Movimiento>               Movimientos         { get; }
    DbSet<Prefijo>                  Prefijos            { get; }
    Task<int> SaveChangesAsync(CancellationToken ct);
}
