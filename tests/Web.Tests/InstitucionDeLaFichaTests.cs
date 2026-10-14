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
/// La institución de una ficha, tal como la ve la API pública: <c>InstitucionId</c>.
///
/// <para>Dos defectos, del mismo síntoma: la ficha se publicaba y no aparecía nunca en Honduras
/// Fácil, sin un error en ninguna parte. Primero (14-10-2026, reportado desde Test): el Editor
/// guardaba la institución solo como texto, y una regla general le ponía a la ficha nueva la
/// institución de quien la creaba. Después: aun leyendo la llave de la sigla, la sigla era texto
/// libre, y 343 fichas de Test tenían una que no era de ninguna institución registrada.</para>
///
/// <para>Ahora la institución se elige de una lista de las registradas, y sin una no se guarda.</para>
/// </summary>
public sealed class InstitucionDeLaFichaTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    // Lo que comparten los dos avisos de Publicado.
    private const string AvisoSinInstitucion = "llegará a Honduras Fácil";
    private const string ErrorSinInstitucion = "Elija la institución responsable de la lista";

    public Task InitializeAsync() => _portal.PrepararAsync();

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    // ── Ayudantes ─────────────────────────────────────────────────────────────

    private static string Token(string html)
    {
        var token = Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;
        token.Should().NotBeEmpty();
        return token;
    }

    private async Task<(HttpClient Cliente, string Html)> AbrirAsync(string ruta, string rol = "Administrador")
    {
        var cliente = _portal.ClienteComo(rol);
        return (cliente, await cliente.GetStringAsync(ruta));
    }

    private static FormUrlEncodedContent Ficha(string token, int id, int idSiger, string codigo, string? institucionId)
        => new(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Form.Id", id.ToString()),
            new KeyValuePair<string, string>("Form.IdSiger", idSiger.ToString()),
            new KeyValuePair<string, string>("Form.Codigo", codigo),
            new KeyValuePair<string, string>("Form.Nombre", "Trámite de prueba " + codigo),
            new KeyValuePair<string, string>("Form.Institucion", "Institución de prueba"),
            new KeyValuePair<string, string>("Form.InstitucionId", institucionId ?? "")
        ]);

    private async Task<TramiteSiger?> LeerAsync(string codigo)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TramitesSiger.AsNoTracking().SingleOrDefaultAsync(t => t.Codigo == codigo);
    }

    private async Task<int> SembrarAsync(string codigo, int idSiger, string? sigla, string? institucionId,
                                         bool publicado = false)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t = new TramiteSiger
        {
            IdSiger = idSiger, Codigo = codigo, Nombre = "Trámite sembrado " + codigo,
            Institucion = "Institución sembrada", Sigla = sigla, InstitucionId = institucionId,
            Publicado = publicado
        };
        db.TramitesSiger.Add(t);
        await db.SaveChangesAsync();
        return t.Id;
    }

    private static async Task<string> SeguirAsync(HttpClient cliente, HttpResponseMessage r)
    {
        r.StatusCode.Should().Be(HttpStatusCode.Redirect);
        // Decodificado: Razor escribe «á» como &#xE1;, y lo que importa es lo que lee la persona.
        return WebUtility.HtmlDecode(await cliente.GetStringAsync(r.Headers.Location!.OriginalString));
    }

    private static bool Elegida(string html, string institucionId) =>
        Regex.IsMatch(html, $"""<option[^>]*value="{institucionId}"[^>]*selected""") ||
        Regex.IsMatch(html, $"""<option[^>]*selected[^>]*value="{institucionId}"[^>]*>""");

    // ── Crear ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Crear_con_la_institucion_elegida_llena_la_llave_y_la_sigla()
    {
        var (cliente, html) = await AbrirAsync("/HondurasSimple/Editor");

        var r = await cliente.PostAsync("/HondurasSimple/Editor",
            Ficha(Token(html), id: 0, idSiger: 9101, codigo: "910-001", institucionId: "INPREMA"));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var ficha = await LeerAsync("910-001");
        ficha!.InstitucionId.Should().Be("INPREMA",
            "sin la llave la API no la cuenta como de INPREMA y Honduras Fácil no la trae nunca");
        ficha.Sigla.Should().Be("INPREMA", "la sigla de texto se deduce de la institución elegida");
    }

    [Fact]
    public async Task Sin_elegir_institucion_no_se_guarda()
    {
        var (cliente, html) = await AbrirAsync("/HondurasSimple/Editor");

        var r = await cliente.PostAsync("/HondurasSimple/Editor",
            Ficha(Token(html), id: 0, idSiger: 9102, codigo: "910-002", institucionId: null));

        r.StatusCode.Should().Be(HttpStatusCode.OK, "vuelve al formulario con el error");
        WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()).Should().Contain(ErrorSinInstitucion);
        (await LeerAsync("910-002")).Should().BeNull("una ficha sin institución no puede llegar al portal");
    }

    [Fact]
    public async Task Una_institucion_que_no_esta_registrada_no_se_guarda()
    {
        // La lista solo ofrece las registradas, pero el valor viaja en el formulario y se puede
        // cambiar a mano en el navegador: la comprobación es del servidor.
        var (cliente, html) = await AbrirAsync("/HondurasSimple/Editor");

        var r = await cliente.PostAsync("/HondurasSimple/Editor",
            Ficha(Token(html), id: 0, idSiger: 9103, codigo: "910-003", institucionId: "NOEXISTE"));

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()).Should().Contain(ErrorSinInstitucion);
        (await LeerAsync("910-003")).Should().BeNull();
    }

    [Fact]
    public async Task Un_operador_que_no_es_administrador_ve_todas_las_instituciones_y_guarda_la_de_otra()
    {
        // El que de verdad usa el Editor es un Jefe de Área de DIGER, no el administrador. El
        // filtro de alcance de Instituciones solo le dejaría ver DIGER; y la regla general de
        // inserción le ponía a la ficha la institución de quien la crea.
        await _portal.OtorgarAsync("JefeArea", "HondurasSimple.Editar");
        var (cliente, html) = await AbrirAsync("/HondurasSimple/Editor", "JefeArea");

        html.Should().Contain("value=\"INPREMA\"", "la lista tiene que ofrecer todas las instituciones");

        var r = await cliente.PostAsync("/HondurasSimple/Editor",
            Ficha(Token(html), id: 0, idSiger: 9104, codigo: "910-004", institucionId: "INPREMA"));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await LeerAsync("910-004"))!.InstitucionId.Should().Be("INPREMA",
            "la institución de un trámite es la que lo atiende, no la de quien lo capturó");
    }

    // ── Editar ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Una_ficha_vieja_sin_llave_abre_con_su_institucion_ya_elegida_si_la_sigla_es_registrada()
    {
        // Las importadas de SIGER: sigla escrita, llave vacía. Al abrirlas no hay que adivinar.
        var id = await SembrarAsync("910-005", 9105, sigla: "inprema", institucionId: null);

        var (_, html) = await AbrirAsync($"/HondurasSimple/Editor?id={id}");

        Elegida(html, "INPREMA").Should().BeTrue();
    }

    [Fact]
    public async Task Una_llave_puesta_por_el_pase_abre_elegida_y_se_conserva_al_guardar()
    {
        var id = await SembrarAsync("910-006", 9106, sigla: null, institucionId: "IHTT");
        var (cliente, html) = await AbrirAsync($"/HondurasSimple/Editor?id={id}");

        Elegida(html, "IHTT").Should().BeTrue();
        await cliente.PostAsync("/HondurasSimple/Editor", Ficha(Token(html), id, 9106, "910-006", "IHTT"));

        (await LeerAsync("910-006"))!.InstitucionId.Should().Be("IHTT");
    }

    // ── Publicar ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Publicar_dice_el_resultado()
    {
        // El mensaje se calculaba y nunca se pintaba: publicar no daba ninguna señal.
        var id = await SembrarAsync("910-007", 9107, sigla: "INPREMA", institucionId: "INPREMA");
        var (cliente, html) = await AbrirAsync("/HondurasSimple/Publicacion?Tab=candidatas");

        var r = await cliente.PostAsync("/HondurasSimple/Publicacion?handler=Publicar",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", Token(html)),
                new KeyValuePair<string, string>("Seleccion", id.ToString())
            ]));

        var pagina = await SeguirAsync(cliente, r);
        pagina.Should().Contain("1 ficha(s) publicada(s)");
        pagina.Should().NotContain(AvisoSinInstitucion);
    }

    [Fact]
    public async Task Publicar_una_ficha_sin_institucion_avisa_que_no_llegara()
    {
        // Ya no se puede crear así desde el Editor, pero quedan las de antes: se avisa al publicar.
        var id = await SembrarAsync("910-008", 9108, sigla: "NOEXISTE", institucionId: null);
        var (cliente, html) = await AbrirAsync("/HondurasSimple/Publicacion?Tab=candidatas");

        var r = await cliente.PostAsync("/HondurasSimple/Publicacion?handler=Publicar",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", Token(html)),
                new KeyValuePair<string, string>("Seleccion", id.ToString())
            ]));

        (await LeerAsync("910-008"))!.Publicado.Should().BeTrue("se publica igual: el aviso informa, no bloquea");
        var pagina = await SeguirAsync(cliente, r);
        pagina.Should().Contain(AvisoSinInstitucion);
        pagina.Should().Contain("910-008", "tiene que decir cuál, no solo cuántas");
    }
}
