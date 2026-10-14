using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La pantalla de completitud de fichas: paginada de veinte en veinte, con búsqueda, filtro de
/// tramo y orden en la dirección, y un tablero arriba.
///
/// <para>Lo que más importa probar es que <b>el tablero cuenta el universo y no la página</b>.
/// La lista se recorta a veinte filas y el tablero se calcula de la misma lista; el día que
/// alguien mueva el cálculo después del Take, los números de arriba empiezan a describir solo
/// la página en pantalla y nadie lo nota, porque siguen pareciendo razonables.</para>
/// </summary>
public sealed class CompletitudFichasTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    // El universo sembrado: 23 de INPREMA (3 completas y 20 vacías) y 2 de IHTT (una al 50 % y
    // otra al 75 %). 25 fichas = dos páginas, la segunda con cinco. El promedio es
    // (3×100 + 20×0 + 50 + 75) / 25 = 17 %.
    private const int Total = 25;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Administrador", "HondurasSimple.Ver", "HondurasSimple.Editar", "Siger.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var categoria = new CategoriaTramite { Nombre = "Pruebas", Orden = 1 };
        db.CategoriasTramite.Add(categoria);
        await db.SaveChangesAsync();

        for (var i = 1; i <= 23; i++)
        {
            var completa = i <= 3;
            db.TramitesSiger.Add(new TramiteSiger
            {
                IdSiger = 7000 + i, Codigo = $"700-{i:000}", Nombre = $"Afiliacion de docentes {i:00}",
                Institucion = "Institucion sembrada", Sigla = "INPREMA", InstitucionId = "INPREMA",
                CategoriaId = completa ? categoria.Id : null,
                Modalidad = completa ? "Virtual" : null,
                TiempoTexto = completa ? "5 dias" : null,
                CostoEsGratuito = completa ? true : null,
                Publicado = i == 1
            });
        }

        db.TramitesSiger.AddRange(
            // Dos de cuatro: 50 %.
            new TramiteSiger
            {
                IdSiger = 7101, Codigo = "530-001", Nombre = "Permiso de transporte",
                Institucion = "Institucion sembrada", Sigla = "IHTT", InstitucionId = "IHTT",
                CategoriaId = categoria.Id, Modalidad = "Presencial"
            },
            // Tres de cuatro: 75 %.
            new TramiteSiger
            {
                IdSiger = 7102, Codigo = "530-002", Nombre = "Renovacion de placa",
                Institucion = "Institucion sembrada", Sigla = "IHTT", InstitucionId = "IHTT",
                CategoriaId = categoria.Id, Modalidad = "Presencial", TiempoTexto = "1 dia"
            },
            // Sin institución: fuera de la pantalla y de sus cuentas.
            new TramiteSiger
            {
                IdSiger = 7999, Codigo = "999-001", Nombre = "Ficha sin institucion vinculada",
                Institucion = "Nadie"
            });

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    // ── Ayudantes ─────────────────────────────────────────────────────────────

    private async Task<string> HtmlDe(string ruta)
    {
        var respuesta = await _portal.ClienteComo("Administrador").GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, "la página {0} tiene que servirse", ruta);
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
    }

    /// <summary>Los códigos de las filas de la tabla, en el orden en que salen.</summary>
    private static List<string> Filas(string html) =>
        Regex.Matches(html, "data-ficha=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();

    private static string Kpi(string html, string clave) =>
        Regex.Match(html, $"data-kpi=\"{clave}\">([^<]+)<").Groups[1].Value.Replace(' ', ' ').Trim();

    // ── Paginación ────────────────────────────────────────────────────────────

    [Fact]
    public async Task La_primera_pagina_trae_veinte_y_la_segunda_el_resto()
    {
        var primera = await HtmlDe("/HondurasSimple/Completitud");
        Filas(primera).Should().HaveCount(CompletitudModelTamano);
        primera.Should().Contain($"1–20 de {Total}");

        var segunda = await HtmlDe("/HondurasSimple/Completitud?pg=2");
        Filas(segunda).Should().HaveCount(Total - CompletitudModelTamano);
        Filas(primera).Should().NotIntersectWith(Filas(segunda), "una ficha no puede salir en dos páginas");
    }

    private const int CompletitudModelTamano = Pages.HondurasSimple.CompletitudModel.TamanoPagina;

    [Fact]
    public async Task El_paginador_conserva_los_filtros()
    {
        var html = await HtmlDe("/HondurasSimple/Completitud?Buscar=Afiliacion&Orden=codigo");

        // 23 de INPREMA: dos páginas, y el enlace a la segunda lleva la búsqueda y el orden.
        Regex.IsMatch(html, @"href=""[^""]*Buscar=Afiliacion[^""]*pg=2""").Should().BeTrue(
            "sin los filtros, la página 2 sería la del universo entero y no la de la búsqueda");
    }

    [Fact]
    public async Task La_ficha_sin_institucion_no_entra()
    {
        var html = await HtmlDe("/HondurasSimple/Completitud?Orden=codigo&pg=2");
        html.Should().NotContain("Ficha sin institucion vinculada");
        Kpi(html, "total").Should().Be(Total.ToString());
    }

    // ── Búsqueda y filtros ────────────────────────────────────────────────────

    [Fact]
    public async Task Buscar_por_codigo_acota_la_lista()
    {
        var html = await HtmlDe("/HondurasSimple/Completitud?Buscar=700-01");

        // 700-010 a 700-019.
        Filas(html).Should().HaveCount(10).And.OnlyContain(c => c.StartsWith("700-01"));
    }

    [Fact]
    public async Task Buscar_por_nombre_de_la_institucion_usa_el_catalogo()
    {
        // «Terrestre» solo está en el nombre de catálogo de IHTT; el texto de la ficha dice otra cosa.
        var html = await HtmlDe("/HondurasSimple/Completitud?Buscar=Terrestre");

        Filas(html).Should().BeEquivalentTo(["530-001", "530-002"]);
    }

    [Fact]
    public async Task Mostrar_un_tramo_lista_solo_ese_tramo()
    {
        var completas = await HtmlDe("/HondurasSimple/Completitud?Mostrar=completas");
        Filas(completas).Should().BeEquivalentTo(["700-001", "700-002", "700-003"]);

        var incompletas = await HtmlDe("/HondurasSimple/Completitud?Mostrar=incompletas");
        incompletas.Should().Contain("1–20 de 22");

        var alta = await HtmlDe("/HondurasSimple/Completitud?Mostrar=alta");
        Filas(alta).Should().BeEquivalentTo(["530-002"]);
    }

    // ── Orden ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Por_defecto_salen_primero_las_mas_atrasadas()
    {
        var filas = Filas(await HtmlDe("/HondurasSimple/Completitud"));
        filas.Should().NotContain(["700-001", "700-002", "700-003", "530-001", "530-002"],
            "las veinte vacías llenan la primera página");
    }

    [Fact]
    public async Task Se_puede_ordenar_por_mayor_completitud()
    {
        var filas = Filas(await HtmlDe("/HondurasSimple/Completitud?Orden=pct-desc"));

        filas.Take(5).Should().Equal("700-001", "700-002", "700-003", "530-002", "530-001");
    }

    [Fact]
    public async Task Se_puede_ordenar_por_codigo()
    {
        var filas = Filas(await HtmlDe("/HondurasSimple/Completitud?Orden=codigo"));

        filas.Should().HaveCount(20);
        filas.Should().BeInAscendingOrder(StringComparer.Ordinal);
        filas.First().Should().Be("530-001");
    }

    [Fact]
    public async Task Un_orden_desconocido_no_rompe_la_pagina()
    {
        Filas(await HtmlDe("/HondurasSimple/Completitud?Orden=inventado&Mostrar=nada")).Should().HaveCount(20);
    }

    // ── Tablero ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task El_tablero_cuenta_todas_las_fichas_y_no_solo_la_pagina()
    {
        // La primera página tiene solo fichas vacías; si el tablero contara la página, diría
        // cero completas y promedio 0 %.
        var html = await HtmlDe("/HondurasSimple/Completitud");

        Kpi(html, "total").Should().Be("25");
        Kpi(html, "completas").Should().Be("3");
        Kpi(html, "incompletas").Should().Be("22");
        Kpi(html, "rezagadas").Should().Be("20");
        Kpi(html, "promedio").Should().Be("17 %");
        Kpi(html, "publicadas").Should().Be("1");
    }

    [Fact]
    public async Task El_tablero_no_cambia_con_la_pagina_ni_con_lo_que_se_muestra()
    {
        foreach (var ruta in new[] { "/HondurasSimple/Completitud?pg=2", "/HondurasSimple/Completitud?Mostrar=completas" })
        {
            var html = await HtmlDe(ruta);
            Kpi(html, "total").Should().Be("25", "«Mostrar» y la página eligen filas, no cambian el universo ({0})", ruta);
            Kpi(html, "completas").Should().Be("3");
        }
    }

    [Fact]
    public async Task El_tablero_respeta_la_institucion_elegida()
    {
        var html = await HtmlDe("/HondurasSimple/Completitud?InstitucionId=IHTT");

        Kpi(html, "total").Should().Be("2");
        Kpi(html, "completas").Should().Be("0");
        Kpi(html, "promedio").Should().Be("63 %");   // (50 + 75) / 2, redondeado
    }

    [Fact]
    public async Task El_tablero_dice_que_campo_falta_mas()
    {
        var html = await HtmlDe("/HondurasSimple/Completitud");

        // Costo falta en 22 (las 20 vacías y las dos de IHTT); categoría solo en las 20 vacías.
        Regex.Match(html, "data-falta=\"costo\">(\\d+)<").Groups[1].Value.Should().Be("22");
        Regex.Match(html, "data-falta=\"categoría\">(\\d+)<").Groups[1].Value.Should().Be("20");
    }

    // ── Vista por institución ─────────────────────────────────────────────────

    [Fact]
    public async Task La_vista_por_institucion_agrupa_las_mismas_fichas()
    {
        var html = await HtmlDe("/HondurasSimple/Completitud?Vista=instituciones");

        var instituciones = Regex.Matches(html, "data-inst=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        // Menor completitud primero: INPREMA (13 %) antes que IHTT (63 %).
        instituciones.Should().Equal("INPREMA", "IHTT");
        Kpi(html, "total").Should().Be("25");
    }
}
