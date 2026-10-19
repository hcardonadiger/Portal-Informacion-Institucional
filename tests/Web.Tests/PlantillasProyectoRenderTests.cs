using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Las plantillas de estructura de proyecto, del lado del navegador.
///
/// <para>La copia en sí está probada en Application (<c>PlantillasProyectoTests</c>). Lo que solo
/// se puede comprobar acá es <b>cuándo aparece el control</b>: la regla es que traer una plantilla
/// solo se ofrece sobre un proyecto sin estructura, y esa regla vive mitad en la vista —el bloque
/// que se pinta— y mitad en el comando. Si la vista se desalinea, el portal ofrece un botón que el
/// servidor rechaza, que es peor que no ofrecerlo.</para>
///
/// <para>El POST va contra el formulario de la ficha —el control desvía ese mismo &lt;form&gt; con
/// <c>formaction</c>, porque anidar formularios no es HTML válido—, así que la prueba también fija
/// que ese desvío siga existiendo: sin él el botón acabaría guardando la ficha.</para>
/// </summary>
public sealed class PlantillasProyectoRenderTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea",
            "Proyectos.Ver", "Proyectos.Crear", "Proyectos.Editar",
            "Proyectos.Plantillas.Ver", "Proyectos.Plantillas.Crear");
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private HttpClient Cliente() => _portal.ClienteComo("JefeArea");

    // ── Sembrado ──────────────────────────────────────────────────

    /// <summary>
    /// Una plantilla de alcance institucional, que es la que alcanza a cualquiera dentro de DIGER.
    /// Se escribe directo en el contexto: lo que se mide es el render, y el alta de la plantilla ya
    /// tiene sus pruebas en Application.
    /// </summary>
    private async Task<int> SembrarPlantillaAsync(string nombre = "Informe trimestral")
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var plantilla = PlantillaProyecto.Crear(nombre, "DIGER", NivelAlcance.Institucion);
        var entregable = new PlantillaEntregable { Orden = 1, Nombre = "Levantamiento" };
        entregable.Agregar(new PlantillaActividad { Orden = 1, Nombre = "Entrevistas" });
        entregable.Agregar(new PlantillaActividad { Orden = 2, Nombre = "Consolidado" });
        plantilla.Agregar(entregable);

        db.PlantillasProyecto.Add(plantilla);
        await db.SaveChangesAsync();
        return plantilla.Id;
    }

    /// <summary>
    /// Crea el proyecto por el alta real del listado y no escribiéndolo en el contexto: el alta es
    /// la que le pone institución, área y unidad, y sin eso el filtro global del DbContext lo deja
    /// fuera del alcance de quien lo pide — la ficha contestaría 404 y la prueba mediría eso.
    /// </summary>
    private async Task<int> SembrarProyectoAsync(bool conEstructura)
    {
        var cliente = Cliente();
        var token   = Token(await cliente.GetStringAsync("/Proyectos"));

        var respuesta = await cliente.PostAsync("/Proyectos?handler=Crear",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("NuevoNombre", "Informe trimestral 2026"),
                new KeyValuePair<string, string>("NuevaPrioridadId",
                    PortalFactory.PrioridadPorDefecto.ToString()),
                new KeyValuePair<string, string>("NuevaFechaInicio", "2026-01-15")
            ]));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Redirect,
            "el alta tiene que pasar para que haya proyecto que probar");

        // El id se lee del contexto y no del Location: la ruta de la ficha ha cambiado de forma
        // más de una vez, y una prueba de render no debería romperse por eso.
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var p = await db.Proyectos
            .Include(x => x.Entregables)
            .OrderByDescending(x => x.Id)
            .FirstAsync();

        if (conEstructura)
        {
            p.Agregar(EntregableProyecto.Crear("Entregable que ya estaba", 1));
            await db.SaveChangesAsync();
        }

        return p.Id;
    }

    private static string Token(string html) => Regex.Match(html,
        """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

    // ── La pantalla de administración ─────────────────────────────

    [Fact]
    public async Task La_pantalla_de_plantillas_se_pinta()
    {
        var id = await SembrarPlantillaAsync("Informe trimestral");

        // El listado dice el nombre y el tamaño del árbol, no el árbol: es lo que distingue dos
        // plantillas de nombre parecido sin tener que abrirlas.
        var lista = await Cliente().GetStringAsync("/Proyectos/Plantillas");
        lista.Should().Contain("Informe trimestral");

        // Y el árbol aparece al editarla, que es donde se toca.
        var edicion = await Cliente().GetStringAsync($"/Proyectos/Plantillas?editar={id}");
        edicion.Should().Contain("Levantamiento");
        edicion.Should().Contain("Entrevistas");
    }

    [Fact]
    public async Task Sin_la_clave_la_pantalla_de_plantillas_no_se_abre()
    {
        // Es su propia clave a propósito: mantener plantillas no debería exigir
        // permiso para editar todos los proyectos, ni al revés.
        var respuesta = await _portal.ClienteComo("Consultor").GetAsync("/Proyectos/Plantillas");

        respuesta.StatusCode.Should().BeOneOf(
            HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task El_menu_de_proyectos_lleva_a_plantillas()
    {
        var html = await Cliente().GetStringAsync("/Proyectos");

        html.Should().Contain("/Proyectos/Plantillas",
            "sin la entrada en el menú la pantalla existe pero nadie llega");
    }

    // ── El control en la ficha ────────────────────────────────────

    [Fact]
    public async Task Un_proyecto_sin_estructura_ofrece_traer_la_plantilla()
    {
        await SembrarPlantillaAsync();
        var id = await SembrarProyectoAsync(conEstructura: false);

        var html = await Cliente().GetStringAsync($"/Proyectos/Editor/{id}");

        html.Should().Contain("plantilla-traer");
        html.Should().Contain("Traer estructura");
        html.Should().Contain("handler=TraerPlantilla",
            "el botón desvía el formulario de la ficha; sin formaction guardaría la ficha");
        html.Should().Contain("name=\"plantillaId\"");
    }

    [Fact]
    public async Task Un_proyecto_que_ya_tiene_estructura_no_lo_ofrece()
    {
        // La regla del usuario: solo sobre estructura vacía. Con entregables cargados
        // la copia pisaría avances y fechas reales, y el comando la rechaza.
        await SembrarPlantillaAsync();
        var id = await SembrarProyectoAsync(conEstructura: true);

        var html = await Cliente().GetStringAsync($"/Proyectos/Editor/{id}");

        html.Should().NotContain("plantilla-traer");
        html.Should().NotContain("Traer estructura");
    }

    [Fact]
    public async Task Sin_plantillas_cargadas_no_se_pinta_un_selector_vacio()
    {
        var id = await SembrarProyectoAsync(conEstructura: false);

        var html = await Cliente().GetStringAsync($"/Proyectos/Editor/{id}");

        html.Should().NotContain("plantilla-traer");
    }

    [Fact]
    public async Task El_select_no_es_required_para_no_bloquear_el_guardado_de_la_ficha()
    {
        // Vive dentro de #form-ficha: la validación del navegador es del formulario
        // entero, así que un required acá impediría pulsar «Guardar».
        await SembrarPlantillaAsync();
        var id = await SembrarProyectoAsync(conEstructura: false);

        var html = await Cliente().GetStringAsync($"/Proyectos/Editor/{id}");

        var select = Regex.Match(html, """<select id="pt-sel"[^>]*>""").Value;
        select.Should().NotBeEmpty();
        select.Should().NotContain("required");
    }

    [Fact]
    public async Task El_bloque_no_abre_un_formulario_propio()
    {
        // Anidar <form> no es HTML válido: el navegador descarta el de adentro y el
        // botón acaba posteando al handler del formulario de afuera.
        await SembrarPlantillaAsync();
        var id = await SembrarProyectoAsync(conEstructura: false);

        var html = await Cliente().GetStringAsync($"/Proyectos/Editor/{id}");

        var bloque = Regex.Match(html,
            """<div class="card plantilla-traer".*?Traer estructura""", RegexOptions.Singleline).Value;
        bloque.Should().NotBeEmpty();
        bloque.Should().NotContain("<form");
    }

    // ── El POST ───────────────────────────────────────────────────

    [Fact]
    public async Task Traerla_carga_la_estructura_y_la_ficha_la_muestra()
    {
        var plantillaId = await SembrarPlantillaAsync();
        var proyectoId  = await SembrarProyectoAsync(conEstructura: false);
        var cliente     = Cliente();

        var token = Token(await cliente.GetStringAsync($"/Proyectos/Editor/{proyectoId}"));
        token.Should().NotBeEmpty();

        var respuesta = await cliente.PostAsync(
            $"/Proyectos/Editor/{proyectoId}?handler=TraerPlantilla",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("plantillaId", plantillaId.ToString())
            ]));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var proyecto = await db.Proyectos
            .Include(p => p.Entregables).ThenInclude(e => e.Actividades)
            .FirstAsync(p => p.Id == proyectoId);

        proyecto.Entregables.Should().HaveCount(1);
        proyecto.Entregables.Single().Actividades.Should().HaveCount(2);

        // Y ya traída, el control desaparece: el proyecto dejó de tener estructura vacía.
        var html = await cliente.GetStringAsync($"/Proyectos/Editor/{proyectoId}");
        html.Should().NotContain("plantilla-traer");
        html.Should().Contain("Levantamiento");
    }

    [Fact]
    public async Task Postear_sin_elegir_plantilla_lo_dice_en_vez_de_reventar()
    {
        var proyectoId = await SembrarProyectoAsync(conEstructura: false);
        var cliente    = Cliente();

        var token = Token(await cliente.GetStringAsync($"/Proyectos/Editor/{proyectoId}"));

        var respuesta = await cliente.PostAsync(
            $"/Proyectos/Editor/{proyectoId}?handler=TraerPlantilla",
            new FormUrlEncodedContent(
            [
                new("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("plantillaId", "")
            ]));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Proyectos.Include(p => p.Entregables).FirstAsync(p => p.Id == proyectoId))
            .Entregables.Should().BeEmpty();
    }
}
