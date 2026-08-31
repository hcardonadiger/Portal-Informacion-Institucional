using MediatR;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diger.TramitesEstado.Infrastructure.Persistence;

// ── DbContext ─────────────────────────────────────────────────────────────
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUserService currentUser,
    IPublisher publisher)
    : DbContext(options), IApplicationDbContext, IUnitOfWork
{
    public DbSet<Institucion>              Instituciones      { get; init; } = default!;
    public DbSet<Area>                     Areas              { get; init; } = default!;
    public DbSet<Unidad>                   Unidades           { get; init; } = default!;
    public DbSet<AsignacionUsuario>        AsignacionesUsuario{ get; init; } = default!;
    public DbSet<Movimiento>               Movimientos        { get; init; } = default!;
    public DbSet<Prefijo>                  Prefijos           { get; init; } = default!;
    public DbSet<TramiteDefinicion>        TramitesDefinicion { get; init; } = default!;
    public DbSet<Usuario>                  Usuarios           { get; init; } = default!;
    public DbSet<Contacto>                 Contactos          { get; init; } = default!;
    public DbSet<Reunion>                  Reuniones          { get; init; } = default!;
    public DbSet<Asistente>                Asistentes         { get; init; } = default!;
    public DbSet<AcuerdoReunion>           Acuerdos           { get; init; } = default!;
    public DbSet<ComentarioCompromiso>     ComentariosCompromisos { get; init; } = default!;
    public DbSet<ReunionInstitucion>       ReunionInstituciones { get; init; } = default!;
    public DbSet<Expediente>               Expedientes        { get; init; } = default!;
    public DbSet<ExpedienteTramite>        Tramites           { get; init; } = default!;
    public DbSet<TramiteRequisito>         Requisitos         { get; init; } = default!;
    public DbSet<FlujoNodo>                Flujos             { get; init; } = default!;
    public DbSet<FundamentoLegal>          Legal              { get; init; } = default!;
    public DbSet<DocumentoSolicitado>      DocsSolicitados    { get; init; } = default!;
    public DbSet<DocumentoInterno>         DocsInternos       { get; init; } = default!;
    public DbSet<InfraPerfil>              Perfiles           { get; init; } = default!;
    public DbSet<InfraCondicion>           Condiciones        { get; init; } = default!;
    public DbSet<InfraChecklistItem>       ChecklistInfra     { get; init; } = default!;
    public DbSet<ExpedienteSeccionEstado>  Secciones          { get; init; } = default!;
    public DbSet<ExpedienteEtapaAvance>    ExpedienteEtapaAvances { get; init; } = default!;
    public DbSet<NotaSeguimientoExpediente> NotasSeguimiento  { get; init; } = default!;
    public DbSet<BitacoraExpediente>       BitacorasExpediente { get; init; } = default!;
    public DbSet<Ticket>                   Tickets            { get; init; } = default!;
    public DbSet<TicketComentario>         TicketComentarios  { get; init; } = default!;
    public DbSet<CategoriaTicket>          CategoriasTicket   { get; init; } = default!;
    public DbSet<TemaTicket>               TemasTicket        { get; init; } = default!;
    public DbSet<UsuarioTema>              UsuarioTemas         { get; init; } = default!;
    public DbSet<RolModuloAcceso>          RolModuloAccesos     { get; init; } = default!;
    public DbSet<Rol>                      Roles                { get; init; } = default!;
    public DbSet<Permiso>                  Permisos             { get; init; } = default!;
    public DbSet<RolPermiso>               RolPermisos          { get; init; } = default!;
    public DbSet<PermisoAuditoria>         PermisosAuditoria    { get; init; } = default!;
    public DbSet<PlantillaTramite>         PlantillasTramite    { get; init; } = default!;
    public DbSet<Notificacion>             Notificaciones       { get; init; } = default!;
    public DbSet<ChatSesion>                ChatSesiones          { get; init; } = default!;
    public DbSet<ChatMensaje>               ChatMensajes          { get; init; } = default!;
    public DbSet<ExpedienteEtapaCronograma> EtapaCronogramas      { get; init; } = default!;
    public DbSet<Levantamiento>             Levantamientos        { get; init; } = default!;
    public DbSet<TramiteChecklist>          TramitesChecklist     { get; init; } = default!;
    public DbSet<MiembroEquipo>             MiembrosEquipo        { get; init; } = default!;
    public DbSet<DocumentoAdjunto>          DocumentosAdjuntos    { get; init; } = default!;
    public DbSet<PlanTrabajo>               PlanTrabajos          { get; init; } = default!;
    public DbSet<MetaTramite>               MetasTrabajo          { get; init; } = default!;
    public DbSet<Recurso>                   Recursos              { get; init; } = default!;
    public DbSet<TramiteSiger>              TramitesSiger         { get; init; } = default!;
    public DbSet<PasoSiger>                 PasosSiger            { get; init; } = default!;
    public DbSet<CategoriaTramite>          CategoriasTramite     { get; init; } = default!;
    public DbSet<RequisitoSiger>            RequisitosSiger       { get; init; } = default!;
    public DbSet<EntregableSiger>           EntregablesSiger      { get; init; } = default!;
    public DbSet<LugarAtencionSiger>        LugaresAtencionSiger  { get; init; } = default!;
    public DbSet<EnlaceSiger>               EnlacesSiger          { get; init; } = default!;
    public DbSet<TareaDigitalizacionSiger>  TareasDigitalizacionSiger { get; init; } = default!;
    public DbSet<ConciliacionSiger>         ConciliacionesSiger   { get; init; } = default!;
    public DbSet<Proyecto>                  Proyectos             { get; init; } = default!;
    public DbSet<EntregableProyecto>        ProyectoEntregables   { get; init; } = default!;
    public DbSet<ActividadProyecto>         ProyectoActividades   { get; init; } = default!;
    public DbSet<AvanceProyecto>            ProyectoAvances       { get; init; } = default!;
    public DbSet<DependenciaActividad>      ProyectoDependencias  { get; init; } = default!;
    public DbSet<CategoriaDocumento>        CategoriasDocumento   { get; init; } = default!;
    public DbSet<DocumentoProyecto>         ProyectoDocumentos    { get; init; } = default!;
    public DbSet<VersionDocumento>          ProyectoDocumentoVersiones { get; init; } = default!;
    public DbSet<DescargaDocumento>         ProyectoDocumentoDescargas { get; init; } = default!;
    public DbSet<BitacoraProyecto>          BitacorasProyecto     { get; init; } = default!;
    public DbSet<RiesgoProyecto>            ProyectoRiesgos       { get; init; } = default!;
    public DbSet<InteresadoProyecto>        ProyectoInteresados   { get; init; } = default!;

    // Alcance institucional del usuario actual (se evalúa una vez por request al crear el contexto).
    private readonly bool    _alcanceGlobal = currentUser.EsGlobal;
    private readonly string? _activeInst    = currentUser.ActiveInstitucionId;
    private readonly string? _activeArea    = currentUser.ActiveAreaId;
    private readonly string? _activeUnidad  = currentUser.ActiveUnidadId;
    private readonly Guid?   _usuarioId     = currentUser.UserId;
    // Alcance y solo-lectura vienen de la tabla Roles (vía IRolCatalogo), no del nombre
    // del rol: así un rol creado desde /Accesos/Roles funciona en RLS sin tocar código.
    private readonly NivelAlcance _nivel    = currentUser.NivelAlcance;
    private readonly bool    _esSoloLectura = currentUser.EsSoloLectura;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ── Colación insensible a tildes (Script F del plan de la Ventanilla Digital) ──
        // Solo aplica en SQL Server: es un nombre de colación propio del motor y SQLite
        // (que usan los Web.Tests vía EnsureCreated) no lo reconoce — "no such collation
        // sequence". Las columnas sobre las que busca ?busqueda= (decisión P-07, Fase 0).
        if (Database.IsSqlServer())
        {
            mb.Entity<TramiteSiger>().Property(x => x.Nombre).UseCollation("Modern_Spanish_CI_AI");
            mb.Entity<TramiteSiger>().Property(x => x.Institucion).UseCollation("Modern_Spanish_CI_AI");
            mb.Entity<TramiteSiger>().Property(x => x.Descripcion).UseCollation("Modern_Spanish_CI_AI");
            mb.Entity<TramiteSiger>().Property(x => x.Objetivo).UseCollation("Modern_Spanish_CI_AI");
            mb.Entity<Institucion>().Property(x => x.Nombre).UseCollation("Modern_Spanish_CI_AI");
        }

        // ── Filtros Globales RLS (Row-Level Security) ───────────────────────
        // El alcance lo determina NivelAlcance del rol (tabla Roles), no su nombre:
        //   Institucion: ve todo de su institución.
        //   Area:        ve todo lo asignado a su área.
        //   Unidad:      ve lo asignado a su unidad.
        //   Global (_alcanceGlobal = true): ve todo, sin filtro.
        // Un rol sin resolver cae en Unidad (lo más restrictivo) — ver CurrentUserService.

        // ── Filtros RLS + Soft-Delete (fusionados) ─────────────────────────
        // Soft-Delete (!IsDeleted) se AND-ea con el filtro RLS existente.
        // COMPATIBILIDAD ESCALABILIDAD: al añadir jerarquía futura (Área/Unidad),
        // solo hay que extender la condición RLS; el !IsDeleted permanece invariante.

        // El ancla `InstitucionId == _activeInst` envuelve TODAS las ramas de rol no-global:
        // sin él, JefeUnidad/JefeArea filtraban solo por Unidad/Área y el `|| == null` dejaba
        // ver registros de otras instituciones (fuga cross-institución). Ver auditoría A-1.
        mb.Entity<Expediente>().HasQueryFilter(e => !e.IsDeleted && (
            _alcanceGlobal ||
            (e.InstitucionId == _activeInst && (
                (_nivel == NivelAlcance.Institucion &&
                    (string.IsNullOrEmpty(_activeArea) || e.AreaId == _activeArea || e.AreaId == null) &&
                    (string.IsNullOrEmpty(_activeUnidad) || e.UnidadId == _activeUnidad || e.UnidadId == null)) ||
                (_nivel == NivelAlcance.Area      && (e.AreaId == _activeArea || e.AreaId == null) &&
                    (string.IsNullOrEmpty(_activeUnidad) || e.UnidadId == _activeUnidad || e.UnidadId == null)) ||
                (_nivel == NivelAlcance.Unidad    && (e.UnidadId == _activeUnidad || e.UnidadId == null))
            ))
        ));

        mb.Entity<Contacto>().HasQueryFilter(c => !c.IsDeleted && (
            _alcanceGlobal ||
            (c.InstitucionId == _activeInst && (
                (_nivel == NivelAlcance.Institucion &&
                    (string.IsNullOrEmpty(_activeArea) || c.AreaId == _activeArea || c.AreaId == null) &&
                    (string.IsNullOrEmpty(_activeUnidad) || c.UnidadId == _activeUnidad || c.UnidadId == null)) ||
                (_nivel == NivelAlcance.Area      && (c.AreaId == _activeArea || c.AreaId == null) &&
                    (string.IsNullOrEmpty(_activeUnidad) || c.UnidadId == _activeUnidad || c.UnidadId == null)) ||
                (_nivel == NivelAlcance.Unidad    && (c.UnidadId == _activeUnidad || c.UnidadId == null))
            ))
        ));

        mb.Entity<Ticket>().HasQueryFilter(t => !t.IsDeleted && (
            _alcanceGlobal ||
            (t.InstitucionId == _activeInst && (
                (_nivel == NivelAlcance.Institucion &&
                    (string.IsNullOrEmpty(_activeArea) || t.AreaId == _activeArea) &&
                    (string.IsNullOrEmpty(_activeUnidad) || t.UnidadId == _activeUnidad)) ||
                (_nivel == NivelAlcance.Area      && t.AreaId == _activeArea &&
                    (string.IsNullOrEmpty(_activeUnidad) || t.UnidadId == _activeUnidad)) ||
                (_nivel == NivelAlcance.Unidad    && t.UnidadId == _activeUnidad)
            ))
        ));

        // Reuniones: las públicas respetan la jerarquía, las privadas solo las ve el creador.
        // Soft-Delete se evalúa primero para corto-circuitar todo el filtro si IsDeleted=true.
        mb.Entity<Reunion>().HasQueryFilter(r => !r.IsDeleted && (
            (r.Visibilidad != VisibilidadReunion.Privada && (
                _alcanceGlobal ||
                (r.InstitucionId == _activeInst && (
                    (_nivel == NivelAlcance.Institucion &&
                        (string.IsNullOrEmpty(_activeArea) || r.AreaId == _activeArea || r.AreaId == null) &&
                        (string.IsNullOrEmpty(_activeUnidad) || r.UnidadId == _activeUnidad || r.UnidadId == null)) ||
                    (_nivel == NivelAlcance.Area      && (r.AreaId == _activeArea || r.AreaId == null) &&
                        (string.IsNullOrEmpty(_activeUnidad) || r.UnidadId == _activeUnidad || r.UnidadId == null)) ||
                    (_nivel == NivelAlcance.Unidad    && (r.UnidadId == _activeUnidad || r.UnidadId == null))
                ))
            )) ||
            (r.Visibilidad == VisibilidadReunion.Privada && r.CreadoPorId != null && r.CreadoPorId == _usuarioId)
        ));

        mb.Entity<Area>().HasQueryFilter(a =>
            _alcanceGlobal || a.InstitucionId == _activeInst
        );

        mb.Entity<Unidad>().HasQueryFilter(u =>
            _alcanceGlobal || Areas.Any(a => a.Id == u.AreaId && a.InstitucionId == _activeInst)
        );

        mb.Entity<Institucion>().HasQueryFilter(i =>
            _alcanceGlobal || i.Id == _activeInst
        );

        mb.Entity<TramiteDefinicion>().HasQueryFilter(t =>
            _alcanceGlobal || t.InstitucionId == _activeInst
        );
        // Plan de Trabajo: institución-nivel (sin subdivisión área/unidad)
        mb.Entity<PlanTrabajo>().HasQueryFilter(p => !p.IsDeleted && (
            _alcanceGlobal || p.InstitucionId == _activeInst
        ));
        mb.Entity<Recurso>().HasQueryFilter(r => !r.IsDeleted);

        // Proyectos: mismo anclaje en InstitucionId que Expediente y Contacto —incluida la razón
        // de por qué el ancla envuelve todas las ramas— más una excepción propia: el responsable
        // ve su proyecto aunque caiga fuera de su alcance. Sin ella alguien puede quedar como
        // responsable de un proyecto que no puede abrir, y las acciones reservadas al propietario
        // (reordenar la estructura, corregir bitácora) serían inalcanzables para el único autorizado.
        //
        // Hasta el 2026-08-23 esta entidad no tenía filtro: el portafolio completo quedaba a la
        // vista de cualquiera con Proyectos.Ver, incluidos los usuarios de instituciones externas
        // con rol Empleado.
        //
        // Segunda excepción, del 2026-08-24: los interesados también ven su proyecto fuera de su
        // alcance. Es el motivo por el que InteresadoProyecto.UsuarioId pasó a ser obligatorio —
        // un interesado sin cuenta no podría abrir nada. Se puede consultar ProyectoInteresados
        // desde acá sin caer en recursión porque esa entidad no tiene filtro propio: hereda la
        // protección de que sus consultas se unen contra Proyectos.
        mb.Entity<Proyecto>().HasQueryFilter(p => !p.IsDeleted && (
            _alcanceGlobal ||
            (_usuarioId != null && p.ResponsableId == _usuarioId) ||
            (_usuarioId != null && ProyectoInteresados.Any(i => i.ProyectoId == p.Id && i.UsuarioId == _usuarioId)) ||
            (p.InstitucionId == _activeInst && (
                (_nivel == NivelAlcance.Institucion &&
                    (string.IsNullOrEmpty(_activeArea) || p.AreaId == _activeArea || p.AreaId == null) &&
                    (string.IsNullOrEmpty(_activeUnidad) || p.UnidadId == _activeUnidad || p.UnidadId == null)) ||
                (_nivel == NivelAlcance.Area      && (p.AreaId == _activeArea || p.AreaId == null) &&
                    (string.IsNullOrEmpty(_activeUnidad) || p.UnidadId == _activeUnidad || p.UnidadId == null)) ||
                (_nivel == NivelAlcance.Unidad    && (p.UnidadId == _activeUnidad || p.UnidadId == null))
            ))
        ));

        // Repositorio documental: el documento NO reimplementa el alcance del proyecto, lo hereda.
        // Al referenciar el DbSet Proyectos dentro del filtro, EF aplica también el filtro de esa
        // entidad, así que un documento se ve exactamente cuando se ve su proyecto —incluidas las
        // dos excepciones, el responsable y los interesados—. Copiar aquí las mismas ramas habría
        // sido la forma segura de que las dos copias se separaran con el tiempo.
        //
        // No hay confidencialidad por documento: decisión explícita del 2026-08-26. Quien puede
        // abrir el proyecto puede leer su documentación.
        mb.Entity<DocumentoProyecto>().HasQueryFilter(d => !d.IsDeleted && Proyectos.Any(p => p.Id == d.ProyectoId));

        // La versión se alcanza normalmente a través de su documento, pero una consulta directa a
        // ProyectoDocumentoVersiones se saltaría todo. Se ancla igual, por el documento —que a su
        // vez cuelga del proyecto—: es el mismo descuido que en SGSEC dejó escrituras sin alcance
        // y que solo apareció auditando por reflexión en vez de confiar en la memoria.
        mb.Entity<VersionDocumento>().HasQueryFilter(v => ProyectoDocumentos.Any(d => d.Id == v.DocumentoId));

        // La bitácora de descargas se ancla por la versión —que a su vez cuelga del documento y
        // este del proyecto—, por el mismo motivo que la versión: una consulta directa a
        // ProyectoDocumentoDescargas se saltaría todo el alcance y diría quién descargó qué en
        // proyectos que el usuario no puede ni abrir.
        mb.Entity<DescargaDocumento>().HasQueryFilter(x => ProyectoDocumentoVersiones.Any(v => v.Id == x.VersionId));

        // El catálogo de categorías es global y no lleva alcance: son etiquetas, no datos de
        // ninguna institución.

        base.OnModelCreating(mb);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // ── Bloqueo de seguridad duro para roles de solo lectura ───────────────
        var hasMutations = ChangeTracker.Entries().Any(e =>
            e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);

        if (hasMutations && _esSoloLectura)
        {
            throw new UnauthorizedAccessException("El rol activo es de solo lectura y no puede mutar datos.");
        }

        // ── Validación de seguridad dura para mutaciones de Áreas y Unidades por Institución ──
        if (!_alcanceGlobal)
        {
            foreach (var entry in ChangeTracker.Entries<Area>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
            {
                if (entry.Entity.InstitucionId != _activeInst)
                {
                    throw new UnauthorizedAccessException($"No tiene permisos para gestionar áreas en la institución {entry.Entity.InstitucionId}.");
                }
            }

            foreach (var entry in ChangeTracker.Entries<Unidad>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
            {
                var areaId = entry.Entity.AreaId;
                var area = Areas.IgnoreQueryFilters().FirstOrDefault(a => a.Id == areaId);
                if (area == null || area.InstitucionId != _activeInst)
                {
                    throw new UnauthorizedAccessException($"No tiene permisos para gestionar unidades en el área {areaId}.");
                }
            }
        }

        // ── Inyección automática de jerarquía institucional en inserciones ──────
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            var instProp = entry.Metadata.FindProperty("InstitucionId");
            if (instProp != null && string.IsNullOrEmpty(entry.Property("InstitucionId").CurrentValue as string))
            {
                entry.Property("InstitucionId").CurrentValue = _activeInst;
            }

            var areaProp = entry.Metadata.FindProperty("AreaId");
            if (areaProp != null && string.IsNullOrEmpty(entry.Property("AreaId").CurrentValue as string))
            {
                entry.Property("AreaId").CurrentValue = _activeArea;
            }

            var unidadProp = entry.Metadata.FindProperty("UnidadId");
            if (unidadProp != null && string.IsNullOrEmpty(entry.Property("UnidadId").CurrentValue as string))
            {
                entry.Property("UnidadId").CurrentValue = _activeUnidad;
            }
        }

        // ── Soft-Delete: convierte eliminaciones físicas en borrado lógico ──────
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>()
            .Where(e => e.State == EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
        }

        // ── Auditoría automática ──────────────────────────────────────────────
        var actor = currentUser.Nombre ?? currentUser.Correo;
        foreach (var entry in ChangeTracker.Entries<BaseAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.CreatedBy = actor;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
                entry.Entity.UpdatedBy = actor;
            }
        }
        var result = await base.SaveChangesAsync(ct);

        // ── Dispatch de domain events ────────────────────────────────────────
        // Se despacha después de guardar: los Ids ya están asignados y solo se
        // publican eventos de una escritura que efectivamente ocurrió.
        var conEventos = ChangeTracker.Entries<IHasDomainEvents>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();
        var eventos = conEventos.SelectMany(e => e.DomainEvents).ToList();
        conEventos.ForEach(e => e.ClearDomainEvents());
        foreach (var ev in eventos)
            await publisher.Publish(ev, ct);

        return result;
    }
}

