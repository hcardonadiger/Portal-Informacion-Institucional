namespace Diger.TramitesEstado.Api.Lectura;

/// <summary>
/// La ventana de solo lectura sobre la base de PortalDigital.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta API no es dueña de este esquema y no debe comportarse como si lo fuera.</b> Las
/// tablas las crea y las cambia PortalDigital, con sus migraciones. Acá no hay ni carpeta
/// <c>Migrations</c> ni forma de generarla: si algún día alguien ejecuta
/// <c>dotnet ef migrations add</c> contra este proyecto, está haciendo algo que no debería, y
/// el esquema que produciría sería una versión mutilada del real —el modelo de acá solo
/// describe ocho tablas de las más de cuarenta que existen—.
/// </para>
/// <para>
/// <b>Escribir está prohibido por construcción, no por costumbre.</b> <c>SaveChanges</c> lanza.
/// La API es de solo lectura por contrato (no hay POST, PUT ni DELETE en la v1) y esto lo hace
/// cierto también hacia adentro: un error de programación no puede acabar en un UPDATE contra
/// el inventario que ve el ciudadano. En producción, además, el usuario de la base es de solo
/// lectura (<c>scripts/sql/10-usuario-solo-lectura-api.sql</c>); esto es la segunda cerradura.
/// </para>
/// <para>
/// <b>Sin filtros globales.</b> El contexto de PortalDigital le pone a <c>Institucion</c> un
/// filtro por alcance institucional, pensado para un usuario con sesión; por eso su consulta
/// pública tenía que llamar a <c>IgnoreQueryFilters()</c> para no depender de un valor por
/// omisión. Acá no hay usuario, no hay alcance y no hay filtro: una API pública no debería
/// heredar los mecanismos de permisos de un portal interno.
/// </para>
/// </remarks>
public sealed class ApiDbContext(DbContextOptions<ApiDbContext> opciones) : DbContext(opciones)
{
    public DbSet<FichaSiger>         Fichas          => Set<FichaSiger>();
    public DbSet<PasoSiger>          Pasos           => Set<PasoSiger>();
    public DbSet<RequisitoSiger>     Requisitos      => Set<RequisitoSiger>();
    public DbSet<EntregableSiger>    Entregables     => Set<EntregableSiger>();
    public DbSet<LugarAtencionSiger> LugaresAtencion => Set<LugarAtencionSiger>();
    public DbSet<EnlaceSiger>        Enlaces         => Set<EnlaceSiger>();
    public DbSet<CategoriaTramite>   Categorias      => Set<CategoriaTramite>();
    public DbSet<Institucion>        Instituciones   => Set<Institucion>();

    protected override void OnConfiguring(DbContextOptionsBuilder opciones)
    {
        // Nada de lo que se lee acá se modifica, así que seguir los cambios de cada entidad
        // es trabajo y memoria a cambio de nada. Puesto en el contexto y no consulta por
        // consulta: así no depende de que alguien se acuerde de escribir AsNoTracking().
        opciones.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Los nombres de tabla son los de PortalDigital y se escriben explícitos: las clases de
        // acá se llaman distinto a propósito (FichaSiger, no TramiteSiger) porque describen lo
        // que esta API lee, no lo que aquel proyecto modela.
        mb.Entity<FichaSiger>(b =>
        {
            b.ToTable("TramitesSiger");
            b.HasKey(x => x.Id);
            // Calculada por la base en PortalDigital. No se marca como generada por el store:
            // esta API nunca escribe (SaveChanges lanza), y marcarla impediría que las pruebas
            // pudieran sembrar fichas completas e incompletas para comprobar que se sirven bien.
        });

        mb.Entity<PasoSiger>()          .ToTable("PasosSiger");
        mb.Entity<RequisitoSiger>()     .ToTable("RequisitosSiger");
        mb.Entity<EntregableSiger>()    .ToTable("EntregablesSiger");
        mb.Entity<LugarAtencionSiger>() .ToTable("LugaresAtencionSiger");
        mb.Entity<EnlaceSiger>()        .ToTable("EnlacesSiger");
        mb.Entity<CategoriaTramite>()   .ToTable("CategoriasTramite");

        mb.Entity<Institucion>(b =>
        {
            b.ToTable("Instituciones");
            b.HasKey(x => x.Id);
            // La llave es la sigla y la pone quien crea la institución en PortalDigital; sin
            // esto EF la trataría como generada por la base.
            b.Property(x => x.Id).ValueGeneratedNever();
        });
    }


    /// <summary>
    /// La única puerta por la que se puede escribir, y existe solo para que las pruebas puedan
    /// sembrar datos. Es <c>internal</c> a propósito: nada del código de producción la ve.
    /// </summary>
    /// <remarks>
    /// Sembrar con SQL crudo sería la alternativa, pero convertiría cada prueba en una lista de
    /// INSERT que hay que mantener a mano cada vez que cambie una columna — justo el trabajo que
    /// el modelo de lectura viene a evitar.
    /// </remarks>
    internal bool SembrandoEnPruebas { get; set; }

    /// <summary>Lanza salvo que se esté sembrando en pruebas. Ver la nota de la clase.</summary>
    public override int SaveChanges() =>
        SembrandoEnPruebas ? base.SaveChanges() : throw new InvalidOperationException(NoSeEscribe);

    /// <summary>Lanza salvo que se esté sembrando en pruebas. Ver la nota de la clase.</summary>
    public override Task<int> SaveChangesAsync(bool aceptarTodo, CancellationToken ct = default) =>
        SembrandoEnPruebas
            ? base.SaveChangesAsync(aceptarTodo, ct)
            : throw new InvalidOperationException(NoSeEscribe);

    private const string NoSeEscribe =
        "La API pública es de solo lectura. El inventario se edita en PortalDigital; " +
        "si algo tiene que cambiar en la base, no es por acá.";
}
