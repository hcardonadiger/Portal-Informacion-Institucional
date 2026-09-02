using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Guardas de autorización dentro de handlers POST.
///
/// <para>Fija un fallo que apareció usando el portal y que ninguna prueba veía: varias páginas
/// comprobaban <c>if (!EsAdmin) return Forbid();</c> dentro de un <c>OnPost…</c>, pero
/// <c>EsAdmin</c> solo se asignaba en <c>OnGetAsync</c>. En un POST, ASP.NET construye un
/// PageModel nuevo y ejecuta únicamente el handler, así que la propiedad valía siempre
/// <c>false</c> y la guarda denegaba a todo el mundo —administrador incluido—.</para>
///
/// <para>Estaba en cinco páginas y trece handlers. Se quitaron las que solo repetían el
/// <c>[Permission]</c> del propio handler; la única que abría una segunda puerta —la contraparte
/// del expediente— se conservó, recalculando el valor dentro del POST.</para>
///
/// <para>Lo que se prueba no es que la acción funcione, sino que <b>no responda 403</b>: el fallo
/// era exactamente ese, y una redirección o un error de validación ya significan que la petición
/// entró al handler.</para>
/// </summary>
public sealed class GuardasDePostTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _reunionId;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Reuniones.Ver", "Reuniones.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var reunion = Reunion.Crear("Mesa técnica de interconectividad");
        reunion.InstitucionId = "DIGER";
        db.Reuniones.Add(reunion);
        await db.SaveChangesAsync();
        _reunionId = reunion.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private async Task<(HttpClient Cliente, string Token)> SesionAsync(string rol = "JefeArea")
    {
        var cliente = _portal.ClienteComo(rol);
        var html = await cliente.GetStringAsync($"/Reuniones/Acta/{_reunionId}");
        var token = Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        token.Should().NotBeEmpty();
        return (cliente, token);
    }

    private static FormUrlEncodedContent Form(string token, params (string, string)[] campos)
    {
        var datos = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token)
        };
        foreach (var (k, v) in campos) datos.Add(new KeyValuePair<string, string>(k, v));
        return new FormUrlEncodedContent(datos);
    }

    [Fact]
    public async Task Enlazar_reuniones_ya_no_deniega_a_quien_tiene_el_permiso()
    {
        var (cliente, token) = await SesionAsync();

        var r = await cliente.PostAsync(
            $"/Reuniones/Acta/{_reunionId}?handler=Enlazar",
            Form(token, ("otraReunionId", "0")));

        // otraReunionId = 0 hace que el handler responda con un mensaje, no con 403: lo que se
        // comprueba es que la petición LLEGÓ al handler.
        r.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "la guarda por EsAdmin denegaba a todos los roles, incluido el administrador");
    }

    [Fact]
    public async Task Desenlazar_tampoco()
    {
        var (cliente, token) = await SesionAsync();

        var r = await cliente.PostAsync(
            $"/Reuniones/Acta/{_reunionId}?handler=Desenlazar", Form(token));

        r.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task El_registro_de_asistencia_tenia_el_mismo_fallo_en_seis_handlers()
    {
        var (cliente, token) = await SesionAsync();

        var r = await cliente.PostAsync(
            $"/Reuniones/Asistencia/{_reunionId}?handler=Toggle",
            Form(token, ("abrir", "true")));

        r.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Y_sin_el_permiso_se_sigue_denegando()
    {
        // La contraparte del arreglo: quitar la guarda no puede haber abierto la puerta. Quien
        // manda es el [Permission] del handler, y Consultor no tiene Reuniones.Editar.
        await _portal.OtorgarAsync("Consultor", "Reuniones.Ver");
        var (cliente, token) = await SesionAsync("Consultor");

        var r = await cliente.PostAsync(
            $"/Reuniones/Acta/{_reunionId}?handler=Enlazar",
            Form(token, ("otraReunionId", "0")));

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