// ── Catálogo: Instituciones ───────────────────────────────────────────────
public sealed class InstitucionConfiguration : IEntityTypeConfiguration<Institucion>
{
    public void Configure(EntityTypeBuilder<Institucion> b)
    {
        b.ToTable("Instituciones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(120).ValueGeneratedNever();
        b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        b.Property(x => x.NombreCorto).HasMaxLength(30);
        b.HasIndex(x => x.Nombre).IsUnique();
        b.HasMany(x => x.Tramites).WithOne()
            .HasForeignKey(t => t.InstitucionId).OnDelete(DeleteBehavior.Cascade);

        // ── Contacto institucional (plan Fase 1, script D) ─────────────────
        b.Property(x => x.Telefono).HasMaxLength(60);
        b.Property(x => x.SitioWeb).HasMaxLength(300);
        b.Property(x => x.Direccion).HasMaxLength(300);
        b.Property(x => x.Horario).HasMaxLength(200);
        b.Property(x => x.Tipo).HasMaxLength(60);
        b.ToTable(t => t.HasCheckConstraint("CK_Instituciones_SitioWeb",
            "[SitioWeb] IS NULL OR [SitioWeb] LIKE 'http://%' OR [SitioWeb] LIKE 'https://%'"));

        b.HasData(Seed.Instituciones);
    }
}

public sealed class TramiteDefinicionConfiguration : IEntityTypeConfiguration<TramiteDefinicion>
{
    public void Configure(EntityTypeBuilder<TramiteDefinicion> b)
    {
        b.ToTable("TramitesDefinicion");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasMaxLength(400).IsRequired();
        b.HasIndex(x => new { x.InstitucionId, x.Orden });
        b.HasData(Seed.TramitesDefinicion);
    }
}

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
        b.Property(x => x.Correo).HasMaxLength(200).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(300).IsRequired();
        b.Property(x => x.PasswordResetToken).HasMaxLength(256);
        b.Property(x => x.PasswordResetTokenExpiration);
        b.HasIndex(x => x.Correo).IsUnique();
    }
}

