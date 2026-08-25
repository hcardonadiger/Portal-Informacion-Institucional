using System.Text.RegularExpressions;
using Diger.TramitesEstado.Api.Lectura;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Api.Tests;

/// <summary>
/// El único hueco que abre separar la API de PortalDigital, y su tapa.
/// </summary>
/// <remarks>
/// <para>
/// Mientras la API compartía las entidades de PortalDigital, renombrar una columna la rompía al
/// compilar. Ahora no: la API describe las ocho tablas que lee por su cuenta, y si PortalDigital
/// renombra <c>TiempoTexto</c> nadie se entera hasta que HondurasÁgil recibe un 500.
/// </para>
/// <para>
/// <b>Ninguna prueba en memoria puede cubrir esto</b>, porque siembra y lee por el mismo modelo:
/// si el modelo se equivoca, se equivoca de forma consistente y todo pasa en verde. Hace falta
/// preguntarle a la base de verdad, y eso es lo que hace esta prueba: compara cada tabla y cada
/// columna que la API declara contra el catálogo de SQL Server.
/// </para>
/// <para>
/// <b>Se salta cuando no hay base a mano</b>, para que las pruebas sigan corriendo en una máquina
/// recién clonada o en un servidor de integración sin SQL Server. Eso la vuelve un guardián de
/// escritorio y de despliegue, no de cada compilación — que es lo correcto: el desfase de esquema
/// no ocurre al escribir código, ocurre al aplicar una migración.
/// </para>
/// </remarks>
public sealed class ModeloContraLaBaseRealTests
{
    /// <summary>Con qué base comparar. Se puede apuntar a otra sin tocar código.</summary>
    private const string VariableDeAmbiente = "PD_CONEXION_PRUEBAS";

    [Fact]
    public async Task Cada_columna_que_la_api_declara_existe_en_la_base()
    {
        var pedidaAdrede = Environment.GetEnvironmentVariable(VariableDeAmbiente) is { Length: > 0 };
        var conexion     = CadenaDeConexion();

        if (conexion is null || !await AlcanzableAsync(conexion))
        {
            // Sin base a mano no se puede comprobar nada. Si alguien la pidió adrede por la
            // variable de ambiente —que es lo que hace el despliegue— no alcanzarla ES el fallo:
            // callarlo convertiría este guardián en un adorno el día que más hace falta.
            pedidaAdrede.Should().BeFalse(
                $"se pidió comparar contra {VariableDeAmbiente} y no se pudo conectar");

            Console.WriteLine(
                $"[omitida] No hay base con que comparar. Para correrla: " +
                $"{VariableDeAmbiente}=\"<cadena>\" dotnet test tests/Api.Tests");
            return;
        }

        var declarado = LoQueLaApiDeclara();
        var enLaBase  = await LoQueLaBaseTieneAsync(conexion);

        var faltantes = declarado
            .Where(c => !enLaBase.Contains(c))
            .OrderBy(c => c)
            .ToList();

        faltantes.Should().BeEmpty(
            "la API lee estas columnas y la base ya no las tiene. Alguien cambió el esquema en " +
            "PortalDigital sin mirar qué publica la API: o se repone el nombre, o se ajusta " +
            "src/Api/Lectura/ModeloDeLectura.cs y se regenera la especificación.");
    }

    // ── Apoyo ─────────────────────────────────────────────────────────────────

    /// <summary>Tabla.columna, tal como el modelo de la API las declara.</summary>
    private static HashSet<string> LoQueLaApiDeclara()
    {
        using var ctx = new ApiDbContext(new DbContextOptionsBuilder<ApiDbContext>()
            .UseSqlServer("Server=irrelevante;Database=irrelevante;").Options);

        var columnas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entidad in ctx.Model.GetEntityTypes())
        {
            var tabla = entidad.GetTableName();
            if (tabla is null) continue;

            foreach (var propiedad in entidad.GetProperties())
                columnas.Add($"{tabla}.{propiedad.GetColumnName()}");
        }

        return columnas;
    }

    private static async Task<HashSet<string>> LoQueLaBaseTieneAsync(string conexion)
    {
        var columnas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();

        await using var cmd = new SqlCommand(
            "SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS", cn);
        await using var lector = await cmd.ExecuteReaderAsync();

        while (await lector.ReadAsync())
            columnas.Add($"{lector.GetString(0)}.{lector.GetString(1)}");

        return columnas;
    }

    private static async Task<bool> AlcanzableAsync(string conexion)
    {
        try
        {
            await using var cn = new SqlConnection(conexion);
            await cn.OpenAsync(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>La del ambiente si está; si no, la de la configuración de desarrollo de la API.</summary>
    private static string? CadenaDeConexion()
    {
        if (Environment.GetEnvironmentVariable(VariableDeAmbiente) is { Length: > 0 } delAmbiente)
            return delAmbiente;

        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !directorio.EnumerateFiles("*.sln").Any())
            directorio = directorio.Parent;

        if (directorio is null) return null;

        var archivo = Path.Combine(directorio.FullName, "src", "Api", "appsettings.Development.json");
        if (!File.Exists(archivo)) return null;

        // A mano y no con el lector de configuración: ese archivo lleva comentarios, y traer
        // Microsoft.Extensions.Configuration.Json solo para esto sería un paquete de más.
        var coincidencia = Regex.Match(File.ReadAllText(archivo),
            @"""DefaultConnection""\s*:\s*""([^""]+)""");

        return coincidencia.Success ? coincidencia.Groups[1].Value : null;
    }
}
