using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Desglose de asistencia por institución del acta (<c>Reuniones/Acta.cshtml</c>).
///
/// <para>El desglose agrupaba a los asistentes con el comparador por omisión —sensible a
/// mayúsculas— y volcaba esos grupos en un diccionario <c>OrdinalIgnoreCase</c>. Dos personas
/// que escribían su institución con distinta capitalización ("DIGER" y "Diger") producían dos
/// grupos que colapsaban en la misma clave del diccionario, y el volcado reventaba con
/// <c>ArgumentException: An item with the same key has already been added</c>.</para>
///
/// <para>La reunión entera quedaba inaccesible: la página del acta respondía 500 y, como el botón
/// de descarga vive en ella, tampoco se podía bajar el PDF. En producción el usuario solo veía
/// «Ocurrió un error inesperado». El texto de la institución es libre en el auto-registro por QR,
/// así que cuantos más participantes se registran, más probable es la colisión.</para>
/// </summary>
public sealed class ActaDesgloseAsistenciaTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _reunionId;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Reuniones.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var r = Reunion.Crear("Capacitación con muchos participantes");
        r.InstitucionId = "DIGER";
        r.InstitucionDuenaId = "DIGER";
        r.AreaId    = PortalFactory.AreaDeCasa;
        r.UnidadId  = PortalFactory.UnidadDeCasa;
        r.Institucion = "DIGER";
        r.Fecha = new DateOnly(2026, 9, 30);
        r.Tipo = "Capacitación";

        // Tal como llegan del auto-registro por QR: cada quien escribe su institución a mano.
        r.RegistrarAsistente("Ana Morales",  "Analista", "DIGER", null, "ana@diger.gob.hn",   null);
        r.RegistrarAsistente("Luis Paz",     "Jefe",     "Diger", null, "luis@diger.gob.hn",  null);
        r.RegistrarAsistente("Carla Rivera", "Técnico",  "diger", null, "carla@diger.gob.hn", null);

        db.Reuniones.Add(r);
        await db.SaveChangesAsync();
        _reunionId = r.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ElActaAbreAunqueLaInstitucionVengaConDistintasMayusculas()
    {
        var respuesta = await _portal.ClienteComo("JefeArea", area: PortalFactory.AreaDeCasa, unidad: PortalFactory.UnidadDeCasa).GetAsync($"/Reuniones/Acta/{_reunionId}");

        respuesta.StatusCode.Should().Be(System.Net.HttpStatusCode.OK,
            "una variante de capitalización en el texto libre no puede tumbar la página entera");
    }

    [Fact]
    public async Task LasVariantesDeMayusculasCuentanComoUnaSolaInstitucion()
    {
        var html = System.Net.WebUtility.HtmlDecode(
            await _portal.ClienteComo("JefeArea", area: PortalFactory.AreaDeCasa, unidad: PortalFactory.UnidadDeCasa).GetStringAsync($"/Reuniones/Acta/{_reunionId}"));

        var desglose = System.Text.RegularExpressions.Regex.Match(
            html, "(?s)Desglose de asistencia por institución.*?</table>").Value;

        desglose.Should().NotBeEmpty("la reunión tiene asistentes, así que el desglose se pinta");
        System.Text.RegularExpressions.Regex.Matches(desglose, "(?s)<tr><td>.*?</td>").Count
            .Should().Be(1, "las tres grafías de la misma institución son una sola fila");
        desglose.Should().Contain("<td>3</td>", "y la fila suma los tres asistentes");
    }

    [Fact]
    public async Task ElPdfDelActaSeDescargaConEsosMismosDatos()
    {
        var respuesta = await _portal.ClienteComo("JefeArea", area: PortalFactory.AreaDeCasa, unidad: PortalFactory.UnidadDeCasa)
            .GetAsync($"/Reuniones/Acta/{_reunionId}?handler=Pdf");

        respuesta.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        respuesta.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await respuesta.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(1000);
    }
}
