using Diger.TramitesEstado.Api.Lectura;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Api.Tests.Consultas;

/// <summary>
/// Una base de una sola prueba, en memoria.
/// </summary>
/// <remarks>
/// SQLite y no el proveedor InMemory: InMemory no es una base de datos sino un diccionario, y
/// deja pasar consultas que SQL Server no traduciría. Acá las consultas se traducen a SQL de
/// verdad, que es lo más parecido a producción que se puede tener sin un servidor.
/// </remarks>
public sealed class BaseDePruebas : IDisposable
{
    private readonly SqliteConnection _conexion;

    public ApiDbContext Ctx { get; }

    public BaseDePruebas()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();

        Ctx = new ApiDbContext(new DbContextOptionsBuilder<ApiDbContext>()
            .UseSqlite(_conexion).Options) { SembrandoEnPruebas = true };

        Ctx.Database.EnsureCreated();
    }

    /// <summary>Guarda lo sembrado. Existe para que ninguna prueba tenga que acordarse de que
    /// el contexto de esta API lleva la escritura cerrada con llave.</summary>
    public Task SembrarAsync() => Ctx.SaveChangesAsync();

    /// <summary>Una ficha mínima. Lo demás se ajusta en cada prueba.</summary>
    public static FichaSiger Ficha(string codigo, string nombre, bool publicado = true,
        string institucionId = "INPREMA",
        string institucion = "Instituto Nacional de Previsión del Magisterio") => new()
    {
        Codigo = codigo, Nombre = nombre, Institucion = institucion,
        InstitucionId = institucionId, Publicado = publicado
    };

    /// <summary>Una institución mínima y activa.</summary>
    public static Institucion Inst(string id, string nombre, string? rutaSol = null) => new()
    {
        Id = id, Nombre = nombre, Activo = true, RutaSol = rutaSol
    };

    public void Dispose()
    {
        Ctx.Dispose();
        _conexion.Dispose();
    }
}
