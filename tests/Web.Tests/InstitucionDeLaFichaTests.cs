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
/// <para>El Editor guardaba la institución solo como texto —nombre y sigla— y nunca llenaba la
/// llave. En pantalla la ficha decía «INPREMA», pero para la API no tenía institución: la API
/// filtra el catálogo por <c>InstitucionId</c>, y Honduras Fácil pide su corte (INPREMA, IHTT,
/// CONSUCOOP) por esa misma columna. Resultado: se creaba o corregía un trámite, se publicaba, y
/// nunca aparecía en el portal ciudadano, sin un solo error en ninguna parte. Reportado desde
/// Test el 14-10-2026.</para>
///
/// <para>La regla es la del script 18 (<c>asignar-institucion-a-tramites.sql</c>): la llave es la
/// sigla cuando existe una institución con ese Id; si no existe, no se inventa.</para>
/// </summary>
public sealed class InstitucionDeLaFichaTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    // Lo que comparten los dos avisos, el del Editor y el de Publicado.
    private const string AvisoSinInstitucion = "llegará a Honduras Fácil";

    public Task InitializeAsync() => _portal.PrepararAsync();

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    // ── Ayudantes ─────────────────────────────────────────────────────────────

    private async Task<(HttpClient Cliente, string Token)> AbrirAsync(string ruta)
    {
        var cliente = _portal.ClienteComo("Administrador");
        var html    = await cliente.GetStringAsync(ruta);
        var token   = Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;
        token.Should().NotBeEmpty();
        return (cliente, token);
    }

    private static FormUrlEncodedContent Ficha(string token, int id, int idSiger, string codigo,
                                               string? sigla, string institucion = "Institución de prueba")
        => new(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Form.Id", id.ToString()),
            new KeyValuePair<string, string>("Form.IdSiger", idSiger.ToString()),
            new KeyValuePair<string, string>("Form.Codigo", codigo),
            new KeyValuePair<string, string>("Form.Nombre", "Trámite de prueba " + codigo),
            new KeyValuePair<string, string>("Form.Institucion", institucion),
            new KeyValuePair<string, string>("Form.Sigla", sigla ?? "")
        ]);

    private async Task<TramiteSiger> LeerAsync(string codigo)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TramitesSiger.AsNoTracking().SingleAsync(t => t.Codigo == codigo);
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

    // ── Crear ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Crear_con_la_sigla_de_una_institucion_registrada_llena_la_llave()
    {
        var (cliente, token) = await AbrirAsync("/HondurasSimple/Editor");

        var r = await cliente.PostAsync("/HondurasSimple/Editor",
            Ficha(token, id: 0, idSiger: 9101, codigo: "910-001", sigla: "INPREMA"));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await LeerAsync("910-001")).InstitucionId.Should().Be("INPREMA",
            "sin la llave la API no la cuenta como de INPREMA y Honduras Fácil no la trae nunca");
    }

    [Fact]
    public async Task La_sigla_se_reconoce_sin_importar_mayusculas_y_se_guarda_la_llave_canonica()
    {
        var (cliente, token) = await AbrirAsync("/HondurasSimple/Editor");

        await cliente.PostAsync("/HondurasSimple/Editor",
            Ficha(token, id: 0, idSiger: 9102, codigo: "910-002", sigla: " ihtt "));

        (await LeerAsync("910-002")).InstitucionId.Should().Be("IHTT");
    }

    [Fact]
    public async Task Una_sigla_que_no_es_institucion_registrada_deja_la_llave_vacia_y_lo_avisa()
    {
        var (cliente, token) = await AbrirAsync("/HondurasSimple/Editor");

        var r = await cliente.PostAsync("/HondurasSimple/Editor",
            Ficha(token, id: 0, idSiger: 9103, codigo: "910-003", sigla: "NOEXISTE"));

        (await LeerAsync("910-003")).InstitucionId.Should().BeNull("no se inventa una institución");

        var html = await SeguirAsync(cliente, r);
        html.Should().Contain(AvisoSinInstitucion,
            "guardar en silencio es justo lo que hacía que nadie se enterara");
        html.Should().Contain("NOEXISTE");
    }

    [Fact]
    public async Task Un_operador_que_no_es_administrador_tambien_obtiene_la_llave_de_otra_institucion()
    {
        // El que de verdad usa el Editor es un Jefe de Área de DIGER, no el administrador. Para
        // él fallaban dos cosas a la vez: la ficha nacía con la institución de quien la crea
        // (DIGER) y no con la del trámite; y el catálogo de instituciones, con su filtro de
        // alcance, solo le deja ver la suya, así que buscar «INPREMA» ahí no la encontraría.
        await _portal.OtorgarAsync("JefeArea", "HondurasSimple.Editar");
        var cliente = _portal.ClienteComo("JefeArea", "DIGER");
        var html    = await cliente.GetStringAsync("/HondurasSimple/Editor");
        var token   = Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        var r = await cliente.PostAsync("/HondurasSimple/Editor",
            Ficha(token, id: 0, idSiger: 9109, codigo: "910-009", sigla: "INPREMA"));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await LeerAsync("910-009")).InstitucionId.Should().Be("INPREMA",
            "la institución de un trámite es la que lo atiende, no la de quien lo capturó");
    }

    // ── Editar ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Guardar_una_ficha_vieja_sin_llave_la_completa()
    {
        // Las 1.057 importadas de SIGER llegaron así: sigla escrita, llave vacía.
        var id = await SembrarAsync("910-004", 9104, sigla: "INPREMA", institucionId: null);
        var (cliente, token) = await AbrirAsync($"/HondurasSimple/Editor?id={id}");

        await cliente.PostAsync("/HondurasSimple/Editor",
            Ficha(token, id, 9104, "910-004", sigla: "INPREMA"));

        (await LeerAsync("910-004")).InstitucionId.Should().Be("INPREMA");
    }

    [Fact]
    public async Task Cambiar_la_sigla_a_una_no_registrada_quita_la_llave_vieja()
    {
        // Una llave vieja es peor que ninguna: mandaría el trámite a la institución equivocada.
        var id = await SembrarAsync("910-005", 9105, sigla: "INPREMA", institucionId: "INPREMA");
        var (cliente, token) = await AbrirAsync($"/HondurasSimple/Editor?id={id}");

        await cliente.PostAsync("/HondurasSimple/Editor",
            Ficha(token, id, 9105, "910-005", sigla: "NOEXISTE"));

        (await LeerAsync("910-005")).InstitucionId.Should().BeNull();
    }

    [Fact]
    public async Task Una_llave_puesta_por_el_pase_no_se_pierde_si_la_sigla_no_cambia()
    {
        // «Pasar a SIGER» pone la llave desde el expediente, y la sigla de texto puede no
        // coincidir o venir vacía. Guardar sin tocar la sigla no puede borrar esa llave.
        var id = await SembrarAsync("910-006", 9106, sigla: null, institucionId: "IHTT");
        var (cliente, token) = await AbrirAsync($"/HondurasSimple/Editor?id={id}");

        await cliente.PostAsync("/HondurasSimple/Editor",
            Ficha(token, id, 9106, "910-006", sigla: null));

        (await LeerAsync("910-006")).InstitucionId.Should().Be("IHTT");
    }

    // ── Publicar ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Publicar_dice_el_resultado()
    {
        // El mensaje se calculaba y nunca se pintaba: publicar no daba ninguna señal.
        var id = await SembrarAsync("910-007", 9107, sigla: "INPREMA", institucionId: "INPREMA");
        var (cliente, token) = await AbrirAsync("/HondurasSimple/Publicacion?Tab=candidatas");

        var r = await cliente.PostAsync("/HondurasSimple/Publicacion?handler=Publicar",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("Seleccion", id.ToString())
            ]));

        var html = await SeguirAsync(cliente, r);
        html.Should().Contain("1 ficha(s) publicada(s)");
        html.Should().NotContain(AvisoSinInstitucion);
    }

    [Fact]
    public async Task Publicar_una_ficha_sin_institucion_avisa_que_no_llegara()
    {
        var id = await SembrarAsync("910-008", 9108, sigla: "NOEXISTE", institucionId: null);
        var (cliente, token) = await AbrirAsync("/HondurasSimple/Publicacion?Tab=candidatas");

        var r = await cliente.PostAsync("/HondurasSimple/Publicacion?handler=Publicar",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("Seleccion", id.ToString())
            ]));

        (await LeerAsync("910-008")).Publicado.Should().BeTrue(
            "se publica igual: el aviso informa, no bloquea");
        var html = await SeguirAsync(cliente, r);
        html.Should().Contain(AvisoSinInstitucion);
        html.Should().Contain("910-008", "tiene que decir cuál, no solo cuántas");
    }
}
