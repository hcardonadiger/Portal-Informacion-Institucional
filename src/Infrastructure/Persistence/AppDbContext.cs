using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diger.TramitesEstado.Infrastructure.Persistence;

// ── DbContext ─────────────────────────────────────────────────────────────
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUserService currentUser)
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
    public DbSet<Ticket>                   Tickets            { get; init; } = default!;
    public DbSet<TicketComentario>         TicketComentarios  { get; init; } = default!;
    public DbSet<CategoriaTicket>          CategoriasTicket   { get; init; } = default!;
    public DbSet<TemaTicket>               TemasTicket        { get; init; } = default!;
    public DbSet<UsuarioTema>              UsuarioTemas         { get; init; } = default!;
    public DbSet<RolModuloAcceso>          RolModuloAccesos     { get; init; } = default!;

    // Alcance institucional del usuario actual (se evalúa una vez por request al crear el contexto).
    private readonly bool    _alcanceGlobal = currentUser.EsGlobal;
    private readonly string? _activeInst    = currentUser.ActiveInstitucionId;
    private readonly string? _activeArea    = currentUser.ActiveAreaId;
    private readonly string? _activeUnidad  = currentUser.ActiveUnidadId;
    private readonly string? _activeRol     = currentUser.Rol;
    private readonly Guid?   _usuarioId     = currentUser.UserId;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ── Filtros Globales RLS (Row-Level Security) ───────────────────────
        // JefeInstitucion: ve todo de su institución.
        // JefeArea: ve todo lo asignado a su área.
        // JefeUnidad / Empleado: ven lo asignado a su unidad.
        // Administrador (_alcanceGlobal = true): ve todo.

        mb.Entity<Expediente>().HasQueryFilter(e => _alcanceGlobal ||
            (_activeRol == "JefeInstitucion" && e.InstitucionId == _activeInst) ||
            (_activeRol == "JefeArea"        && e.AreaId == _activeArea) ||
            ((_activeRol == "JefeUnidad" || _activeRol == "Empleado" || _activeRol == "Consultor") && e.UnidadId == _activeUnidad)
        );

        mb.Entity<Contacto>().HasQueryFilter(c => _alcanceGlobal ||
            (_activeRol == "JefeInstitucion" && c.InstitucionId == _activeInst) ||
            (_activeRol == "JefeArea"        && c.AreaId == _activeArea) ||
            ((_activeRol == "JefeUnidad" || _activeRol == "Empleado" || _activeRol == "Consultor") && c.UnidadId == _activeUnidad)
        );

        mb.Entity<Ticket>().HasQueryFilter(t => _alcanceGlobal ||
            (_activeRol == "JefeInstitucion" && t.InstitucionId == _activeInst) ||
            (_activeRol == "JefeArea"        && t.AreaId == _activeArea) ||
            ((_activeRol == "JefeUnidad" || _activeRol == "Empleado" || _activeRol == "Consultor") && t.UnidadId == _activeUnidad)
        );

        // Reuniones: las públicas respetan la jerarquía, las privadas solo las ve el creador.
        mb.Entity<Reunion>().HasQueryFilter(r =>
            (r.Visibilidad != VisibilidadReunion.Privada && (
                _alcanceGlobal ||
                (_activeRol == "JefeInstitucion" && r.InstitucionId == _activeInst) ||
                (_activeRol == "JefeArea"        && r.AreaId == _activeArea) ||
                ((_activeRol == "JefeUnidad" || _activeRol == "Empleado" || _activeRol == "Consultor") && r.UnidadId == _activeUnidad)
            )) || 
            (r.Visibilidad == VisibilidadReunion.Privada && r.CreadoPorId != null && r.CreadoPorId == _usuarioId)
        );

        base.OnModelCreating(mb);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
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
        return await base.SaveChangesAsync(ct);
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
        b.HasIndex(x => x.Nombre).IsUnique();
        b.HasMany(x => x.Tramites).WithOne()
            .HasForeignKey(t => t.InstitucionId).OnDelete(DeleteBehavior.Cascade);
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
        b.Property(x => x.Cargo).HasMaxLength(150);
        b.Property(x => x.Correo).HasMaxLength(200);
        b.Property(x => x.Telefono).HasMaxLength(40);
        b.Property(x => x.Notas).HasMaxLength(1000);
        b.Property(x => x.Origen).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.InstitucionId).IsRequired();
        b.Property(x => x.AreaId).HasMaxLength(120);
        b.Property(x => x.UnidadId).HasMaxLength(120);
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
        b.Property(x => x.Institucion).HasMaxLength(120);
        b.Property(x => x.Departamento).HasMaxLength(150);
        b.Property(x => x.Correo).HasMaxLength(200);
        b.Property(x => x.Telefono).HasMaxLength(40);
        b.HasIndex(x => x.ReunionId);
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
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20).HasDefaultValue(EstadoCompromiso.Pendiente);
        b.Property(x => x.NotaSeguimiento).HasMaxLength(1000);
        b.Property(x => x.SeguimientoActualizadoPor).HasMaxLength(150);
        b.HasIndex(x => x.ReunionId);
        b.HasIndex(x => x.Estado);
        b.HasIndex(x => x.Plazo);
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
        b.HasIndex(x => x.ExpedienteId);
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

