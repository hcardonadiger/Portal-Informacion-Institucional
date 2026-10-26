using System.Security.Claims;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Security;
using Diger.TramitesEstado.Web.Mcp;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Las herramientas MCP <c>crear_entregable</c> y <c>crear_actividad</c>, de punta a punta: la clave
/// que exigen, el alcance institucional y —lo que justifica que no reusen el guardado de la
/// ficha— que agregar no toca lo que ya había, ni los avances imputados a ello.
/// </summary>
public sealed class McpCrearEstructuraTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _proyectoId;
    private int _proyectoDeOtraInstitucion;
    private int _avanceId;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea",  "Proyectos.Ver", "Proyectos.Editar");
        await _portal.OtorgarAsync("Empleado",  "Proyectos.Ver");
        await _portal.OtorgarAsync("Consultor", "Proyectos.Ver", "Proyectos.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var proyecto = Proyecto("PRY-2026-801", "DIGER");
        var vigente  = EntregableProyecto.Crear("Levantamiento", 1);
        vigente.Agregar(ActividadProyecto.Crear("Entrevistas", 1));
        proyecto.Agregar(vigente);

        var otra = Proyecto("PRY-2026-802", "COPECO");
        db.Proyectos.AddRange(proyecto, otra);
        await db.SaveChangesAsync();

        var avance = AvanceProyecto.Crear(
            proyectoId:          proyecto.Id,
            entregableId:        vigente.Id,
            actividadId:         vigente.Actividades.Single().Id,
            descripcion:         "Se hicieron las entrevistas",
            porcentajeReportado: 40,
            autor:               "Tester");
        db.ProyectoAvances.Add(avance);
        await db.SaveChangesAsync();

        _proyectoId                = proyecto.Id;
        _proyectoDeOtraInstitucion = otra.Id;
        _avanceId                  = avance.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private static Proyecto Proyecto(string codigo, string institucion)
    {
        var p = Diger.TramitesEstado.Domain.Entities.Proyecto.Crear(codigo, $"Proyecto {codigo}");
        p.PrioridadId   = PortalFactory.PrioridadPorDefecto;
        p.InstitucionId = institucion;
        return p;
    }

    /// <summary>Misma forma de identidad que emite un token MCP: los claims del login.</summary>
    private async Task<T> ComoMcpAsync<T>(string rol, Func<ProyectosTools, Task<T>> accion)
    {
        using var scope = _portal.Services.CreateScope();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier,   Guid.NewGuid().ToString()),
            new(AppClaims.UserId,            Guid.NewGuid().ToString()),
            new(ClaimTypes.Name,             "Usuario de prueba"),
            new(ClaimTypes.Role,             rol),
            new(AppClaims.ActiveRol,         rol),
            new(AppClaims.ActiveInstitucion, "DIGER")
        };
        var contexto = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        contexto.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "McpBearer"));
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = contexto;

        return await accion(ActivatorUtilities.CreateInstance<ProyectosTools>(scope.ServiceProvider));
    }

    private static int IdDe(object respuesta) =>
        (int)respuesta.GetType().GetProperty("Id")!.GetValue(respuesta)!;

    private async Task<Proyecto> LeerAsync()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Proyectos.IgnoreQueryFilters().AsNoTracking()
            .Include(p => p.Entregables).ThenInclude(e => e.Actividades)
            .SingleAsync(p => p.Id == _proyectoId);
    }

    [Fact]
    public async Task El_Id_del_entregable_nuevo_sirve_para_colgarle_una_actividad()
    {
        var entregable = await ComoMcpAsync("JefeArea", t => t.CrearEntregable(
            _proyectoId, "Construcción", fechaPlan: new DateOnly(2026, 6, 30)));
        var entregableId = IdDe(entregable);

        var actividad = await ComoMcpAsync("JefeArea", t => t.CrearActividad(
            _proyectoId, entregableId, "Desarrollo",
            fechaInicioPlan: new DateOnly(2026, 4, 1), fechaFinPlan: new DateOnly(2026, 6, 15)));

        var nuevo = (await LeerAsync()).Entregables.Single(e => e.Id == entregableId);
        nuevo.FechaPlan.Should().Be(new DateOnly(2026, 6, 30));
        var a = nuevo.Actividades.Single();
        a.Id.Should().Be(IdDe(actividad));
        a.FechaInicioPlan.Should().Be(new DateOnly(2026, 4, 1));
        a.FechaFinPlan.Should().Be(new DateOnly(2026, 6, 15));
    }

    /// <summary>Lo que no hace el guardado de la ficha: agregar no recrea lo que ya estaba, así
    /// que el avance imputado a la actividad vieja sigue apuntándola.</summary>
    [Fact]
    public async Task Agregar_no_toca_la_estructura_ni_los_avances_que_ya_habia()
    {
        var antes   = (await LeerAsync()).Entregables.Single();
        var actId   = antes.Actividades.Single().Id;

        await ComoMcpAsync("JefeArea", t => t.CrearEntregable(_proyectoId, "Construcción"));
        await ComoMcpAsync("JefeArea", t => t.CrearActividad(_proyectoId, antes.Id, "Informe"));

        var despues = await LeerAsync();
        despues.Entregables.Select(e => e.Nombre).Should().Equal("Levantamiento", "Construcción");
        despues.Entregables.Single(e => e.Id == antes.Id).Actividades
            .OrderBy(a => a.Orden).Select(a => a.Id).First().Should().Be(actId);

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var avance = await db.ProyectoAvances.AsNoTracking().SingleAsync(a => a.Id == _avanceId);
        avance.EntregableId.Should().Be(antes.Id);
        avance.ActividadId.Should().Be(actId);
    }

    [Fact]
    public async Task Ver_proyectos_no_alcanza_para_crear_estructura()
    {
        var entregable = () => ComoMcpAsync("Empleado", t => t.CrearEntregable(_proyectoId, "Colado"));
        var actividad  = () => ComoMcpAsync("Empleado", t => t.CrearActividad(_proyectoId, 1, "Colada"));

        (await entregable.Should().ThrowAsync<McpPermisoDenegadoException>()).WithMessage("*Proyectos.Editar*");
        await actividad.Should().ThrowAsync<McpPermisoDenegadoException>();
        (await LeerAsync()).Entregables.Should().ContainSingle();
    }

    [Fact]
    public async Task Un_rol_de_solo_lectura_no_crea_estructura_aunque_tenga_la_clave()
    {
        var accion = () => ComoMcpAsync("Consultor", t => t.CrearEntregable(_proyectoId, "Colado"));

        (await accion.Should().ThrowAsync<McpPermisoDenegadoException>()).WithMessage("*solo lectura*");
    }

    [Fact]
    public async Task Un_proyecto_de_otra_institucion_responde_no_encontrado()
    {
        var accion = () => ComoMcpAsync("JefeArea", t => t.CrearEntregable(_proyectoDeOtraInstitucion, "Colado"));

        await accion.Should().ThrowAsync<NotFoundException>();

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ProyectoEntregables.IgnoreQueryFilters().AnyAsync(e => e.ProyectoId == _proyectoDeOtraInstitucion))
            .Should().BeFalse();
    }
}
