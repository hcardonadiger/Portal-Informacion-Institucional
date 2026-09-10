using System.Net;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Los archivos subidos no se sirven sin sesión.
///
/// <para>Hasta el 2026-08-26 <c>Program.cs</c> publicaba <c>App_Data/uploads</c> como archivos
/// estáticos en <c>/uploads</c>, y lo hacía <b>antes</b> de <c>UseAuthentication</c>: cualquiera
/// con la URL bajaba el archivo. Se comprobó en ejecución —un <c>fetch</c> con
/// <c>credentials:'omit'</c> devolvió 200 y el contenido—, así que la regresión es real y barata
/// de reintroducir: alcanza con que alguien reponga un <c>UseStaticFiles</c> «para que se vean
/// las fotos».</para>
///
/// <para><b>Cada caso siembra un archivo de verdad</b> antes de pedirlo. Una primera versión de
/// estas pruebas pedía rutas inventadas y pasaba igual con el agujero abierto: un 404 sobre algo
/// que no existe no prueba nada. Verificado con el middleware repuesto a propósito — con él, estas
/// pruebas fallan.</para>
/// </summary>
public sealed class ArchivosProtegidosTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    /// <summary>Las carpetas reales donde cada módulo deja sus subidas.</summary>
    private static readonly string[] Carpetas = ["tickets", "reuniones", "proyectos"];

    /// <summary>Carpeta → ruta web del archivo sembrado en ella.</summary>
    private readonly Dictionary<string, string> _rutasWeb = new();
    private readonly List<string> _sembrados = [];

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        // La raíz de contenido del host de pruebas es la del proyecto Web, que es donde el portal
        // guarda y busca las subidas. Si esto dejara de ser cierto los archivos caerían en otro
        // lado y las pruebas volverían a pasar en vacío, así que se comprueba.
        var env = (IWebHostEnvironment)_portal.Services.GetService(typeof(IWebHostEnvironment))!;
        var raiz = Path.Combine(env.ContentRootPath, "App_Data", "uploads");

        foreach (var carpeta in Carpetas)
        {
            var dir = Path.Combine(raiz, carpeta);
            Directory.CreateDirectory(dir);

            var nombre = $"{Guid.NewGuid():N}.txt";
            var ruta   = Path.Combine(dir, nombre);
            await File.WriteAllTextAsync(ruta, "contenido reservado");

            _sembrados.Add(ruta);
            _rutasWeb[carpeta] = $"/uploads/{carpeta}/{nombre}";
        }
    }

    public Task DisposeAsync()
    {
        foreach (var ruta in _sembrados.Where(File.Exists))
            File.Delete(ruta);

        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("tickets")]
    [InlineData("reuniones")]
    [InlineData("proyectos")]
    public async Task Ninguna_carpeta_de_subidas_se_sirve_sin_sesion(string carpeta)
    {
        var respuesta = await _portal.ClienteAnonimo().GetAsync(_rutasWeb[carpeta]);

        respuesta.StatusCode.Should().NotBe(HttpStatusCode.OK,
            $"el archivo existe en App_Data/uploads/{carpeta}: un 200 significa que volvió a publicarse como estático");
    }

    [Fact]
    public async Task Tampoco_se_sirve_con_sesion_por_la_ruta_cruda()
    {
        // La sesión no era lo que faltaba: la ruta simplemente ya no la atiende nadie. Cada módulo
        // entrega lo suyo por su handler, que además resuelve la entidad y hereda su alcance.
        var respuesta = await _portal.ClienteComo("Administrador").GetAsync(_rutasWeb["tickets"]);

        respuesta.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task El_archivo_sembrado_existe_donde_el_portal_lo_buscaria()
    {
        // Guarda de las pruebas de arriba, no del portal: si la raíz de contenido del host dejara
        // de ser la del proyecto Web, los archivos caerían fuera y todo pasaría sin probar nada.
        _sembrados.Should().HaveCount(3).And.OnlyContain(r => File.Exists(r));
        _sembrados.Should().OnlyContain(r => r.Contains("App_Data"));
    }

    [Fact]
    public async Task Los_estaticos_normales_siguen_sirviendose()
    {
        // Guarda del arreglo: quitar el middleware de /uploads no puede haberse llevado por
        // delante el UseStaticFiles de wwwroot, que es el que entrega el CSS del portal.
        var respuesta = await _portal.ClienteAnonimo().GetAsync("/css/diger.css");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
