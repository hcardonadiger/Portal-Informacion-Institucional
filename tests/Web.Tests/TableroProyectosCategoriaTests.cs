using System.Net;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El filtro por categoría del tablero de institución (<c>/Tableros/Proyectos</c>) y las dos
/// maneras en que la categoría llega a la pantalla: la insignia de cada fila del semáforo y el
/// desglose «Proyectos por categoría».
///
/// <para>Se sirve la página de verdad porque lo que se rompe acá solo se ve servido: que el
/// desplegable exista y traiga las categorías, que el recorte llegue desde la URL hasta la
/// consulta, y que la insignia se pinte con el color del catálogo y <b>no</b> se pinte cuando
/// nadie clasificó el proyecto.</para>
///
/// <para>Todo el texto que se busca es ASCII a propósito —Razor codifica lo demás, así que un
/// <c>Contain("categoría")</c> pasaría hasta sobre una página en blanco—. De ahí que el balde se
/// busque como <c>"Sin categor"</c>.</para>
/// </summary>
public sealed class TableroProyectosCategoriaTests : IAsyncLifetime
{
    private const string Ruta = "/Tableros/Proyectos";

    private readonly PortalFactory _portal = new();

    private int _q3;
    private int _q2;
    private int _q1;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Administrador", "Proyectos.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Las tres rondas de clasificación de la F1, tal como las dejó la migración
        // LasQSonCategoriasNoPrioridades: Q3 primero, porque es la ronda final.
        var q3 = CategoriaProyecto.Crear("Q3", 1, ColorEtiqueta.Verde);
        var q2 = CategoriaProyecto.Crear("Q2", 2, ColorEtiqueta.Azul);
        // Retirada: no se puede ofrecer al clasificar, pero tiene que sobrevivir en la URL de
        // quien la traía seleccionada.
        var q1 = CategoriaProyecto.Crear("Q1", 3, ColorEtiqueta.Gris);
        q1.Actualizar(q1.Nombre, q1.Orden, q1.Color, activo: false);

        db.CategoriasProyecto.AddRange(q3, q2, q1);
        await db.SaveChangesAsync();

        _q3 = q3.Id;
        _q2 = q2.Id;
        _q1 = q1.Id;

        var enQ3    = Nuevo("PRY-CAT-01", "DEMO Conectividad digital", q3.Id);
        var otroQ3  = Nuevo("PRY-CAT-02", "DEMO Talento digital",      q3.Id);
        var enQ2    = Nuevo("PRY-CAT-03", "DEMO Proyectos digitales",  q2.Id);
        var enQ1    = Nuevo("PRY-CAT-04", "DEMO Datos abiertos",       q1.Id);
        // Sin clasificar: el que sostiene el balde del desglose y la fila sin insignia.
        var sinNada = Nuevo("PRY-CAT-05", "Mesa tecnica", null);

        var todos = new[] { enQ3, otroQ3, enQ2, enQ1, sinNada };
        db.Proyectos.AddRange(todos);
        await db.SaveChangesAsync();

        // El estado se mueve después del primer guardado: CambiarEstado emite un evento de
        // dominio que la bitácora rechaza si el proyecto todavía no tiene Id.
        foreach (var p in todos)
            p.CambiarEstado(EstadoProyecto.EnEjecucion, "Pruebas");

        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IRolCatalogo>().RecargarAsync();

