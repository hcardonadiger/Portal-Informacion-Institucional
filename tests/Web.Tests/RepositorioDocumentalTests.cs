using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El repositorio documental, recorrido de punta a punta contra el portal levantado: subir un
/// documento, verlo en la ficha, descargarlo y versionarlo.
///
/// <para>Es la prueba que sustituye a lo que no se puede hacer a mano acá —entrar con una cuenta y
/// hacer clic—, y cubre justamente lo que el compilador no ve: que el formulario postee los
/// nombres que el modelo espera, que el archivo llegue a disco, y que la descarga vuelva con el
/// contenido correcto.</para>
/// </summary>
public sealed class RepositorioDocumentalTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _proyectoId;
    private readonly List<string> _archivos = [];
    private string _raizUploads = "";

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea",
            "Proyectos.Ver", "Proyectos.Editar",
            "Proyectos.Documentos.Ver", "Proyectos.Documentos.Crear",
            "Proyectos.Documentos.Editar", "Proyectos.Documentos.Eliminar");
        await _portal.OtorgarAsync("Consultor", "Proyectos.Ver", "Proyectos.Documentos.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.CategoriasDocumento.Add(CategoriaDocumento.Crear("Convenio", 1));
        await db.SaveChangesAsync();

        var proyecto = Proyecto.Crear("PRY-2026-90", "Frente documental");
        proyecto.InstitucionId = "DIGER";
        db.Proyectos.Add(proyecto);
        await db.SaveChangesAsync();
        _proyectoId = proyecto.Id;

        var env = (IWebHostEnvironment)_portal.Services.GetService(typeof(IWebHostEnvironment))!;
        _raizUploads = Path.Combine(env.ContentRootPath, "App_Data", "uploads", "proyectos", "documentos");
    }

    public Task DisposeAsync()
    {
        // Los archivos que la prueba dejó en disco. Se identifican por contenido, no por nombre:
        // el portal los guarda con GUID.
        foreach (var ruta in _archivos.Where(File.Exists))
            File.Delete(ruta);

        _portal.Dispose();
        return Task.CompletedTask;
    }

    // ── Andamiaje ─────────────────────────────────────────────────
    /// <summary>
    /// Razor Pages valida el token antifalsificación en todo POST, así que hay que pedirlo como lo
    /// haría un navegador: cargando la página primero. La cookie viaja sola porque el cliente de
    /// pruebas las conserva.
    /// </summary>
    private async Task<(HttpClient Cliente, string Token)> SesionAsync(string rol = "JefeArea")
    {
        var cliente = _portal.ClienteComo(rol);
        var html = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoId}");

        var token = Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        token.Should().NotBeEmpty("sin el token ningún POST del portal pasaría");
        return (cliente, token);
    }

    private static MultipartFormDataContent Formulario(string token, string archivo, string contenido,
        params (string Campo, string Valor)[] campos)
    {
        var form = new MultipartFormDataContent { { new StringContent(token), "__RequestVerificationToken" } };

        foreach (var (campo, valor) in campos)
            form.Add(new StringContent(valor), campo);

        var bytes = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(contenido));
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(bytes, "DocArchivo", archivo);

        return form;
    }

    private void RecordarArchivos()
    {
        if (Directory.Exists(_raizUploads))
            _archivos.AddRange(Directory.GetFiles(_raizUploads));
    }

    private async Task<int> IdDelUnicoDocumentoAsync()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ProyectoDocumentos.IgnoreQueryFilters()
            .Where(d => d.ProyectoId == _proyectoId).Select(d => d.Id).SingleAsync();
    }

    // ── Alta y descarga ───────────────────────────────────────────
    [Fact]
    public async Task Subir_un_documento_lo_deja_visible_y_descargable()
    {
        var (cliente, token) = await SesionAsync();

        var alta = await cliente.PostAsync(
            $"/Proyectos/Editor/{_proyectoId}?handler=SubirDocumento",
            Formulario(token, "convenio.pdf", "contenido del convenio",
                ("DocTitulo", "Convenio marco DIGER–SRECI"),
                ("DocCategoriaId", "1"),
                ("DocDescripcion", "Firmado el 12 de agosto")));
        RecordarArchivos();

        // Los handlers redirigen a la ficha con el ancla de su pestaña.
        alta.StatusCode.Should().Be(HttpStatusCode.Redirect);
        alta.Headers.Location!.ToString().Should().Contain("#documentos");

        var ficha = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoId}");
        ficha.Should().Contain("Convenio marco DIGER–SRECI");
        ficha.Should().Contain("convenio.pdf");
        ficha.Should().Contain("Firmado el 12 de agosto");

        // Y el archivo baja de verdad, con su nombre original y su contenido.
        var versionId = await PrimeraVersionAsync();
        var descarga = await cliente.GetAsync($"/Proyectos/Editor/{_proyectoId}?handler=Documento&versionId={versionId}");

        descarga.StatusCode.Should().Be(HttpStatusCode.OK);
        (await descarga.Content.ReadAsStringAsync()).Should().Be("contenido del convenio");
        descarga.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("convenio.pdf");
    }

    private async Task<int> PrimeraVersionAsync()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ProyectoDocumentoVersiones.IgnoreQueryFilters()
            .OrderBy(v => v.Id).Select(v => v.Id).FirstAsync();
    }

    [Fact]
    public async Task El_archivo_queda_en_disco_con_nombre_GUID_y_su_huella_registrada()
    {
        var (cliente, token) = await SesionAsync();

        await cliente.PostAsync($"/Proyectos/Editor/{_proyectoId}?handler=SubirDocumento",
            Formulario(token, "acta.pdf", "hola",
                ("DocTitulo", "Acta"), ("DocCategoriaId", "1")));
        RecordarArchivos();

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var version = await db.ProyectoDocumentoVersiones.IgnoreQueryFilters().SingleAsync();

        version.ArchivoNombre.Should().Be("acta.pdf", "el nombre original se conserva en la base");
        version.ArchivoUrl.Should().NotContain("acta.pdf", "en disco va con nombre GUID");

        // SHA-256 de "hola", calculado aparte: la huella tiene que ser del contenido, no del nombre.
        version.Sha256.Should().Be("b221d9dbb083a7f33428d7c2a3c3198ae925614d70210e28716ccaa7cd4ddb79");
    }

    // ── Versionado ────────────────────────────────────────────────
    [Fact]
    public async Task Subir_una_version_conserva_la_anterior_en_el_historial()
    {
        var (cliente, token) = await SesionAsync();

        await cliente.PostAsync($"/Proyectos/Editor/{_proyectoId}?handler=SubirDocumento",
            Formulario(token, "v1.pdf", "primera",
                ("DocTitulo", "Convenio"), ("DocCategoriaId", "1")));

        var documentoId = await IdDelUnicoDocumentoAsync();

        await cliente.PostAsync(
            $"/Proyectos/Editor/{_proyectoId}?handler=SubirVersion&documentoId={documentoId}",
            Formulario(token, "v2.pdf", "segunda", ("DocNotas", "se corrigió la cláusula tercera")));
        RecordarArchivos();

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var versiones = await db.ProyectoDocumentoVersiones.IgnoreQueryFilters()
            .OrderBy(v => v.Numero).ToListAsync();

        versiones.Should().HaveCount(2);
        versiones.Select(v => v.Numero).Should().Equal(1, 2);
        versiones[0].ArchivoNombre.Should().Be("v1.pdf", "la primera versión no se pisa");
        versiones[1].Notas.Should().Be("se corrigió la cláusula tercera");

        var ficha = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoId}");
        ficha.Should().Contain("Historial de versiones (2)");
        ficha.Should().Contain("v1.pdf", "la versión vieja se sigue viendo y descargando");
    }

    // ── Auditoría ─────────────────────────────────────────────────
    [Fact]
    public async Task Cada_movimiento_queda_en_la_auditoria_del_proyecto()
    {
        var (cliente, token) = await SesionAsync();

        await cliente.PostAsync($"/Proyectos/Editor/{_proyectoId}?handler=SubirDocumento",
            Formulario(token, "acta.pdf", "x",
                ("DocTitulo", "Acta de entrega"), ("DocCategoriaId", "1")));
        RecordarArchivos();

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entradas = await db.BitacorasProyecto
            .Where(b => b.ProyectoId == _proyectoId && b.Tipo == TipoEventoProyecto.Documentacion)
            .ToListAsync();

        entradas.Should().ContainSingle()
            .Which.Detalle.Should().Contain("Acta de entrega");
    }

    [Fact]
    public async Task El_numero_de_version_se_renderiza_y_no_sale_como_texto_crudo()
    {
        // «v@doc.Vigente!.Numero» compila y pasa el build, pero Razor lo lee como una dirección de
        // correo y lo emite literal: en pantalla decía «v@doc.Vigente!.Numero · 2 versiones».
        // Hay que escribirlo v@(...). Es el mismo gotcha que ya había aparecido en otro portal.
        var (cliente, token) = await SesionAsync();

        await cliente.PostAsync($"/Proyectos/Editor/{_proyectoId}?handler=SubirDocumento",
            Formulario(token, "v1.pdf", "primera", ("DocTitulo", "Convenio"), ("DocCategoriaId", "1")));

        var documentoId = await IdDelUnicoDocumentoAsync();
        await cliente.PostAsync(
            $"/Proyectos/Editor/{_proyectoId}?handler=SubirVersion&documentoId={documentoId}",
            Formulario(token, "v2.pdf", "segunda", ("DocNotas", "corrección")));
        RecordarArchivos();

        var ficha = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoId}");

        ficha.Should().Contain("v2 · 2 versiones");
        ficha.Should().NotContain("@doc.Vigente", "Razor lo habría emitido crudo");
        ficha.Should().NotContain("@ver.Numero");
    }

    // ── Permisos ──────────────────────────────────────────────────
    [Fact]
    public async Task Quien_solo_puede_consultar_no_recibe_el_formulario_de_subida()
    {
        var ficha = await _portal.ClienteComo("Consultor").GetStringAsync($"/Proyectos/Editor/{_proyectoId}");

        ficha.Should().Contain("id=\"panel-documentos\"", "consultar la documentación sí puede");
        ficha.Should().NotContain("handler=\"SubirDocumento\"");
        ficha.Should().NotContain("+ Agregar un documento");
    }

    [Fact]
    public async Task Sin_la_clave_de_subida_el_handler_rechaza_aunque_se_lo_llame_directo()
    {
        // El formulario no se pinta, pero eso solo esconde el botón: lo que de verdad protege es
        // el permiso sobre el handler.
        var cliente = _portal.ClienteComo("Consultor");
        var html = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoId}");
        var token = Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        var respuesta = await cliente.PostAsync(
            $"/Proyectos/Editor/{_proyectoId}?handler=SubirDocumento",
            Formulario(token, "colado.pdf", "x",
                ("DocTitulo", "Colado"), ("DocCategoriaId", "1")));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
