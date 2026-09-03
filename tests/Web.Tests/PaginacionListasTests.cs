using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El cableado de la paginación de cliente en las tres pantallas que la usan.
///
/// <para>Lo que se puede probar acá es el <b>contrato entre el servidor y el navegador</b>: que cada
/// lista larga salga marcada con su <c>data-pg</c> y que exista el control que la gobierna con el
/// mismo identificador. Si alguien mueve una tabla y se lleva el marcado a medias, la lista deja de
/// paginar en silencio —se ve entera, sin error en ninguna parte— y solo se nota mirándola.</para>
///
/// <para>La otra mitad, y la que de verdad importa, es que <b>el servidor manda todas las filas</b>.
/// Es lo que sostiene la decisión de paginar en el cliente: los indicadores de los tableros y el CSV
/// del portafolio se calculan sobre el conjunto completo. El día que alguien "optimice" esto con un
/// <c>Take(10)</c> en la consulta, esos números empiezan a mentir y esta prueba es la que avisa.</para>
///
/// <para>Mostrar y ocultar filas es JavaScript y xUnit no lo alcanza: eso se verifica en el
/// navegador.</para>
/// </summary>
public sealed class PaginacionListasTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    /// <summary>Doce de cada cosa: más que el tamaño de arranque (10), para que la lista tenga
    /// al menos dos páginas y el control tenga sentido.</summary>
    private const int Sembrados = 12;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        // Administrador y no JefeArea: /Tableros/Proyectos es el portafolio de la institución y
        // desde la rama de los tres tableros redirige a quien no llega a ese alcance — un rol de
        // área recibe 302 hacia el tablero que sí es suyo, y esta suite nunca vería la página.
        // Lo que se mide acá es el cableado de la paginación, no la autorización: el corte por
        // alcance tiene sus propias pruebas en TableroProyectosInstitucionTests.
        await _portal.OtorgarAsync("Administrador", "Proyectos.Ver", "Tableros.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var i = 1; i <= Sembrados; i++)
        {
            var p = Proyecto.Crear($"PRY-PAG-{i:00}", $"Proyecto paginado {i:00}");
            p.InstitucionId = "DIGER";   // sin ancla queda fuera del filtro de alcance
            db.Proyectos.Add(p);
        }

        // El tablero de proyectos esconde cada una de sus cuatro listas de atención cuando no
        // tiene nada que poner en ella: sin estructura de verdad, la prueba pasaría sobre cuatro
        // estados vacíos y no probaría el cableado de ninguna. Un proyecto con todo lo que hace
        // falta las enciende a las cuatro.
        var conEstructura = Proyecto.Crear("PRY-PAG-EST", "Proyecto con estructura");
        conEstructura.InstitucionId = "DIGER";

        var entregable = EntregableProyecto.Crear("Entregable vencido", 1);
        entregable.Definir("Entregable vencido", null, hoy.AddDays(-15), null, null);   // vencido
        entregable.Agregar(ConVentana("Actividad vencida", 1, hoy.AddDays(-30), hoy.AddDays(-5)));
        entregable.Agregar(ConVentana("Actividad trancada", 2, hoy.AddDays(-3), hoy.AddDays(20)));
        conEstructura.Agregar(entregable);

        db.Proyectos.Add(conEstructura);
        await db.SaveChangesAsync();

        // La dependencia se ata después de guardar: antes las actividades no tienen Id y no hay
        // a qué apuntar. Es la misma restricción que documenta ActividadProyecto.FijarPredecesoras.
        var actividades = conEstructura.Entregables.Single().Actividades.OrderBy(a => a.Orden).ToList();
        actividades[1].FijarPredecesoras([actividades[0].Id]);

        // Un bloqueo en el último reporte del proyecto enciende la quinta lista.
        db.ProyectoAvances.Add(AvanceProyecto.Crear(
            conEstructura.Id, null, null, "Reporte con bloqueo", null, "Analista",
            bloqueo: "Falta la contraparte para cerrar el entregable."));

        await db.SaveChangesAsync();

        // Un expediente con doce trámites: el tablero de trámites lista un renglón por trámite.
        // La fecha tiene que caer después del corte de legado o la consulta lo descarta.
        var exp = Expediente.Crear("EXP-PAG-01", "DIGER", null, null, "DIGER", "Analista");
        exp.FechaApertura = new DateOnly(2026, 5, 1);
        for (var i = 0; i < Sembrados; i++)
            exp.Agregar(new ExpedienteTramite { NombreTramite = $"Tramite paginado {i:00}", TramiteIndex = i });
        db.Expedientes.Add(exp);

        await db.SaveChangesAsync();

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

    private async Task<string> HtmlDe(string ruta)
    {
        var respuesta = await _portal.ClienteComo("Administrador").GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, "la página {0} tiene que servirse", ruta);
        return await respuesta.Content.ReadAsStringAsync();
    }

    /// <summary>Las dos mitades del cableado: la lista marcada y su control apuntándole.</summary>
    private static void DebeEstarCableada(string html, string id)
    {
        html.Should().Contain($"data-pg=\"{id}\"",
            "la lista «{0}» tiene que quedar marcada para que el paginador la encuentre", id);
        html.Should().Contain($"data-pg-for=\"{id}\"",
            "sin el control, «{0}» se muestra entera y nadie se entera de que dejó de paginar", id);
    }

    [Fact]
    public async Task El_portafolio_cablea_su_tabla_de_proyectos()
    {
        DebeEstarCableada(await HtmlDe("/Proyectos"), "pry-portafolio");
    }

    [Fact]
    public async Task El_portafolio_manda_todas_las_filas_y_no_solo_la_primera_pagina()
    {
        var html = await HtmlDe("/Proyectos");

        // Los doce, incluido el que cae en la segunda página. Recortar en la consulta rompería
        // el CSV, que exporta lo mismo que muestra el listado.
        for (var i = 1; i <= Sembrados; i++)
            html.Should().Contain($"Proyecto paginado {i:00}");
    }

    [Fact]
    public async Task El_tablero_de_proyectos_cablea_sus_cinco_listas()
    {
        var html = await HtmlDe("/Tableros/Proyectos");

        foreach (var id in new[] { "pry-semaforo", "pry-actividades", "pry-bloqueadas",
                                   "pry-entregables", "pry-bloqueos" })
            DebeEstarCableada(html, id);
    }

    [Fact]
    public async Task El_tablero_de_tramites_cablea_su_tabla_de_avances()
    {
        DebeEstarCableada(await HtmlDe("/Tableros/Tramites"), "trm-avances");
    }

    [Fact]
    public async Task El_tablero_de_tramites_manda_los_doce_tramites()
    {
        var html = await HtmlDe("/Tableros/Tramites");

        for (var i = 0; i < Sembrados; i++)
            html.Should().Contain($"Tramite paginado {i:00}");
    }

    [Fact]
    public async Task Cada_control_gobierna_una_sola_lista()
    {
        var html = await HtmlDe("/Tableros/Proyectos");

        // Cinco listas en una misma pantalla: un identificador repetido haría que el primer
        // control se quedara con las filas del otro, y una de las dos tablas no paginaría.
        var ids = Regex.Matches(html, "data-pg-for=\"([^\"]+)\"")
                       .Select(m => m.Groups[1].Value)
                       .ToList();

        ids.Should().OnlyHaveUniqueItems("los identificadores de paginación son únicos por página");
    }
}
