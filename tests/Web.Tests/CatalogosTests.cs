using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La portada de catálogos y el catálogo de prioridades.
///
/// <para>La portada no tiene permiso propio: es una lista de enlaces y decide qué mostrar
/// preguntando por la clave de <b>cada destino</b>. Eso es lo que hay que probar —que no ofrezca
/// lo que el usuario no puede abrir, y que no esconda lo que sí— porque es la clase de regla que
/// se rompe en silencio: una tarjeta de más solo se nota cuando alguien hace clic y recibe un
/// Forbidden.</para>
/// </summary>
public sealed class CatalogosTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        // JefeArea administra áreas y unidades, nada más: es el caso que el navbar viejo
        // resolvía por nombre de rol y prometía de más.
        await _portal.OtorgarAsync("JefeArea", "Areas.Ver", "Unidades.Ver");
        // Consultor queda sin ningún catálogo a propósito.
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    // ── Portada ───────────────────────────────────────────────────
    // Se afirma sobre el destino y no sobre el rótulo: el ayudante de etiquetas convierte
    // asp-page="/Areas/Index" en href="/Areas", y el texto que sale de una expresión de Razor
    // —a diferencia del escrito en la plantilla— lleva las tildes como entidad numérica. El
    // destino es además lo que de verdad importa de una tarjeta.
    [Fact]
    public async Task El_administrador_ve_todas_las_tarjetas()
    {
        var html = await _portal.ClienteComo("Administrador").GetStringAsync("/Catalogos/Index");

        html.Should().Contain(@"href=""/Instituciones""");
        html.Should().Contain(@"href=""/Areas""");
        html.Should().Contain(@"href=""/Unidades""");
        html.Should().Contain(@"href=""/Catalogos/Prioridades""");
        html.Should().Contain(@"href=""/Catalogos/PrioridadesTicket""");
        html.Should().Contain(@"href=""/Tickets/Temas""");
    }

    [Fact]
    public async Task Quien_solo_administra_areas_y_unidades_ve_esas_dos_y_no_las_demas()
    {
        var html = await _portal.ClienteComo("JefeArea").GetStringAsync("/Catalogos/Index");

        html.Should().Contain(@"href=""/Areas""");
        html.Should().Contain(@"href=""/Unidades""");
        html.Should().NotContain(@"href=""/Instituciones""",
            "no tiene Instituciones.Ver y la tarjeta lo llevaría a un Forbidden");
        html.Should().NotContain(@"href=""/Catalogos/Prioridades""");
    }

    [Fact]
    public async Task Sin_ningun_catalogo_la_portada_lo_dice_en_vez_de_salir_vacia()
    {
        var html = await _portal.ClienteComo("Consultor").GetStringAsync("/Catalogos/Index");

        html.Should().Contain("No tiene catálogos asignados para administrar.");
    }

    [Fact]
    public async Task La_portada_se_abre_sin_permiso_propio()
    {
        // No lleva [Permission]: inventarle una clave obligaría a otorgarla en cada rol que ya
        // administra algún catálogo, solo para dejarlo pasar por la puerta.
        var r = await _portal.ClienteComo("Consultor").GetAsync("/Catalogos/Index");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task El_menu_de_administracion_ofrece_la_portada_y_no_los_catalogos_sueltos()
    {
        var html = await _portal.ClienteComo("Administrador").GetStringAsync("/Ayuda/Index");

        html.Should().Contain(@"href=""/Catalogos""", "la portada se alcanza desde Administración");

        // Los enlaces uno por uno se fueron del navbar: ahora se llega por la portada, que es lo
        // que evita que el menú crezca con cada catálogo nuevo. Se mira en la ayuda —no en la
        // portada, donde sí tienen que estar— y el navbar es lo único de esa página que podría
        // enlazarlos.
        html.Should().NotContain(@"href=""/Instituciones""");
        html.Should().NotContain(@"href=""/Areas""");
        html.Should().NotContain(@"href=""/Unidades""");
    }

    [Fact]
    public async Task Quien_administra_un_catalogo_sin_ser_administrador_igual_llega_a_el()
    {
        // El grupo «Administración» se abría solo para EsAdministrador. Al meter Catálogos ahí
        // dentro, gatearlo igual habría dejado sin puerta a los roles que administran un catálogo
        // sin ser administradores —JefeArea tiene Areas.Ver y Unidades.Ver en esta suite—.
        var html = await _portal.ClienteComo("JefeArea").GetStringAsync("/Ayuda/Index");

        html.Should().Contain(@"href=""/Catalogos""");
        html.Should().NotContain(@"href=""/Usuarios""", "el control de acceso sigue siendo solo del administrador");
        html.Should().NotContain(@"href=""/Accesos/Roles""");
    }

    [Fact]
    public async Task Una_opcion_de_administracion_que_no_es_catalogo_tambien_abre_el_menu()
    {
        // El menú miraba «es administrador» para Usuarios, Roles y Permisos, así que un rol con
        // Usuarios.Ver no veía nada. Ahora cada entrada se gatea con la clave de su destino, que
        // es la convención del proyecto.
        await _portal.OtorgarAsync("Empleado", "Usuarios.Ver");

        var html = await _portal.ClienteComo("Empleado").GetStringAsync("/Ayuda/Index");

        html.Should().Contain(@"href=""/Usuarios""");
        html.Should().NotContain(@"href=""/Accesos/Roles""", "no le otorgamos esa");
        html.Should().NotContain(@"href=""/Catalogos""", "tampoco administra ningún catálogo");
    }

    // Cada catálogo tiene que poder devolver a la portada sin pasar por el navbar.
    [Theory]
    [InlineData("/Instituciones")]
    [InlineData("/Areas")]
    [InlineData("/Unidades")]
    [InlineData("/Catalogos/Prioridades")]
    [InlineData("/Catalogos/PrioridadesTicket")]
    [InlineData("/Tickets/Temas")]
    public async Task Cada_catalogo_ofrece_su_regreso_a_la_portada(string ruta)
    {
        var html = await _portal.ClienteComo("Administrador").GetStringAsync(ruta);

        html.Should().Contain("← Catálogos");
    }

    [Fact]
    public async Task Quien_solo_puede_ver_no_recibe_el_boton_de_crear()
    {
        // La lista pide Ver y el editor pide Editar: ofrecer «+ Nueva» a quien solo puede ver lo
        // manda a un Forbidden. Áreas y Unidades lo ofrecían sin condición.
        await _portal.OtorgarAsync("Consultor", "Unidades.Ver");

        var html = await _portal.ClienteComo("Consultor").GetStringAsync("/Unidades");

        html.Should().Contain("← Catálogos");
        html.Should().NotContain("+ Nueva Unidad");
    }

    [Fact]
    public async Task Quien_no_administra_nada_no_ve_el_menu()
    {
        // Consultor no alcanza ningún catálogo ni es administrador: ofrecerle el menú sería
        // abrirle un desplegable vacío.
        var html = await _portal.ClienteComo("Consultor").GetStringAsync("/Ayuda/Index");

        html.Should().NotContain(@"href=""/Catalogos""");
        html.Should().NotContain("Administración<span");
    }

    // ── Catálogo de prioridades ───────────────────────────────────
    [Fact]
    public async Task La_pantalla_lista_las_prioridades_sembradas()
    {
        var html = await _portal.ClienteComo("Administrador").GetStringAsync("/Catalogos/Prioridades");

        html.Should().Contain("Alta");
        html.Should().Contain("Media");
        html.Should().Contain("Baja");
    }

    // Invariante barato de una tabla editable. No es el que falló —cuando la insignia de color
    // cayó bajo el rótulo «Predeterminada» los conteos cuadraban igual, ocho y ocho— pero una
    // celda de más corre todas las columnas siguientes, y eso sí lo caza.
    [Theory]
    [InlineData("/Catalogos/Prioridades")]
    [InlineData("/Catalogos/PrioridadesTicket")]
    public async Task Cada_fila_tiene_tantas_celdas_como_encabezados(string ruta)
    {
        var html = await _portal.ClienteComo("Administrador").GetStringAsync(ruta);

        // El patrón exige que al «th» le siga un espacio o el cierre: «<th» a secas cuenta también
        // el propio <thead> y deja el conteo una unidad arriba.
        var encabezados = Regex.Matches(
            Regex.Match(html, @"<thead>.*?</thead>", RegexOptions.Singleline).Value, @"<th[\s>]").Count;
        encabezados.Should().BeGreaterThan(0, "la tabla tiene que haberse pintado");

        var cuerpo = Regex.Match(html, @"<tbody>.*?</tbody>", RegexOptions.Singleline).Value;
        foreach (Match fila in Regex.Matches(cuerpo, @"<tr>.*?</tr>", RegexOptions.Singleline))
            Regex.Matches(fila.Value, "<td").Count.Should().Be(encabezados,
                "una celda de más o de menos corre todas las columnas siguientes bajo el rótulo equivocado");
    }

    // Ésta sí es la que falló. El control de «predeterminada» vivía en una columna sin rótulo, y
    // bajo el rótulo «Predeterminada» se pintaba la insignia de color: quien leyera la tabla creía
    // que las cuatro filas eran la predeterminada. Se comprueba por posición, que es como lo lee
    // una persona: el encabezado N manda sobre la celda N.
    [Fact]
    public async Task El_control_de_predeterminada_esta_bajo_su_propio_rotulo()
    {
        var html = await _portal.ClienteComo("Administrador").GetStringAsync("/Catalogos/Prioridades");

        var encabezados = Regex.Matches(
                Regex.Match(html, @"<thead>.*?</thead>", RegexOptions.Singleline).Value,
                @"<th[^>]*>(?<t>.*?)</th>", RegexOptions.Singleline)
            .Select(m => m.Groups["t"].Value.Trim()).ToList();

        var iPredet = encabezados.FindIndex(t => t.StartsWith("Predet"));
        var iVista  = encabezados.FindIndex(t => t == "Vista");
        iPredet.Should().BeGreaterThanOrEqualTo(0, "la columna de la estrella tiene que estar rotulada");
        iVista.Should().BeGreaterThanOrEqualTo(0, "la muestra del color tiene que tener su propio rótulo");

        var filaMedia = Regex.Match(html, @"<tr>(?:(?!</tr>).)*?value=""Media"".*?</tr>",
                                    RegexOptions.Singleline).Value;
        var celdas = Regex.Matches(filaMedia, @"<td[^>]*>(?<c>.*?)</td>", RegexOptions.Singleline)
            .Select(m => m.Groups["c"].Value).ToList();

        celdas[iPredet].Should().Contain("★", "«Media» es la predeterminada del sembrado");
        celdas[iVista].Should().Contain("prio-badge", "acá va la muestra de cómo se verá la insignia");
        celdas[iVista].Should().NotContain("★");
    }

    [Fact]
    public async Task La_prioridad_que_nadie_usa_ofrece_su_boton_de_eliminar()
    {
        var html = await _portal.ClienteComo("Administrador").GetStringAsync("/Catalogos/Prioridades");

        html.Should().Contain("handler=Eliminar");
        html.Should().Contain("Eliminar prioridad");
    }

    [Fact]
    public async Task La_predeterminada_no_ofrece_eliminarla()
    {
        // «Media» es la predeterminada del sembrado. Ofrecer el botón para después rechazar el
        // clic con un mensaje sería prometer algo que el comando no va a hacer.
        var html = await _portal.ClienteComo("Administrador").GetStringAsync("/Catalogos/Prioridades");

        var filaMedia = Regex.Match(html, @"<tr>(?:(?!</tr>).)*?value=""Media"".*?</tr>",
                                    RegexOptions.Singleline).Value;
        filaMedia.Should().NotBeEmpty("la fila de «Media» tiene que estar en la tabla");
        filaMedia.Should().NotContain("handler=Eliminar");
        filaMedia.Should().Contain("★");
    }

    [Fact]
    public async Task Sin_el_permiso_no_se_entra()
    {
        var r = await _portal.ClienteComo("JefeArea").GetAsync("/Catalogos/Prioridades");

        r.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Crear_una_prioridad_la_deja_disponible_para_los_proyectos()
    {
        // Es el caso que motivó todo el cambio: agregar «Q3» sin tocar código.
        var (cliente, token) = await PantallaAsync();

        var r = await cliente.PostAsync("/Catalogos/Prioridades?handler=Crear", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Nombre", "Q3"),
            new KeyValuePair<string, string>("Orden", "4"),
            new KeyValuePair<string, string>("Color", "3")   // Verde
        ]));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var creada = await db.PrioridadesProyecto.SingleAsync(p => p.Nombre == "Q3");
        creada.Activo.Should().BeTrue();
        creada.Orden.Should().Be(4);

        // Y aparece donde tiene que aparecer: en el desplegable del listado de proyectos.
        (await cliente.GetStringAsync("/Catalogos/Prioridades")).Should().Contain("Q3");
    }

    [Fact]
    public async Task Un_nombre_repetido_se_rechaza_con_su_motivo()
    {
        var (cliente, token) = await PantallaAsync();

        var r = await cliente.PostAsync("/Catalogos/Prioridades?handler=Crear", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Nombre", "Alta"),
            new KeyValuePair<string, string>("Orden", "9"),
            new KeyValuePair<string, string>("Color", "5")
        ]));

        // Se queda en la página con el motivo a la vista, no redirige como si hubiera funcionado.
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await r.Content.ReadAsStringAsync()).Should().Contain("Ya existe una prioridad llamada");
    }

    // ── Catálogo de prioridades de ticket ─────────────────────────
    [Fact]
    public async Task La_pantalla_de_tickets_lista_su_propio_catalogo()
    {
        var html = await _portal.ClienteComo("Administrador").GetStringAsync("/Catalogos/PrioridadesTicket");

        html.Should().Contain("Critica");
        html.Should().Contain("Cuenta como crítica",
            "la marca que alimenta el indicador de los tableros tiene que ser visible y editable");
    }

    [Fact]
    public async Task Crear_una_prioridad_de_ticket_marcada_como_critica()
    {
        var cliente = _portal.ClienteComo("Administrador");
        var html = await cliente.GetStringAsync("/Catalogos/PrioridadesTicket");
        var token = Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        var r = await cliente.PostAsync("/Catalogos/PrioridadesTicket?handler=Crear", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Nombre", "Bloqueante"),
            new KeyValuePair<string, string>("Orden", "0"),
            new KeyValuePair<string, string>("Color", "5"),   // Rojo
            new KeyValuePair<string, string>("EsCritica", "true")
        ]));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var creada = await db.PrioridadesTicket.SingleAsync(p => p.Nombre == "Bloqueante");
        creada.EsCritica.Should().BeTrue();
    }

    private async Task<(HttpClient Cliente, string Token)> PantallaAsync()
    {
        var cliente = _portal.ClienteComo("Administrador");
        var html = await cliente.GetStringAsync("/Catalogos/Prioridades");
        var token = Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        token.Should().NotBeEmpty();
        return (cliente, token);
    }
}
