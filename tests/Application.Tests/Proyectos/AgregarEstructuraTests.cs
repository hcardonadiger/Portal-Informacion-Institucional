using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Services;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

/// <summary>
/// Agregar un entregable o una actividad sin pasar por el guardado de la ficha. Es la puerta de
/// las herramientas MCP <c>crear_entregable</c> y <c>crear_actividad</c>: solo agregan —no reciben
/// la estructura entera, así que no hay nada que se pueda borrar por omisión— y devuelven el Id,
/// que es lo que el agente necesita para colgar actividades del entregable que acaba de crear.
/// </summary>
public class AgregarEstructuraTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly CatalogoPrioridades _prio;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();

    private static readonly DateOnly Marzo1  = new(2026, 3, 1);
    private static readonly DateOnly Marzo31 = new(2026, 3, 31);

    public AgregarEstructuraTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
        _prio = PrioridadesDePrueba.Sembrar(_ctx);
        _usuario.Nombre.Returns("Henry Cardona");
    }

    private async Task<Proyecto> SembrarProyectoAsync(EstadoProyecto estado = EstadoProyecto.Planificado)
    {
        var p = Proyecto.Crear($"PRY-{Guid.NewGuid():N}"[..12], "Expediente digital");
        p.PrioridadId = _prio.Media;
        _ctx.Proyectos.Add(p);
        await _ctx.SaveChangesAsync();

        if (estado != EstadoProyecto.Planificado)
        {
            p.CambiarEstado(EstadoProyecto.EnEjecucion, "siembra");
            if (estado != EstadoProyecto.EnEjecucion) p.CambiarEstado(estado, "siembra");
            await _ctx.SaveChangesAsync();
        }
        return p;
    }

    private async Task<Guid> SembrarInteresadoAsync(int proyectoId, string nombre)
    {
        var usuarioId = Guid.NewGuid();
        _ctx.ProyectoInteresados.Add(InteresadoProyecto.Crear(
            proyectoId, usuarioId, nombre, RolInteresado.Ejecutor, "siembra"));
        await _ctx.SaveChangesAsync();
        return usuarioId;
    }

    private Task<int> AgregarEntregableAsync(AgregarEntregableCommand cmd) =>
        new AgregarEntregableCommandHandler(_ctx, _usuario).Handle(cmd, CancellationToken.None);

    private Task<int> AgregarActividadAsync(AgregarActividadCommand cmd) =>
        new AgregarActividadCommandHandler(_ctx, _usuario).Handle(cmd, CancellationToken.None);

    private Task<Proyecto> LeerAsync(int id) =>
        _ctx.Proyectos.AsNoTracking()
            .Include(p => p.Entregables).ThenInclude(e => e.Actividades)
            .SingleAsync(p => p.Id == id);

    // ── Entregable ────────────────────────────────────────────────────────

    [Fact]
    public async Task El_entregable_se_crea_con_su_fecha_y_devuelve_su_Id()
    {
        var p = await SembrarProyectoAsync();

        var id = await AgregarEntregableAsync(new AgregarEntregableCommand(
            p.Id, "  Diagnóstico  ", "Levantamiento inicial", Marzo31));

        var e = (await LeerAsync(p.Id)).Entregables.Single();
        e.Id.Should().Be(id);
        e.Nombre.Should().Be("Diagnóstico");
        e.Descripcion.Should().Be("Levantamiento inicial");
        e.FechaPlan.Should().Be(Marzo31);
        e.Estado.Should().Be(EstadoEntregable.Pendiente);
    }

    [Fact]
    public async Task El_entregable_nuevo_va_al_final()
    {
        var p = await SembrarProyectoAsync();

        await AgregarEntregableAsync(new AgregarEntregableCommand(p.Id, "Primero"));
        await AgregarEntregableAsync(new AgregarEntregableCommand(p.Id, "Segundo"));

        (await LeerAsync(p.Id)).Entregables.OrderBy(e => e.Orden).Select(e => e.Nombre)
            .Should().Equal("Primero", "Segundo");
    }

    [Fact]
    public async Task Agregar_un_entregable_queda_en_la_bitacora()
    {
        var p = await SembrarProyectoAsync();

        await AgregarEntregableAsync(new AgregarEntregableCommand(p.Id, "Diagnóstico"));

        var b = await _ctx.BitacorasProyecto.AsNoTracking().SingleAsync(x => x.ProyectoId == p.Id);
        b.Tipo.Should().Be(TipoEventoProyecto.ModificacionEstructura);
        b.Detalle.Should().Contain("«Diagnóstico»");
        b.Actor.Should().Be("Henry Cardona");
    }

    [Fact]
    public async Task El_responsable_del_entregable_toma_el_nombre_del_interesado()
    {
        var p   = await SembrarProyectoAsync();
        var uid = await SembrarInteresadoAsync(p.Id, "María López");

        await AgregarEntregableAsync(new AgregarEntregableCommand(p.Id, "Diagnóstico", ResponsableId: uid));

        var e = (await LeerAsync(p.Id)).Entregables.Single();
        e.ResponsableId.Should().Be(uid);
        e.Responsable.Should().Be("María López");
    }

    [Fact]
    public async Task Un_responsable_que_no_es_interesado_se_rechaza()
    {
        var p = await SembrarProyectoAsync();

        var act = () => AgregarEntregableAsync(
            new AgregarEntregableCommand(p.Id, "Diagnóstico", ResponsableId: Guid.NewGuid()));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*interesado*");
        (await LeerAsync(p.Id)).Entregables.Should().BeEmpty();
    }

    [Theory]
    [InlineData(EstadoProyecto.Cerrado)]
    [InlineData(EstadoProyecto.Cancelado)]
    public async Task Un_proyecto_terminado_no_admite_entregables(EstadoProyecto estado)
    {
        var p = await SembrarProyectoAsync(estado);

        var act = () => AgregarEntregableAsync(new AgregarEntregableCommand(p.Id, "Tardío"));

        await act.Should().ThrowAsync<DomainException>();
        (await LeerAsync(p.Id)).Entregables.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_proyecto_inexistente_responde_no_encontrado()
    {
        var act = () => AgregarEntregableAsync(new AgregarEntregableCommand(999_999, "Huérfano"));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Actividad ─────────────────────────────────────────────────────────

    [Fact]
    public async Task La_actividad_se_crea_con_su_ventana_y_devuelve_su_Id()
    {
        var p  = await SembrarProyectoAsync();
        var eid = await AgregarEntregableAsync(new AgregarEntregableCommand(p.Id, "Diagnóstico"));

        var id = await AgregarActividadAsync(new AgregarActividadCommand(
            p.Id, eid, "Entrevistas", "Con cada jefatura", Marzo1, Marzo31));

        var a = (await LeerAsync(p.Id)).Entregables.Single().Actividades.Single();
        a.Id.Should().Be(id);
        a.Nombre.Should().Be("Entrevistas");
        a.Descripcion.Should().Be("Con cada jefatura");
        a.FechaInicioPlan.Should().Be(Marzo1);
        a.FechaFinPlan.Should().Be(Marzo31);
        a.AvancePct.Should().Be(0);
        a.Estado.Should().Be(EstadoActividad.Pendiente);
    }

    [Fact]
    public async Task La_actividad_nueva_va_al_final_de_su_entregable()
    {
        var p   = await SembrarProyectoAsync();
        var eid = await AgregarEntregableAsync(new AgregarEntregableCommand(p.Id, "Diagnóstico"));

        await AgregarActividadAsync(new AgregarActividadCommand(p.Id, eid, "Primera"));
        await AgregarActividadAsync(new AgregarActividadCommand(p.Id, eid, "Segunda"));

        (await LeerAsync(p.Id)).Entregables.Single().Actividades.OrderBy(a => a.Orden).Select(a => a.Nombre)
            .Should().Equal("Primera", "Segunda");
    }

    [Fact]
    public async Task Una_ventana_que_termina_antes_de_empezar_se_rechaza()
    {
        var p   = await SembrarProyectoAsync();
        var eid = await AgregarEntregableAsync(new AgregarEntregableCommand(p.Id, "Diagnóstico"));

        var act = () => AgregarActividadAsync(
            new AgregarActividadCommand(p.Id, eid, "Al revés", FechaInicioPlan: Marzo31, FechaFinPlan: Marzo1));

        await act.Should().ThrowAsync<DomainException>();
        (await LeerAsync(p.Id)).Entregables.Single().Actividades.Should().BeEmpty();
    }

    /// <summary>El entregable se busca dentro del proyecto indicado: con el Id de un entregable
    /// ajeno se colgaría una actividad en un proyecto que el agente no estaba mirando.</summary>
    [Fact]
    public async Task Un_entregable_de_otro_proyecto_se_rechaza()
    {
        var p     = await SembrarProyectoAsync();
        var otro  = await SembrarProyectoAsync();
        var ajeno = await AgregarEntregableAsync(new AgregarEntregableCommand(otro.Id, "Ajeno"));

        var act = () => AgregarActividadAsync(new AgregarActividadCommand(p.Id, ajeno, "Colada"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*no pertenece*");
        (await LeerAsync(otro.Id)).Entregables.Single().Actividades.Should().BeEmpty();
    }

    [Fact]
    public async Task El_responsable_de_la_actividad_tiene_que_ser_interesado()
    {
        var p   = await SembrarProyectoAsync();
        var eid = await AgregarEntregableAsync(new AgregarEntregableCommand(p.Id, "Diagnóstico"));
        var uid = await SembrarInteresadoAsync(p.Id, "María López");

        await AgregarActividadAsync(new AgregarActividadCommand(p.Id, eid, "Entrevistas", ResponsableId: uid));
        var act = () => AgregarActividadAsync(
            new AgregarActividadCommand(p.Id, eid, "Encuesta", ResponsableId: Guid.NewGuid()));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*interesado*");
        var a = (await LeerAsync(p.Id)).Entregables.Single().Actividades.Single();
        a.ResponsableId.Should().Be(uid);
        a.Responsable.Should().Be("María López");
    }

    [Fact]
    public async Task Un_proyecto_terminado_no_admite_actividades()
    {
        var p   = await SembrarProyectoAsync(EstadoProyecto.EnEjecucion);
        var eid = await AgregarEntregableAsync(new AgregarEntregableCommand(p.Id, "Diagnóstico"));
        var vivo = await _ctx.Proyectos.SingleAsync(x => x.Id == p.Id);
        vivo.CambiarEstado(EstadoProyecto.Cancelado, "siembra");
        await _ctx.SaveChangesAsync();

        var act = () => AgregarActividadAsync(new AgregarActividadCommand(p.Id, eid, "Tardía"));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Agregar_una_actividad_queda_en_la_bitacora()
    {
        var p   = await SembrarProyectoAsync();
        var eid = await AgregarEntregableAsync(new AgregarEntregableCommand(p.Id, "Diagnóstico"));

        await AgregarActividadAsync(new AgregarActividadCommand(p.Id, eid, "Entrevistas"));

        var ultima = await _ctx.BitacorasProyecto.AsNoTracking()
            .Where(x => x.ProyectoId == p.Id).OrderByDescending(x => x.Id).FirstAsync();
        ultima.Tipo.Should().Be(TipoEventoProyecto.ModificacionEstructura);
        ultima.Detalle.Should().Contain("«Diagnóstico»").And.Contain("«Entrevistas»");
    }

    /// <summary>El avance es el del árbol: un entregable en proceso sin actividades vale 50, y al
    /// colgarle su primera actividad en 0 % pasa a promediarlas. Es lo mismo que hace la ficha.</summary>
    [Fact]
    public async Task Agregar_una_actividad_recalcula_el_avance_del_proyecto()
    {
        var p   = await SembrarProyectoAsync();
        var eid = await AgregarEntregableAsync(new AgregarEntregableCommand(p.Id, "Diagnóstico"));
        var e   = await _ctx.ProyectoEntregables.SingleAsync(x => x.Id == eid);
        e.CambiarEstado(EstadoEntregable.EnProceso);
        var vivo = await _ctx.Proyectos.Include(x => x.Entregables).ThenInclude(x => x.Actividades).SingleAsync(x => x.Id == p.Id);
        vivo.RecalcularAvance(vivo.Entregables);
        await _ctx.SaveChangesAsync();
        (await LeerAsync(p.Id)).AvancePct.Should().Be(50);

        await AgregarActividadAsync(new AgregarActividadCommand(p.Id, eid, "Entrevistas"));

        (await LeerAsync(p.Id)).AvancePct.Should().Be(0);
    }

    public void Dispose() => _ctx.Dispose();
}
