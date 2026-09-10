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
/// Alta de trámites SIGER desde <c>/Siger/Editor</c>.
///
/// <para>Fija un fallo reportado desde el portal: se llenaba el formulario, se pulsaba crear y la
/// pantalla saltaba a «Ocurrió un error inesperado» sin guardar nada. La causa: <c>IdSiger</c> y
/// <c>Codigo</c> tienen índice único y el handler insertaba sin comprobarlos. Con 1057 filas
/// ocupando el 92 % del rango de IdSiger, casi cualquier número tecleado choca. SQL Server
/// rechazaba el INSERT, EF lo envolvía en <c>DbUpdateException</c> y <c>WebExceptionHandler</c>
/// —que solo distingue NotFound/Validation/Domain— lo degradaba al mensaje genérico.</para>
///
/// <para>Estas pruebas corren sobre SQLite, que <b>sí</b> aplica los índices únicos. El provider
/// InMemory de las Application.Tests no los aplica, y por eso el fallo era invisible ahí.</para>
/// </summary>
public sealed class CrearTramiteSigerTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    private const int    IdSigerOcupado = 500;
    private const string CodigoOcupado  = "400-001";

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.TramitesSiger.Add(new TramiteSiger
        {
            IdSiger     = IdSigerOcupado,
            Codigo      = CodigoOcupado,
            Nombre      = "Trámite que ya existe",
            Institucion = "Dirección de Gestión por Resultados"
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Abre el editor vacío y devuelve el cliente con su antiforgery y el HTML servido.
    /// El administrador aprueba toda clave por código, así que no hace falta otorgar nada.</summary>
    private async Task<(HttpClient Cliente, string Token, string Html)> EditorNuevoAsync()
    {
        var cliente = _portal.ClienteComo("Administrador");
        var html    = await cliente.GetStringAsync("/Siger/Editor");
        var token   = Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        token.Should().NotBeEmpty();
        return (cliente, token, html);
    }

    /// <summary>El mínimo que el editor exige: los cuatro campos obligatorios más el token.</summary>
    private static FormUrlEncodedContent Alta(string token, int idSiger, string codigo,
                                              string nombre = "Trámite nuevo de prueba")
    {
        return new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Form.Id", "0"),
            new KeyValuePair<string, string>("Form.IdSiger", idSiger.ToString()),
            new KeyValuePair<string, string>("Form.Codigo", codigo),
            new KeyValuePair<string, string>("Form.Nombre", nombre),
            new KeyValuePair<string, string>("Form.Institucion", "Secretaría de prueba")
        ]);
    }

    private async Task<int> TramitesEnBaseAsync()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TramitesSiger.CountAsync();
    }

    [Fact]
    public async Task Un_codigo_repetido_se_avisa_en_el_formulario_y_no_cae_al_error_generico()
    {
        var (cliente, token, _) = await EditorNuevoAsync();

        var r = await cliente.PostAsync("/Siger/Editor",
            Alta(token, idSiger: 9001, codigo: CodigoOcupado));

        // El fallo reportado era exactamente esto: un 302 hacia /Error en vez de la página de vuelta.
        r.StatusCode.Should().Be(HttpStatusCode.OK,
            "un código repetido es un error del usuario, no una falla del portal");

        var html = await r.Content.ReadAsStringAsync();
        html.Should().Contain(CodigoOcupado);
        html.Should().MatchRegex("[Yy]a (lo usa|está)",
            "el mensaje tiene que decir cuál es el trámite que ya ocupa ese código");

        (await TramitesEnBaseAsync()).Should().Be(1, "no se pudo haber insertado nada");
    }

    [Fact]
    public async Task Un_IdSiger_repetido_tambien()
    {
        var (cliente, token, _) = await EditorNuevoAsync();

        var r = await cliente.PostAsync("/Siger/Editor",
            Alta(token, idSiger: IdSigerOcupado, codigo: "999-999"));

        r.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await r.Content.ReadAsStringAsync();
        html.Should().Contain(IdSigerOcupado.ToString());

        (await TramitesEnBaseAsync()).Should().Be(1);
    }

    [Fact]
    public async Task El_editor_vacio_precarga_el_siguiente_IdSiger_libre()
    {
        // Sin esto el campo llega en 0 y el usuario tiene que adivinar un hueco entre los ocupados.
        var (_, _, html) = await EditorNuevoAsync();

        Regex.Match(html, """id="Form_IdSiger"[^>]*value="(\d+)""").Groups[1].Value
            .Should().Be((IdSigerOcupado + 1).ToString());
    }

    [Fact]
    public async Task Con_los_dos_valores_libres_el_tramite_se_crea()
    {
        // La contraparte: la validación nueva no puede haber bloqueado el alta legítima.
        var (cliente, token, _) = await EditorNuevoAsync();

        var r = await cliente.PostAsync("/Siger/Editor",
            Alta(token, idSiger: 9002, codigo: "912-001"));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);
        r.Headers.Location!.OriginalString.Should().Contain("/Siger/Detalle");

        (await TramitesEnBaseAsync()).Should().Be(2);
    }
}
