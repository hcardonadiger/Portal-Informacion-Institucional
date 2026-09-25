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
/// valor no deja una fila vacía, el QR del afiche es el de resolución de impresión, y lo que
/// se pidió quitar —la institución convocada y el enlace al pie— sigue afuera.</para>
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
        completa.Institucion   = "Instituto Nacional de Previsión del Magisterio";
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

    /// <summary>
    /// Solo el marcado del afiche. Hace falta porque la página también habla de la reunión fuera
    /// del afiche —la cabecera muestra la institución, la tarjeta muestra el enlace—, así que una
    /// aserción sobre todo el HTML no distingue "lo quitamos del afiche" de "no está en la página".
    /// Se corta en la barra de acciones, que es lo primero que viene después del afiche.
    /// </summary>
    private static string SoloElAfiche(string html)
    {
        var inicio = html.IndexOf("id=\"afiche\"", StringComparison.Ordinal);
        var fin    = html.IndexOf("afq-acciones", inicio, StringComparison.Ordinal);

        inicio.Should().BeGreaterThan(0, "el afiche debe estar en la página");
        fin.Should().BeGreaterThan(inicio, "la barra de acciones va después del afiche");

        return html[inicio..fin];
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
    public async Task El_titular_se_pone_en_mayuscula_por_estilo_y_no_en_el_dato()
    {
        var html = await AficheAsync(_completaId);
        var hoja = await _portal.ClienteComo("JefeArea").GetStringAsync("/css/afiche-qr.css");

        html.Should().NotContain("ENLACE RESPONSABLE DE ENCUESTA",
            "el dato guardado no se altera: la mayúscula la pone el CSS");

        var titulo = hoja[hoja.IndexOf(".afiche-titulo {", StringComparison.Ordinal)..];
        titulo[..titulo.IndexOf('}')].Should().Contain("text-transform: uppercase",
            "el afiche muestra el nombre de la reunión siempre en mayúscula");
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
    public async Task Un_campo_sin_valor_no_deja_linea_vacia()
    {
        var afiche = SoloElAfiche(await AficheAsync(_minimaId));

        afiche.Should().Contain("5 de octubre de 2026", "la fecha sí está y debe salir");
        afiche.Should().NotContain("afiche-cuando",
            "sin hora ni modalidad, esa línea de la ficha no se dibuja");
        afiche.Should().NotContain("afiche-lugar",
            "sin lugar, esa línea de la ficha no se dibuja");
    }

    [Fact]
    public async Task La_fecha_va_pegada_al_titulo_y_no_en_una_rejilla_al_pie()
    {
        var afiche = SoloElAfiche(await AficheAsync(_completaId));

        // Lo que se prueba es el ORDEN de lectura, que es el cambio: antes fecha, hora y lugar
        // iban en una rejilla de fichas después del QR, o sea al final. Ahora la ficha va entre
        // el título y la banda de escaneo, que es donde se busca en un afiche de evento.
        var titulo = afiche.IndexOf("afiche-titulo", StringComparison.Ordinal);
        var ficha  = afiche.IndexOf("afiche-ficha", StringComparison.Ordinal);
        var scan   = afiche.IndexOf("afiche-scan", StringComparison.Ordinal);

        titulo.Should().BeGreaterThan(0);
        ficha.Should().BeGreaterThan(titulo, "la ficha va después del título");
        scan.Should().BeGreaterThan(ficha, "la banda de escaneo va después de la ficha");

        afiche.Should().NotContain("afiche-campos", "la rejilla de fichas del pie se eliminó");
    }

    [Fact]
    public async Task El_tipo_de_reunion_encabeza_el_afiche_y_sin_tipo_dice_convocatoria()
    {
        var conTipo = SoloElAfiche(await AficheAsync(_completaId));
        var sinTipo = SoloElAfiche(await AficheAsync(_minimaId));

        conTipo.Should().Contain("afiche-kicker");
        conTipo.Should().Contain("Capacitación",
            "el tipo de reunión pasó a antetítulo: dice qué convocatoria es antes del nombre");

        sinTipo.Should().Contain("Convocatoria",
            "sin tipo, el antetítulo cae en lo que el afiche es en cualquier caso");
    }

    [Fact]
    public async Task El_afiche_no_lleva_la_institucion_convocada_ni_el_enlace_al_pie()
    {
        var afiche = SoloElAfiche(await AficheAsync(_completaId));

        afiche.Should().NotContain("Institución convocada");
        afiche.Should().NotContain("Instituciones convocadas");
        afiche.Should().NotContain("Instituto Nacional de Previsión del Magisterio",
            "la institución convocada se quitó del afiche aunque la reunión la tenga");

        afiche.Should().NotContain("afiche-pie", "el enlace al pie del afiche se quitó");
        afiche.Should().NotContain("/Asistencia/Registro",
            "el afiche ya no imprime el enlace; sigue disponible en la tarjeta de la página");
    }

    [Fact]
    public async Task El_afiche_llama_a_inscribirse_y_explica_los_pasos()
    {
        var html = await AficheAsync(_completaId);

        html.Should().Contain("Escanee el código QR para inscribirse",
            "la llamada dice para qué sirve escanear, no solo que se escanee");

        html.Should().Contain("class=\"afiche-pasos\"");
        html.Should().Contain("Abra la cámara de su teléfono");
        html.Should().Contain("Apunte al código y toque el aviso");
        html.Should().Contain("Complete el formulario");
        html.Should().NotContain("Saque su teléfono",
            "la instrucción se redactó en un registro más formal para una pieza institucional");
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
    public async Task Se_puede_compartir_descargar_e_imprimir_el_afiche()
    {
        var html = await AficheAsync(_completaId);

        html.Should().Contain("compartirEnlace(event)", "compartir es la acción principal del modal");
        html.Should().Contain("descargarAfiche(event)", "descargar da el afiche completo, no solo el QR");
        html.Should().Contain("Descargar afiche");
        html.Should().NotContain("Descargar QR", "el botón ya no baja el código suelto");
        html.Should().Contain("imprimirAfiche(event)");
        html.Should().Contain("orientarAfiche('apaisado')", "el afiche se imprime vertical y se proyecta horizontal");
        html.Should().Contain("js/afiche-qr.js");
        html.Should().Contain("css/afiche-qr.css", "la hoja del afiche es la misma que usan impresión y descarga");
    }

    [Fact]
    public async Task La_hoja_del_afiche_usa_la_letra_del_sistema_y_no_una_fuente_web()
    {
        var hoja = await _portal.ClienteComo("JefeArea").GetStringAsync("/css/afiche-qr.css");

        var familia = hoja[hoja.IndexOf("font-family:", StringComparison.Ordinal)..];
        familia = familia[..familia.IndexOf(';')];

        familia.Should().Contain("'Segoe UI'", "el afiche va en la letra institucional del sistema");
        familia.Should().NotContain("Poppins",
            "una fuente web no carga dentro del SVG de la descarga ni sin conexión: el afiche "
          + "saldría con otra letra de la que se ve en pantalla");

        hoja.Should().NotContain("@import", "el afiche no debe depender de descargar una fuente");
        hoja.Should().NotContain("fonts.googleapis.com");
    }
}
