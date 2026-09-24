using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Afiche del QR de asistencia (<c>Reuniones/_AficheQr.cshtml</c>).
///
/// <para>El afiche reemplazó a un modal que solo mostraba el QR con el nombre de la reunión en
/// letra chica. Lo que se prueba acá es el contrato que hace que sirva como cartel: el titular
/// es el nombre de la reunión, los datos de la reunión se imprimen como campos, un campo sin
/// valor no deja una fila vacía, y el QR del afiche es el de resolución de impresión y no el
/// mismo de la tarjeta.</para>
///
/// <para>Se prueba por HTML renderizado y no por el view model porque el armado vive en un
/// método privado del PageModel: lo que importa es lo que termina en la página.</para>
/// </summary>
public sealed class AficheQrTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _completaId;
    private int _minimaId;
    private int _cerradaId;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Reuniones.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var completa = Reunion.Crear("Enlace responsable de encuesta - Grupo 2");
        completa.InstitucionId = "DIGER";
        completa.Fecha     = new DateOnly(2026, 9, 24);
        completa.Hora      = "09:00 a 12:00";
        completa.Modalidad = "Presencial";
        completa.Lugar     = "Salón de capacitaciones, 3.er piso";
        completa.Tipo      = "Capacitación";

        // Sin hora, sin modalidad, sin lugar y sin tipo: el caso que antes dejaba filas con guiones.
        var minima = Reunion.Crear("Mesa técnica de racionalización");
        minima.InstitucionId = "DIGER";
        minima.Fecha = new DateOnly(2026, 10, 5);

        var cerrada = Reunion.Crear("Reunión con registro cerrado");
        cerrada.InstitucionId  = "DIGER";
        cerrada.RegistroAbierto = false;

        db.Reuniones.AddRange(completa, minima, cerrada);
        await db.SaveChangesAsync();

        _completaId = completa.Id;
        _minimaId   = minima.Id;
        _cerradaId  = cerrada.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// HTML de la página, con las entidades resueltas. El codificador de Razor escapa todo lo que
    /// no es ASCII (<c>Capacitación</c> sale como <c>Capacitaci&amp;#xF3;n</c>), así que sin
    /// decodificar habría que escribir las aserciones en entidades y no se entendería ninguna.
    /// </summary>
    private async Task<string> AficheAsync(int reunionId)
    {
        var html = await _portal.ClienteComo("JefeArea")
            .GetStringAsync($"/Reuniones/Asistencia/{reunionId}");

        return System.Net.WebUtility.HtmlDecode(html);
    }

    /// <summary>Contenido base64 del primer data-URI que aparece desde <paramref name="desde"/>.</summary>
    private static string Base64Desde(string html, int desde)
    {
        var inicio = html.IndexOf("base64,", desde, StringComparison.Ordinal) + "base64,".Length;
        var fin    = html.IndexOf('"', inicio);
        return html[inicio..fin];
    }

    [Fact]
    public async Task El_titular_del_afiche_es_el_nombre_de_la_reunion()
    {
        var html = await AficheAsync(_completaId);

        html.Should().Contain("class=\"afiche-titulo\"",
            "el afiche titula con el nombre de la reunión, que antes iba en letra chica");
        html.Should().Contain("Enlace responsable de encuesta - Grupo 2");
    }

    [Fact]
    public async Task Los_datos_de_la_reunion_se_imprimen_como_campos()
    {
        var html = await AficheAsync(_completaId);

        html.Should().Contain("24 de septiembre de 2026", "la fecha del afiche va en palabras, no en dd-MM-yyyy");
        html.Should().Contain("09:00 a 12:00");
        html.Should().Contain("Presencial");
        html.Should().Contain("Salón de capacitaciones, 3.er piso");
        html.Should().Contain("Capacitación");
    }

    [Fact]
    public async Task Un_campo_sin_valor_no_deja_fila_vacia()
    {
        var html = await AficheAsync(_minimaId);

        html.Should().Contain("5 de octubre de 2026", "la fecha sí está y debe salir");
        html.Should().NotContain("<dt>Hora</dt>");
        html.Should().NotContain("<dt>Modalidad</dt>");
        html.Should().NotContain("<dt>Lugar</dt>");
        html.Should().NotContain("<dt>Tipo de reunión</dt>");
    }

    [Fact]
    public async Task El_afiche_lleva_su_propio_qr_de_impresion()
    {
        var html = await AficheAsync(_completaId);

        var enElAfiche = html.IndexOf("id=\"aficheQrImg\"", StringComparison.Ordinal);
        enElAfiche.Should().BeGreaterThan(0);

        // La tarjeta de la página dibuja su QR antes que el modal, así que el primer data-URI del
        // documento es el chico y el que sigue al id del afiche es el de impresión.
        var qrTarjeta = Base64Desde(html, 0);
        var qrAfiche  = Base64Desde(html, enElAfiche);

        qrAfiche.Should().NotBe(qrTarjeta, "el afiche no reusa el QR de 110 px de la tarjeta");
        qrAfiche.Length.Should().BeGreaterThan(qrTarjeta.Length,
            "el afiche usa QrImagen.DataUriAfiche: módulos grandes y corrección Q, porque se "
          + "escanea impreso y a distancia. Si alguien unificara los dos hacia abajo, el afiche "
          + "saldría interpolado y esto lo avisa.");
    }

    [Fact]
    public async Task El_afiche_avisa_cuando_el_registro_esta_cerrado()
    {
        var abierto = await AficheAsync(_completaId);
        var cerrado = await AficheAsync(_cerradaId);

        cerrado.Should().Contain("El registro está cerrado",
            "imprimir o compartir un afiche que nadie puede usar es el error que hay que evitar");
        abierto.Should().NotContain("El registro está cerrado");
    }

    [Fact]
    public async Task El_afiche_se_puede_compartir_e_imprimir()
    {
        var html = await AficheAsync(_completaId);

        html.Should().Contain("compartirEnlace(event)", "compartir es la acción principal del modal");
        html.Should().Contain("imprimirAfiche(event)");
        html.Should().Contain("orientarAfiche('apaisado')", "el afiche se imprime vertical y se proyecta horizontal");
        html.Should().Contain("css/afiche-qr.css", "la hoja del afiche es la misma que usa la ventana de impresión");
    }
}
