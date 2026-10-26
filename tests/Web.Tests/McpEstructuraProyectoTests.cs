using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Web.Mcp;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La prueba más importante del servidor MCP.
///
/// <para><c>ActualizarProyectoCommand</c> no parchea la ficha: la <b>reconcilia</b> contra la
/// lista de entregables que recibe. Lo que no venga en esa lista se borra —las actividades
/// ausentes desaparecen, sus avances quedan sin imputar y sus dependencias se eliminan—, y
/// una fila sin Id no se actualiza sino que se borra y se recrea con identidad nueva, lo que
/// deja en NULL el <c>EntregableId</c> de cada avance que la apuntaba.</para>
///
/// <para>Eso es perfectamente razonable para un formulario que postea la pantalla entera. Es
/// una trampa cuando quien arma el llamado es un modelo de lenguaje: basta con que omita una
/// actividad o le invente un Id para destrozar el cronograma y la bitácora de un proyecto. Por
/// eso la herramienta MCP no le pide la estructura a nadie: la relee y la reenvía tal cual, y
/// esto es lo que verifica que ese reenvío no pierda nada.</para>
/// </summary>
public sealed class McpEstructuraProyectoTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _proyectoId;
    private int _avanceId;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var proyecto = Proyecto.Crear("PRY-2026-MCP", "Proyecto con estructura");
        proyecto.PrioridadId  = PortalFactory.PrioridadPorDefecto;
        proyecto.InstitucionId = "DIGER";

        var primero = EntregableProyecto.Crear("Levantamiento", 1);
        primero.Agregar(ActividadProyecto.Crear("Entrevistas", 1));
        primero.Agregar(ActividadProyecto.Crear("Informe", 2));

        var segundo = EntregableProyecto.Crear("Construcción", 2);
        segundo.Agregar(ActividadProyecto.Crear("Desarrollo", 1));

        proyecto.Agregar(primero);
        proyecto.Agregar(segundo);

        db.Proyectos.Add(proyecto);
        await db.SaveChangesAsync();

        _proyectoId = proyecto.Id;

        // Un avance imputado a una actividad: es lo que se pierde si la estructura se recrea.
        var actividad = primero.Actividades.First();
        var avance = AvanceProyecto.Crear(
            proyectoId:          proyecto.Id,
            entregableId:        primero.Id,
            actividadId:         actividad.Id,
            descripcion:         "Se hicieron las entrevistas",
            porcentajeReportado: 40,
            autor:               "Tester");

        db.ProyectoAvances.Add(avance);
        await db.SaveChangesAsync();

        _avanceId = avance.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Cambiar solo el nombre no puede tocar ni un entregable ni una actividad.</summary>
    [Fact]
    public async Task Actualizar_la_ficha_conserva_entregables_y_actividades()
    {
        using var scope = _portal.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var antes = await sender.Send(new GetProyectoQuery(_proyectoId));
        antes.Should().NotBeNull();

        var idsEntregablesAntes = antes!.Entregables.Select(e => e.Id).ToList();
        var idsActividadesAntes = antes.Entregables.SelectMany(e => e.Actividades.Select(a => a.Id)).ToList();

        idsEntregablesAntes.Should().HaveCount(2);
        idsActividadesAntes.Should().HaveCount(3);

        // Exactamente lo que hace la herramienta actualizar_ficha_proyecto.
        await sender.Send(new ActualizarProyectoCommand(
            Id:              _proyectoId,
            Nombre:          "Nombre cambiado desde MCP",
            Objetivo:        antes.Objetivo,
            AreaId:          antes.AreaId,
            UnidadId:        antes.UnidadId,
            ResponsableId:   antes.ResponsableId,
            PrioridadId:     antes.PrioridadId,
            CategoriaId:     antes.CategoriaId,
            Accion:          antes.Accion,
            FechaInicioPlan: antes.FechaInicioPlan,
            FechaFinPlan:    antes.FechaFinPlan,
            Entregables:     EstructuraProyectoMcp.Conservar(antes)));

        var despues = await sender.Send(new GetProyectoQuery(_proyectoId));

        despues!.Nombre.Should().Be("Nombre cambiado desde MCP");

        despues.Entregables.Select(e => e.Id).Should().BeEquivalentTo(idsEntregablesAntes,
            "los entregables deben conservar su identidad, no borrarse y recrearse");

        despues.Entregables.SelectMany(e => e.Actividades.Select(a => a.Id))
            .Should().BeEquivalentTo(idsActividadesAntes,
                "una actividad que desaparece se lleva puestos sus avances y sus dependencias");
    }

    /// <summary>El daño real de recrear la estructura: el avance queda desimputado y la
    /// bitácora del proyecto pierde contra qué se reportó.</summary>
    [Fact]
    public async Task Actualizar_la_ficha_no_desimputa_los_avances()
    {
        using var scope = _portal.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var actividadAntes = await db.ProyectoAvances.AsNoTracking()
            .Where(a => a.Id == _avanceId).Select(a => a.ActividadId).SingleAsync();

        actividadAntes.Should().NotBeNull();

        var actual = await sender.Send(new GetProyectoQuery(_proyectoId));
        await sender.Send(new ActualizarProyectoCommand(
            Id:              _proyectoId,
            Nombre:          "Otro nombre",
            Objetivo:        actual!.Objetivo,
            AreaId:          actual.AreaId,
            UnidadId:        actual.UnidadId,
            ResponsableId:   actual.ResponsableId,
            PrioridadId:     actual.PrioridadId,
            CategoriaId:     actual.CategoriaId,
            Accion:          actual.Accion,
            FechaInicioPlan: actual.FechaInicioPlan,
            FechaFinPlan:    actual.FechaFinPlan,
            Entregables:     EstructuraProyectoMcp.Conservar(actual)));

        using var scope2 = _portal.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

        var actividadDespues = await db2.ProyectoAvances.AsNoTracking()
            .Where(a => a.Id == _avanceId).Select(a => a.ActividadId).SingleAsync();

        actividadDespues.Should().Be(actividadAntes,
            "el avance debe seguir imputado a la misma actividad");
    }

    /// <summary>Lo que demuestra que la prueba anterior mide algo: mandar la estructura vacía
    /// —lo que haría un modelo que no la conoce— sí borra todo. La herramienta jamás debe
    /// permitir llegar hasta acá, y por eso ni siquiera expone el parámetro.</summary>
    [Fact]
    public async Task Mandar_la_estructura_vacia_si_borraria_todo()
    {
        using var scope = _portal.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var actual = await sender.Send(new GetProyectoQuery(_proyectoId));

        await sender.Send(new ActualizarProyectoCommand(
            Id:              _proyectoId,
            Nombre:          actual!.Nombre,
            Objetivo:        actual.Objetivo,
            AreaId:          actual.AreaId,
            UnidadId:        actual.UnidadId,
            ResponsableId:   actual.ResponsableId,
            PrioridadId:     actual.PrioridadId,
            CategoriaId:     actual.CategoriaId,
            Accion:          actual.Accion,
            FechaInicioPlan: actual.FechaInicioPlan,
            FechaFinPlan:    actual.FechaFinPlan,
            Entregables:     []));

        var despues = await sender.Send(new GetProyectoQuery(_proyectoId));

        despues!.Entregables.Should().BeEmpty(
            "si esto dejara de ser cierto, la protección de la herramienta ya no haría falta " +
            "y habría que revisar por qué");
    }
}
