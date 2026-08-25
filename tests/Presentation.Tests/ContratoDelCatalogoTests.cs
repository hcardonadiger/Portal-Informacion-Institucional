using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Presentation.Tests;

/// <summary>
/// Lo que la documentación del catálogo <b>afirma</b>, comprobado contra lo que la API
/// <b>hace</b>.
///
/// Estas pruebas nacieron de la Fase 6, redactando la documentación. Al contrastar la prosa con
/// el código aparecieron tres afirmaciones falsas que llevaban meses publicadas en Swagger:
/// que se podía ordenar por institución y por tiempo (no se puede), que la modalidad admitía
/// «Mixto» (se llama «Hibrido»), y que un tamaño de página fuera de rango se recortaba al
/// intervalo (vuelve al valor por omisión).
///
/// Ninguna de las tres la habría atrapado una prueba de la lógica: el código siempre estuvo
/// bien. Lo que estaba mal era lo que le decíamos al integrador. Por eso estas pruebas no
/// verifican comportamiento por verificarlo —verifican <b>exactamente las frases</b> que hoy
/// están escritas en los comentarios de los controladores—. Si alguien cambia el
/// comportamiento, se entera de que hay una frase que corregir.
/// </summary>
public sealed class ContratoDelCatalogoTests : IAsyncLifetime
{
    private readonly ApiFactory _api = new();