        static Proyecto Nuevo(string codigo, string nombre, int? categoriaId)
        {
            var p = Proyecto.Crear(codigo, nombre);
            p.PrioridadId = PortalFactory.PrioridadPorDefecto;
            p.InstitucionId = "DIGER";
            p.CategoriaId = categoriaId;
            return p;
        }
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> TableroAsync(string consulta = "")
    {
        var respuesta = await _portal.ClienteComo("Administrador").GetAsync(Ruta + consulta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await respuesta.Content.ReadAsStringAsync();
    }

    // ── El desplegable ──────────────────────────────────────────────────────

    [Fact]
    public async Task El_tablero_ofrece_un_desplegable_de_categoria()
    {
        var html = await TableroAsync();

        html.Should().Contain("name=\"CategoriaId\"", "es el filtro que se pidio");
        html.Should().Contain(">Q3<").And.Contain(">Q2<");
    }

    [Fact]
    public async Task El_desplegable_no_ofrece_una_categoria_retirada()
    {
        var html = await TableroAsync();

        // La asercion apunta al <option> y no al rotulo suelto: ">Q1<" aparece igual en la insignia
        // de PRY-CAT-04, que SI tiene que seguir mostrando su categoria retirada —retirarla no puede
        // falsear lo que ya se clasifico con ella—. Tampoco basta value="<id>": los ids son enteros
        // chicos y ese mismo value lo usan los otros desplegables de la barra.
        html.Should().NotContain($"value=\"{_q1}\">Q1<",
            "Q1 esta desactivada: ya no es una opcion a futuro en el desplegable");
        html.Should().Contain("PRY-CAT-04",
            "pero el proyecto que ya la tiene sigue en el tablero, con su insignia");
    }

    [Fact]
    public async Task Una_categoria_retirada_sobrevive_si_viene_seleccionada_en_la_URL()
    {
        // Una URL guardada de cuando la categoria seguia activa sigue recortando el tablero. Si su
        // opcion desapareciera del desplegable, el recorte quedaria puesto y sin forma de quitarlo
        // desde la propia lista.
        var html = await TableroAsync($"?CategoriaId={_q1}");

        html.Should().Contain($"value=\"{_q1}\" selected=\"selected\">Q1<",
            "la opcion retirada aparece y viene marcada, para poder deseleccionarla");
        html.Should().Contain("PRY-CAT-04", "el recorte de la URL guardada sigue aplicandose");
        html.Should().NotContain("PRY-CAT-01", "Q3 es otra categoria");
    }

    // ── El recorte ──────────────────────────────────────────────────────────

    [Fact]
    public async Task El_filtro_de_categoria_recorta_el_tablero()
    {
        var html = await TableroAsync($"?CategoriaId={_q3}");

        html.Should().Contain("PRY-CAT-01").And.Contain("PRY-CAT-02");
        html.Should().NotContain("PRY-CAT-03", "es de otra categoria");
        html.Should().NotContain("PRY-CAT-05",
            "no tiene categoria: se pidio una categoria concreta, no 'esa o ninguna'");
    }

    [Fact]
    public async Task Sin_el_parametro_no_recorta_nada()
    {
        var html = await TableroAsync();

        html.Should().Contain("PRY-CAT-01").And.Contain("PRY-CAT-03").And.Contain("PRY-CAT-05");
    }

    [Fact]
    public async Task Una_categoria_que_el_catalogo_no_conoce_se_descarta_en_vez_de_aplicarse()
    {
        // Mismo criterio que un area tecleada a mano: aplicado, el tablero saldria vacio, sin
        // ninguna opcion marcada y sin mas pista que el boton Limpiar.
        var html = await TableroAsync("?CategoriaId=9999");

        html.Should().Contain("PRY-CAT-01").And.Contain("PRY-CAT-05");
    }

    [Fact]
    public async Task El_filtro_de_categoria_aparece_en_el_boton_de_limpiar()
    {
        var conFiltro = await TableroAsync($"?CategoriaId={_q3}");
        var sinFiltro = await TableroAsync();

        conFiltro.Should().Contain(">Limpiar<", "hay un recorte puesto y tiene que poder quitarse");
        sinFiltro.Should().NotContain(">Limpiar<");
    }

    // ── La categoría en pantalla ────────────────────────────────────────────

    [Fact]
    public async Task El_semaforo_pinta_la_insignia_con_el_color_del_catalogo()
    {
        var html = await TableroAsync($"?CategoriaId={_q3}");

        // Verde es el color con que la migracion sembro Q3, y la clase la resuelve
        // Etiquetas.ClaseColor —"prio-badge " + el color en minusculas— igual que en el listado de
        // proyectos: la insignia de categoria no estrena estilo propio.
        html.Should().Contain("prio-badge verde").And.Contain(">Q3<");
    }

    [Fact]
    public async Task Un_proyecto_sin_clasificar_no_lleva_insignia_de_categoria()
    {
        var html = await TableroAsync();

        // No hay aserción sobre la ausencia de un color —otros proyectos si lo llevan— sino sobre
        // que no se inventa una etiqueta: una insignia «Sin clasificar» en cada fila vieja es ruido,
        // y el filtro ya permite encontrarlas.
        html.Should().NotContain(">Sin clasificar<");
    }

    [Fact]
    public async Task El_desglose_por_categoria_cuenta_y_deja_el_balde_al_final()
    {
        var html = await TableroAsync();

        html.Should().Contain("Proyectos por categor", "es la tarjeta del desglose");
        // El balde de los no clasificados: sin el, el desglose sumaria 4 sobre un portafolio de 5
        // y se leeria como si faltaran proyectos.
        html.Should().Contain("Sin categor");

        var posicionQ3    = html.IndexOf("bar-lbl\">Q3<", StringComparison.Ordinal);
        var posicionBalde = html.IndexOf("bar-lbl\">Sin categor", StringComparison.Ordinal);
        posicionQ3.Should().BeGreaterThan(0, "Q3 tiene proyectos, asi que tiene barra");
        posicionBalde.Should().BeGreaterThan(posicionQ3,
            "el balde va al final: no es una clasificacion, es su ausencia");
    }

    [Fact]
    public async Task El_desglose_respeta_el_recorte_del_filtro()
    {
        var html = await TableroAsync($"?CategoriaId={_q3}");

        // Filtrado a Q3 no queda ningun proyecto sin clasificar, asi que el balde no se dibuja.
        html.Should().NotContain("bar-lbl\">Sin categor");
        html.Should().NotContain("bar-lbl\">Q2<");
    }
}
