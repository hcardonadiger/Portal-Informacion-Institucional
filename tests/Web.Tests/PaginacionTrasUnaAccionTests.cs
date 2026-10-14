using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El paginador después de publicar (14-10-2026, reportado desde Test). Publicar redirige con
/// «Pg=3»; el paginador solo quitaba «pg» en minúscula, así que sus enlaces salían como
/// «?Pg=3&amp;pg=1» y la página no cambiaba: se ganaba el primero.
/// </summary>
public sealed class PaginacionTrasUnaAccionTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Administrador", "HondurasSimple.Publicacion.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // 60 candidatas: tres páginas de 25.
        for (var i = 1; i <= 60; i++)
            db.TramitesSiger.Add(new TramiteSiger
            {
                Codigo = $"940-{i:000}", Nombre = $"Candidata {i:000}", EstadoSiger = "Aprobado",
                Institucion = "Instituto de Prueba"
            });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Desde_la_direccion_de_vuelta_de_publicar_se_puede_saltar_hacia_atras()
    {
        var cliente = _portal.ClienteComo("Administrador");
        var html = await LeerAsync(cliente, "/HondurasSimple/Publicacion?Tab=candidatas&Pg=3");
        html.Should().Contain("51–60 de 60");

        var aLaUno = Regex.Match(html, """<a class="pager-b[^"]*" href="([^"]+)">1</a>""").Groups[1].Value;
        aLaUno.Should().NotBeEmpty();
        aLaUno.Should().NotContainEquivalentOf("Pg=3", "el número de página viejo no puede quedarse en el enlace");

        (await LeerAsync(cliente, aLaUno)).Should().Contain("1–25 de 60");
    }

    private static async Task<string> LeerAsync(HttpClient cliente, string ruta)
    {
        var respuesta = await cliente.GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
    }
}
