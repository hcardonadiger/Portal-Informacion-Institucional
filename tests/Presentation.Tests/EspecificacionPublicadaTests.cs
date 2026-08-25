using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Presentation.Tests;

/// <summary>
/// La comprobación que impide que vuelva a haber dos documentaciones.
///
/// Hasta la Fase 6 convivían dos descripciones de la misma API: la que Swagger genera del código
/// —que no puede mentir— y un <c>openapi-v1.yaml</c> escrito y mantenido a mano. Cuando dos
/// documentos describen lo mismo, divergen; y esta divergencia en particular no la nota nadie de
/// este lado, porque quien la sufre es el integrador, y la descubre cuando su código ya falló.
///
/// A partir de acá hay un solo documento y lo genera el código. Este archivo comprueba que la
/// copia comprometida al repositorio siga siendo la que el código produce hoy.
/// </summary>
public sealed class EspecificacionPublicadaTests : IAsyncLifetime
{
    private readonly ApiFactory _api = new();

    /// <summary>Dónde vive la copia comprometida, relativa a la raíz del repositorio.</summary>
    private const string RutaRelativa = "docs/api-v1/openapi-v1.yaml";

    /// <summary>Poner esta variable de entorno regenera el archivo en vez de fallar.</summary>
    private const string VariableParaRegenerar = "ACTUALIZAR_SPEC";

    /// <summary>
    /// Lo que va al principio del archivo comprometido. Forma parte de la comparación a
    /// propósito: si alguien borra el aviso, la prueba lo repone. Un archivo generado sin un
    /// cartel que lo diga es una invitación a editarlo a mano, que es exactamente el problema
    /// que esta fase vino a cerrar.
    /// </summary>
    private const string Encabezado =
        "# =============================================================================\n" +
        "# ESTE ARCHIVO SE GENERA. No lo edite a mano: la próxima regeneración lo pisa.\n" +
        "#\n" +
        "# Lo produce el código de src/Presentation — las rutas, los tipos de respuesta y los\n" +
        "# comentarios XML de los controladores — y lo vigila\n" +
        "# tests/Presentation.Tests/EspecificacionPublicadaTests.cs, que falla si este archivo\n" +
        "# y el código dejan de coincidir.\n" +
        "#\n" +
        "# Para regenerarlo después de cambiar la API:\n" +
        "#\n" +
        "#     ACTUALIZAR_SPEC=1 dotnet test tests/Presentation.Tests\n" +
        "#\n" +
        "# Lo que una especificación no sabe decir —cómo integrarse, las dos cadencias de\n" +
        "# sincronización, qué significa fichaCompleta, dónde vive cada ambiente— está en\n" +
        "# docs/api-v1/README.md, que sí se escribe a mano.\n" +
        "# =============================================================================\n";

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _api.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task La_especificacion_comprometida_es_la_que_el_codigo_genera_hoy()
    {
        var generada = await DescargarAsync();
        var archivo  = ArchivoComprometido();

        if (Environment.GetEnvironmentVariable(VariableParaRegenerar) is { Length: > 0 })
        {
            await File.WriteAllTextAsync(archivo, Normalizar(Encabezado + generada));
            return;
        }

        File.Exists(archivo).Should().BeTrue(
            $"la especificación debería estar comprometida en {RutaRelativa}; " +
            $"para crearla, corra las pruebas con {VariableParaRegenerar}=1");

        var comprometida = await File.ReadAllTextAsync(archivo);

        Normalizar(comprometida).Should().Be(Normalizar(Encabezado + generada),
            $"la API cambió de forma y {RutaRelativa} se quedó atrás. No edite ese archivo a " +
            $"mano —se genera—: corra las pruebas con {VariableParaRegenerar}=1 y revise el " +
            "diff. Si el cambio no era intencional, el error está en el código, no en el archivo.");
    }

    /// <summary>
    /// El documento no debe traer un «servidor» fijo. Si lo trajera sería el host de quien lo
    /// generó, y la especificación diría que la API vive en la máquina de esa persona.
    /// </summary>
    [Fact]
    public async Task La_especificacion_no_clava_la_direccion_de_ningun_servidor()
    {
        var generada = await DescargarAsync();

        generada.Should().NotContain("localhost",
            "la dirección de la API depende del ambiente, no del contrato; las de cada ambiente " +
            "van en docs/api-v1/README.md");
    }

    /// <summary>
    /// La superficie exacta, ni una ruta más ni una menos.
    ///
    /// Hacia abajo protege a HondurasÁgil: ya depende de las siete, y retirar cualquiera es un
    /// cambio de versión y no un ajuste. Hacia arriba obliga a que una ruta nueva pase por acá
    /// antes de existir para nadie, que es cuando todavía es barato decidir si de verdad
    /// pertenece a la v1 o si es la primera pieza de una v2.
    /// </summary>
    [Fact]
    public async Task La_superficie_publica_son_estas_siete_rutas_y_ninguna_mas()
    {
        var generada = await DescargarAsync();

        RutasDe(generada).Should().BeEquivalentTo(
        [
            "/api/v1/salud",
            "/api/v1/tramites",
            "/api/v1/tramites/{codigo}",
            "/api/v1/instituciones",
            "/api/v1/categorias",
            "/api/v1/cambios",
            "/api/v1/codigos-publicados"
        ]);
    }

    // ── Apoyo ─────────────────────────────────────────────────────────────────

    private async Task<string> DescargarAsync()
    {
        // Sin clave a propósito: la documentación de una API no puede exigir la credencial que
        // uno todavía no tiene. Si algún día se protege, este 200 lo delata.
        var respuesta = await _api.ClienteSinClave().GetAsync("/swagger/v1/swagger.yaml");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
            "sin esta ruta no hay especificación generada y toda la fase se cae");

        return await respuesta.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Las claves de <c>paths:</c>. Se leen con expresión regular y no partiendo por «:» porque
    /// YAML entrecomilla las rutas con llaves —<c>'/api/v1/tramites/{codigo}':</c>— y esa comilla
    /// ya costó un falso fallo.
    /// </summary>
    private static IReadOnlyList<string> RutasDe(string yaml) =>
        Regex.Matches(yaml, @"^  '?(/[^':\s]*)'?:\s*$", RegexOptions.Multiline)
             .Select(m => m.Groups[1].Value)
             .ToList();

    /// <summary>
    /// Los finales de línea dependen de la configuración de git y del sistema de quien genere el
    /// archivo; el contrato no. Se normalizan los del archivo y también los que viajan
    /// <b>escapados dentro</b> del YAML, porque esos vienen del .xml de documentación y salen
    /// distintos según dónde se haya compilado.
    /// </summary>
    private static string Normalizar(string texto) =>
        texto.Replace("\r\n", "\n").Replace("\\r\\n", "\\n").TrimEnd() + "\n";

    /// <summary>
    /// Sube desde el directorio de salida hasta encontrar la raíz del repositorio. Se busca por
    /// el .sln y no por una cantidad fija de «..» porque esa cantidad cambia con el framework y
    /// la configuración de compilación, y romper la prueba al pasar de Debug a Release sería un
    /// fallo sin ninguna relación con la API.
    /// </summary>
    private static string ArchivoComprometido()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null && !directorio.EnumerateFiles("*.sln").Any())
            directorio = directorio.Parent;

        directorio.Should().NotBeNull("no se encontró la raíz del repositorio desde " + AppContext.BaseDirectory);

        return Path.Combine(directorio!.FullName, RutaRelativa.Replace('/', Path.DirectorySeparatorChar));
    }
}
