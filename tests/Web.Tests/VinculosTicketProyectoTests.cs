using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La relación opcional entre un proyecto y los tickets de soporte que atiende.
///
/// <para>Es la tercera pata de <see cref="VinculosProyectoTests"/> y arrastra las mismas reglas: el
/// vínculo se ancla en el proyecto, el ticket sigue detrás de su propio filtro, y lo que no se
/// alcanza se <b>cuenta</b> en vez de esconderse. Acá se prueba además lo que es propio del ticket:
/// que vincular exige <c>Proyectos.Editar</c> y no <c>Tickets.Editar</c> —la acción escribe en la
/// ficha y en la bitácora del proyecto, no en el ticket— y que se puede hacer desde los dos
/// extremos.</para>
///
/// <para>Todo el texto de prueba va sin acentos: Razor codifica lo que sale del Basic Latin, así
/// que un <c>NotContain</c> con acentos pasaría siempre aunque el texto estuviera.</para>
/// </summary>
public sealed class VinculosTicketProyectoTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    private int _proyectoId;
    private int _ticketPropio;
    private int _ticketAjeno;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea",
            "Proyectos.Ver", "Proyectos.Editar", "Tickets.Ver", "Tickets.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var proyecto = Proyecto.Crear("PRY-2026-90", "Portal de tramites");
        proyecto.InstitucionId = "DIGER";
        db.Proyectos.Add(proyecto);

        var propio = Ticket.Crear("TCK-2026-0001", "No carga el listado de expedientes");
        propio.InstitucionId = "DIGER";

        // El ajeno es de otra institución: el proyecto sí se ve, el ticket no.
        var ajeno = Ticket.Crear("TCK-2026-0002", "Incidencia reservada de CONSUCOOP");
        ajeno.InstitucionId = "CONSUCOOP";

        db.Tickets.AddRange(propio, ajeno);
        await db.SaveChangesAsync();

        _proyectoId   = proyecto.Id;
        _ticketPropio = propio.Id;
        _ticketAjeno  = ajeno.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private static readonly Regex TokenRx = new(
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.Compiled);

    private static FormUrlEncodedContent Form(string token, params (string, string)[] campos)
    {
        var datos = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token) };
        foreach (var (k, v) in campos) datos.Add(new KeyValuePair<string, string>(k, v));
        return new FormUrlEncodedContent(datos);
    }

    private async Task<(HttpClient Cliente, string Token)> DesdeFichaAsync(string rol = "JefeArea")
    {
        var cliente = _portal.ClienteComo(rol);
        var html = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoId}");
        var token = TokenRx.Match(html).Groups[1].Value;
        token.Should().NotBeEmpty();
        return (cliente, token);
    }

    private async Task<(HttpClient Cliente, string Token)> DesdeTicketAsync(string rol = "JefeArea")
    {
        var cliente = _portal.ClienteComo(rol);
        var html = await cliente.GetStringAsync($"/Tickets/Detalle/{_ticketPropio}");
        var token = TokenRx.Match(html).Groups[1].Value;
        token.Should().NotBeEmpty();
        return (cliente, token);
    }

    private async Task<int> ContarAsync()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ProyectoTickets.IgnoreQueryFilters().CountAsync();
    }

    // ── Alta y baja desde la ficha del proyecto ───────────────────
    [Fact]
    public async Task Vincular_un_ticket_lo_deja_en_la_ficha()
    {
        var (cliente, token) = await DesdeFichaAsync();

        var r = await cliente.PostAsync(
            $"/Proyectos/Editor/{_proyectoId}?handler=VincularTicket",
            Form(token, ("VinculoTicketId", _ticketPropio.ToString()),
                        ("VinculoNota", "lo pedido entra en el entregable de reportes")));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var ficha = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoId}");
        ficha.Should().Contain("TCK-2026-0001");
        ficha.Should().Contain("lo pedido entra en el entregable de reportes");
    }

    [Fact]
    public async Task El_mismo_par_no_se_vincula_dos_veces()
    {
        var (cliente, token) = await DesdeFichaAsync();
        var url = $"/Proyectos/Editor/{_proyectoId}?handler=VincularTicket";

        await cliente.PostAsync(url, Form(token, ("VinculoTicketId", _ticketPropio.ToString())));
        await cliente.PostAsync(url, Form(token, ("VinculoTicketId", _ticketPropio.ToString())));

        (await ContarAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Quitar_el_vinculo_no_borra_el_ticket()
    {
        var (cliente, token) = await DesdeFichaAsync();
        await cliente.PostAsync($"/Proyectos/Editor/{_proyectoId}?handler=VincularTicket",
            Form(token, ("VinculoTicketId", _ticketPropio.ToString())));

        int vinculoId;
        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            vinculoId = await db.ProyectoTickets.IgnoreQueryFilters()
                .Where(x => x.ProyectoId == _proyectoId).Select(x => x.Id).SingleAsync();
        }

        await cliente.PostAsync($"/Proyectos/Editor/{_proyectoId}?handler=QuitarVinculoTicket",
            Form(token, ("VinculoId", vinculoId.ToString())));

        (await ContarAsync()).Should().Be(0);

        using var scope2 = _portal.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db2.Tickets.IgnoreQueryFilters().AnyAsync(t => t.Id == _ticketPropio))
            .Should().BeTrue("quitar el vinculo es una afirmacion sobre el proyecto, no un borrado");
    }

    // ── Alcance ───────────────────────────────────────────────────
    [Fact]
    public async Task No_se_puede_vincular_un_ticket_fuera_del_alcance()
    {
        // Sin esto, pasar el Id en el formulario permitiria colgar del proyecto —y de paso
        // averiguar que existe— un ticket que la persona no puede ni abrir.
        var (cliente, token) = await DesdeFichaAsync();

        await cliente.PostAsync($"/Proyectos/Editor/{_proyectoId}?handler=VincularTicket",
            Form(token, ("VinculoTicketId", _ticketAjeno.ToString())));

        (await ContarAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_vinculo_fuera_de_alcance_se_cuenta_pero_no_se_muestra()
    {
        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ProyectoTickets.Add(ProyectoTicket.Crear(_proyectoId, _ticketAjeno, "siembra"));
            await db.SaveChangesAsync();
        }

        var ficha = await _portal.ClienteComo("JefeArea").GetStringAsync($"/Proyectos/Editor/{_proyectoId}");

        ficha.Should().NotContain("Incidencia reservada de CONSUCOOP",
            "el titulo de un ticket ajeno no puede filtrarse por la ficha del proyecto");
        ficha.Should().Contain("fuera de su alcance",
            "pero se dice que hay algo vinculado que no se esta viendo");
    }

    [Fact]
    public async Task El_selector_no_ofrece_tickets_ajenos()
    {
        var ficha = await _portal.ClienteComo("JefeArea").GetStringAsync($"/Proyectos/Editor/{_proyectoId}");

        ficha.Should().Contain("No carga el listado de expedientes");
        ficha.Should().NotContain("Incidencia reservada de CONSUCOOP");
    }

    // ── El mismo vínculo desde el otro extremo ────────────────────
    [Fact]
    public async Task Se_puede_vincular_desde_el_detalle_del_ticket()
    {
        var (cliente, token) = await DesdeTicketAsync();

        var r = await cliente.PostAsync(
            $"/Tickets/Detalle/{_ticketPropio}?handler=VincularProyecto",
            Form(token, ("VinculoProyectoId", _proyectoId.ToString()),
                        ("VinculoNota", "salio de este reporte")));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Y se ve desde los dos lados: es la misma fila.
        var detalle = await cliente.GetStringAsync($"/Tickets/Detalle/{_ticketPropio}");
        detalle.Should().Contain("PRY-2026-90");
        detalle.Should().Contain("salio de este reporte");

        var ficha = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoId}");
        ficha.Should().Contain("TCK-2026-0001");
    }

    [Fact]
    public async Task Desde_el_ticket_tampoco_se_alcanza_un_proyecto_ajeno()
    {
        int ajeno;
        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var p2 = Proyecto.Crear("PRY-2026-91", "Proyecto de otra institucion");
            p2.InstitucionId = "CONSUCOOP";
            db.Proyectos.Add(p2);
            await db.SaveChangesAsync();
            ajeno = p2.Id;
        }

        var (cliente, token) = await DesdeTicketAsync();

        await cliente.PostAsync($"/Tickets/Detalle/{_ticketPropio}?handler=VincularProyecto",
            Form(token, ("VinculoProyectoId", ajeno.ToString())));

        using var scope2 = _portal.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db2.ProyectoTickets.IgnoreQueryFilters().CountAsync(x => x.ProyectoId == ajeno))
            .Should().Be(0);

        var detalle = await cliente.GetStringAsync($"/Tickets/Detalle/{_ticketPropio}");
        detalle.Should().NotContain("Proyecto de otra institucion",
            "el selector no puede ofrecer proyectos ajenos");
    }

    [Fact]
    public async Task Desvincular_desde_el_ticket_apunta_al_proyecto_correcto()
    {
        // El handler del ticket recibe el proyecto por la ruta y el vínculo por el formulario, y el
        // comando comprueba que coincidan. Un formulario viejo que mande el vínculo de OTRO
        // proyecto no puede borrarlo.
        var (cliente, token) = await DesdeTicketAsync();
        await cliente.PostAsync($"/Tickets/Detalle/{_ticketPropio}?handler=VincularProyecto",
            Form(token, ("VinculoProyectoId", _proyectoId.ToString())));

        int vinculoId;
        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            vinculoId = await db.ProyectoTickets.IgnoreQueryFilters().Select(x => x.Id).SingleAsync();
        }

        // Con el proyecto equivocado en la ruta: el comando lo rechaza y el vinculo sigue.
        await cliente.PostAsync(
            $"/Tickets/Detalle/{_ticketPropio}?handler=DesvincularProyecto&proyectoId=999999",
            Form(token, ("VinculoId", vinculoId.ToString())));
        (await ContarAsync()).Should().Be(1);

        // Con el correcto, se va.
        await cliente.PostAsync(
            $"/Tickets/Detalle/{_ticketPropio}?handler=DesvincularProyecto&proyectoId={_proyectoId}",
            Form(token, ("VinculoId", vinculoId.ToString())));
        (await ContarAsync()).Should().Be(0);
    }

    // ── Permisos ──────────────────────────────────────────────────
    [Fact]
    public async Task Atender_tickets_no_alcanza_para_vincularlos_a_un_proyecto()
    {
        // Este es el punto propio del ticket: el rol puede editar tickets de sobra, pero vincular
        // escribe en la ficha y en la bitacora del PROYECTO. Si esto se cayera a Tickets.Editar,
        // cualquier persona de soporte podria escribir en la bitacora de cualquier proyecto.
        await _portal.OtorgarAsync("Consultor", "Tickets.Ver", "Tickets.Editar", "Proyectos.Ver");

        var cliente = _portal.ClienteComo("Consultor");
        var html = await cliente.GetStringAsync($"/Tickets/Detalle/{_ticketPropio}");
        var token = TokenRx.Match(html).Groups[1].Value;

        var r = await cliente.PostAsync($"/Tickets/Detalle/{_ticketPropio}?handler=VincularProyecto",
            Form(token, ("VinculoProyectoId", _proyectoId.ToString())));

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ContarAsync()).Should().Be(0);

        // Y el formulario tampoco se le ofrece: el boton se gatea con la clave que el servidor
        // exige. Se busca la forma en que asp-page-handler LLEGA al HTML —dentro del action, como
        // ?handler=…— y no el atributo del tag helper, que nunca se sirve: buscar el atributo daria
        // un NotContain que pasa siempre.
        html.Should().NotContain("handler=VincularProyecto");

        // Prueba de que la cadena anterior es la correcta: a quien SI puede, se le sirve.
        var conPermiso = await _portal.ClienteComo("JefeArea")
            .GetStringAsync($"/Tickets/Detalle/{_ticketPropio}");
        conPermiso.Should().Contain("handler=VincularProyecto");
    }
}