public sealed class RolModuloAccesoConfiguration : IEntityTypeConfiguration<RolModuloAcceso>
{
    public void Configure(EntityTypeBuilder<RolModuloAcceso> b)
    {
        b.ToTable("RolModuloAccesos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Rol).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Modulo).HasMaxLength(40).IsRequired();
        b.HasIndex(x => new { x.Rol, x.Modulo }).IsUnique();
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

// ── Datos semilla (catálogo de instituciones) ─────────────────────────────
internal static class Seed
{
    internal static readonly DateTime SeedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    internal static readonly object[] Instituciones =
    [
        new { Id = "DIGER", Nombre = "DIGER (Sistema)",  Activo = true, CreatedAt = SeedDate },
        new { Id = "1",  Nombre = "CONVIVIENDA",    Activo = true, CreatedAt = SeedDate },
        new { Id = "2",  Nombre = "COPECO",         Activo = true, CreatedAt = SeedDate },
        new { Id = "3",  Nombre = "SIT",            Activo = true, CreatedAt = SeedDate },
        new { Id = "4",  Nombre = "IHADFA",         Activo = true, CreatedAt = SeedDate },
        new { Id = "5",  Nombre = "BANHPROVI",      Activo = true, CreatedAt = SeedDate },
        new { Id = "6",  Nombre = "INPREUNAH",      Activo = true, CreatedAt = SeedDate },
        new { Id = "7",  Nombre = "CNBS",           Activo = true, CreatedAt = SeedDate },
        new { Id = "8",  Nombre = "INPREMA",        Activo = true, CreatedAt = SeedDate },
        new { Id = "9",  Nombre = "IHTT",           Activo = true, CreatedAt = SeedDate },
        new { Id = "10", Nombre = "SEN",            Activo = true, CreatedAt = SeedDate },
        new { Id = "11", Nombre = "CONSUCOOP",      Activo = true, CreatedAt = SeedDate },
        new { Id = "12", Nombre = "CONATEL",        Activo = true, CreatedAt = SeedDate },
        new { Id = "13", Nombre = "IHCINE",         Activo = true, CreatedAt = SeedDate },
        new { Id = "14", Nombre = "SAG",            Activo = true, CreatedAt = SeedDate },
        new { Id = "15", Nombre = "SECAPPH",        Activo = true, CreatedAt = SeedDate },
        new { Id = "16", Nombre = "SRECI",          Activo = true, CreatedAt = SeedDate },
        new { Id = "17", Nombre = "SERNA",          Activo = true, CreatedAt = SeedDate },
        new { Id = "18", Nombre = "SGJD",           Activo = true, CreatedAt = SeedDate },
        new { Id = "19", Nombre = "CANATURH / IHT", Activo = true, CreatedAt = SeedDate },
        new { Id = "20", Nombre = "IP",             Activo = true, CreatedAt = SeedDate },
        new { Id = "21", Nombre = "SENASA",         Activo = true, CreatedAt = SeedDate },
        new { Id = "22", Nombre = "SESAL",          Activo = true, CreatedAt = SeedDate },
        new { Id = "23", Nombre = "FOSOVI",         Activo = true, CreatedAt = SeedDate },
        new { Id = "24", Nombre = "IHT",            Activo = true, CreatedAt = SeedDate },
    ];

    internal static readonly object[] TramitesDefinicion = [];
}