    public async Task InitializeAsync()
    {
        await _api.PrepararAsync();

        await _api.ConLaBaseAsync(async db =>
        {
            // Cada grupo lleva su prefijo para que las pruebas se filtren entre sí con ?busqueda=
            // y no dependan del contenido que sembró otra.
            var ordenables = new[]
            {
                ApiFactory.Ficha("900-001", "Ordenable alfa"),
                ApiFactory.Ficha("900-002", "Ordenable zeta")
            };
            ordenables[1].EsPopular = true;

            var modalizados = new[]
            {
                ApiFactory.Ficha("901-001", "Modalizado uno"),
                ApiFactory.Ficha("901-002", "Modalizado dos"),
                ApiFactory.Ficha("901-003", "Modalizado tres")
            };
            modalizados[0].Modalidad = "Virtual";
            modalizados[1].Modalidad = "Hibrido";
            modalizados[2].Modalidad = "Presencial";

            // Veinticinco para poder distinguir «se recortó a 100» de «volvió a 20».
            var relleno = Enumerable.Range(1, 25)
                .Select(n => ApiFactory.Ficha($"902-{n:000}", $"Relleno {n:000}"))
                .ToArray();

            var sinPublicar = ApiFactory.Ficha("903-001", "Fantasma sin publicar", publicado: false);

            db.TramitesSiger.AddRange(ordenables);
            db.TramitesSiger.AddRange(modalizados);
            db.TramitesSiger.AddRange(relleno);
            db.TramitesSiger.Add(sinPublicar);

            await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync()
    {
        _api.Dispose();
        return Task.CompletedTask;
    }

    // ── Orden ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Orden_nombre_ordena_de_la_a_a_la_z()
    {
        var nombres = await NombresAsync("?busqueda=Ordenable&orden=nombre");

        nombres.Should().ContainInOrder("Ordenable alfa", "Ordenable zeta");
    }

    [Fact]
    public async Task Sin_orden_los_populares_van_primero()
    {
        var nombres = await NombresAsync("?busqueda=Ordenable");

        nombres.Should().ContainInOrder(new[] { "Ordenable zeta", "Ordenable alfa" },
            "«zeta» está marcado como popular; el orden por omisión no es alfabético");
    }

    /// <summary>
    /// La documentación decía que se podía ordenar por institución. No se puede, y pedirlo
    /// tampoco da error: cae en el orden por omisión sin avisar. Un integrador que confiara en
    /// esa frase habría recibido resultados ordenados de otra forma sin ninguna señal de que su
    /// parámetro se estaba ignorando.
    /// </summary>
    [Fact]
    public async Task Un_orden_que_no_existe_no_da_error_y_cae_en_el_de_por_omision()
    {
        var respuesta = await _api.ClienteConClave().GetAsync("/api/v1/tramites?busqueda=Ordenable&orden=institucion");
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, "un orden desconocido no es un error del cliente");

        var nombres = await NombresAsync("?busqueda=Ordenable&orden=institucion");
        nombres.Should().ContainInOrder(new[] { "Ordenable zeta", "Ordenable alfa" },
            "se ignora en silencio y se usa el orden por omisión");
    }

    // ── Modalidad ─────────────────────────────────────────────────────────────

    /// <summary>
    /// La asimetría que más fácil sorprende a quien integra: pedir Virtual devuelve también los
    /// híbridos. Es intencional —un trámite híbrido también se puede hacer en línea— pero si no
    /// está escrito, el integrador ve un conteo que no le cuadra y no sabe por qué.
    /// </summary>
    [Fact]
    public async Task Modalidad_virtual_tambien_trae_los_hibridos()
    {
        var nombres = await NombresAsync("?busqueda=Modalizado&modalidad=Virtual");

        nombres.Should().BeEquivalentTo(["Modalizado uno", "Modalizado dos"]);
    }

    [Fact]
    public async Task Modalidad_hibrido_solo_trae_los_hibridos()
    {
        var nombres = await NombresAsync("?busqueda=Modalizado&modalidad=Hibrido");

        nombres.Should().BeEquivalentTo(["Modalizado dos"],
            "la ampliación va en un solo sentido: Virtual incluye Hibrido, no al revés");
    }

    [Fact]
    public async Task La_modalidad_se_escribe_sin_tilde_y_mixto_no_existe()
    {
        var nombres = await NombresAsync("?busqueda=Modalizado&modalidad=Mixto");

        nombres.Should().BeEmpty("«Mixto» no es un valor del catálogo; el híbrido se llama «Hibrido»");
    }

    // ── Paginación ────────────────────────────────────────────────────────────

    /// <summary>
    /// Decía «se recorta en silencio a ese intervalo», que haría esperar 100. Devuelve 20.
    /// Para quien pagina en bucle la diferencia no es cosmética: son cinco veces más peticiones
    /// de las que había presupuestado.
    /// </summary>
    [Fact]
    public async Task Un_tamano_fuera_de_rango_no_se_recorta_vuelve_a_veinte()
    {
        var pagina = await PaginaAsync("?busqueda=Relleno&tamano=500");

        pagina.Tamano.Should().Be(20, "no se recorta a 100: vuelve al valor por omisión");
        pagina.Cuantos.Should().Be(20);
        pagina.Total.Should().Be(25, "el total no depende del tamaño de página");
    }

    [Fact]
    public async Task Un_tamano_dentro_del_rango_se_respeta()
    {
        var pagina = await PaginaAsync("?busqueda=Relleno&tamano=100");

        pagina.Tamano.Should().Be(100);
        pagina.Cuantos.Should().Be(25);
    }

    [Fact]
    public async Task Una_pagina_menor_que_uno_se_trata_como_la_primera()
    {
        var pagina = await PaginaAsync("?busqueda=Relleno&pagina=0");

        pagina.Pagina.Should().Be(1);
    }

    // ── Lo publicado y la clave ───────────────────────────────────────────────

    /// <summary>Un trámite sin publicar no existe para esta API, y da el mismo 404 que uno que
    /// nunca existió: distinguirlos permitiría averiguar qué códigos hay sin poder verlos.</summary>
    [Fact]
    public async Task Sin_publicar_e_inexistente_dan_la_misma_respuesta()
    {
        var cliente = _api.ClienteConClave();

        var sinPublicar = await cliente.GetAsync("/api/v1/tramites/903-001");
        var inventado   = await cliente.GetAsync("/api/v1/tramites/000-000");

        sinPublicar.StatusCode.Should().Be(HttpStatusCode.NotFound);
        inventado.StatusCode.Should().Be(sinPublicar.StatusCode);
    }

    [Fact]
    public async Task Sin_desde_la_ruta_de_cambios_lo_dice_en_vez_de_devolver_el_catalogo_entero()
    {
        var respuesta = await _api.ClienteConClave().GetAsync("/api/v1/cambios");

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "enlazarlo a 0001-01-01 devolvía el catálogo completo en silencio");
    }

    [Fact]
    public async Task Salud_es_la_unica_ruta_que_no_pide_clave()
    {
        var cliente = _api.ClienteSinClave();

        (await cliente.GetAsync("/api/v1/salud")).StatusCode.Should().Be(HttpStatusCode.OK,
            "un monitor externo no debería tener que custodiar un secreto");

        foreach (var ruta in new[]
        {
            "/api/v1/tramites", "/api/v1/tramites/900-001", "/api/v1/instituciones",
            "/api/v1/categorias", "/api/v1/cambios?desde=2026-01-01", "/api/v1/codigos-publicados"
        })
        {
            (await cliente.GetAsync(ruta)).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                $"{ruta} no puede quedar abierta");
        }
    }

    // ── Apoyo ─────────────────────────────────────────────────────────────────

    private sealed record Hoja(int Total, int Pagina, int Tamano, int Cuantos, IReadOnlyList<string> Nombres);

    private async Task<Hoja> PaginaAsync(string consulta)
    {
        var respuesta = await _api.ClienteConClave().GetAsync("/api/v1/tramites" + consulta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var raiz  = doc.RootElement;
        var items = raiz.GetProperty("items");

        return new Hoja(
            raiz.GetProperty("total").GetInt32(),
            raiz.GetProperty("pagina").GetInt32(),
            raiz.GetProperty("tamano").GetInt32(),
            items.GetArrayLength(),
            items.EnumerateArray().Select(i => i.GetProperty("nombre").GetString()!).ToList());
    }

    private async Task<IReadOnlyList<string>> NombresAsync(string consulta) =>
        (await PaginaAsync(consulta)).Nombres;
}
