using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El alta rápida del listado cuando el servidor la rechaza.
///
/// <para>Existe por una pérdida de trabajo, no por un error de cálculo: el handler dejaba el motivo
/// en <c>TempData</c> y redirigía, así que la página volvía en blanco y lo que la persona acababa de
/// escribir en el modal —nombre, objetivo, fechas— desaparecía. Con una ventana invertida eso
/// significaba teclear el alta entera otra vez para corregir una fecha.</para>
///
/// <para>El navegador ahora revisa esa regla antes de postear (<c>prevalidacion.js</c>), pero eso no
/// se puede comprobar desde acá y tampoco sería suficiente: lo que se fija en estas pruebas es la
/// red de abajo —que el rechazo del servidor devuelva la misma página, con el modal abierto, lo
/// escrito en su lugar y el motivo adentro—.</para>
/// </summary>
public sealed class AltaProyectoRechazadaTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Proyectos.Ver", "Proyectos.Crear");
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private async Task<(HttpClient cliente, string token)> ListadoAsync()
    {
        var cliente = _portal.ClienteComo("JefeArea");
        var html    = await cliente.GetStringAsync("/Proyectos");
        var token   = Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        token.Should().NotBeEmpty();
        return (cliente, token);
    }

    private static FormUrlEncodedContent Alta(string token, params (string Campo, string Valor)[] extra)
    {
        var campos = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token)
        };
        foreach (var (campo, valor) in extra) campos.Add(new(campo, valor));
        return new FormUrlEncodedContent(campos);
    }

    [Fact]
    public async Task Una_ventana_invertida_no_borra_lo_que_se_habia_escrito()
    {
        var (cliente, token) = await ListadoAsync();

        // Datos sin acentos a propósito: el encoder por omisión escribe los no-ASCII como
        // referencias numéricas, y buscarlos tal cual en el HTML fallaría por la codificación y no
        // por lo que esta prueba quiere fijar.
        var respuesta = await cliente.PostAsync("/Proyectos?handler=Crear", Alta(token,
            ("NuevoNombre",      "Digitalizacion de tramites fase 2"),
            ("NuevoObjetivo",    "Dejar el tramite en linea de punta a punta"),
            ("NuevaPrioridadId", PortalFactory.PrioridadPorDefecto.ToString()),
            ("NuevaFechaInicio", "2026-06-01"),
            ("NuevaFechaFin",    "2026-03-31")));

        // Lo que antes era un redirect: ahí se perdía todo.
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
            "el rechazo tiene que devolver la misma página, no redirigir");

        var html = await respuesta.Content.ReadAsStringAsync();

        html.Should().Contain("La fecha de cierre planificada no puede ser anterior a la de inicio.",
            "el motivo del rechazo se dice, y se dice dentro del modal");
        html.Should().Contain("id=\"modal-nuevo\" style=\"display:flex",
            "el modal vuelve abierto: cerrado, lo escrito quedaría fuera de la vista y se tiraría");

        html.Should().Contain("Digitalizacion de tramites fase 2");
        html.Should().Contain("Dejar el tramite en linea de punta a punta");
        html.Should().Contain("value=\"2026-06-01\"");
        html.Should().Contain("value=\"2026-03-31\"");
    }

    [Fact]
    public async Task El_nombre_vacio_tambien_vuelve_al_modal_con_el_resto_puesto()
    {
        var (cliente, token) = await ListadoAsync();

        var respuesta = await cliente.PostAsync("/Proyectos?handler=Crear", Alta(token,
            ("NuevoNombre",      "   "),
            ("NuevoObjetivo",    "Objetivo ya escrito"),
            ("NuevaPrioridadId", PortalFactory.PrioridadPorDefecto.ToString()),
            ("NuevaFechaInicio", "2026-02-01")));

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await respuesta.Content.ReadAsStringAsync();

        html.Should().Contain("El proyecto necesita un nombre.");
        html.Should().Contain("id=\"modal-nuevo\" style=\"display:flex");
        html.Should().Contain("Objetivo ya escrito", "corregir el nombre no puede costar el objetivo");
        html.Should().Contain("value=\"2026-02-01\"");
    }

    [Fact]
    public async Task Un_alta_valida_sigue_llevando_a_la_ficha_del_proyecto_nuevo()
    {
        var (cliente, token) = await ListadoAsync();

        var respuesta = await cliente.PostAsync("/Proyectos?handler=Crear", Alta(token,
            ("NuevoNombre",      "Proyecto que sí se crea"),
            ("NuevaPrioridadId", PortalFactory.PrioridadPorDefecto.ToString()),
            ("NuevaFechaInicio", "2026-01-15"),
            ("NuevaFechaFin",    "2026-12-15")));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Redirect);
        respuesta.Headers.Location!.OriginalString.Should().Contain("/Proyectos/Editor");
    }

    [Fact]
    public async Task El_listado_sirve_el_modal_cerrado_y_enganchado_a_la_prevalidacion()
    {
        var html = await _portal.ClienteComo("JefeArea").GetStringAsync("/Proyectos");

        html.Should().Contain("id=\"modal-nuevo\" style=\"display:none",
            "sin rechazo que mostrar, el modal llega cerrado");
        html.Should().Contain("id=\"form-nuevo\"",
            "la prevalidación engancha el formulario por este id");
        html.Should().Contain("_dgPrevalidar",
            "si el enganche desaparece, el rechazo vuelve a costar un viaje al servidor");
    }
}
