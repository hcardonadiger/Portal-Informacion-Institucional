using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La alerta que le dice al técnico qué le falta a una ficha para publicarse, probada sobre el
/// HTML que de verdad sale del servidor y no sobre el modelo de página.
///
/// Se prueba así a propósito: el dato ya se calculaba antes en el editor —<c>FichaCompleta</c>
/// existía y estaba bien— pero la vista nunca lo pintaba, así que el técnico no se enteraba de
/// nada. Una prueba sobre el PageModel habría pasado en verde con ese defecto adentro.
/// </summary>
public sealed class AlertaFichaIncompletaTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    private int _idIncompleta;
    private int _idCompleta;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Administrador", "Siger.Ver", "Siger.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Las claves foráneas son reales: sin la institución y la categoría, SQLite
        // rechaza las fichas de prueba.
        db.Instituciones.Add(Institucion.Crear("IDP", "Instituto de Prueba"));
        var categoria = new CategoriaTramite { Nombre = "Pruebas", Orden = 1 };
        db.CategoriasTramite.Add(categoria);
        await db.SaveChangesAsync();

        // A esta le falta todo menos la modalidad: tres huecos, para comprobar que la alerta
        // los enumera y no se queda en el primero.
        var incompleta = new TramiteSiger
        {
            IdSiger = 9001, Codigo = "900-001", Institucion = "Instituto de Prueba", Sigla = "IDP",
            Nombre = "Constancia de prueba con ficha incompleta",
            EstadoSiger = "Aprobado", InstitucionId = "IDP",
            Modalidad = "Presencial"
            // CategoriaId, TiempoTexto y CostoEsGratuito quedan en null a propósito.
        };

        var completa = new TramiteSiger
        {
            IdSiger = 9002, Codigo = "900-002", Institucion = "Instituto de Prueba", Sigla = "IDP",
            Nombre = "Constancia de prueba con ficha completa",
            EstadoSiger = "Aprobado", InstitucionId = "IDP",
            Modalidad = "Presencial", CategoriaId = categoria.Id, TiempoTexto = "5 dias habiles",
            CostoEsGratuito = true
        };

        db.TramitesSiger.AddRange(incompleta, completa);
        await db.SaveChangesAsync();

        _idIncompleta = incompleta.Id;
        _idCompleta = completa.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Devuelve el HTML ya decodificado. Razor escapa los acentos como entidades
    /// (<c>categor&amp;#xED;a</c>) y afirmar sobre esa forma haría que las pruebas dependieran
    /// de la configuración del codificador y no de lo que el técnico lee en pantalla.</summary>
    private async Task<string> LeerAsync(string ruta)
    {
        var respuesta = await _portal.ClienteComo("Administrador").GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, "la ruta {0} debe responder", ruta);
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task El_inventario_avisa_cuantas_fichas_estan_incompletas()
    {
        var html = await LeerAsync("/Siger/Index");

        html.Should().Contain("fichas están incompletas");
        html.Should().Contain("Ver solo las incompletas");
    }

    [Fact]
    public async Task El_inventario_marca_solo_la_ficha_incompleta()
    {
        var html = await LeerAsync("/Siger/Index");

        // Una sola marca entre las dos fichas: la completa no debe llevarla.
        Regex.Matches(html, "Ficha incompleta").Count.Should().Be(1);

        // El detalle de qué falta viaja en el title de la marca, en el orden del editor.
        html.Should().Contain("Falta capturar: categoría, tiempo, costo.");
    }

    [Fact]
    public async Task El_filtro_deja_solo_las_incompletas()
    {
        var html = await LeerAsync("/Siger/Index?Completa=No");

        html.Should().Contain("900-001");
        html.Should().NotContain("900-002");
    }

    [Fact]
    public async Task El_filtro_inverso_deja_solo_las_completas()
    {
        var html = await LeerAsync("/Siger/Index?Completa=Si");

        html.Should().Contain("900-002");
        html.Should().NotContain("900-001");
    }

    [Fact]
    public async Task El_detalle_enumera_lo_que_falta_y_ofrece_completarlo()
    {
        var html = await LeerAsync($"/Siger/Detalle/{_idIncompleta}");

        html.Should().Contain("Esta ficha está incompleta y por eso no se publica.");
        html.Should().Contain("<li><strong>categoría</strong></li>");
        html.Should().Contain("<li><strong>tiempo</strong></li>");
        html.Should().Contain("<li><strong>costo</strong></li>");
        html.Should().NotContain("<li><strong>modalidad</strong></li>");
        html.Should().Contain("Completar la ficha");
    }

    [Fact]
    public async Task El_detalle_de_una_ficha_completa_no_muestra_alerta()
    {
        var html = await LeerAsync($"/Siger/Detalle/{_idCompleta}");

        html.Should().NotContain("Esta ficha está incompleta");
        html.Should().NotContain("Falta capturar:");
    }

    [Fact]
    public async Task El_editor_dice_lo_mismo_que_el_inventario()
    {
        var html = await LeerAsync($"/Siger/Editor?id={_idIncompleta}");

        html.Should().Contain("Falta capturar: categoría, tiempo, costo.");
        html.Should().Contain("el trámite no se publica");
    }

    [Fact]
    public async Task El_editor_confirma_cuando_la_ficha_ya_esta_completa()
    {
        var html = await LeerAsync($"/Siger/Editor?id={_idCompleta}");

        html.Should().Contain("La ficha pública está completa.");
    }
}
