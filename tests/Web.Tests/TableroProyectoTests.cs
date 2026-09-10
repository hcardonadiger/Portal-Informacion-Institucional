using System.Net;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El tablero de un proyecto, servido de verdad.
///
/// <para>Lo que se prueba acá no es que los números salgan —eso lo hace la aritmética de la
/// consulta— sino que <b>el tablero diga sobre qué muestra los calculó</b>. En este portafolio 190
/// de 214 actividades no tienen fechas planificadas: sin ese aviso, un «0 actividades vencidas» se
/// lee como «vamos bien» cuando en realidad significa «no hay contra qué medir». Es el falso verde
/// que estas pruebas existen para impedir.</para>
///
/// <para>Todo el texto que se busca acá es ASCII a propósito: Razor codifica lo demás
/// (<c>é</c> sale como <c>&amp;#xE9;</c>), así que un <c>Contain("línea base")</c> pasaría siempre
/// —incluso sobre una página en blanco— y no probaría nada.</para>
/// </summary>
public sealed class TableroProyectoTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _sinPlan;
    private int _conPlan;
    private int _planParcial;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Proyectos.Ver", "Proyectos.Editar");
        await _portal.OtorgarAsync("Consultor", "Proyectos.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // (1) Como está hoy la mayor parte del portafolio: actividades sin una sola fecha.
        var sinPlan = Nuevo("PRY-TAB-01", "Proyecto sin linea base");
        var e1 = EntregableProyecto.Crear("Entregable uno", 1);
        e1.Agregar(ActividadProyecto.Crear("Actividad sin fechas", 1));
        e1.Agregar(ActividadProyecto.Crear("Otra sin fechas", 2));
        sinPlan.Agregar(e1);

        // (2) Cronograma completo y atrasado: la ventana ya cerró y nadie reportó nada.
        var conPlan = Nuevo("PRY-TAB-02", "Proyecto con linea base");
        var e2 = EntregableProyecto.Crear("Entregable dos", 1);
        e2.Agregar(ConVentana("Actividad vencida", 1, hoy.AddDays(-30), hoy.AddDays(-10)));
        e2.Agregar(ConVentana("Actividad en curso", 2, hoy.AddDays(-5), hoy.AddDays(20)));
        conPlan.Agregar(e2);

        // (3) La mitad justa: una con fechas de cuatro. Cae debajo del umbral de cobertura.
        var parcial = Nuevo("PRY-TAB-03", "Proyecto con plan parcial");
        var e3 = EntregableProyecto.Crear("Entregable tres", 1);
        e3.Agregar(ConVentana("La unica con fechas", 1, hoy.AddDays(-10), hoy.AddDays(10)));
        e3.Agregar(ActividadProyecto.Crear("Sin fechas A", 2));
        e3.Agregar(ActividadProyecto.Crear("Sin fechas B", 3));
        e3.Agregar(ActividadProyecto.Crear("Sin fechas C", 4));
        parcial.Agregar(e3);

        db.Proyectos.AddRange(sinPlan, conPlan, parcial);
        await db.SaveChangesAsync();

        _sinPlan     = sinPlan.Id;
        _conPlan     = conPlan.Id;
        _planParcial = parcial.Id;

        static Proyecto Nuevo(string codigo, string nombre)
        {
            var p = Proyecto.Crear(codigo, nombre);
            // El filtro de alcance se ancla en la institución; sin esto no lo ve nadie.
            p.InstitucionId = "DIGER";
            return p;
        }

        static ActividadProyecto ConVentana(string nombre, int orden, DateOnly ini, DateOnly fin)
        {
            var a = ActividadProyecto.Crear(nombre, orden);
            a.Definir(nombre, null, ini, fin, null, null);
            return a;
        }
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> TableroAsync(int id, string rol = "JefeArea")
    {
        var respuesta = await _portal.ClienteComo(rol).GetAsync($"/Tableros/Proyecto/{id}");
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await respuesta.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Sin_fechas_planificadas_avisa_y_no_finge_un_avance_esperado()
    {
        var html = await TableroAsync(_sinPlan);

        html.Should().Contain("alert-warn", "el aviso de cobertura tiene que estar");
        html.Should().Contain("Ninguna de las 2 actividades");

        // Y lo que no puede estar: el bloque que compara contra plan. Un «esperado 0 %» sobre un
        // proyecto sin cronograma sería un numero inventado con cara de medicion.
        html.Should().NotContain("Esperado hoy",
            "sin fechas no hay contra que comparar; el bloque se omite en vez de mostrar cero");
    }

    [Fact]
    public async Task Con_cronograma_completo_compara_contra_el_plan()
    {
        var html = await TableroAsync(_conPlan);

        html.Should().Contain("Esperado hoy");
        html.Should().NotContain("Ninguna de las", "este proyecto si tiene linea base");
        html.Should().NotContain("alert-warn", "cobertura del 100 %: no hay nada que advertir");
    }

    [Fact]
    public async Task Un_plan_a_medias_dice_de_cuantas_actividades_salen_los_numeros()
    {
        var html = await TableroAsync(_planParcial);

        // Los indicadores se calculan igual, pero encabezados por el tamaño de la muestra.
        html.Should().Contain("Esperado hoy");
        html.Should().Contain("alert-warn");
        html.Should().Contain("1 de 4 actividades");
    }

    [Fact]
    public async Task La_actividad_vencida_llega_al_cuadro_de_atencion()
    {
        var html = await TableroAsync(_conPlan);

        html.Should().Contain("Actividad vencida");
        html.Should().Contain("badge-danger");

        // La que todavía está dentro de su ventana no exige atención: si apareciera, el cuadro
        // dejaría de ser una lista de pendientes y volvería a ser el cronograma completo.
        html.Should().NotContain("Actividad en curso");
    }

    [Fact]
    public async Task Sin_reportes_no_dibuja_el_grafico_de_ritmo()
    {
        var html = await TableroAsync(_conPlan);

        // Ninguno de los tres proyectos tiene reportes de avance. Servir el canvas y Chart.js para
        // pintar seis meses de ceros da una tarjeta que parece un dato y no lo es.
        html.Should().NotContain("chRitmo");
        html.Should().NotContain("chart.umd.min.js");
        html.Should().Contain("no tiene ning");   // «ningún reporte de avance», antes del acento
    }

    [Fact]
    public async Task Quien_solo_puede_ver_recibe_el_tablero()
    {
        var html = await TableroAsync(_conPlan, "Consultor");

        html.Should().Contain("Cumplimiento a la fecha");
    }

    [Fact]
    public async Task Un_proyecto_que_no_existe_da_404_y_no_una_pagina_vacia()
    {
        // Mismo camino que un proyecto fuera del alcance del usuario: la consulta devuelve null.
        // Devolver una página con todo en cero delataría que el proyecto existe.
        var respuesta = await _portal.ClienteComo("JefeArea").GetAsync("/Tableros/Proyecto/999999");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
