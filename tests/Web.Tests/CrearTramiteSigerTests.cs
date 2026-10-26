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
/// Alta de trámites SIGER desde <c>/HondurasSimple/Editor</c>.
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

        // Como todo el inventario importado de SIGER: institución en texto, sin InstitucionId.
        db.TramitesSiger.Add(new TramiteSiger
        {
            IdSiger     = IdSigerOcupado,
            Codigo      = CodigoOcupado,
            Nombre      = "Trámite que ya existe",
            Institucion = "Dirección de Gestión por Resultados",
            Sigla       = "DGR-SIGER"
        });

        // Llave derivada del nombre completo, más larga que la columna Sigla (30).
        db.Instituciones.Add(Institucion.Crear(InstitucionLlaveLarga,
            "Administración Nacional de Servicio Civil", nombreCorto: "ANSEC"));
        await db.SaveChangesAsync();
    }

    private const string InstitucionLlaveLarga = "ADMINISTRACIONNACIONALDESERVICIOCIVILANSEC";

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
        var html    = await cliente.GetStringAsync("/HondurasSimple/Editor");
        var token   = Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        token.Should().NotBeEmpty();
        return (cliente, token, html);
    }

    /// <summary>El mínimo que el editor exige: los cuatro campos obligatorios más el token.</summary>
    private static FormUrlEncodedContent Alta(string token, int idSiger, string codigo,
                                              string nombre = "Trámite nuevo de prueba",
                                              string institucionId = "DIGER")
    {
        return new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Form.Id", "0"),
            new KeyValuePair<string, string>("Form.IdSiger", idSiger.ToString()),
            new KeyValuePair<string, string>("Form.Codigo", codigo),
            new KeyValuePair<string, string>("Form.Nombre", nombre),
            new KeyValuePair<string, string>("Form.InstitucionId", institucionId)
        ]);
    }

    private async Task<TramiteSiger> TramiteAsync(string codigo)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TramitesSiger.AsNoTracking().SingleAsync(t => t.Codigo == codigo);
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

        var r = await cliente.PostAsync("/HondurasSimple/Editor",
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

        var r = await cliente.PostAsync("/HondurasSimple/Editor",
            Alta(token, idSiger: IdSigerOcupado, codigo: "999-999"));

        r.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await r.Content.ReadAsStringAsync();
        html.Should().Contain(IdSigerOcupado.ToString());

        (await TramitesEnBaseAsync()).Should().Be(1);
    }

    [Fact]
    public async Task El_editor_vacio_sugiere_el_siguiente_IdSiger_libre()
    {
        // Sin esto hay que adivinar un hueco entre los ocupados.
        //
        // Se SUGIERE, no se precarga. En dev el campo venia relleno; en esta rama el vacio
        // significa algo —una ficha promovida desde un expediente no existe en SIGER y no tiene
        // identificador, y el indice unico esta filtrado por IS NOT NULL justamente para eso, lo
        // que fija FichaSinIdSigerTests—. Rellenarlo le quitaria ese significado sin avisar.
        var (_, _, html) = await EditorNuevoAsync();

        html.Should().Contain($"El siguiente libre es {IdSigerOcupado + 1}.");
    }

    [Fact]
    public async Task Con_los_dos_valores_libres_el_tramite_se_crea()
    {
        // La contraparte: la validación nueva no puede haber bloqueado el alta legítima.
        var (cliente, token, _) = await EditorNuevoAsync();

        var r = await cliente.PostAsync("/HondurasSimple/Editor",
            Alta(token, idSiger: 9002, codigo: "912-001"));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);
        r.Headers.Location!.OriginalString.Should().Contain("/Siger/Detalle");

        (await TramitesEnBaseAsync()).Should().Be(2);
    }

    [Fact]
    public async Task La_institucion_se_elige_del_catalogo_y_de_ella_salen_nombre_y_sigla()
    {
        // Cuando era texto libre la ficha nacía sin InstitucionId y no aparecía al filtrar por
        // institución en la API pública ni en la captura en lote.
        var (cliente, token, html) = await EditorNuevoAsync();
        html.Should().Contain("<option value=\"DIGER\"");

        var r = await cliente.PostAsync("/HondurasSimple/Editor", Alta(token, 9003, "912-002"));
        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var t = await TramiteAsync("912-002");
        t.InstitucionId.Should().Be("DIGER");
        t.Institucion.Should().Be("Dirección de Gestión por Resultados");
        t.Sigla.Should().Be("DIGER");
    }

    [Fact]
    public async Task Sin_institucion_no_se_crea()
    {
        var (cliente, token, _) = await EditorNuevoAsync();

        var r = await cliente.PostAsync("/HondurasSimple/Editor", Alta(token, 9004, "912-003", institucionId: ""));

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()).Should().Contain("Elija la institución del trámite.");
        (await TramitesEnBaseAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Una_llave_que_no_cabe_en_la_sigla_usa_el_nombre_corto()
    {
        // Copiar la llave tal cual reventaba el INSERT contra la columna de 30.
        var (cliente, token, _) = await EditorNuevoAsync();

        var r = await cliente.PostAsync("/HondurasSimple/Editor",
            Alta(token, 9005, "912-004", institucionId: InstitucionLlaveLarga));
        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var t = await TramiteAsync("912-004");
        t.InstitucionId.Should().Be(InstitucionLlaveLarga);
        t.Sigla.Should().Be("ANSEC");
    }

    [Fact]
    public async Task Editar_una_ficha_sin_vincular_sin_elegir_institucion_no_toca_su_texto()
    {
        // El inventario importado no tiene InstitucionId y parte de él es de instituciones que el
        // catálogo no tiene. Guardarlo sin tocar el campo no puede ni fallar ni reescribirlo.
        var id = (await TramiteAsync(CodigoOcupado)).Id;
        var cliente = _portal.ClienteComo("Administrador");
        var html = await cliente.GetStringAsync($"/HondurasSimple/Editor?id={id}");
        WebUtility.HtmlDecode(html).Should().Contain("Sin vincular — Dirección de Gestión por Resultados (DGR-SIGER)");
        var token = Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        var r = await cliente.PostAsync("/HondurasSimple/Editor", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Form.Id", id.ToString()),
            new KeyValuePair<string, string>("Form.IdSiger", IdSigerOcupado.ToString()),
            new KeyValuePair<string, string>("Form.Codigo", CodigoOcupado),
            new KeyValuePair<string, string>("Form.Nombre", "Trámite que ya existe"),
            new KeyValuePair<string, string>("Form.InstitucionId", "")
        ]));
        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var t = await TramiteAsync(CodigoOcupado);
        t.InstitucionId.Should().BeNull();
        t.Institucion.Should().Be("Dirección de Gestión por Resultados");
        t.Sigla.Should().Be("DGR-SIGER");
    }
}
