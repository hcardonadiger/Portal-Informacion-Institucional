using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La ficha de proyecto, renderizada de verdad.
///
/// <para>Existe por algo que el compilador no puede ver: la ficha se reorganizó en pestañas y dos
/// de ellas —estructura y datos del proyecto— viven dentro del <b>mismo formulario</b>. Si el panel
/// inactivo se ocultara sacándolo del DOM en vez de con <c>[hidden]</c>, la página compilaría igual
/// y guardar borraría en silencio el nombre y el objetivo del proyecto. Acá se comprueba sobre el
/// HTML servido que los campos de la pestaña oculta siguen estando.</para>
/// </summary>
public sealed class FichaProyectoTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _proyectoId;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Proyectos.Ver", "Proyectos.Editar");
        await _portal.OtorgarAsync("Consultor", "Proyectos.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var proyecto = Proyecto.Crear("PRY-2026-99", "Proyecto de la ficha");
        // El filtro de alcance se ancla en la institución; sin esto el proyecto no lo ve nadie.
        proyecto.InstitucionId = "DIGER";

        var entregable = EntregableProyecto.Crear("Integración", 1);
        entregable.Agregar(ActividadProyecto.Crear("Levantamiento", 1));
        proyecto.Agregar(entregable);

        db.Proyectos.Add(proyecto);
        await db.SaveChangesAsync();

        _proyectoId = proyecto.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> FichaAsync(string rol = "JefeArea")
    {
        var respuesta = await _portal.ClienteComo(rol).GetAsync($"/Proyectos/Editor/{_proyectoId}");
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await respuesta.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Sirve_las_siete_pestanas_con_un_solo_panel_visible()
    {
        var html = await FichaAsync();

        html.Should().Contain("role=\"tablist\"");

        foreach (var panel in new[] { "panel-estructura", "panel-cronograma", "panel-bitacora",
                                      "panel-documentos", "panel-equipo", "panel-datos",
                                      "panel-auditoria" })
            html.Should().Contain($"id=\"{panel}\"", $"la pestaña {panel} tiene que existir en el DOM");

        // La estructura es la que abre; el resto llega oculto. Se mira el atributo pegado al id
        // para no depender del orden en que se serialicen los demás.
        html.Should().Contain("id=\"panel-estructura\" data-tab=\"estructura\"");
        html.Should().Contain("aria-labelledby=\"tab-datos\" hidden");
    }

    [Fact]
    public async Task El_panel_oculto_sigue_mandando_sus_campos_al_guardar()
    {
        var html = await FichaAsync();

        // «Nombre» y «Objetivo» viven en la pestaña Datos, que se sirve oculta. Si se ocultara
        // removiendo el panel, estos campos no estarían y el guardado los borraría.
        html.Should().Contain("name=\"Nombre\"");
        html.Should().Contain("name=\"Objetivo\"");
        html.Should().Contain("name=\"FechaFinPlan\"");

        // Y el panel oculto está DENTRO del formulario que los guarda: el <form> abre antes que
        // el panel de estructura y cierra después del de datos.
        var form   = html.IndexOf("asp-page-handler=\"Guardar\"", StringComparison.Ordinal) >= 0
                   ? html.IndexOf("asp-page-handler=\"Guardar\"", StringComparison.Ordinal)
                   : html.IndexOf("handler=Guardar", StringComparison.Ordinal);
        var datos  = html.IndexOf("id=\"panel-datos\"", StringComparison.Ordinal);
        var cierre = html.IndexOf("</form>", form, StringComparison.Ordinal);

        form.Should().BeGreaterThan(-1, "el formulario de guardado tiene que estar en la página");
        datos.Should().BeGreaterThan(form).And.BeLessThan(cierre);
    }

    [Fact]
    public async Task La_barra_de_guardado_llega_identificada_para_poder_ocultarla()
    {
        var html = await FichaAsync();

        // El JS la esconde en las pestañas que no guardan; sin el id no tendría a qué agarrarse.
        html.Should().Contain("id=\"barra-guardar\"");
    }

    [Fact]
    public async Task El_indice_de_anclas_viejo_ya_no_se_sirve()
    {
        var html = await FichaAsync();

        html.Should().NotContain("ficha-nav", "lo reemplazaron las pestañas; su CSS también se renombró");
    }

    [Fact]
    public async Task Quien_solo_puede_ver_recibe_la_ficha_sin_barra_de_guardado()
    {
        var html = await FichaAsync("Consultor");

        html.Should().Contain("role=\"tablist\"");
        html.Should().NotContain("id=\"barra-guardar\"");
    }

    [Fact]
    public async Task La_actividad_ofrece_declarar_de_que_depende()
    {
        var html = await FichaAsync();

        html.Should().Contain("abrirDependencia(this)");
        html.Should().Contain("id=\"dlg-dep\"", "el selector de dependencias es uno solo para toda la tabla");
    }

    [Fact]
    public async Task La_estructura_muestra_cuando_se_creo_cada_fila()
    {
        var html = await FichaAsync();

        html.Should().Contain(">Creado</th>");

        // El proyecto de esta prueba se siembra ahora, así que su actividad SÍ tiene fecha real.
        // Las 191 del portafolio son anteriores a que la entidad llevara auditoría y salen con
        // guion: se muestra vacío en vez de inventarles la fecha de la migración.
        html.Should().Contain(DateTime.UtcNow.ToLocalTime().ToString("dd/MM/yy"));
    }

    [Fact]
    public async Task El_colspan_del_JS_sigue_al_del_servidor()
    {
        // La tabla la pintan dos lugares: Razor para lo que ya existe y el JS para las filas que
        // se agregan en el navegador. Agregar una columna y olvidar uno de los dos desalinea la
        // tabla sin que nada falle. Acá se comparan los dos números en el HTML servido.
        var html = await FichaAsync();

        var columnas = Regex.Matches(html, @"<th[ >]").Count;
        columnas.Should().BeGreaterThan(0);

        // El JS declara su propio recuento; tiene que coincidir con el colspan que emite Razor.
        var colspanRazor = Regex.Match(html, @"act-add[^>]*>\s*<td colspan=""(\d+)""").Groups[1].Value;
        var colspanJs    = Regex.Match(html, @"var cols = conOrden \? (\d+) : (\d+);");

        colspanRazor.Should().NotBeEmpty();
        colspanJs.Success.Should().BeTrue("el script declara el recuento de columnas");

        // El rol de la prueba no puede reordenar, así que aplica la rama sin la columna «Orden».
        colspanJs.Groups[2].Value.Should().Be(colspanRazor,
            "si no coinciden, las filas que agrega el navegador quedan corridas respecto de las del servidor");
    }
}
