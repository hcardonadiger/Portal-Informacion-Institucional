using System.Net;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La biblioteca transversal.
///
/// <para>Lo que se prueba acá es lo que la propuesta dejó explícitamente para el final, porque
/// necesitaba documentos cargados para significar algo: <b>que una pantalla que cruza todo el
/// portafolio no filtre documentación de otra institución</b>. Y no solo en el listado —esconder
/// una fila es fácil— sino también en la descarga directa por Id, que es por donde se cuela un
/// IDOR.</para>
/// </summary>
public sealed class BibliotecaTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    private int _versionDiger;
    private int _versionConsucoop;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea",
            "Proyectos.Ver", "Proyectos.Documentos.Ver", "Proyectos.Documentos.Crear");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var convenio = CategoriaDocumento.Crear("Convenio", 1);
        var acta     = CategoriaDocumento.Crear("Acta", 2);
        db.CategoriasDocumento.AddRange(convenio, acta);
        await db.SaveChangesAsync();

        _versionDiger     = await SembrarAsync(db, "PRY-2026-70", "Frente DIGER",     "DIGER",
                                               convenio.Id, "Convenio marco DIGER", "convenio-diger.pdf");
        _versionConsucoop = await SembrarAsync(db, "PRY-2026-71", "Frente CONSUCOOP", "CONSUCOOP",
                                               acta.Id, "Acta reservada de CONSUCOOP", "acta-consucoop.pdf");
    }

    private static async Task<int> SembrarAsync(
        AppDbContext db, string codigo, string nombre, string institucion,
        int categoriaId, string titulo, string archivo)
    {
        var proyecto = Proyecto.Crear(codigo, nombre);
        proyecto.InstitucionId = institucion;
        db.Proyectos.Add(proyecto);
        await db.SaveChangesAsync();

        var doc = DocumentoProyecto.Crear(proyecto.Id, categoriaId, titulo);
        var version = doc.AgregarVersion(archivo, $"/uploads/proyectos/documentos/{archivo}",
                                         1024, new string('a', 64), "Quien sembró");
        db.ProyectoDocumentos.Add(doc);
        await db.SaveChangesAsync();

        return version.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private Task<string> BibliotecaAsync(string institucion, string? consulta = null) =>
        _portal.ClienteComo("JefeArea", institucion)
               .GetStringAsync("/Proyectos/Biblioteca" + (consulta is null ? "" : "?" + consulta));

    // ── Aislamiento ───────────────────────────────────────────────
    [Fact]
    public async Task Solo_se_lista_la_documentacion_de_los_proyectos_que_uno_puede_ver()
    {
        var deDiger = await BibliotecaAsync("DIGER");

        deDiger.Should().Contain("Convenio marco DIGER");
        deDiger.Should().NotContain("Acta reservada de CONSUCOOP",
            "la biblioteca cruza proyectos, no instituciones");
    }

    [Fact]
    public async Task Y_al_reves_desde_la_otra_institucion()
    {
        var deConsucoop = await BibliotecaAsync("CONSUCOOP");

        deConsucoop.Should().Contain("Acta reservada de CONSUCOOP");
        deConsucoop.Should().NotContain("Convenio marco DIGER");
    }

    [Fact]
    public async Task El_selector_de_proyectos_tampoco_revela_los_ajenos()
    {
        // Las facetas se calculan sobre lo visible: si se calcularan antes del filtro de alcance,
        // el nombre del proyecto ajeno aparecería en el desplegable aunque su fila no se listara.
        var deDiger = await BibliotecaAsync("DIGER");

        deDiger.Should().Contain("PRY-2026-70");
        deDiger.Should().NotContain("PRY-2026-71");
    }

    [Fact]
    public async Task No_se_descarga_una_version_de_otra_institucion_ni_pidiendola_por_Id()
    {
        // El caso que de verdad importa: la fila no se ve, pero el Id se puede adivinar. La
        // consulta lleva su ancla al documento y por él al proyecto, así que devuelve null.
        var respuesta = await _portal.ClienteComo("JefeArea", "CONSUCOOP")
            .GetAsync($"/Proyectos/Biblioteca?handler=Descargar&versionId={_versionDiger}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task La_propia_si_se_resuelve()
    {
        // Contrapeso del anterior: sin esto, un filtro que escondiera TODO también pasaría.
        // El archivo físico no existe, así que el handler redirige avisándolo — pero no es 404,
        // que es lo que distingue «no te toca» de «no está en el disco».
        var respuesta = await _portal.ClienteComo("JefeArea", "CONSUCOOP")
            .GetAsync($"/Proyectos/Biblioteca?handler=Descargar&versionId={_versionConsucoop}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    // ── Filtros ───────────────────────────────────────────────────
    [Fact]
    public async Task El_buscador_mira_titulo_y_nombre_de_archivo()
    {
        (await BibliotecaAsync("DIGER", "Q=convenio-diger.pdf")).Should().Contain("Convenio marco DIGER");
        (await BibliotecaAsync("DIGER", "Q=marco")).Should().Contain("Convenio marco DIGER");

        var sinCoincidencias = await BibliotecaAsync("DIGER", "Q=inexistente");
        sinCoincidencias.Should().Contain("Ningún documento cumple con estos filtros");
        sinCoincidencias.Should().NotContain("Convenio marco DIGER");
    }

    [Fact]
    public async Task El_vacio_por_filtro_se_distingue_del_vacio_de_verdad()
    {
        // Dos mensajes distintos a propósito: decirle «no hay documentación» a quien tiene 1
        // documento y un filtro puesto sería mentirle.
        var porFiltro = await BibliotecaAsync("DIGER", "Q=inexistente");
        porFiltro.Should().Contain("Hay 1 en total");

        // Una institución sin ningún proyecto visible: ahí sí no hay nada.
        var sinNada = await BibliotecaAsync("IHADFA");
        sinNada.Should().Contain("Todavía no hay documentación cargada");
    }

    [Fact]
    public async Task El_conteo_del_encabezado_distingue_filtrado_de_total()
    {
        (await BibliotecaAsync("DIGER")).Should().Contain("documentos");

        // Con filtro, el encabezado dice «N de M».
        (await BibliotecaAsync("DIGER", "Q=marco")).Should().Contain("de 1 documentos");
    }

    [Fact]
    public async Task El_enlace_al_proyecto_abre_directo_su_pestana_de_documentos()
    {
        // Se escribió primero como fragment= en vez de asp-fragment=, que Razor emite como un
        // atributo literal sin decir nada: el enlace compilaba, se veía bien y aterrizaba en la
        // pestaña equivocada. Queda fijado.
        var html = await BibliotecaAsync("DIGER");

        html.Should().Contain("/Proyectos/Editor/").And.Contain("#documentos");
        html.Should().NotContain("fragment=\"documentos\"", "eso sería el atributo sin procesar");
    }

    // ── Permisos ──────────────────────────────────────────────────
    [Fact]
    public async Task Sin_la_clave_de_documentos_la_biblioteca_no_se_abre()
    {
        // Consultor tiene Proyectos.Ver pero no Proyectos.Documentos.Ver en esta prueba.
        await _portal.OtorgarAsync("Consultor", "Proyectos.Ver");

        var respuesta = await _portal.ClienteComo("Consultor").GetAsync("/Proyectos/Biblioteca");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Modos de vista ────────────────────────────────────────────
    [Fact]
    public async Task El_modo_por_defecto_sigue_siendo_la_lista()
    {
        // Deliberado: no se le cambia la pantalla a quien ya usa la biblioteca, y el listado es lo
        // único que muestra los títulos —de lo que dependen las pruebas de aislamiento de arriba—.
        var html = await BibliotecaAsync("DIGER");

        html.Should().Contain("Convenio marco DIGER");
        html.Should().Contain("bib-seg on", "el selector de modo marca cuál está activo");
    }

    [Fact]
    public async Task En_carpetas_se_ven_los_grupos_y_no_los_documentos()
    {
        var html = await BibliotecaAsync("DIGER", "Modo=Carpetas&Organizar=Categoria");

        html.Should().Contain("bib-carpeta").And.Contain("Convenio");
        html.Should().NotContain("Convenio marco DIGER",
            "el mosaico muestra carpetas, no los documentos de dentro");
    }

    [Fact]
    public async Task Al_abrir_una_carpeta_aparecen_sus_documentos_y_la_ruta_de_vuelta()
    {
        var html = await BibliotecaAsync("DIGER", "Modo=Carpetas&Organizar=Categoria&Carpeta=Convenio");

        html.Should().Contain("Convenio marco DIGER");
        html.Should().Contain("← Carpetas");
    }

    [Fact]
    public async Task Las_carpetas_no_se_saltan_el_alcance()
    {
        // El agrupado es presentación: agrupar por proyecto no puede mostrar proyectos ajenos.
        var html = await BibliotecaAsync("DIGER", "Modo=Carpetas&Organizar=Proyecto");

        html.Should().Contain("PRY-2026-70");
        html.Should().NotContain("PRY-2026-71", "sigue siendo la documentación que uno puede ver");
    }

    // ── Filtros nuevos ────────────────────────────────────────────
    [Fact]
    public async Task Se_puede_filtrar_por_quien_carga_la_documentacion()
    {
        var html = await BibliotecaAsync("DIGER", "SubidoPor=Quien%20sembr%C3%B3");
        html.Should().Contain("Convenio marco DIGER");

        var vacio = await BibliotecaAsync("DIGER", "SubidoPor=Alguien%20que%20no%20existe");
        vacio.Should().NotContain("Convenio marco DIGER");
        vacio.Should().Contain("Ningún documento cumple con estos filtros");
    }

    [Fact]
    public async Task Se_puede_filtrar_por_tipo_de_archivo()
    {
        var pdf = await BibliotecaAsync("DIGER", "Tipo=pdf");
        pdf.Should().Contain("Convenio marco DIGER");

        var docx = await BibliotecaAsync("DIGER", "Tipo=docx");
        docx.Should().NotContain("Convenio marco DIGER");
    }

    [Fact]
    public async Task El_filtro_de_historial_deja_fuera_los_de_una_sola_version()
    {
        // El documento sembrado tiene una sola versión: no se negoció, se archivó.
        var html = await BibliotecaAsync("DIGER", "ConHistorial=true");

        html.Should().NotContain("Convenio marco DIGER");
        html.Should().Contain("Ningún documento cumple con estos filtros");
    }
}
