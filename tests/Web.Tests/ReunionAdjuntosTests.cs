using System.Net;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Documentos y enlaces del cierre de una reunión, en el editor y en el acta.
///
/// <para>Cubre tres cosas que se decidieron juntas el 2026-10-02:</para>
///
/// <list type="number">
/// <item>«Detalle de capacitación» se quitó de las dos pantallas porque repetía el objetivo y el
/// desarrollo de la memoria. El <c>Tema</c>, que era lo único propio de esa sección, se mudó a la
/// memoria: no lo captura ninguna pantalla —lo trae el importador— y si no se mudaba, dejaba de
/// verse.</item>
/// <item>«Documentos y recursos» pasó de ser un cuadro de texto a aceptar archivos y enlaces.</item>
/// <item>La evidencia conserva sus dos fotos y gana enlaces, porque el video no se sube: se deja en
/// OneDrive.</item>
/// </list>
///
/// <para><b>La prueba que más importa</b> es la del adjunto ajeno. Todos los módulos escriben en la
/// misma carpeta de subidas, así que si el handler aceptara la ruta por la URL, cualquiera con
/// acceso a una reunión bajaría los archivos de las demás con solo cambiarla. Siembra un archivo de
/// verdad: un 404 sobre algo que no existe no probaría nada.</para>
/// </summary>
public sealed class ReunionAdjuntosTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    private int _reunionId;
    private int _otraReunionId;
    private int _adjuntoDocId;
    private int _adjuntoAjenoId;
    private string _urlPropia = "";
    private string _urlAjena  = "";
    private readonly List<string> _sembrados = [];

    private const string EnlaceOneDrive = "https://digerhn-my.sharepoint.com/:v:/g/personal/abc/XYZ?e=aB1cD2";

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Reuniones.Ver", "Reuniones.Editar");

        var env = _portal.Services.GetRequiredService<IWebHostEnvironment>();
        var dir = Path.Combine(env.ContentRootPath, "App_Data", "uploads", "reuniones");
        Directory.CreateDirectory(dir);

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _reunionId     = await SembrarAsync(db, dir, "Taller de modelado con CONSUCOOP", "Ventanilla única");
        _otraReunionId = await SembrarAsync(db, dir, "Reunión técnica de seguimiento", tema: null);

        _adjuntoDocId = await db.Set<ReunionAdjunto>()
            .Where(a => a.ReunionId == _reunionId && a.Clase == ClaseAdjuntoReunion.Archivo)
            .Select(a => a.Id).FirstAsync();

        _adjuntoAjenoId = await db.Set<ReunionAdjunto>()
            .Where(a => a.ReunionId == _otraReunionId && a.Clase == ClaseAdjuntoReunion.Archivo)
            .Select(a => a.Id).FirstAsync();

        _urlPropia = await db.Set<ReunionAdjunto>()
            .Where(a => a.Id == _adjuntoDocId).Select(a => a.Url).FirstAsync();
        _urlAjena = await db.Set<ReunionAdjunto>()
            .Where(a => a.Id == _adjuntoAjenoId).Select(a => a.Url).FirstAsync();
    }

    /// <summary>Crea una reunión con un archivo real en disco y un enlace externo.</summary>
    private async Task<int> SembrarAsync(AppDbContext db, string dir, string titulo, string? tema)
    {
        var nombreEnDisco = $"{Guid.NewGuid():N}.pdf";
        var ruta = Path.Combine(dir, nombreEnDisco);
        await File.WriteAllTextAsync(ruta, "acta firmada de mentira");
        _sembrados.Add(ruta);

        var r = Reunion.Crear(titulo);
        r.InstitucionId = "DIGER";
        r.Institucion   = "CONSUCOOP";
        r.Fecha         = new DateOnly(2026, 9, 1);
        r.Tipo          = "Capacitación";
        r.EsCapacitacionPlataforma = true;
        r.Tema          = tema;
        r.ObjetivoAgenda = "Levantar el flujo actual del trámite";

        // Lo que antes se capturaba en «Detalle de capacitación». Las columnas siguen ahí con su
        // contenido; lo que se prueba abajo es que ninguna pantalla lo vuelva a mostrar.
        r.ObjetivoCap = "OBJETIVO FORMATIVO HEREDADO";
        r.Contenido   = "CONTENIDO HEREDADO";

        r.Agregar(ReunionAdjunto.ParaArchivo(
            SeccionAdjuntoReunion.Documento, 0,
            "Acta firmada", null, "Acta firmada.pdf", $"/uploads/reuniones/{nombreEnDisco}", 23));

        r.Agregar(ReunionAdjunto.ParaEnlace(
            SeccionAdjuntoReunion.Evidencia, 0,
            "Grabación de la sesión", "Dura 48 minutos", EnlaceOneDrive));

        db.Reuniones.Add(r);
        await db.SaveChangesAsync();
        return r.Id;
    }

    public Task DisposeAsync()
    {
        foreach (var ruta in _sembrados.Where(File.Exists)) File.Delete(ruta);
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private Task<string> ActaAsync(int? id = null) =>
        _portal.ClienteComo("JefeArea").GetStringAsync($"/Reuniones/Acta/{id ?? _reunionId}");

    private Task<string> EditorAsync() =>
        _portal.ClienteComo("JefeArea").GetStringAsync($"/Reuniones/Editor/{_reunionId}");

    // ── El detalle de capacitación se fue ─────────────────────────────────────

    [Fact]
    public async Task El_acta_ya_no_muestra_el_detalle_de_capacitacion()
    {
        var html = await ActaAsync();

        html.Should().NotContain("Detalle de capacitación");
        html.Should().NotContain("OBJETIVO FORMATIVO HEREDADO");
        html.Should().NotContain("CONTENIDO HEREDADO");
    }

    [Fact]
    public async Task El_editor_ya_no_pide_el_detalle_de_capacitacion()
    {
        var html = await EditorAsync();

        html.Should().NotContain("Detalle de capacitación");
        html.Should().NotContain("Objetivo de la capacitación");
        html.Should().NotContain("Datos.ObjetivoCap");
        html.Should().NotContain("Datos.Contenido");
    }

    [Fact]
    public async Task El_paso_de_capacitacion_conserva_sus_resultados()
    {
        // Se quitó una tarjeta de las dos, no el paso entero: las cifras de asistencia y los
        // compromisos sí son propios de una capacitación.
        var html = await EditorAsync();

        html.Should().Contain("Resultados de la capacitación");
        html.Should().Contain("Datos.Convocados");
    }

    [Fact]
    public async Task El_tema_se_sigue_viendo_dentro_de_la_memoria()
    {
        // Decodificado: Razor escapa las tildes y «Ventanilla única» sale como «Ventanilla &#xFA;nica».
        var html = System.Net.WebUtility.HtmlDecode(await ActaAsync());

        html.Should().Contain("Ventanilla única", "el Tema no lo captura ninguna pantalla: si no sale acá, se pierde");

        var memoria = System.Text.RegularExpressions.Regex.Match(
            html, "(?s)<h2>Memoria de la reunión</h2>.*?</section>").Value;
        memoria.Should().Contain("Ventanilla única");
    }

    // ── Documentos y recursos ─────────────────────────────────────────────────

    [Fact]
    public async Task El_editor_ofrece_subir_archivos_y_pegar_enlaces()
    {
        var html = await EditorAsync();

        html.Should().Contain("+ Agregar archivo");
        html.Should().Contain("+ Agregar enlace");
        html.Should().Contain("AdjuntosDoc[0].Titulo");
    }

    [Fact]
    public async Task El_editor_explica_que_los_videos_van_por_enlace_a_onedrive()
    {
        // La nota es la que el usuario pidió explícitamente: sin ella nadie sabe por qué no hay
        // un botón para subir el video.
        var html = System.Net.WebUtility.HtmlDecode(await EditorAsync());

        html.Should().Contain("OneDrive");
        html.Should().Contain("No se suben al portal");
    }

    [Fact]
    public async Task El_editor_acepta_formatos_de_office_y_pdf()
    {
        var html = await EditorAsync();

        foreach (var ext in new[] { ".pdf", ".docx", ".xlsx", ".pptx" })
            html.Should().Contain(ext);
    }

    [Fact]
    public async Task El_acta_lista_el_documento_adjunto_con_su_enlace_de_descarga()
    {
        var html = await ActaAsync();

        html.Should().Contain("Acta firmada");
        html.Should().Contain($"adjuntoId={_adjuntoDocId}",
            "el archivo se pide por Id y no por ruta: ver OnGetAdjuntoAsync");
    }

    // ── Evidencia ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task El_editor_conserva_los_dos_cupos_de_foto()
    {
        var html = await EditorAsync();

        html.Should().Contain("Foto1File");
        html.Should().Contain("Foto2File");
        html.Should().Contain("Evidencia fotográfica y de video");
    }

    [Fact]
    public async Task El_editor_permite_varios_enlaces_de_evidencia()
    {
        var html = await EditorAsync();

        html.Should().Contain("AdjuntosEvi[0].Url");
        html.Should().Contain("Videos y evidencia adicional");
    }

    [Fact]
    public async Task El_acta_abre_el_enlace_externo_en_otra_pestana_y_sin_opener()
    {
        var html = System.Net.WebUtility.HtmlDecode(await ActaAsync());

        html.Should().Contain("Grabación de la sesión");
        html.Should().Contain(EnlaceOneDrive);

        var ancla = System.Text.RegularExpressions.Regex.Match(
            html, "<a[^>]*href=\"" + System.Text.RegularExpressions.Regex.Escape(EnlaceOneDrive) + "\"[^>]*>").Value;

        ancla.Should().Contain("target=\"_blank\"");
        ancla.Should().Contain("noopener", "la pestaña destino no debe poder tocar la que la abrió");
        ancla.Should().Contain("noreferrer");
    }

    // ── Servir el archivo ─────────────────────────────────────────────────────

    [Fact]
    public async Task El_adjunto_se_descarga_con_su_nombre_original()
    {
        var r = await _portal.ClienteComo("JefeArea")
            .GetAsync($"/Reuniones/Acta/{_reunionId}?handler=Adjunto&adjuntoId={_adjuntoDocId}");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        r.Content.Headers.ContentDisposition!.FileName.Should().Contain("Acta firmada.pdf",
            "el GUID del disco no le dice nada a quien lo baja");
    }

    [Fact]
    public async Task No_se_puede_bajar_el_adjunto_de_otra_reunion()
    {
        // El archivo existe y el usuario puede ver las dos reuniones; lo que se rechaza es pedirle
        // a una reunión un adjunto que no es suyo. Si el handler aceptara rutas en vez de Ids,
        // esto bajaría el archivo sin chistar.
        var r = await _portal.ClienteComo("JefeArea")
            .GetAsync($"/Reuniones/Acta/{_reunionId}?handler=Adjunto&adjuntoId={_adjuntoAjenoId}");

        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task El_adjunto_no_se_sirve_sin_sesion()
    {
        var r = await _portal.ClienteAnonimo()
            .GetAsync($"/Reuniones/Acta/{_reunionId}?handler=Adjunto&adjuntoId={_adjuntoDocId}");

        r.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Pedir_un_adjunto_inexistente_es_404_y_no_un_error()
    {
        var r = await _portal.ClienteComo("JefeArea")
            .GetAsync($"/Reuniones/Acta/{_reunionId}?handler=Adjunto&adjuntoId=999999");

        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Guardar desde el formulario ───────────────────────────────────────────

    /// <summary>El cliente y el token antifalsificación del editor de la reunión sembrada.</summary>
    private async Task<(HttpClient Cliente, string Token)> EditorConTokenAsync()
    {
        var cliente = _portal.ClienteComo("JefeArea");
        var html = await cliente.GetStringAsync($"/Reuniones/Editor/{_reunionId}");
        var token = System.Text.RegularExpressions.Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        token.Should().NotBeEmpty();
        return (cliente, token);
    }

    /// <summary>Lo mínimo que el editor exige para aceptar un POST, sin tocar los adjuntos.</summary>
    private static void CamposBase(MultipartFormDataContent form, string token)
    {
        form.Add(new StringContent(token), "__RequestVerificationToken");
        form.Add(new StringContent("Taller de modelado con CONSUCOOP"), "Datos.Titulo");
        form.Add(new StringContent("Capacitación"), "Datos.Tipo");
        form.Add(new StringContent("Publica"), "Datos.Visibilidad");
    }

    [Fact]
    public async Task Guardar_el_formulario_sube_el_archivo_y_pega_el_enlace()
    {
        // Es el camino que ninguna prueba de unidad cubre: el enlace de modelo de una lista con un
        // IFormFile adentro —AdjuntosDoc[0].Archivo— más la escritura en disco. Si el nombre de un
        // campo no calza, el guardado no falla: simplemente llega vacío.
        var (cliente, token) = await EditorConTokenAsync();

        using var form = new MultipartFormDataContent();
        CamposBase(form, token);

        var archivo = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("presentación de mentira"));
        archivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(archivo, "AdjuntosDoc[0].Archivo", "Presentación institucional.pdf");

        form.Add(new StringContent("0"), "AdjuntosDoc.Index");
        form.Add(new StringContent("Archivo"), "AdjuntosDoc[0].Clase");
        form.Add(new StringContent("Presentación del taller"), "AdjuntosDoc[0].Titulo");
        form.Add(new StringContent(""), "AdjuntosDoc[0].Url");
        form.Add(new StringContent("0"), "AdjuntosDoc[0].Tamano");

        form.Add(new StringContent("0"), "AdjuntosEvi.Index");
        form.Add(new StringContent("Enlace"), "AdjuntosEvi[0].Clase");
        form.Add(new StringContent("Grabación completa"), "AdjuntosEvi[0].Titulo");
        form.Add(new StringContent(EnlaceOneDrive), "AdjuntosEvi[0].Url");
        form.Add(new StringContent("0"), "AdjuntosEvi[0].Tamano");

        var r = await cliente.PostAsync($"/Reuniones/Editor/{_reunionId}", form);
        r.StatusCode.Should().Be(HttpStatusCode.Redirect, "un guardado bueno redirige al listado");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var guardados = await db.Set<ReunionAdjunto>()
            .Where(a => a.ReunionId == _reunionId).ToListAsync();

        guardados.Should().HaveCount(2, "los de antes se reemplazan: quedan exactamente los enviados");

        var doc = guardados.Single(a => a.Seccion == SeccionAdjuntoReunion.Documento);
        doc.Clase.Should().Be(ClaseAdjuntoReunion.Archivo);
        doc.Titulo.Should().Be("Presentación del taller");
        doc.NombreArchivo.Should().Be("Presentación institucional.pdf");
        doc.Url.Should().StartWith("/uploads/reuniones/");
        doc.Tamano.Should().BeGreaterThan(0);

        // El archivo tiene que haber llegado a disco de verdad, no solo a la base.
        var env = _portal.Services.GetRequiredService<IWebHostEnvironment>();
        var enDisco = Path.Combine(env.ContentRootPath, "App_Data", "uploads",
            "reuniones", Path.GetFileName(doc.Url));
        File.Exists(enDisco).Should().BeTrue();
        _sembrados.Add(enDisco);

        var evi = guardados.Single(a => a.Seccion == SeccionAdjuntoReunion.Evidencia);
        evi.Clase.Should().Be(ClaseAdjuntoReunion.Enlace);
        evi.Url.Should().Be(EnlaceOneDrive);
        evi.Tamano.Should().Be(0, "el portal no guarda el contenido de un enlace");
    }

    [Fact]
    public async Task Un_enlace_con_esquema_peligroso_no_llega_a_guardarse()
    {
        var (cliente, token) = await EditorConTokenAsync();

        using var form = new MultipartFormDataContent();
        CamposBase(form, token);
        form.Add(new StringContent("0"), "AdjuntosEvi.Index");
        form.Add(new StringContent("Enlace"), "AdjuntosEvi[0].Clase");
        form.Add(new StringContent("Grabación"), "AdjuntosEvi[0].Titulo");
        form.Add(new StringContent("javascript:alert(document.cookie)"), "AdjuntosEvi[0].Url");
        form.Add(new StringContent("0"), "AdjuntosEvi[0].Tamano");

        var r = await cliente.PostAsync($"/Reuniones/Editor/{_reunionId}", form);

        // Se queda en la pantalla con el motivo a la vista, no redirige como si hubiera funcionado.
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        System.Net.WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync())
            .Should().Contain("http o https");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Set<ReunionAdjunto>().Where(a => a.ReunionId == _reunionId).ToListAsync())
            .Should().NotContain(a => a.Url.StartsWith("javascript:"));
    }

    [Fact]
    public async Task Una_fila_en_blanco_no_crea_un_adjunto_vacio()
    {
        var (cliente, token) = await EditorConTokenAsync();

        using var form = new MultipartFormDataContent();
        CamposBase(form, token);
        form.Add(new StringContent("0"), "AdjuntosDoc.Index");
        form.Add(new StringContent("Enlace"), "AdjuntosDoc[0].Clase");
        form.Add(new StringContent(""), "AdjuntosDoc[0].Titulo");
        form.Add(new StringContent(""), "AdjuntosDoc[0].Url");
        form.Add(new StringContent("0"), "AdjuntosDoc[0].Tamano");

        var r = await cliente.PostAsync($"/Reuniones/Editor/{_reunionId}", form);
        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Set<ReunionAdjunto>().CountAsync(a => a.ReunionId == _reunionId))
            .Should().Be(0, "un renglón que nadie llenó no es un adjunto");
    }

    [Fact]
    public async Task El_acta_sigue_descargando_su_pdf()
    {
        // La sección 6 del PDF cambió para incluir adjuntos; que siga generándose es lo mínimo.
        var r = await _portal.ClienteComo("JefeArea")
            .GetAsync($"/Reuniones/Acta/{_reunionId}?handler=Pdf");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        r.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
    }

    // ── La clase del adjunto la decide el servidor ────────────────────────

    /// <summary>
    /// El cierre por el que se colaba un archivo ajeno. <c>Clase</c> y <c>Url</c> viajan en el POST,
    /// así que no alcanzaba con pedir el archivo por Id: bastaba <b>inscribir</b> en la propia
    /// reunión una fila que dijera «archivo» con la ruta de otra —todos los módulos escriben en la
    /// misma carpeta de subidas— y bajarla después con un Id que sí era propio. El cercado de ruta
    /// de <c>ArchivosProtegidos</c> no lo veía: la ruta nunca salía de la carpeta.
    /// </summary>
    [Fact]
    public async Task Una_fila_que_dice_ser_archivo_con_la_ruta_de_otra_reunion_no_se_guarda()
    {
        var (cliente, token) = await EditorConTokenAsync();

        using var form = new MultipartFormDataContent();
        CamposBase(form, token);
        form.Add(new StringContent("0"), "AdjuntosDoc.Index");
        form.Add(new StringContent("Archivo"), "AdjuntosDoc[0].Clase");
        form.Add(new StringContent("Acta de la otra reunión"), "AdjuntosDoc[0].Titulo");
        form.Add(new StringContent("robado.pdf"), "AdjuntosDoc[0].NombreArchivo");
        form.Add(new StringContent(_urlAjena), "AdjuntosDoc[0].Url");
        form.Add(new StringContent("23"), "AdjuntosDoc[0].Tamano");

        var r = await cliente.PostAsync($"/Reuniones/Editor/{_reunionId}", form);
        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Set<ReunionAdjunto>().Where(a => a.ReunionId == _reunionId).ToListAsync())
            .Should().NotContain(a => a.Url == _urlAjena,
                "una ruta que esta reunión nunca guardó no se adopta porque el formulario lo diga");
    }

    /// <summary>La otra cara: el archivo que la reunión <b>sí</b> tenía vuelve en el POST como una
    /// fila sin <c>IFormFile</c> —el navegador no puede repoblar un selector de archivos— y tiene
    /// que sobrevivir al guardado. Sin esto, la corrección de arriba borraría los adjuntos de
    /// cualquiera que entre a corregir una coma.</summary>
    [Fact]
    public async Task El_archivo_que_ya_estaba_sobrevive_a_un_guardado_sin_volver_a_subirlo()
    {
        var (cliente, token) = await EditorConTokenAsync();

        using var form = new MultipartFormDataContent();
        CamposBase(form, token);
        form.Add(new StringContent("0"), "AdjuntosDoc.Index");
        form.Add(new StringContent("Archivo"), "AdjuntosDoc[0].Clase");
        form.Add(new StringContent("Acta firmada"), "AdjuntosDoc[0].Titulo");
        form.Add(new StringContent("Acta firmada.pdf"), "AdjuntosDoc[0].NombreArchivo");
        form.Add(new StringContent(_urlPropia), "AdjuntosDoc[0].Url");
        form.Add(new StringContent("23"), "AdjuntosDoc[0].Tamano");

        var r = await cliente.PostAsync($"/Reuniones/Editor/{_reunionId}", form);
        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var guardados = await db.Set<ReunionAdjunto>()
            .Where(a => a.ReunionId == _reunionId).ToListAsync();

        guardados.Should().ContainSingle(a => a.Url == _urlPropia);
        guardados.Single(a => a.Url == _urlPropia).Clase.Should().Be(ClaseAdjuntoReunion.Archivo);
    }
}