public sealed class ContactoConfiguration : IEntityTypeConfiguration<Contacto>
{
    public void Configure(EntityTypeBuilder<Contacto> b)
    {
        b.ToTable("Contactos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
        b.Property(x => x.Institucion).HasMaxLength(120).IsRequired();
        b.Property(x => x.Area).HasMaxLength(150);
        b.Property(x => x.Cargo).HasMaxLength(150);
        b.Property(x => x.Correo).HasMaxLength(200);
        b.Property(x => x.Telefono).HasMaxLength(40);
        b.Property(x => x.Notas).HasMaxLength(1000);
        b.Property(x => x.Origen).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.InstitucionId).IsRequired();
        b.Property(x => x.AreaId).HasMaxLength(120);
        b.Property(x => x.UnidadId).HasMaxLength(120);
        b.Property(x => x.Activo).HasDefaultValue(true);
        b.HasOne<Institucion>().WithMany()
            .HasForeignKey(x => x.InstitucionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Area>().WithMany()
            .HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Unidad>().WithMany()
            .HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.InstitucionId);
        b.HasIndex(x => x.Institucion);
        b.HasIndex(x => x.Nombre);
    }
}

public sealed class ReunionConfiguration : IEntityTypeConfiguration<Reunion>
{
    public void Configure(EntityTypeBuilder<Reunion> b)
    {
        b.ToTable("Reuniones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Titulo).HasMaxLength(250).IsRequired();
        b.Property(x => x.OrigenExternoId).HasMaxLength(60);
        b.Property(x => x.Hora).HasMaxLength(20);
        b.Property(x => x.Duracion).HasMaxLength(60);
        b.Property(x => x.Modalidad).HasMaxLength(40);
        b.Property(x => x.Lugar).HasMaxLength(250);
        b.Property(x => x.Institucion).HasMaxLength(120);
        b.Property(x => x.Tipo).HasMaxLength(60);
        b.Property(x => x.ObjetivoAgenda).HasMaxLength(4000);
        b.Property(x => x.Desarrollo); // nvarchar(max): guarda HTML del editor enriquecido
        b.Property(x => x.Tema).HasMaxLength(250);
        b.Property(x => x.ObjetivoCap).HasMaxLength(2000);
        b.Property(x => x.Contenido).HasMaxLength(4000);
        b.Property(x => x.EpNombre).HasMaxLength(150);
        b.Property(x => x.EpCargo).HasMaxLength(150);
        b.Property(x => x.EpCorreo).HasMaxLength(200);
        b.Property(x => x.EpTel).HasMaxLength(40);
        b.Property(x => x.FacNombre).HasMaxLength(150);
        b.Property(x => x.FacCargo).HasMaxLength(150);
        b.Property(x => x.FacCorreo).HasMaxLength(200);
        b.Property(x => x.Satisfaccion).HasMaxLength(60);
        b.Property(x => x.Compromisos).HasMaxLength(4000);
        b.Property(x => x.ValDiger).HasMaxLength(200);
        b.Property(x => x.ValInst).HasMaxLength(200);
        b.Property(x => x.DocsRecursos).HasMaxLength(4000);
        b.Property(x => x.Foto1Url).HasMaxLength(600);
        b.Property(x => x.Foto1Desc).HasMaxLength(300);
        b.Property(x => x.Foto2Url).HasMaxLength(600);
        b.Property(x => x.Foto2Desc).HasMaxLength(300);

        b.Property(x => x.RegistroToken).HasDefaultValueSql("NEWID()");
        b.Property(x => x.Visibilidad).HasConversion<string>().HasMaxLength(20).HasDefaultValue(VisibilidadReunion.Publica);

        b.HasIndex(x => x.Fecha);
        b.HasIndex(x => x.HiloId);
        b.HasIndex(x => new { x.Visibilidad, x.CreadoPorId });
        b.HasIndex(x => x.RegistroToken).IsUnique();
        b.HasIndex(x => x.OrigenExternoId)
            .IsUnique()
            .HasFilter("[OrigenExternoId] IS NOT NULL");
        b.HasOne<Institucion>().WithMany()
            .HasForeignKey(x => x.InstitucionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Area>().WithMany()
            .HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Unidad>().WithMany()
            .HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>().WithMany()
            .HasForeignKey(x => x.CreadoPorId).OnDelete(DeleteBehavior.SetNull);

        b.HasMany(x => x.Asistentes).WithOne()
            .HasForeignKey(a => a.ReunionId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Acuerdos).WithOne()
            .HasForeignKey(a => a.ReunionId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.InstitucionesParticipantes).WithOne()
            .HasForeignKey(x => x.ReunionId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Instituciones convocadas a una reunión (join, acumulable — ver <see cref="Reunion.AgregarInstitucion"/>).</summary>
public sealed class ReunionInstitucionConfiguration : IEntityTypeConfiguration<ReunionInstitucion>
{
    public void Configure(EntityTypeBuilder<ReunionInstitucion> b)
    {
        b.ToTable("ReunionInstituciones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasIndex(x => new { x.ReunionId, x.InstitucionId }).IsUnique();
        b.HasOne<Institucion>().WithMany()
            .HasForeignKey(x => x.InstitucionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AsistenteConfiguration : IEntityTypeConfiguration<Asistente>
{
    public void Configure(EntityTypeBuilder<Asistente> b)
    {
        b.ToTable("Asistentes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
        b.Property(x => x.Cargo).HasMaxLength(150);
        b.Property(x => x.InstitucionId).HasMaxLength(120);
        b.Property(x => x.Institucion).HasMaxLength(120);
        b.Property(x => x.Departamento).HasMaxLength(150);
        b.Property(x => x.Correo).HasMaxLength(200);
        b.Property(x => x.Telefono).HasMaxLength(40);
        b.Property(x => x.EsPreregistro).HasDefaultValue(false);
        b.Property(x => x.Confirmado);
        b.HasIndex(x => x.ReunionId);
        b.HasIndex(x => x.EsPreregistro);
    }
}

public sealed class AcuerdoReunionConfiguration : IEntityTypeConfiguration<AcuerdoReunion>
{
    public void Configure(EntityTypeBuilder<AcuerdoReunion> b)
    {
        b.ToTable("AcuerdosReunion");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Compromiso).HasMaxLength(500).IsRequired();
        b.Property(x => x.Responsable).HasMaxLength(200);
        // FK al directorio de contactos; NoAction porque Contacto usa soft-delete
        b.HasOne<Contacto>().WithMany()
            .HasForeignKey(x => x.ResponsableContactoId)
            .OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => x.ResponsableContactoId);
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20).HasDefaultValue(EstadoCompromiso.Pendiente);
        b.Property(x => x.NotaSeguimiento).HasMaxLength(1000);
        b.Property(x => x.SeguimientoActualizadoPor).HasMaxLength(150);
        b.HasIndex(x => x.ReunionId);
        b.HasIndex(x => x.Estado);
        b.HasIndex(x => x.Plazo);
    }
}

public sealed class ComentarioCompromisoConfiguration : IEntityTypeConfiguration<ComentarioCompromiso>
{
    public void Configure(EntityTypeBuilder<ComentarioCompromiso> b)
    {
        b.ToTable("ComentariosCompromisos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Comentario).HasMaxLength(4000);
        b.Property(x => x.ArchivoNombre).HasMaxLength(255);
        b.Property(x => x.ArchivoUrl).HasMaxLength(1000);
        b.Property(x => x.CreadoPor).HasMaxLength(200).IsRequired();
        b.Property(x => x.CreadoPorRol).HasMaxLength(100);
        b.HasOne(x => x.Acuerdo).WithMany(a => a.Comentarios).HasForeignKey(x => x.AcuerdoReunionId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.AcuerdoReunionId);
    }
}

// ── Expediente (aggregate root) ───────────────────────────────────────────
public sealed class ExpedienteConfiguration : IEntityTypeConfiguration<Expediente>
{
    public void Configure(EntityTypeBuilder<Expediente> b)
    {
        b.ToTable("Expedientes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();

        b.Property(x => x.Codigo).HasMaxLength(40).IsRequired();
        b.Property(x => x.OrigenExternoId).HasMaxLength(120);
        b.Property(x => x.Institucion).HasMaxLength(120).IsRequired();
        b.Property(x => x.Analista).HasMaxLength(150).IsRequired();
        b.Property(x => x.DirSede).HasMaxLength(300);
        b.Property(x => x.ContactoNombre).HasMaxLength(150);
        b.Property(x => x.ContactoCargo).HasMaxLength(150);
        b.Property(x => x.ContactoCorreo).HasMaxLength(200);
        b.Property(x => x.ContactoTel).HasMaxLength(40);
        b.Property(x => x.ObsLegal).HasMaxLength(4000);
        b.Property(x => x.TiempoObservado).HasMaxLength(100);
        b.Property(x => x.TiempoNorma).HasMaxLength(100);
        b.Property(x => x.DescProceso).HasMaxLength(4000);
        b.Property(x => x.DocsAdicionales).HasMaxLength(2000);
        b.Property(x => x.ObsFlujo).HasMaxLength(2000);
        b.Property(x => x.TiempoDig).HasMaxLength(100);
        b.Property(x => x.ObsModelo).HasMaxLength(2000);
        b.Property(x => x.InfraPersonal).HasMaxLength(30);
        b.Property(x => x.InfraRespSol).HasMaxLength(200);
        b.Property(x => x.InfraAcomp).HasMaxLength(10);
        b.Property(x => x.InfraDcModalidad).HasMaxLength(60);
        b.Property(x => x.InfraDcVirt).HasMaxLength(60);
        b.Property(x => x.InfraDcVirtOtro).HasMaxLength(120);
        b.Property(x => x.InfraDcDisp).HasMaxLength(60);
        b.Property(x => x.InfraDcObs).HasMaxLength(2000);
        b.Property(x => x.InfraPlan).HasMaxLength(4000);
        b.Property(x => x.ObsExpediente).HasMaxLength(2000);
        b.Property(x => x.ObsLevantamiento).HasMaxLength(2000);
        b.Property(x => x.ValidadoDiger).HasMaxLength(150);
        b.Property(x => x.ValidadoInst).HasMaxLength(200);
        b.Property(x => x.NumActa).HasMaxLength(60);

        b.Property(x => x.EstadoExpediente).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.EstadoLevantamiento).HasConversion<string>().HasMaxLength(30);

        b.HasIndex(x => x.Codigo).IsUnique();
        b.HasIndex(x => x.OrigenExternoId)
            .IsUnique()
            .HasFilter("[OrigenExternoId] IS NOT NULL");
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => x.EstadoExpediente);

        b.HasOne<Institucion>().WithMany()
            .HasForeignKey(x => x.InstitucionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Area>().WithMany()
            .HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Unidad>().WithMany()
            .HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Tramites).WithOne().HasForeignKey(t => t.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Requisitos).WithOne().HasForeignKey(t => t.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Flujos).WithOne().HasForeignKey(t => t.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Legal).WithOne().HasForeignKey(t => t.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.DocsSolicitados).WithOne().HasForeignKey(t => t.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.DocsInternos).WithOne().HasForeignKey(t => t.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Perfiles).WithOne().HasForeignKey(t => t.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Condiciones).WithOne().HasForeignKey(t => t.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.ChecklistInfra).WithOne().HasForeignKey(t => t.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Secciones).WithOne().HasForeignKey(t => t.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ExpedienteTramiteConfiguration : IEntityTypeConfiguration<ExpedienteTramite>
{
    public void Configure(EntityTypeBuilder<ExpedienteTramite> b)
    {
        b.ToTable("ExpedienteTramites");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.NombreTramite).HasMaxLength(400).IsRequired();
        b.Property(x => x.NombreCorto).HasMaxLength(120);
        b.Property(x => x.AreaResponsable).HasMaxLength(200);
        b.Property(x => x.Modalidad).HasMaxLength(60);
        b.Property(x => x.PlazoLegal).HasMaxLength(100);
        b.Property(x => x.Tercero).HasMaxLength(200);
        b.Property(x => x.TiempoReal).HasMaxLength(100);
        b.Property(x => x.MetodoPago).HasMaxLength(60);
        b.Property(x => x.PagoBanco).HasMaxLength(120);
        b.Property(x => x.PagoCuenta).HasMaxLength(60);
        b.Property(x => x.TgrInst).HasMaxLength(200);
        b.Property(x => x.TgrRubro).HasMaxLength(200);
        b.Property(x => x.TgrMonto).HasMaxLength(60);
        b.Property(x => x.DocEntregado).HasMaxLength(300);
        b.Property(x => x.Objetivo).HasMaxLength(2000);
        b.Property(x => x.Alcance).HasMaxLength(60);
        b.Property(x => x.AlcanceObs).HasMaxLength(2000);
        b.Property(x => x.Descripcion).HasMaxLength(4000);
        b.Property(x => x.Dirigido).HasMaxLength(300);
        b.Property(x => x.Horario).HasMaxLength(120);
        b.Property(x => x.Telefono).HasMaxLength(60);
        b.Property(x => x.EmailTramite).HasMaxLength(200);
        b.Property(x => x.SitioWeb).HasMaxLength(300);
        b.Property(x => x.EstadoTramite).HasDefaultValue(EstadoTramite.Pendiente);
        b.HasOne<TramiteSiger>().WithMany()
            .HasForeignKey(x => x.TramiteSigerId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.TramiteSigerId);
        b.HasIndex(x => new { x.ExpedienteId, x.TramiteIndex });
    }
}

public sealed class TramiteRequisitoConfiguration : IEntityTypeConfiguration<TramiteRequisito>
{
    public void Configure(EntityTypeBuilder<TramiteRequisito> b)
    {
        b.ToTable("TramiteRequisitos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Requisito).HasMaxLength(500).IsRequired();
        b.Property(x => x.Obs).HasMaxLength(2000);
        b.Property(x => x.Justificacion).HasMaxLength(2000);
        b.Property(x => x.Accion).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.EsPersonalizado).HasDefaultValue(false);
        b.HasIndex(x => new { x.ExpedienteId, x.TramiteIndex });
    }
}

public sealed class FlujoNodoConfiguration : IEntityTypeConfiguration<FlujoNodo>
{
    public void Configure(EntityTypeBuilder<FlujoNodo> b)
    {
        b.ToTable("FlujoNodos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Fase).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Titulo).HasMaxLength(300);
        b.Property(x => x.Area).HasMaxLength(200);
        b.Property(x => x.Tiempo).HasMaxLength(100);
        b.Property(x => x.DocEmitido).HasMaxLength(300);
        b.Property(x => x.Obs).HasMaxLength(2000);
        b.Property(x => x.RetornoA).HasMaxLength(100);
        b.HasIndex(x => new { x.ExpedienteId, x.TramiteIndex, x.Fase });
    }
}

public sealed class FundamentoLegalConfiguration : IEntityTypeConfiguration<FundamentoLegal>
{
    public void Configure(EntityTypeBuilder<FundamentoLegal> b)
    {
        b.ToTable("FundamentosLegales");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Instrumento).HasMaxLength(400).IsRequired();
        b.Property(x => x.Articulos).HasMaxLength(300);
        b.Property(x => x.Obs).HasMaxLength(1000);
        b.Property(x => x.EsPersonalizado).HasDefaultValue(false);
        b.HasIndex(x => x.ExpedienteId);
    }
}

public sealed class PlantillaTramiteConfiguration : IEntityTypeConfiguration<PlantillaTramite>
{
    public void Configure(EntityTypeBuilder<PlantillaTramite> b)
    {
        b.ToTable("PlantillasTramite");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasMaxLength(300).IsRequired();
        b.Property(x => x.Activa).HasDefaultValue(true);
        b.HasIndex(x => x.Nombre).IsUnique();

        b.HasMany(x => x.Legal).WithOne().HasForeignKey(l => l.PlantillaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Requisitos).WithOne().HasForeignKey(r => r.PlantillaId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PlantillaFundamentoLegalConfiguration : IEntityTypeConfiguration<PlantillaFundamentoLegal>
{
    public void Configure(EntityTypeBuilder<PlantillaFundamentoLegal> b)
    {
        b.ToTable("PlantillaFundamentosLegales");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Instrumento).HasMaxLength(400).IsRequired();
        b.Property(x => x.Articulos).HasMaxLength(300);
        b.Property(x => x.Obs).HasMaxLength(1000);
        b.HasIndex(x => x.PlantillaId);
    }
}

public sealed class PlantillaRequisitoConfiguration : IEntityTypeConfiguration<PlantillaRequisito>
{
    public void Configure(EntityTypeBuilder<PlantillaRequisito> b)
    {
        b.ToTable("PlantillaRequisitos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Requisito).HasMaxLength(500).IsRequired();
        b.Property(x => x.Obs).HasMaxLength(2000);
        b.HasIndex(x => x.PlantillaId);
    }
}

public sealed class DocumentoSolicitadoConfiguration : IEntityTypeConfiguration<DocumentoSolicitado>
{
    public void Configure(EntityTypeBuilder<DocumentoSolicitado> b)
    {
        b.ToTable("DocumentosSolicitados");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasMaxLength(300).IsRequired();
        b.Property(x => x.Tipo).HasMaxLength(60);
        b.Property(x => x.Url).HasMaxLength(600);
        b.HasIndex(x => x.ExpedienteId);
    }
}

public sealed class DocumentoInternoConfiguration : IEntityTypeConfiguration<DocumentoInterno>
{
    public void Configure(EntityTypeBuilder<DocumentoInterno> b)
    {
        b.ToTable("DocumentosInternos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Documento).HasMaxLength(300).IsRequired();
        b.Property(x => x.Area).HasMaxLength(200);
        b.Property(x => x.Obs).HasMaxLength(1000);
        b.HasIndex(x => x.ExpedienteId);
    }
}

public sealed class InfraPerfilConfiguration : IEntityTypeConfiguration<InfraPerfil>
{
    public void Configure(EntityTypeBuilder<InfraPerfil> b)
    {
        b.ToTable("InfraPerfiles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Perfil).HasMaxLength(120).IsRequired();
        b.Property(x => x.Nombre).HasMaxLength(150);
        b.Property(x => x.Correo).HasMaxLength(200);
        b.HasIndex(x => x.ExpedienteId);
    }
}

public sealed class InfraCondicionConfiguration : IEntityTypeConfiguration<InfraCondicion>
{
    public void Configure(EntityTypeBuilder<InfraCondicion> b)
    {
        b.ToTable("InfraCondiciones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Condicion).HasMaxLength(120).IsRequired();
        b.HasIndex(x => x.ExpedienteId);
    }
}

public sealed class InfraChecklistItemConfiguration : IEntityTypeConfiguration<InfraChecklistItem>
{
    public void Configure(EntityTypeBuilder<InfraChecklistItem> b)
    {
        b.ToTable("InfraChecklist");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Grupo).HasMaxLength(120).IsRequired();
        b.Property(x => x.Requisito).HasMaxLength(300).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Obs).HasMaxLength(1000);
        b.HasIndex(x => x.ExpedienteId);
    }
}

public sealed class ExpedienteSeccionEstadoConfiguration : IEntityTypeConfiguration<ExpedienteSeccionEstado>
{
    public void Configure(EntityTypeBuilder<ExpedienteSeccionEstado> b)
    {
        b.ToTable("ExpedienteSecciones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Nota).HasMaxLength(500);
        b.HasIndex(x => x.ExpedienteId);
    }
}

public sealed class ExpedienteEtapaAvanceConfiguration : IEntityTypeConfiguration<ExpedienteEtapaAvance>
{
    public void Configure(EntityTypeBuilder<ExpedienteEtapaAvance> b)
    {
        b.ToTable("ExpedienteEtapaAvances");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.SubId).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.ExpedienteId, x.TramiteIndex, x.SubId }).IsUnique();
        b.HasOne<Expediente>().WithMany()
            .HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NotaSeguimientoExpedienteConfiguration : IEntityTypeConfiguration<NotaSeguimientoExpediente>
{
    public void Configure(EntityTypeBuilder<NotaSeguimientoExpediente> b)
    {
        b.ToTable("NotasSeguimientoExpediente");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Texto).HasMaxLength(NotaSeguimientoExpediente.MaxTexto).IsRequired();
        b.Property(x => x.CreadoPor).HasMaxLength(150).IsRequired();
        // El tablero pide la última nota de cada expediente: el índice descendente
        // por fecha evita ordenar en memoria.
        b.HasIndex(x => new { x.ExpedienteId, x.CreadoEl }).IsDescending(false, true);
        b.HasOne<Expediente>().WithMany()
            .HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BitacoraExpedienteConfiguration : IEntityTypeConfiguration<BitacoraExpediente>
{
    public void Configure(EntityTypeBuilder<BitacoraExpediente> b)
    {
        b.ToTable("BitacoraExpediente");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.Detalle).HasMaxLength(500).IsRequired();
        b.Property(x => x.Actor).HasMaxLength(150).IsRequired();
        // La bitácora se consulta como historial por expediente, más reciente primero.
        b.HasIndex(x => new { x.ExpedienteId, x.Fecha }).IsDescending(false, true);
        b.HasOne<Expediente>().WithMany()
            .HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BitacoraProyectoConfiguration : IEntityTypeConfiguration<BitacoraProyecto>
{
    public void Configure(EntityTypeBuilder<BitacoraProyecto> b)
    {
        b.ToTable("BitacoraProyecto");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.Detalle).HasMaxLength(BitacoraProyecto.MaxDetalle).IsRequired();
        b.Property(x => x.Actor).HasMaxLength(150).IsRequired();
        // Se consulta como historial por proyecto, más reciente primero.
        b.HasIndex(x => new { x.ProyectoId, x.Fecha }).IsDescending(false, true);
        // Cascada: si el proyecto se borra de verdad, su auditoría se va con él. El borrado
        // normal es lógico (IsDeleted), así que en la práctica no se dispara.
        b.HasOne<Proyecto>().WithMany()
            .HasForeignKey(x => x.ProyectoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RiesgoProyectoConfiguration : IEntityTypeConfiguration<RiesgoProyecto>
{
    public void Configure(EntityTypeBuilder<RiesgoProyecto> b)
    {
        b.ToTable("ProyectoRiesgos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Descripcion).HasMaxLength(RiesgoProyecto.MaxDescripcion).IsRequired();
        b.Property(x => x.Mitigacion).HasMaxLength(RiesgoProyecto.MaxMitigacion);
        b.Property(x => x.Responsable).HasMaxLength(200);
        b.Property(x => x.RegistradoPor).HasMaxLength(200).IsRequired();
        b.Property(x => x.Categoria).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Probabilidad).HasConversion<string>().HasMaxLength(10).IsRequired();
        b.Property(x => x.Impacto).HasConversion<string>().HasMaxLength(10).IsRequired();
        b.Property(x => x.Estrategia).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();

        // La matriz se lee por proyecto y se ordena por lo que más pesa; el estado entra en el
        // índice porque casi toda consulta filtra los cerrados.
        b.HasIndex(x => new { x.ProyectoId, x.Estado });

        b.HasOne<Proyecto>().WithMany()
            .HasForeignKey(x => x.ProyectoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class InteresadoProyectoConfiguration : IEntityTypeConfiguration<InteresadoProyecto>
{
    public void Configure(EntityTypeBuilder<InteresadoProyecto> b)
    {
        b.ToTable("ProyectoInteresados");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasMaxLength(InteresadoProyecto.MaxNombre).IsRequired();
        b.Property(x => x.Institucion).HasMaxLength(200);
        b.Property(x => x.Cargo).HasMaxLength(200);
        b.Property(x => x.Correo).HasMaxLength(200);
        b.Property(x => x.Notas).HasMaxLength(InteresadoProyecto.MaxNotas);
        b.Property(x => x.RegistradoPor).HasMaxLength(200).IsRequired();
        b.Property(x => x.Rol).HasConversion<string>().HasMaxLength(25).IsRequired();
        b.Property(x => x.Influencia).HasConversion<string>().HasMaxLength(10).IsRequired();
        b.Property(x => x.UsuarioId).IsRequired();

        b.HasIndex(x => new { x.ProyectoId, x.Rol });

        // La misma persona no puede figurar dos veces en un proyecto. Antes el nombre era texto
        // libre y el duplicado era solo ruido; ahora cada fila otorga acceso, así que repetirla
        // significaría dos permisos que hay que revocar por separado para sacar a alguien.
        b.HasIndex(x => new { x.ProyectoId, x.UsuarioId }).IsUnique();

        // Índice del lado del usuario: lo usa la rama del filtro de alcance que pregunta «¿de qué
        // proyectos es interesado este usuario?», que corre en toda consulta de proyectos.
        b.HasIndex(x => x.UsuarioId);

        b.HasOne<Proyecto>().WithMany()
            .HasForeignKey(x => x.ProyectoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> b)
    {
        b.ToTable("Tickets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Numero).HasMaxLength(30).IsRequired();
        b.Property(x => x.Titulo).HasMaxLength(200).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(4000);
        b.Property(x => x.TemaOtro).HasMaxLength(200);
        b.Property(x => x.Prioridad).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Institucion).HasMaxLength(120);
        b.Property(x => x.ExpedienteCodigo).HasMaxLength(40);
        b.Property(x => x.ReportanteNombre).HasMaxLength(150);
        b.Property(x => x.ReportanteCorreo).HasMaxLength(200);
        b.Property(x => x.ReportanteTelefono).HasMaxLength(40);
        b.Property(x => x.AsignadoA).HasMaxLength(150);
        b.Property(x => x.CreadoPor).HasMaxLength(150);
        b.Property(x => x.NotaResolucion).HasMaxLength(2000);

        b.HasIndex(x => x.Numero).IsUnique();
        b.HasIndex(x => x.Estado);
        b.HasIndex(x => x.CreatedAt);

        b.HasOne<Institucion>().WithMany()
            .HasForeignKey(x => x.InstitucionId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Area>().WithMany()
            .HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Unidad>().WithMany()
            .HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Expediente>().WithMany()
            .HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Usuario>().WithMany()
            .HasForeignKey(x => x.AsignadoAId).OnDelete(DeleteBehavior.SetNull);
        // NoAction (no SetNull) para evitar múltiples rutas de cascade a Usuarios en SQL Server;
        // los usuarios se desactivan, no se eliminan.
        b.HasOne<Usuario>().WithMany()
            .HasForeignKey(x => x.CreadoPorId).OnDelete(DeleteBehavior.NoAction);
        // Tema del catálogo administrable. NoAction: un tema con tickets no se puede eliminar (se desactiva).
        b.HasOne(x => x.TemaRef).WithMany()
            .HasForeignKey(x => x.TemaId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => x.TemaId);

        b.HasMany(x => x.Comentarios).WithOne()
            .HasForeignKey(c => c.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Tramites).WithOne()
            .HasForeignKey(t => t.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Adjuntos).WithOne()
            .HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TicketAdjuntoConfiguration : IEntityTypeConfiguration<TicketAdjunto>
{
    public void Configure(EntityTypeBuilder<TicketAdjunto> b)
    {
        b.ToTable("TicketAdjuntos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.NombreArchivo).HasMaxLength(260).IsRequired();
        b.Property(x => x.Url).HasMaxLength(600).IsRequired();
        b.HasIndex(x => x.TicketId);
        // ComentarioId es referencia suave (sin FK): al borrar el ticket, sus adjuntos caen por TicketId.
    }
}

public sealed class TicketTramiteConfiguration : IEntityTypeConfiguration<TicketTramite>
{
    public void Configure(EntityTypeBuilder<TicketTramite> b)
    {
        b.ToTable("TicketTramites");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Tramite).HasMaxLength(400).IsRequired();
        b.HasIndex(x => x.TicketId);
        // TramiteDefinicionId es una referencia suave (el catálogo se reemplaza en bloque): sin FK.
    }
}

public sealed class TicketComentarioConfiguration : IEntityTypeConfiguration<TicketComentario>
{
    public void Configure(EntityTypeBuilder<TicketComentario> b)
    {
        b.ToTable("TicketComentarios");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Autor).HasMaxLength(150).IsRequired();
        b.Property(x => x.Texto).HasMaxLength(2000).IsRequired();
        b.HasIndex(x => x.TicketId);
    }
}

public sealed class AsignacionUsuarioConfiguration : IEntityTypeConfiguration<AsignacionUsuario>
{
    public void Configure(EntityTypeBuilder<AsignacionUsuario> b)
    {
        b.ToTable("AsignacionesUsuario");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever(); // GUID is generated in domain
        b.Property(x => x.InstitucionId).HasMaxLength(120).IsRequired();
        b.Property(x => x.AreaId).HasMaxLength(120);
        b.Property(x => x.UnidadId).HasMaxLength(120);
        b.Property(x => x.Rol).HasMaxLength(60).IsRequired();
        b.HasIndex(x => new { x.UsuarioId, x.InstitucionId, x.AreaId, x.UnidadId }).IsUnique();
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Institucion>().WithMany().HasForeignKey(x => x.InstitucionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> b)
    {
        b.ToTable("Areas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(120).ValueGeneratedNever();
        b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        b.Property(x => x.InstitucionId).HasMaxLength(120).IsRequired();
        b.Property(x => x.Activo).HasDefaultValue(true);
        b.HasOne<Institucion>().WithMany().HasForeignKey(x => x.InstitucionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UnidadConfiguration : IEntityTypeConfiguration<Unidad>
{
    public void Configure(EntityTypeBuilder<Unidad> b)
    {
        b.ToTable("Unidades");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(120).ValueGeneratedNever();
        b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        b.Property(x => x.AreaId).HasMaxLength(120).IsRequired();
        b.Property(x => x.Activo).HasDefaultValue(true);
        b.HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MovimientoConfiguration : IEntityTypeConfiguration<Movimiento>
{
    public void Configure(EntityTypeBuilder<Movimiento> b)
    {
        b.ToTable("Movimientos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(120).ValueGeneratedNever();
        b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
    }
}

public sealed class PrefijoConfiguration : IEntityTypeConfiguration<Prefijo>
{
    public void Configure(EntityTypeBuilder<Prefijo> b)
    {
        b.ToTable("Prefijos");
        b.HasKey(x => new { x.PrefijoInstitucion, x.PrefijoMovimiento });
        b.Property(x => x.PrefijoInstitucion).HasMaxLength(120);
        b.Property(x => x.PrefijoMovimiento).HasMaxLength(120);
    }
}

public sealed class CategoriaTicketConfiguration : IEntityTypeConfiguration<CategoriaTicket>
{
    public void Configure(EntityTypeBuilder<CategoriaTicket> b)
    {
        b.ToTable("CategoriasTicket");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        b.Property(x => x.Activo).HasDefaultValue(true);
        b.HasIndex(x => x.Nombre).IsUnique();
    }
}

public sealed class TemaTicketConfiguration : IEntityTypeConfiguration<TemaTicket>
{
    public void Configure(EntityTypeBuilder<TemaTicket> b)
    {
        b.ToTable("TemasTicket");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        b.Property(x => x.HorasResolucion).HasDefaultValue(0);
        b.Property(x => x.Activo).HasDefaultValue(true);
        b.HasIndex(x => x.Nombre).IsUnique();
        // Categoría (nivel superior) opcional. NoAction: al eliminar una categoría, sus temas se
        // desvinculan explícitamente (CategoriaId = null) en el comando, no por cascade.
        b.HasOne(x => x.CategoriaRef).WithMany()
            .HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => x.CategoriaId);
    }
}

public sealed class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> b)
    {
        b.ToTable("Roles");
        b.HasKey(x => x.Id);
        // El Id es el código del rol asignado por el usuario/seed (no autogenerado),
        // mismo ancho que AsignacionesUsuario.Rol para que cualquier rol creado sea asignable.
        b.Property(x => x.Id).HasMaxLength(60).ValueGeneratedNever();
        b.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(300);
        b.Property(x => x.Color).HasMaxLength(20);
        b.Property(x => x.NivelAlcance).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => x.Activo);
    }
}

public sealed class RolModuloAccesoConfiguration : IEntityTypeConfiguration<RolModuloAcceso>
{
    public void Configure(EntityTypeBuilder<RolModuloAcceso> b)
    {
        b.ToTable("RolModuloAccesos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.RolId).HasMaxLength(60).IsRequired();
        b.Property(x => x.Modulo).HasMaxLength(40).IsRequired();
        b.HasIndex(x => new { x.RolId, x.Modulo }).IsUnique();
        b.HasOne<Rol>().WithMany().HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PermisoConfiguration : IEntityTypeConfiguration<Permiso>
{
    public void Configure(EntityTypeBuilder<Permiso> b)
    {
        b.ToTable("Permisos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(80).ValueGeneratedNever();
        b.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
        b.Property(x => x.Modulo).HasMaxLength(60).IsRequired();
        b.Property(x => x.Accion).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.Modulo, x.Accion });
    }
}

public sealed class RolPermisoConfiguration : IEntityTypeConfiguration<RolPermiso>
{
    public void Configure(EntityTypeBuilder<RolPermiso> b)
    {
        b.ToTable("RolPermisos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.RolId).HasMaxLength(60).IsRequired();
        b.Property(x => x.PermisoClave).HasMaxLength(80).IsRequired();
        b.HasIndex(x => new { x.RolId, x.PermisoClave }).IsUnique();
        b.HasOne<Rol>().WithMany().HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PermisoAuditoriaConfiguration : IEntityTypeConfiguration<PermisoAuditoria>
{
    public void Configure(EntityTypeBuilder<PermisoAuditoria> b)
    {
        b.ToTable("PermisosAuditoria");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        // Sin FK a Roles: la bitácora debe sobrevivir a la eliminación del rol auditado.
        b.Property(x => x.RolId).HasMaxLength(60).IsRequired();
        b.Property(x => x.PermisoClave).HasMaxLength(80).IsRequired();
        b.Property(x => x.PermisoNombre).HasMaxLength(150).IsRequired();
        b.Property(x => x.Accion).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Actor).HasMaxLength(150).IsRequired();
        b.HasIndex(x => new { x.PermisoClave, x.Fecha });
    }
}

public sealed class UsuarioTemaConfiguration : IEntityTypeConfiguration<UsuarioTema>
{
    public void Configure(EntityTypeBuilder<UsuarioTema> b)
    {
        b.ToTable("UsuarioTemas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasIndex(x => new { x.UsuarioId, x.TemaId }).IsUnique();
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<TemaTicket>().WithMany().HasForeignKey(x => x.TemaId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NotificacionConfiguration : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> b)
    {
        b.ToTable("Notificaciones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Titulo).HasMaxLength(200).IsRequired();
        b.Property(x => x.Url).HasMaxLength(500);
        b.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => new { x.DestinatarioId, x.Leida });
        b.HasIndex(x => x.FechaCreacion);
    }
}

// ── Chat de soporte ───────────────────────────────────────────────────────
public sealed class ChatSesionConfiguration : IEntityTypeConfiguration<ChatSesion>
{
    public void Configure(EntityTypeBuilder<ChatSesion> b)
    {
        b.ToTable("ChatSesiones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.UsuarioNombre).HasMaxLength(120).IsRequired();
        b.Property(x => x.TecnicoNombre).HasMaxLength(120);
        b.Property(x => x.TemaNombre).HasMaxLength(80);
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
        b.HasMany(x => x.Mensajes).WithOne()
            .HasForeignKey(m => m.SesionId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.UsuarioId);
        b.HasIndex(x => new { x.TecnicoId, x.Estado });
        b.HasIndex(x => x.Inicio);
    }
}

public sealed class ChatMensajeConfiguration : IEntityTypeConfiguration<ChatMensaje>
{
    public void Configure(EntityTypeBuilder<ChatMensaje> b)
    {
        b.ToTable("ChatMensajes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Texto).HasMaxLength(2000).IsRequired();
        b.Property(x => x.AutorNombre).HasMaxLength(120).IsRequired();
        b.HasIndex(x => new { x.SesionId, x.Leido });
    }
}

// ── Levantamientos de campo ───────────────────────────────────────────────
public sealed class LevantamientoConfiguration : IEntityTypeConfiguration<Levantamiento>
{
    public void Configure(EntityTypeBuilder<Levantamiento> b)
    {
        b.ToTable("Levantamientos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Institucion).HasMaxLength(120).IsRequired();
        b.Property(x => x.Encargado).HasMaxLength(150).IsRequired();
        b.Property(x => x.Correo).HasMaxLength(200);
        b.Property(x => x.Celular).HasMaxLength(40);
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ObsEstado).HasMaxLength(1000);
        b.Property(x => x.LimitanteObs).HasMaxLength(1000);
        b.Property(x => x.PersonalObs).HasMaxLength(1000);
        b.Property(x => x.HabilidadObs).HasMaxLength(1000);
        b.Property(x => x.ObsGenerales).HasMaxLength(2000);
        b.HasIndex(x => x.Institucion);
        b.HasIndex(x => x.Estado);
        b.HasIndex(x => x.CreatedAt);
        b.HasMany(x => x.Tramites).WithOne()
            .HasForeignKey(t => t.LevantamientoId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Equipo).WithOne()
            .HasForeignKey(m => m.LevantamientoId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Documentos).WithOne()
            .HasForeignKey(d => d.LevantamientoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TramiteChecklistConfiguration : IEntityTypeConfiguration<TramiteChecklist>
{
    public void Configure(EntityTypeBuilder<TramiteChecklist> b)
    {
        b.ToTable("LevantamientoTramites");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.NombreTramite).HasMaxLength(400).IsRequired();
        b.Property(x => x.Observaciones).HasMaxLength(2000);
        b.HasIndex(x => new { x.LevantamientoId, x.Orden });
    }
}

public sealed class MiembroEquipoConfiguration : IEntityTypeConfiguration<MiembroEquipo>
{
    public void Configure(EntityTypeBuilder<MiembroEquipo> b)
    {
        b.ToTable("LevantamientoEquipo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Funcion).HasMaxLength(150).IsRequired();
        b.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
        b.Property(x => x.Contacto).HasMaxLength(200);
        b.HasIndex(x => x.LevantamientoId);
    }
}

public sealed class DocumentoAdjuntoConfiguration : IEntityTypeConfiguration<DocumentoAdjunto>
{
    public void Configure(EntityTypeBuilder<DocumentoAdjunto> b)
    {
        b.ToTable("LevantamientoDocumentos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasMaxLength(300).IsRequired();
        b.Property(x => x.Tipo).HasMaxLength(80);
        b.Property(x => x.Url).HasMaxLength(600).IsRequired();
        b.HasIndex(x => x.LevantamientoId);
    }
}

public sealed class ExpedienteEtapaCronogramaConfiguration : IEntityTypeConfiguration<ExpedienteEtapaCronograma>
{
    public void Configure(EntityTypeBuilder<ExpedienteEtapaCronograma> b)
    {
        b.ToTable("ExpedienteEtapaCronogramas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.EtapaNum).HasMaxLength(5).IsRequired();
        b.Property(x => x.Responsable).HasMaxLength(150);
        b.Property(x => x.Observacion).HasMaxLength(1000);
        b.HasIndex(x => new { x.ExpedienteId, x.TramiteIndex, x.EtapaNum }).IsUnique();
        b.HasOne<Expediente>().WithMany()
            .HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
    }
}

// ── Datos semilla (catálogo de instituciones) ─────────────────────────────
internal static class Seed
{
    internal static readonly DateTime SeedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    internal static readonly object[] Instituciones =
    [
        new { Id = "DIGER",       Nombre = "Dirección de Gestión por Resultados", Activo = true, CreatedAt = SeedDate },
        new { Id = "CONVIVIENDA", Nombre = "Comisión Nacional de Vivienda y Asentamientos Humanos", Activo = true, CreatedAt = SeedDate },
        new { Id = "COPECO",      Nombre = "Secretaría de Estado en los Despachos de Gestión de Riesgos y Contingencias Nacionales", Activo = true, CreatedAt = SeedDate },
        new { Id = "SIT",         Nombre = "Secretaría de Infraestructura y Transporte", Activo = true, CreatedAt = SeedDate },
        new { Id = "IHADFA",      Nombre = "Instituto Hondureño para la Prevención del Alcoholismo, Drogadicción y Farmacodependencia", Activo = true, CreatedAt = SeedDate },
        new { Id = "BANHPROVI",   Nombre = "Banco Hondureño para la Producción y la Vivienda", Activo = true, CreatedAt = SeedDate },
        new { Id = "INPREUNAH",   Nombre = "Instituto de Previsión de la Universidad Nacional Autónoma de Honduras", Activo = true, CreatedAt = SeedDate },
        new { Id = "CNBS",        Nombre = "Comisión Nacional de Bancos y Seguros", Activo = true, CreatedAt = SeedDate },
        new { Id = "INPREMA",     Nombre = "Instituto Nacional de Previsión del Magisterio", Activo = true, CreatedAt = SeedDate },
        new { Id = "IHTT",        Nombre = "Instituto Hondureño del Transporte Terrestre", Activo = true, CreatedAt = SeedDate },
        new { Id = "SEN",         Nombre = "Secretaría de Energía", Activo = true, CreatedAt = SeedDate },
        new { Id = "CONSUCOOP",   Nombre = "Consejo Nacional Supervisor de Cooperativas", Activo = true, CreatedAt = SeedDate },
        new { Id = "CONATEL",     Nombre = "Comisión Nacional de Telecomunicaciones", Activo = true, CreatedAt = SeedDate },
        new { Id = "IHCINE",      Nombre = "Instituto Hondureño de Cinematografía", Activo = true, CreatedAt = SeedDate },
        new { Id = "SAG",         Nombre = "Secretaría de Agricultura y Ganadería", Activo = true, CreatedAt = SeedDate },
        new { Id = "SECAPPH",     Nombre = "Secretaría de las Culturas, las Artes y los Patrimonios de los Pueblos de Honduras", Activo = true, CreatedAt = SeedDate },
        new { Id = "SRECI",       Nombre = "Secretaría de Relaciones Exteriores y Cooperación Internacional", Activo = true, CreatedAt = SeedDate },
        new { Id = "SERNA",       Nombre = "Secretaría de Recursos Naturales y Ambiente", Activo = true, CreatedAt = SeedDate },
        new { Id = "SGJD",        Nombre = "Secretaría de Gobernación, Justicia y Descentralización", Activo = true, CreatedAt = SeedDate },
        new { Id = "CANATURH",    Nombre = "Cámara Nacional de Turismo de Honduras", Activo = true, CreatedAt = SeedDate },
        new { Id = "IP",          Nombre = "Instituto de la Propiedad", Activo = true, CreatedAt = SeedDate },
        new { Id = "SENASA",      Nombre = "Servicio Nacional de Sanidad e Inocuidad Agroalimentaria", Activo = true, CreatedAt = SeedDate },
        new { Id = "SESAL",       Nombre = "Secretaría de Salud", Activo = true, CreatedAt = SeedDate },
        new { Id = "FOSOVI",      Nombre = "Fondo Social de Vivienda", Activo = true, CreatedAt = SeedDate },
        new { Id = "IHT",         Nombre = "Instituto Hondureño de Turismo", Activo = true, CreatedAt = SeedDate },
    ];

    internal static readonly object[] TramitesDefinicion = [];

    // Mismas ocho categorías, íconos y orden que ya usa el sistema consumidor (HondurasÁgil),
    // para que el catálogo público no se contradiga con lo que el ciudadano ya conoce.
    internal static readonly object[] Categorias =
    [
        new { Id = 1, Nombre = "Salud y Seguridad Social", Icono = "HeartPulse",    Orden = 10, Activo = true, CreatedAt = SeedDate },
        new { Id = 2, Nombre = "Educación y Cultura",       Icono = "GraduationCap", Orden = 20, Activo = true, CreatedAt = SeedDate },
        new { Id = 3, Nombre = "Impuestos y Finanzas",      Icono = "CreditCard",   Orden = 30, Activo = true, CreatedAt = SeedDate },
        new { Id = 4, Nombre = "Identidad y Ciudadanía",    Icono = "Contact",      Orden = 40, Activo = true, CreatedAt = SeedDate },
        new { Id = 5, Nombre = "Empresas y Negocios",       Icono = "Building2",    Orden = 50, Activo = true, CreatedAt = SeedDate },
        new { Id = 6, Nombre = "Vivienda y Propiedad",      Icono = "Home",         Orden = 60, Activo = true, CreatedAt = SeedDate },
        new { Id = 7, Nombre = "Transporte y Vehículos",    Icono = "Car",          Orden = 70, Activo = true, CreatedAt = SeedDate },
        new { Id = 8, Nombre = "Medio Ambiente",            Icono = "Leaf",         Orden = 80, Activo = true, CreatedAt = SeedDate },
    ];
}

// ── Plan de Trabajo ───────────────────────────────────────────────────────
public sealed class PlanTrabajoConfiguration : IEntityTypeConfiguration<PlanTrabajo>
{
    public void Configure(EntityTypeBuilder<PlanTrabajo> b)
    {
        b.ToTable("PlanTrabajo");
        b.Property(x => x.InstitucionId).HasMaxLength(120).IsRequired();
        b.Property(x => x.Institucion).HasMaxLength(200).IsRequired();
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Observaciones).HasMaxLength(2000);
        b.HasIndex(x => new { x.InstitucionId, x.Anio }).IsUnique();
        b.HasMany(x => x.Metas)
            .WithOne()
            .HasForeignKey(m => m.PlanTrabajoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MetaTramiteConfiguration : IEntityTypeConfiguration<MetaTramite>
{
    public void Configure(EntityTypeBuilder<MetaTramite> b)
    {
        b.ToTable("PlanTrabajoMetas");
        b.Property(x => x.NombreTramite).HasMaxLength(300).IsRequired();
        b.Property(x => x.Responsable).HasMaxLength(200);
        b.Property(x => x.Observaciones).HasMaxLength(2000);
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => new { x.PlanTrabajoId, x.Orden });
    }
}

// ── Inventario SIGER ─────────────────────────────────────────────────────
public sealed class TramiteSigerConfiguration : IEntityTypeConfiguration<TramiteSiger>
{
    public void Configure(EntityTypeBuilder<TramiteSiger> b)
    {
        b.ToTable("TramitesSiger");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Nombre).HasMaxLength(600).IsRequired();
        b.Property(x => x.Institucion).HasMaxLength(200).IsRequired();
        b.Property(x => x.Sigla).HasMaxLength(30);
        b.Property(x => x.Dependencia).HasMaxLength(400);
        b.Property(x => x.Descripcion).HasMaxLength(4000);
        b.Property(x => x.Objetivo).HasMaxLength(4000);
        b.Property(x => x.DirigidoA).HasMaxLength(2000);
        b.Property(x => x.EstadoSiger).HasMaxLength(30);
        b.Property(x => x.VigenciaDocumento).HasMaxLength(60);
        b.Property(x => x.Temporalidad).HasMaxLength(60);
        b.Property(x => x.DiagramaUrl).HasMaxLength(600);
        b.Property(x => x.EnlacePrincipal).HasMaxLength(600);
        b.Property(x => x.ObservacionesDiger).HasMaxLength(4000);

        // ── Campos para la ficha pública (Ventanilla Digital / plan Fase 1, script A) ──
        b.Property(x => x.SolUrl).HasMaxLength(500);
        b.Property(x => x.CostoTexto).HasMaxLength(250);
        b.Property(x => x.TiempoTexto).HasMaxLength(120);
        b.Property(x => x.Modalidad).HasMaxLength(20);
        b.Property(x => x.EstaEnSol).HasDefaultValue(false);
        b.Property(x => x.EsPopular).HasDefaultValue(false);

        b.HasOne<CategoriaTramite>().WithMany()
            .HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.SetNull);

        b.ToTable(t =>
        {
            // Catálogo cerrado de modalidad. Sin prefijo N': los valores son ASCII y el
            // literal N'...' (T-SQL) no lo entiende SQLite, que usan los Web.Tests vía EnsureCreated.
            t.HasCheckConstraint("CK_TramitesSiger_Modalidad",
                "[Modalidad] IS NULL OR [Modalidad] IN ('Virtual', 'Presencial', 'Hibrido')");
            // La regla de D-01 (plan), protegida en la base y no solo en el formulario.
            t.HasCheckConstraint("CK_TramitesSiger_Sol",
                "[EstaEnSol] = 0 OR ([SolUrl] IS NOT NULL AND ([SolUrl] LIKE 'http://%' OR [SolUrl] LIKE 'https://%'))");
        });

        b.HasIndex(x => x.IdSiger).IsUnique();
        b.HasIndex(x => x.Codigo).IsUnique();
        b.HasIndex(x => x.Institucion);
        b.HasIndex(x => x.Sigla);
        b.HasIndex(x => x.EstadoSiger);
        b.HasIndex(x => x.Publicado);
        b.HasIndex(x => x.DisponibleEnLinea);
        b.HasIndex(x => x.EnPlanDigitalizacion);

        // Filtrados: estas columnas van a estar mayormente en NULL/0 durante meses;
        // un índice completo sobre 1000+ filas casi todas nulas no ayuda a nadie.
        b.HasIndex(x => x.CategoriaId).HasFilter("[CategoriaId] IS NOT NULL");
        b.HasIndex(x => x.Modalidad).HasFilter("[Modalidad] IS NOT NULL");
        b.HasIndex(x => x.EstaEnSol).IncludeProperties(x => x.SolUrl).HasFilter("[EstaEnSol] = 1");

        // El índice de la consulta que la API hace todo el día: el catálogo paginado.
        b.HasIndex(x => new { x.Publicado, x.CategoriaId, x.InstitucionId })
            .HasDatabaseName("IX_TramitesSiger_Catalogo")
            .IncludeProperties(x => new { x.Codigo, x.Nombre, x.Modalidad, x.EsPopular, x.CostoEsGratuito });

        b.Property(x => x.InstitucionId).HasMaxLength(120);
        b.HasOne<Institucion>().WithMany()
            .HasForeignKey(x => x.InstitucionId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.InstitucionId);

        b.HasMany(x => x.Pasos).WithOne().HasForeignKey(p => p.TramiteSigerId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Requisitos).WithOne().HasForeignKey(r => r.TramiteSigerId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Entregables).WithOne().HasForeignKey(e => e.TramiteSigerId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.LugaresAtencion).WithOne().HasForeignKey(l => l.TramiteSigerId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Enlaces).WithOne().HasForeignKey(e => e.TramiteSigerId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.TareasDigitalizacion).WithOne().HasForeignKey(t => t.TramiteSigerId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CategoriaTramiteConfiguration : IEntityTypeConfiguration<CategoriaTramite>
{
    public void Configure(EntityTypeBuilder<CategoriaTramite> b)
    {
        b.ToTable("CategoriasTramite");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        b.Property(x => x.Icono).HasMaxLength(60);
        b.HasIndex(x => x.Nombre).IsUnique();
        b.HasData(Seed.Categorias);
    }
}

public sealed class PasoSigerConfiguration : IEntityTypeConfiguration<PasoSiger>
{
    public void Configure(EntityTypeBuilder<PasoSiger> b)
    {
        b.ToTable("PasosSiger");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Descripcion).HasMaxLength(2000).IsRequired();
        b.Property(x => x.LugarDependencia).HasMaxLength(400);
        b.Property(x => x.SalidaResultado).HasMaxLength(2000);
        b.Property(x => x.TiempoRegistrado).HasMaxLength(60);
        b.Property(x => x.Titulo).HasMaxLength(200);
        b.Property(x => x.Modalidad).HasMaxLength(20);
        b.ToTable(t => t.HasCheckConstraint("CK_PasosSiger_Modalidad",
            "[Modalidad] IS NULL OR [Modalidad] IN ('Virtual', 'Presencial', 'Hibrido', 'Interno')"));
        b.HasIndex(x => new { x.TramiteSigerId, x.NumeroPaso });
    }
}

public sealed class RequisitoSigerConfiguration : IEntityTypeConfiguration<RequisitoSiger>
{
    public void Configure(EntityTypeBuilder<RequisitoSiger> b)
    {
        b.ToTable("RequisitosSiger");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Requisito).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Tipo).HasMaxLength(100);
        b.Property(x => x.DocumentoSoporte).HasMaxLength(2000);
        b.Property(x => x.Formato).HasMaxLength(600);
        b.HasIndex(x => new { x.TramiteSigerId, x.Numero });
    }
}

public sealed class EntregableSigerConfiguration : IEntityTypeConfiguration<EntregableSiger>
{
    public void Configure(EntityTypeBuilder<EntregableSiger> b)
    {
        b.ToTable("EntregablesSiger");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Entregable).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Formato).HasMaxLength(2000);
        b.Property(x => x.Presentacion).HasMaxLength(600);
        b.HasIndex(x => new { x.TramiteSigerId, x.Numero });
    }
}

public sealed class LugarAtencionSigerConfiguration : IEntityTypeConfiguration<LugarAtencionSiger>
{
    public void Configure(EntityTypeBuilder<LugarAtencionSiger> b)
    {
        b.ToTable("LugaresAtencionSiger");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Lugar).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Ciudad).HasMaxLength(2000);
        b.Property(x => x.Direccion).HasMaxLength(2000);
        b.Property(x => x.Telefonos).HasMaxLength(1000);
        b.HasIndex(x => new { x.TramiteSigerId, x.Numero });
    }
}

public sealed class EnlaceSigerConfiguration : IEntityTypeConfiguration<EnlaceSiger>
{
    public void Configure(EntityTypeBuilder<EnlaceSiger> b)
    {
        b.ToTable("EnlacesSiger");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Url).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Tipo).HasMaxLength(100);
        b.HasIndex(x => new { x.TramiteSigerId, x.Numero });
    }
}

public sealed class TareaDigitalizacionSigerConfiguration : IEntityTypeConfiguration<TareaDigitalizacionSiger>
{
    public void Configure(EntityTypeBuilder<TareaDigitalizacionSiger> b)
    {
        b.ToTable("TareasDigitalizacionSiger");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Descripcion).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Estado).HasMaxLength(60);
        b.HasIndex(x => new { x.TramiteSigerId, x.NumeroTarea });
    }
}

public sealed class ConciliacionSigerConfiguration : IEntityTypeConfiguration<ConciliacionSiger>
{
    public void Configure(EntityTypeBuilder<ConciliacionSiger> b)
    {
        b.ToTable("ConciliacionesSiger");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nota).HasMaxLength(500);

        // Una sola decisión vigente por trámite: al revisar de nuevo se actualiza, no se acumula.
        b.HasIndex(x => x.ExpedienteTramiteId).IsUnique();

        // Si el trámite del expediente desaparece, su decisión deja de tener sentido.
        b.HasOne<ExpedienteTramite>().WithMany()
            .HasForeignKey(x => x.ExpedienteTramiteId).OnDelete(DeleteBehavior.Cascade);

        // La ficha SIGER se conserva aunque se borre: la decisión sigue siendo historial válido.
        b.HasOne<TramiteSiger>().WithMany()
            .HasForeignKey(x => x.TramiteSigerId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.TramiteSigerId);
    }
}

// ── Seguimiento de proyectos internos ─────────────────────────────────────
// El filtro de Proyecto (arriba, junto a los demás) solo excluye los borrados: a propósito no
// lleva rama de alcance, porque son proyectos de DIGER y no hay InstitucionId del que colgarla.
// Quién los ve lo decide el permiso Proyectos.Ver. Ver el XML doc de la entidad Proyecto.
public sealed class ProyectoConfiguration : IEntityTypeConfiguration<Proyecto>
{
    public void Configure(EntityTypeBuilder<Proyecto> b)
    {
        b.ToTable("Proyectos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
        b.Property(x => x.Nombre).HasMaxLength(300).IsRequired();
        b.Property(x => x.Objetivo).HasMaxLength(2000);
        b.Property(x => x.InstitucionId).HasMaxLength(120);
        b.Property(x => x.AreaId).HasMaxLength(120);
        b.Property(x => x.UnidadId).HasMaxLength(120);
        b.Property(x => x.Responsable).HasMaxLength(200);
        // El filtro de alcance entra por acá, así que conviene que el ancla esté indexada.
        b.HasIndex(x => x.InstitucionId);
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Prioridad).HasConversion<string>().HasMaxLength(20);

        // Filtrado: el índice único va sobre los vivos, porque el borrado es lógico y un código
        // liberado por un borrado tiene que poder reutilizarse.
        b.HasIndex(x => x.Codigo).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => new { x.Estado, x.FechaFinPlan });

        b.HasMany(x => x.Entregables)
            .WithOne()
            .HasForeignKey(e => e.ProyectoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class EntregableProyectoConfiguration : IEntityTypeConfiguration<EntregableProyecto>
{
    public void Configure(EntityTypeBuilder<EntregableProyecto> b)
    {
        b.ToTable("ProyectoEntregables");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasMaxLength(EntregableProyecto.MaxNombre).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(EntregableProyecto.MaxDescripcion);
        b.Property(x => x.Responsable).HasMaxLength(200);
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => new { x.ProyectoId, x.Orden });

        b.HasMany(x => x.Actividades)
            .WithOne()
            .HasForeignKey(a => a.EntregableId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ActividadProyectoConfiguration : IEntityTypeConfiguration<ActividadProyecto>
{
    public void Configure(EntityTypeBuilder<ActividadProyecto> b)
    {
        b.ToTable("ProyectoActividades");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasMaxLength(ActividadProyecto.MaxNombre).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(ActividadProyecto.MaxDescripcion);
        b.Property(x => x.Responsable).HasMaxLength(200);
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(30);

        // El editor y la ficha siempre piden "las actividades de este entregable, en orden".
        b.HasIndex(x => new { x.EntregableId, x.Orden });

        // El tablero barre las actividades abiertas por fecha de cierre, sin filtrar por
        // entregable: vencidas y próximas del portafolio entero.
        b.HasIndex(x => x.FechaFinPlan);

        // Las dependencias cuelgan de la sucesora y se van con ella.
        b.HasMany(x => x.Predecesoras)
            .WithOne()
            .HasForeignKey(d => d.SucesoraId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DependenciaActividadConfiguration : IEntityTypeConfiguration<DependenciaActividad>
{
    public void Configure(EntityTypeBuilder<DependenciaActividad> b)
    {
        b.ToTable("ProyectoDependenciasActividad");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);

        // Que la misma pareja no se pueda registrar dos veces. El dominio ya deduplica al fijar
        // las predecesoras; el índice es la red por si algún script carga dependencias por SQL.
        b.HasIndex(x => new { x.SucesoraId, x.PredecesoraId }).IsUnique();

        // El lado de la predecesora se consulta al revés —«¿a quién desbloquea esta actividad?»—
        // y no lo cubre el índice de arriba, que arranca por la sucesora.
        b.HasIndex(x => x.PredecesoraId);

        // NoAction, no Cascade: ya hay una ruta de borrado desde Proyectos hacia esta tabla
        // (Proyecto→Entregables→Actividades→Dependencias, por SucesoraId) y esta sería la segunda.
        // SQL Server rechaza el modelo de plano con el error 1785, igual que pasó con
        // ProyectoAvances. Las filas que apuntan a una actividad borrada las limpia a mano la
        // reconciliación del editor, antes de que EF intente el DELETE.
        b.HasOne<ActividadProyecto>().WithMany()
            .HasForeignKey(x => x.PredecesoraId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class CategoriaDocumentoConfiguration : IEntityTypeConfiguration<CategoriaDocumento>
{
    public void Configure(EntityTypeBuilder<CategoriaDocumento> b)
    {
        b.ToTable("CategoriasDocumento");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Nombre).HasMaxLength(CategoriaDocumento.MaxNombre).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(CategoriaDocumento.MaxDescripcion);

        // Sin filtro por nombre repetido entre activas e inactivas: desactivar una y volver a
        // crearla con el mismo nombre es una operación legítima del administrador.
        b.HasIndex(x => x.Nombre).IsUnique();
        b.HasIndex(x => x.Orden);
    }
}

public sealed class DocumentoProyectoConfiguration : IEntityTypeConfiguration<DocumentoProyecto>
{
    public void Configure(EntityTypeBuilder<DocumentoProyecto> b)
    {
        b.ToTable("ProyectoDocumentos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Titulo).HasMaxLength(DocumentoProyecto.MaxTitulo).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(DocumentoProyecto.MaxDescripcion);

        // La pantalla siempre pide "los documentos de este proyecto, agrupados por categoría".
        b.HasIndex(x => new { x.ProyectoId, x.CategoriaId });

        // La biblioteca cruza el portafolio filtrando por categoría, sin acotar proyecto.
        b.HasIndex(x => x.CategoriaId);

        // Cascada desde el proyecto: si el proyecto se borra de verdad, su documentación se va con
        // él. Es la ÚNICA ruta de borrado que llega a las versiones —Proyecto → Documento →
        // Version— y por eso la de categoría de abajo tiene que ser NoAction.
        b.HasOne<Proyecto>().WithMany()
            .HasForeignKey(x => x.ProyectoId).OnDelete(DeleteBehavior.Cascade);

        // NoAction, no Cascade ni SetNull: borrar una categoría no puede llevarse documentos por
        // delante, y una segunda ruta de borrado hacia esta tabla haría que SQL Server rechazara
        // el modelo con el error 1785 —el mismo que ya apareció con los avances, los riesgos y las
        // dependencias—. Por eso la categoría se desactiva en vez de borrarse.
        b.HasOne<CategoriaDocumento>().WithMany()
            .HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.NoAction);

        b.HasMany(x => x.Versiones)
            .WithOne()
            .HasForeignKey(v => v.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class VersionDocumentoConfiguration : IEntityTypeConfiguration<VersionDocumento>
{
    public void Configure(EntityTypeBuilder<VersionDocumento> b)
    {
        b.ToTable("ProyectoDocumentoVersiones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.ArchivoNombre).HasMaxLength(VersionDocumento.MaxNombre).IsRequired();
        b.Property(x => x.ArchivoUrl).HasMaxLength(500).IsRequired();
        b.Property(x => x.Notas).HasMaxLength(VersionDocumento.MaxNotas);
        b.Property(x => x.SubidoPor).HasMaxLength(200).IsRequired();

        // SHA-256 en hexadecimal: 64 caracteres exactos, y nunca acentos ni Unicode.
        b.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsRequired();

        // La red de la numeración: el dominio calcula el número, pero si dos personas suben a la
        // vez el índice es lo que impide que queden dos "versión 3" del mismo documento.
        b.HasIndex(x => new { x.DocumentoId, x.Numero }).IsUnique();

        // Para avisar "este archivo ya está subido" sin recorrer la tabla entera.
        b.HasIndex(x => x.Sha256);
    }
}

public sealed class DescargaDocumentoConfiguration : IEntityTypeConfiguration<DescargaDocumento>
{
    public void Configure(EntityTypeBuilder<DescargaDocumento> b)
    {
        b.ToTable("ProyectoDocumentoDescargas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Usuario).HasMaxLength(DescargaDocumento.MaxUsuario).IsRequired();

        // Borrar la versión se lleva su bitácora de descargas: sin el archivo, saber quién lo bajó
        // no responde nada. La versión, a su vez, solo desaparece si desaparece el proyecto.
        b.HasOne<VersionDocumento>()
         .WithMany()
         .HasForeignKey(x => x.VersionId)
         .OnDelete(DeleteBehavior.Cascade);

        // Las dos preguntas que se le hacen a esta tabla: «quién descargó este archivo» y
        // «qué se llevó esta persona».
        b.HasIndex(x => new { x.VersionId, x.FechaHora });
        b.HasIndex(x => new { x.UsuarioId, x.FechaHora });
    }
}

public sealed class AvanceProyectoConfiguration : IEntityTypeConfiguration<AvanceProyecto>
{
    public void Configure(EntityTypeBuilder<AvanceProyecto> b)
    {
        b.ToTable("ProyectoAvances");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Descripcion).HasMaxLength(AvanceProyecto.MaxDescripcion).IsRequired();
        b.Property(x => x.Autor).HasMaxLength(200).IsRequired();
        // Mismo largo que Autor: guarda un nombre de usuario, no texto libre.
        b.Property(x => x.EditadoPor).HasMaxLength(200);
        b.Property(x => x.Bloqueo).HasMaxLength(1000);
        b.Property(x => x.ArchivoNombre).HasMaxLength(300);
        b.Property(x => x.ArchivoUrl).HasMaxLength(500);

        // El timeline siempre pide "los avances de este proyecto, del más nuevo al más viejo".
        b.HasIndex(x => new { x.ProyectoId, x.Fecha });

        // El timeline también agrupa por actividad al pintar la ficha del entregable.
        b.HasIndex(x => x.ActividadId);

        // El entregable puede desaparecer al reeditar la estructura; el avance no se pierde por
        // eso, solo deja de estar imputado. Este SetNull es obligatorio: sin él, guardar el editor
        // reventaría por violación de FK.
        b.HasOne<EntregableProyecto>().WithMany()
            .HasForeignKey(x => x.EntregableId).OnDelete(DeleteBehavior.SetNull);

        // La actividad NO puede llevar SetNull aunque quisiéramos el mismo efecto: sería la
        // segunda ruta de borrado desde Proyectos hacia esta tabla —una por
        // Proyecto→Entregables→Avances y otra por Proyecto→Entregables→Actividades→Avances— y
        // SQL Server rechaza el modelo de plano (Msg 1785). El desvínculo lo hace a mano la
        // reconciliación del editor antes de borrar la actividad, con DesimputarActividad().
        // Mismo arreglo que ya llevaba el riesgo, acá abajo.
        b.HasOne<ActividadProyecto>().WithMany()
            .HasForeignKey(x => x.ActividadId).OnDelete(DeleteBehavior.NoAction);

        // NoAction, no SetNull, aunque el efecto buscado sea el mismo que con el entregable:
        // borrar un proyecto ya cascadea a ProyectoRiesgos, y un SetNull acá abriría otra ruta de
        // borrado hacia esta misma tabla — SQL Server lo rechaza de plano (Msg 1785). El desvínculo
        // lo hace EliminarRiesgoCommand antes de borrar; ver el comentario allá.
        b.HasOne<RiesgoProyecto>().WithMany()
            .HasForeignKey(x => x.RiesgoId).OnDelete(DeleteBehavior.NoAction);

        // Sin cascada desde Proyecto, a diferencia de los entregables. SQL Server rechaza el modelo
        // con error 1785 ("multiple cascade paths") porque Proyecto→Entregables→Avances ya es un
        // camino de borrado y Proyecto→Avances sería un segundo. No se pierde nada: el borrado
        // de proyectos es lógico (IsDeleted), así que esta cascada nunca llegaría a dispararse,
        // y para una bitácora append-only negarse a desaparecer en silencio es lo correcto.
        b.HasOne<Proyecto>().WithMany()
            .HasForeignKey(x => x.ProyectoId).OnDelete(DeleteBehavior.NoAction);
    }
}
