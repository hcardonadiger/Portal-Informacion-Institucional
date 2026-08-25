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
/// El editor de fichas después de la Fase 7: se captura el <b>tramo</b>, no la dirección completa.
///
/// Lo que de verdad se protege acá es que <b>una dirección heredada no se pierda por guardar</b>.
/// El campo dejó de ser editable —lo enseña la pantalla pero no lo escribe la persona— y ese es
/// justo el momento en que un formulario silenciosamente lo borra: el POST no trae el valor y el
/// código lo cree vacío. Nadie se daría cuenta hasta que un enlace del portal ciudadano dejara de
/// funcionar, y para entonces el dato ya no estaría.
/// </summary>
public sealed class DireccionSolEnElEditorTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    private int _conHeredada;   // arrastra una URL completa de antes (D-14)
    private int _limpia;        // sin nada: acá se captura un tramo

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Administrador", "Siger.Ver", "Siger.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Instituciones.Add(Institucion.Crear("IDP", "Instituto de Prueba"));
        await db.SaveChangesAsync();

        var heredada = Ficha("910-001", "Tramite con enlace viejo");
        heredada.EstaEnSol = true;
        heredada.SolUrl    = "https://sol-viejo.gob.hn/ruta/completa";

        var limpia = Ficha("910-002", "Tramite sin enlace");

        db.TramitesSiger.AddRange(heredada, limpia);
        await db.SaveChangesAsync();

        _conHeredada = heredada.Id;
        _limpia      = limpia.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    // ── Capturar el tramo ─────────────────────────────────────────────────────

    [Fact]
    public async Task La_pantalla_enseña_el_prefijo_que_la_persona_no_escribe()
    {
        var html = await LeerAsync($"/Siger/Editor?id={_limpia}");

        html.Should().Contain("/IDP/",
            "sin ver el prefijo, quien captura no sabe qué dirección va a producir lo que teclea");
    }

    [Fact]
    public async Task Capturar_un_tramo_lo_guarda_normalizado()
    {
        await GuardarAsync(_limpia, [new("Form.SolTramo", "  /licencia-de-operacion/  ")]);

        var ficha = await FichaAsync(_limpia);
        ficha.SolTramo.Should().Be("licencia-de-operacion",
            "las barras y los espacios de sobra se recortan en un solo lugar");
    }

    /// <summary>
    /// El error más probable de quien conocía este campo cuando pedía la URL entera. Que el
    /// mensaje lo nombre importa: el genérico de forma le haría buscar un carácter mal escrito.
    /// </summary>
    [Fact]
    public async Task Pegar_la_direccion_completa_se_rechaza_diciendo_por_que()
    {
        var html = await GuardarEsperandoRechazoAsync(_limpia,
            [new("Form.SolTramo", "https://sol.pdihonduras.gob.hn/IDP/licencia")]);

        html.Should().Contain("solo el tramo final");
        (await FichaAsync(_limpia)).SolTramo.Should().BeNull("nada se guardó");
    }

    [Fact]
    public async Task Un_tramo_con_espacios_se_rechaza()
    {
        await GuardarEsperandoRechazoAsync(_limpia, [new("Form.SolTramo", "licencia de operacion")]);

        (await FichaAsync(_limpia)).SolTramo.Should().BeNull();
    }

    /// <summary>Marcar «está en SOL» sin decir dónde deja una ficha que promete un enlace que no
    /// existe. La base también lo impide, pero el formulario tiene que decirlo antes.</summary>
    [Fact]
    public async Task Decir_que_esta_en_SOL_sin_tramo_ni_heredada_se_rechaza()
    {
        await GuardarEsperandoRechazoAsync(_limpia,
            [new("Form.EstaEnSol", "true"), new("Form.SolTramo", "")]);

        (await FichaAsync(_limpia)).EstaEnSol.Should().BeFalse();
    }

    // ── La dirección heredada (D-14) ──────────────────────────────────────────

    /// <summary>
    /// La prueba que sostiene la fase. Guardar la ficha sin tocar el enlace no puede borrarlo,
    /// aunque el formulario ya no lo mande de vuelta.
    /// </summary>
    [Fact]
    public async Task Guardar_sin_tocar_el_enlace_no_borra_la_direccion_heredada()
    {
        var antes = (await FichaAsync(_conHeredada)).SolUrl;
        antes.Should().NotBeNull("la prueba no valdría nada si el campo estuviera vacío");

        await GuardarAsync(_conHeredada, [new("Form.EstaEnSol", "true")]);

        (await FichaAsync(_conHeredada)).SolUrl.Should().Be(antes,
            "es un dato que la persona no edita: se lee de la base, no del formulario");
    }

    [Fact]
    public async Task Quitar_la_heredada_a_proposito_si_la_borra()
    {
        await GuardarAsync(_conHeredada,
            [new("Form.QuitarSolHeredada", "true"), new("Form.SolTramo", "reemplazo")]);

        var ficha = await FichaAsync(_conHeredada);
        ficha.SolUrl.Should().BeNull("se pidió quitarla");
        ficha.SolTramo.Should().Be("reemplazo");
    }

    /// <summary>Capturar un tramo sobre una ficha con dirección heredada no la borra: la deja
    /// dormida. El tramo manda al componer, y si alguien se arrepiente, lo viejo sigue ahí.</summary>
    [Fact]
    public async Task Capturar_un_tramo_no_borra_la_heredada_por_su_cuenta()
    {
        await GuardarAsync(_conHeredada, [new("Form.EstaEnSol", "true"), new("Form.SolTramo", "nuevo")]);

        var ficha = await FichaAsync(_conHeredada);
        ficha.SolTramo.Should().Be("nuevo");
        ficha.SolUrl.Should().NotBeNull("quitarla es una decisión aparte, con su propia casilla");
    }

    // ── Apoyo ─────────────────────────────────────────────────────────────────

    private async Task<string> LeerAsync(string ruta)
    {
        var respuesta = await _portal.ClienteComo("Administrador").GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await respuesta.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Envía el formulario del editor con los campos mínimos obligatorios más los que pida la
    /// prueba. Se rellena a mano y no se reenvía el HTML entero a propósito: así cada prueba
    /// declara exactamente qué manda, que es lo que se está afirmando.
    /// </summary>
    private static List<KeyValuePair<string, string>> CamposBase(TramiteSiger ficha) =>
    [
        new("Form.Id",          ficha.Id.ToString()),
        new("Form.Codigo",      ficha.Codigo),
        new("Form.Nombre",      ficha.Nombre),
        new("Form.Institucion", ficha.Institucion),
        new("Form.Sigla",       ficha.Sigla ?? ""),
        new("Form.EstadoSiger", ficha.EstadoSiger ?? "")
    ];

    private async Task<HttpResponseMessage> EnviarAsync(int id, List<KeyValuePair<string, string>> extra)
    {
        var cliente = _portal.ClienteComo("Administrador");

        var pagina = await cliente.GetAsync($"/Siger/Editor?id={id}");
        pagina.StatusCode.Should().Be(HttpStatusCode.OK);

        var campos = CamposBase(await FichaAsync(id));
        campos.AddRange(extra);
        campos.Add(new("__RequestVerificationToken", Token(await pagina.Content.ReadAsStringAsync())));

        return await cliente.PostAsync($"/Siger/Editor?id={id}", new FormUrlEncodedContent(campos));
    }

    private async Task GuardarAsync(int id, List<KeyValuePair<string, string>> extra)
    {
        var respuesta = await EnviarAsync(id, extra);

        respuesta.StatusCode.Should().BeOneOf(new[] { HttpStatusCode.Redirect, HttpStatusCode.Found },
            "un guardado correcto redirige; un 200 significa que la validación lo rechazó");
    }

    private async Task<string> GuardarEsperandoRechazoAsync(int id, List<KeyValuePair<string, string>> extra)
    {
        var respuesta = await EnviarAsync(id, extra);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
            "un rechazo vuelve a pintar el formulario, no redirige");

        return await respuesta.Content.ReadAsStringAsync();
    }

    private static string Token(string html)
    {
        var token = Regex.Match(html,
            @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""").Groups[1].Value;
        token.Should().NotBeEmpty("sin token el POST se rechaza y la prueba no probaría nada");
        return token;
    }

    private async Task<TramiteSiger> FichaAsync(int id)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TramitesSiger.AsNoTracking().FirstAsync(t => t.Id == id);
    }

    private static TramiteSiger Ficha(string codigo, string nombre) => new()
    {
        Codigo = codigo, Nombre = nombre, EstadoSiger = "Registrado",
        Institucion = "Instituto de Prueba", Sigla = "IDP", InstitucionId = "IDP"
    };
}
