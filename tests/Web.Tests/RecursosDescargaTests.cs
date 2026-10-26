using System.Net;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La descarga de Recursos y Plantillas.
///
/// <para>Recursos resolvía la ruta física a su manera en vez de usar <c>ArchivosProtegidos</c>,
/// como el resto de los módulos. De ahí salían dos fallas: anotaba la descarga <b>antes</b> de saber
/// si el archivo estaba —cada clic fallido subía el contador— y la ruta guardada podía salirse de la
/// carpeta de subidas, porque no se comprobaba adónde llegaba.</para>
/// </summary>
public sealed class RecursosDescargaTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private readonly List<string> _sembrados = [];

    private int _conArchivo;
    private int _sinArchivo;
    private int _fueraDeLaCarpeta;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Empleado", "Recursos.Ver");

        var env = _portal.Services.GetRequiredService<IWebHostEnvironment>();
        var dir = Path.Combine(env.ContentRootPath, "App_Data", "uploads", "recursos");
        Directory.CreateDirectory(dir);

        var nombreEnDisco = $"{Guid.NewGuid():N}.pdf";
        var ruta = Path.Combine(dir, nombreEnDisco);
        await File.WriteAllTextAsync(ruta, "contenido de la plantilla");
        _sembrados.Add(ruta);

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conArchivo = Recurso.Crear("Plantilla vigente", null, "Plantilla",
            "plantilla.pdf", $"/uploads/recursos/{nombreEnDisco}", 25);
        var sinArchivo = Recurso.Crear("Plantilla perdida", null, "Plantilla",
            "perdida.pdf", $"/uploads/recursos/{Guid.NewGuid():N}.pdf", 25);
        // appsettings.json está en la raíz del sitio: existe, así que si la ruta no se acotara
        // a la carpeta de subidas se serviría.
        var fuera = Recurso.Crear("Fila cargada por script", null, "Otro",
            "config.json", "/uploads/../../appsettings.json", 25);

        db.Recursos.AddRange(conArchivo, sinArchivo, fuera);
        await db.SaveChangesAsync();

        _conArchivo = conArchivo.Id;
        _sinArchivo = sinArchivo.Id;
        _fueraDeLaCarpeta = fuera.Id;
    }

    public Task DisposeAsync()
    {
        foreach (var ruta in _sembrados)
            if (File.Exists(ruta)) File.Delete(ruta);
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private Task<HttpResponseMessage> DescargarAsync(int id) =>
        _portal.ClienteComo("Empleado").GetAsync($"/Recursos?handler=Descargar&id={id}");

    private async Task<int> DescargasDeAsync(int id)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Recursos.AsNoTracking().Where(r => r.Id == id)
            .Select(r => r.DescargasCount).SingleAsync();
    }

    [Fact]
    public async Task Un_archivo_que_esta_se_descarga_y_cuenta_una_vez()
    {
        var respuesta = await DescargarAsync(_conArchivo);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await respuesta.Content.ReadAsStringAsync()).Should().Be("contenido de la plantilla");
        (await DescargasDeAsync(_conArchivo)).Should().Be(1);
    }

    [Fact]
    public async Task Un_archivo_que_falta_avisa_y_no_suma_descargas()
    {
        var respuesta = await DescargarAsync(_sinArchivo);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await DescargasDeAsync(_sinArchivo)).Should().Be(0,
            "una descarga que no ocurrió no se anota");
    }

    [Fact]
    public async Task Una_ruta_que_se_sale_de_la_carpeta_de_subidas_no_se_sirve()
    {
        var respuesta = await DescargarAsync(_fueraDeLaCarpeta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await DescargasDeAsync(_fueraDeLaCarpeta)).Should().Be(0);
    }

    [Fact]
    public async Task Un_id_que_no_existe_es_404()
    {
        var respuesta = await DescargarAsync(999_999);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
