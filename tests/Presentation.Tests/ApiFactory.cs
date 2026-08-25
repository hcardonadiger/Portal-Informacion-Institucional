using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diger.TramitesEstado.Presentation.Tests;

/// <summary>
/// Levanta la API pública en memoria.
///
/// Es hermana de <c>PortalFactory</c> (Web.Tests) pero deliberadamente más chica: esta API no
/// tiene sesión, ni roles, ni permisos —lleva una clave estática y nada más—, así que no hace
/// falta sembrar usuarios ni recargar catálogos.
///
/// Tres cosas que conviene saber antes de tocar esto:
///
/// 1. **SQLite en memoria, no el provider InMemory.** Igual que en el portal: los filtros de
///    AppDbContext usan subconsultas que InMemory no traduce, y la base vive mientras la
///    conexión siga abierta.
///
/// 2. **Swagger se enciende a mano.** El ambiente acá es «Testing», y Program.cs solo publica
///    Swagger en Development o si alguien lo pide por configuración. Se pide por configuración,
///    que es justo la ruta que usa el servidor de integración: así la prueba comprueba el mismo
///    camino que corre en un ambiente real, no uno exclusivo de las pruebas.
///
/// 3. **La clave se inyecta por configuración.** Es donde el handler la busca de verdad
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
                ["PortalDigitalApi:ApiKey"]         = Clave,
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
                    s.ServiceType == typeof(DbContextOptions<AppDbContext>)
                 || s.ServiceType == typeof(DbContextOptions)
                 || s.ServiceType == typeof(AppDbContext)
                 || (s.ServiceType.IsGenericType
                     && s.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration")
                     && s.ServiceType.GenericTypeArguments.Contains(typeof(AppDbContext))))
                .ToList();

            foreach (var registro in registrosDelContexto)
                services.Remove(registro);

            _conexion = new SqliteConnection("DataSource=:memory:");
            _conexion.Open();

            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_conexion));
        });
    }

    /// <summary>Crea el esquema y siembra la institución a la que apuntan las fichas de prueba.
    /// Llamar una vez por fixture, antes de cualquier petición que toque datos.</summary>
    public async Task PrepararAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        // Sin ella, sembrar una ficha con InstitucionId = "IDP" viola la clave foránea. Va acá
        // y no en cada prueba porque el helper Ficha() la da por sentada.
        if (!await db.Instituciones.AnyAsync(i => i.Id == SiglaDePrueba))
        {
            db.Instituciones.Add(Institucion.Crear(SiglaDePrueba, "Instituto de Prueba"));
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Ejecuta algo contra la base de la API. Se usa para sembrar.</summary>
    public async Task ConLaBaseAsync(Func<AppDbContext, Task> accion)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await accion(db);
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
    public static TramiteSiger Ficha(string codigo, string nombre, bool publicado = true) => new()
    {
        Codigo        = codigo,
        Nombre        = nombre,
        EstadoSiger   = "Registrado",
        Institucion   = "Instituto de Prueba",
        Sigla         = SiglaDePrueba,
        InstitucionId = SiglaDePrueba,
        Publicado     = publicado
    };

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _conexion?.Dispose();
    }
}
