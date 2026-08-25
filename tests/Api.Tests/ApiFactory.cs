using Diger.TramitesEstado.Api.Lectura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diger.TramitesEstado.Api.Tests;

/// <summary>
/// Levanta la API pública en memoria.
///
/// Cuatro cosas que conviene saber antes de tocar esto:
///
/// 1. **Se siembra por el modelo de lectura de la propia API**, no por el de PortalDigital.
///    Es a propósito: si un día PortalDigital renombra una columna que la API lee, estas
///    pruebas seguirían pasando —siembran y leen por el mismo modelo— pero la API fallaría
///    contra la base real. Ese hueco lo cubre otra cosa, y no puede cubrirlo una prueba en
///    memoria: <c>ModeloContraLaBaseRealTests</c>, que consulta el catálogo de SQL Server.
///
/// 2. **SQLite en memoria, no el provider InMemory.** La base vive mientras la conexión siga
///    abierta, y las consultas se traducen a SQL de verdad.
///
/// 3. **Swagger se enciende a mano.** El ambiente acá es «Testing», y Program.cs solo publica
///    Swagger en Development o si alguien lo pide por configuración. Se pide por configuración,
///    que es justo la ruta que usa el servidor de integración: así la prueba comprueba el mismo
///    camino que corre en un ambiente real, no uno exclusivo de las pruebas.
///
/// 4. **La clave se inyecta por configuración.** Es donde el handler la busca de verdad
///    (<c>PortalDigitalApi:ApiKey</c>), así que la prueba de que sin clave no se pasa está
///    ejercitando el mecanismo real y no una simulación.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>La clave que aceptará esta instancia. No es un secreto: la API de las pruebas
    /// no sirve datos reales.</summary>
    public const string Clave = "clave-de-pruebas";

    /// <summary>La única institución sembrada; es la que apuntan todas las fichas de prueba.</summary>
    public const string SiglaDePrueba = "IDP";

    private SqliteConnection? _conexion;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PortalDigitalApi:ApiKey"]          = Clave,
                ["PortalDigitalApi:PublicarSwagger"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            // No alcanza con quitar DbContextOptions: desde EF Core 9, AddDbContext guarda
            // también la acción de configuración como IDbContextOptionsConfiguration<T>, y esa
            // sigue aplicando UseSqlServer sobre las mismas opciones. Si queda, EF ve dos
            // providers registrados y falla al primer query.
            var registrosDelContexto = services.Where(s =>
                    s.ServiceType == typeof(DbContextOptions<ApiDbContext>)
                 || s.ServiceType == typeof(DbContextOptions)
                 || s.ServiceType == typeof(ApiDbContext)
                 || (s.ServiceType.IsGenericType
                     && s.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration")
                     && s.ServiceType.GenericTypeArguments.Contains(typeof(ApiDbContext))))
                .ToList();

            foreach (var registro in registrosDelContexto)
                services.Remove(registro);

            _conexion = new SqliteConnection("DataSource=:memory:");
            _conexion.Open();

            services.AddDbContext<ApiDbContext>(o => o.UseSqlite(_conexion));
        });
    }

    /// <summary>Crea el esquema y siembra la institución a la que apuntan las fichas de prueba.
    /// Llamar una vez por fixture, antes de cualquier petición que toque datos.</summary>
    public async Task PrepararAsync()
    {
        await ConLaBaseAsync(async db =>
        {
            await db.Database.EnsureCreatedAsync();

            if (!await db.Instituciones.AnyAsync(i => i.Id == SiglaDePrueba))
                db.Instituciones.Add(new Institucion
                {
                    Id = SiglaDePrueba, Nombre = "Instituto de Prueba", Activo = true
                });
        });
    }

    /// <summary>
    /// Ejecuta algo contra la base de la API y guarda. Se usa para sembrar.
    /// </summary>
    /// <remarks>
    /// Abre a mano la puerta de escritura del contexto. En producción esa puerta está cerrada y
    /// <c>SaveChanges</c> lanza: la API es de solo lectura hacia adentro también, no solo por
    /// contrato. Que sembrar cueste una línea extra es el precio de que esa garantía sea real.
    /// </remarks>
    public async Task ConLaBaseAsync(Func<ApiDbContext, Task> accion)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        db.SembrandoEnPruebas = true;
        await accion(db);
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
    }

    /// <summary>Cliente con la clave puesta: es como habla HondurasÁgil.</summary>
    public HttpClient ClienteConClave()
    {
        var cliente = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        cliente.DefaultRequestHeaders.Add("X-Api-Key", Clave);
        return cliente;
    }

    /// <summary>Cliente sin clave: es como llega un desconocido.</summary>
    public HttpClient ClienteSinClave() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>Una ficha mínima que la API pueda servir. Todo lo demás se ajusta en cada prueba.</summary>
    public static FichaSiger Ficha(string codigo, string nombre, bool publicado = true) => new()
    {
        Codigo        = codigo,
        Nombre        = nombre,
        Institucion   = "Instituto de Prueba",
        InstitucionId = SiglaDePrueba,
        Publicado     = publicado
    };

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _conexion?.Dispose();
    }
}
