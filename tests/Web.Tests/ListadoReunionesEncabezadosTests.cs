using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Encabezados ordenables del listado de reuniones (<c>Reuniones/Index.cshtml</c>).
///
/// <para>Razor trata <c>Texto@Metodo(...)</c> como una dirección de correo y lo imprime literal:
/// los encabezados salieron en pantalla como <c>TÍTULO@FLECHA(ORDENREUNIONES.TITULO)</c>, y ese
/// texto larguísimo ensanchó cada columna hasta meter scroll horizontal en la tabla.</para>
///
/// <para>Se prueba sobre el HTML renderizado y no sobre el .cshtml porque lo que importa es lo
/// que termina en la página: la forma de escribir la expresión en Razor puede cambiar.</para>
/// </summary>
public sealed class ListadoReunionesEncabezadosTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Reuniones.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var r = Reunion.Crear("Taller de modelado con CONSUCOOP");
        r.InstitucionId = "DIGER";
        r.Institucion = "CONSUCOOP";
        r.Fecha = new DateOnly(2026, 9, 1);
        r.Tipo = "Taller";
        db.Reuniones.Add(r);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> ListadoAsync(string url = "/Reuniones") =>
        System.Net.WebUtility.HtmlDecode(await _portal.ClienteComo("JefeArea").GetStringAsync(url));

    [Fact]
    public async Task LosEncabezadosNoImprimenLaExpresionRazorEnCrudo()
    {
        var html = await ListadoAsync();

        html.Should().NotContain("@Flecha", "Razor debe evaluar la flecha, no imprimirla");
        html.Should().NotContain("OrdenReuniones.Titulo",
            "el nombre del enum en pantalla significa que la expresión salió literal");
    }

    [Fact]
    public async Task ElEncabezadoDeCadaColumnaEsSuNombreCorto()
    {
        var html = await ListadoAsync();

        foreach (var encabezado in new[] { ">Título", ">Fecha", ">Institución", ">Tipo", ">Asistentes" })
            html.Should().Contain(encabezado);
    }

    [Fact]
    public async Task LaColumnaOrdenadaMuestraLaFlechaDeSuSentido()
    {
        var descendente = await ListadoAsync("/Reuniones?orden=Titulo&desc=true");
        descendente.Should().Contain("▼");

        var ascendente = await ListadoAsync("/Reuniones?orden=Titulo&desc=false");
        ascendente.Should().Contain("▲");
    }

    [Fact]
    public async Task LaColumnaDeAccionesNoLlevaEncabezadoDeTexto()
    {
        // El control para volver al orden por registro vivía acá y le ponía texto al
        // encabezado de los botones, ensanchando la columna.
        var html = await ListadoAsync();

        var thead = System.Text.RegularExpressions.Regex.Match(html, "(?s)<thead>.*?</thead>").Value;
        var encabezados = System.Text.RegularExpressions.Regex.Matches(thead, "(?s)<th[^>]*>(.*?)</th>");
        var ultimo = System.Text.RegularExpressions.Regex.Replace(
            encabezados[^1].Groups[1].Value, "<[^>]+>", "").Trim();

        ultimo.Should().BeEmpty("la columna de botones no lleva encabezado");
    }

    [Fact]
    public async Task LaTablaMuestraLaFechaDeRegistroDeCadaReunion()
    {
        var html = await ListadoAsync();

        html.Should().Contain(">Registro", "la fecha de creación es una columna propia");
        html.Should().MatchRegex(@"\d{2}-\d{2}-\d{4}", "la fecha de registro sale en formato corto");
    }

    [Fact]
    public async Task LaColumnaDeRegistroTambienOrdena()
    {
        var html = await ListadoAsync("/Reuniones?orden=Creacion&desc=false");

        html.Should().Contain("▲");
    }

    [Fact]
    public async Task ElNombreLargoDeInstitucionPuedeBajarDeRenglon()
    {
        // La píldora traía white-space: nowrap del CSS global, así que un nombre como
        // "SECRETARÍA DE DESARROLLO ECONÓMICO" estiraba la columna y con ella toda la
        // tabla, metiendo scroll horizontal en la página.
        var html = await ListadoAsync();

        html.Should().Contain("#reunion-tbl .inst-pill");
        html.Should().Contain("white-space: normal");
    }
}
