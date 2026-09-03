using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Dashboards.Common;
using Diger.TramitesEstado.Application.Dashboards.Queries;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Application.Proyectos.Services;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Tableros;

public class ProyectosDashboardTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private readonly IInteresadosAutomaticosSync _sync = Substitute.For<IInteresadosAutomaticosSync>();

    public ProyectosDashboardTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
        _usuario.Nombre.Returns("Henry Ortez");
    }

    private async Task<int> ProyectoEnEjecucionAsync(string nombre)
    {
        var id = await new CrearProyectoCommandHandler(_ctx, _usuario, _sync)
            .Handle(new CrearProyectoCommand(nombre), CancellationToken.None);
        await new CambiarEstadoProyectoCommandHandler(_ctx, _usuario)
            .Handle(new CambiarEstadoProyectoCommand(id, EstadoProyecto.EnEjecucion), CancellationToken.None);
        return id;
    }

    private Task<ProyectosDashboardDto> TableroAsync(GetProyectosDashboardQuery? q = null) =>
        new GetProyectosDashboardQueryHandler(_ctx).Handle(q ?? new GetProyectosDashboardQuery(), CancellationToken.None);

    [Fact]
    public async Task Marca_como_sin_reportar_solo_a_los_que_llevan_mas_de_treinta_dias()
    {
        var callado   = await ProyectoEnEjecucionAsync("Sin noticias");
        var reportado = await ProyectoEnEjecucionAsync("Con reporte");

        await new RegistrarAvanceCommandHandler(_ctx, _usuario)
            .Handle(new RegistrarAvanceCommand(reportado, "Avance de hoy"), CancellationToken.None);

        var d = await TableroAsync();

        d.EnEjecucion.Should().Be(2);
        d.SinReportar.Should().Be(1);
        d.Semaforo.Single(p => p.ProyectoId == callado).SinReportar.Should().BeTrue();
        d.Semaforo.Single(p => p.ProyectoId == reportado).SinReportar.Should().BeFalse();
    }

    [Fact]
    public async Task El_avance_promedio_solo_toma_los_proyectos_en_ejecucion()
    {
        var a = await ProyectoEnEjecucionAsync("A");
        await ProyectoEnEjecucionAsync("B");
        // Un planificado con 0 % no debe arrastrar el promedio hacia abajo.
        await new CrearProyectoCommandHandler(_ctx, _usuario, _sync).Handle(new CrearProyectoCommand("Planificado"), CancellationToken.None);

        // Dos entregables, uno cumplido: 50 % por la regla 0/50/100. El avance ya no se declara.
        await new ActualizarProyectoCommandHandler(_ctx, _usuario, _sync).Handle(new ActualizarProyectoCommand(
            a, "A", null, null, null, null, null, PrioridadProyecto.Media, null, null, null,
            [
                new EntregableInput(0, "Cumplido", null, null, EstadoEntregable.Completado, null, null, []),
                new EntregableInput(0, "Pendiente", null, null, EstadoEntregable.Pendiente, null, null, [])
            ]), CancellationToken.None);

        var d = await TableroAsync();

        d.AvancePromedio.Should().Be(25);   // (50 + 0) / 2, sin contar el planificado
    }

    [Fact]
    public async Task Un_bloqueo_que_el_reporte_siguiente_ya_no_menciona_se_da_por_superado()
    {
        var id = await ProyectoEnEjecucionAsync("Con bloqueo");
        var handler = new RegistrarAvanceCommandHandler(_ctx, _usuario);

        await handler.Handle(new RegistrarAvanceCommand(id, "Primer corte", Bloqueo: "Falta la firma"), CancellationToken.None);
        (await TableroAsync()).Bloqueos.Should().ContainSingle().Which.Texto.Should().Be("Falta la firma");

        await handler.Handle(new RegistrarAvanceCommand(id, "Ya se resolvió"), CancellationToken.None);
        (await TableroAsync()).Bloqueos.Should().BeEmpty();
    }

    [Fact]
    public async Task Los_entregables_vencidos_y_los_proximos_se_separan_por_la_fecha_de_hoy()
    {
        var id = await ProyectoEnEjecucionAsync("Con entregables");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        await new ActualizarProyectoCommandHandler(_ctx, _usuario, _sync).Handle(new ActualizarProyectoCommand(
            id, "Con entregables", null, null, null, null, null, PrioridadProyecto.Media, null, null, null,
            [
                new EntregableInput(0, "Vencido",    null, hoy.AddDays(-5),  EstadoEntregable.EnProceso,  null, null, []),
                new EntregableInput(0, "Por vencer", null, hoy.AddDays(10),  EstadoEntregable.Pendiente,  null, null, []),
                new EntregableInput(0, "Lejano",     null, hoy.AddDays(120), EstadoEntregable.Pendiente,  null, null, []),
                new EntregableInput(0, "Ya cerrado", null, hoy.AddDays(-20), EstadoEntregable.Completado, null, null, [])
            ]), CancellationToken.None);

        var d = await TableroAsync();

        d.EntregablesVencidos.Should().Be(1);
        d.EntregablesProximos.Should().Be(1);   // el lejano queda fuera de la ventana de 30 días
        d.Entregables.Select(e => e.Entregable).Should().Equal("Vencido", "Por vencer");
        d.Semaforo.Single().EntregablesVencidos.Should().Be(1);
    }

    [Fact]
    public async Task Las_actividades_vencidas_y_las_proximas_se_separan_por_la_fecha_de_hoy()
    {
        var id = await ProyectoEnEjecucionAsync("Con actividades");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        await new ActualizarProyectoCommandHandler(_ctx, _usuario, _sync).Handle(new ActualizarProyectoCommand(
            id, "Con actividades", null, null, null, null, null, PrioridadProyecto.Media, null, null, null,
            [
                new EntregableInput(0, "Único", null, hoy.AddDays(150), EstadoEntregable.EnProceso, null, null,
                [
                    new ActividadInput(0, "Vencida",    null, hoy.AddDays(-30), hoy.AddDays(-5),  40, false, null, null),
                    new ActividadInput(0, "Por vencer", null, hoy.AddDays(-2),  hoy.AddDays(10),  10, false, null, null),
                    new ActividadInput(0, "Lejana",     null, hoy.AddDays(90),  hoy.AddDays(120),  0, false, null, null),
                    new ActividadInput(0, "Terminada",  null, hoy.AddDays(-40), hoy.AddDays(-20),100, false, null, null)
                ])
            ]), CancellationToken.None);

        var d = await TableroAsync();

        d.ActividadesVencidas.Should().Be(1);
        d.ActividadesProximas.Should().Be(1);
        d.Actividades.Select(a => a.Actividad).Should().Equal("Vencida", "Por vencer");
        d.EntregablesVencidos.Should().Be(0, "el entregable vence en cinco meses");
    }

    [Fact]
    public async Task Los_filtros_recortan_tambien_los_entregables_y_los_bloqueos()
    {
        var mio  = await ProyectoEnEjecucionAsync("Mío");
        var otro = await ProyectoEnEjecucionAsync("Ajeno");

        var handler = new RegistrarAvanceCommandHandler(_ctx, _usuario);
        await handler.Handle(new RegistrarAvanceCommand(mio,  "A", Bloqueo: "Bloqueo del mío"), CancellationToken.None);
        await handler.Handle(new RegistrarAvanceCommand(otro, "B", Bloqueo: "Bloqueo del ajeno"), CancellationToken.None);

        // Se le pone responsable solo al primero y se filtra por él.
        var p = await _ctx.Proyectos.FirstAsync(x => x.Id == mio);
        var responsable = Guid.NewGuid();
        p.ResponsableId = responsable;
        p.Responsable = "Henry Ortez";
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var d = await TableroAsync(new GetProyectosDashboardQuery(ResponsableId: responsable));

        d.Total.Should().Be(1);
        d.Semaforo.Should().ContainSingle().Which.Nombre.Should().Be("Mío");
        d.Bloqueos.Should().ContainSingle().Which.Texto.Should().Be("Bloqueo del mío");
    }

    [Fact]
    public async Task Los_proyectos_sin_responsable_se_cuentan_y_se_agrupan_aparte()
    {
        await ProyectoEnEjecucionAsync("Huérfano uno");
        await ProyectoEnEjecucionAsync("Huérfano dos");

        var d = await TableroAsync();

        d.SinResponsable.Should().Be(2);
        d.PorResponsable.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Etiqueta = "Sin asignar", Cantidad = 2 });
    }

    [Fact]
    public async Task Lista_las_actividades_bloqueadas_por_una_dependencia_y_encabeza_las_que_ya_arrancaron()
    {
        var id = await ProyectoEnEjecucionAsync("Con dependencias");

        await new ActualizarProyectoCommandHandler(_ctx, _usuario, _sync).Handle(new ActualizarProyectoCommand(
            id, "Con dependencias", null, null, null, null, null, PrioridadProyecto.Media, null, null, null,
            [
                new EntregableInput(0, "Único", null, null, EstadoEntregable.EnProceso, null, null,
                [
                    new ActividadInput(0, "Levantamiento", null, null, null,  0, false, null, null),
                    new ActividadInput(0, "Desarrollo",    null, null, null, 30, false, null, null),
                    new ActividadInput(0, "Piloto",        null, null, null,  0, false, null, null)
                ])
            ]), CancellationToken.None);

        var actividades = await _ctx.ProyectoActividades.ToDictionaryAsync(a => a.Nombre, a => a.Id);

        // Las dos cuelgan de «Levantamiento», que sigue en cero: ambas quedan bloqueadas, pero
        // «Desarrollo» ya se está trabajando y por eso encabeza.
        var ficha = await new GetProyectoQueryHandler(_ctx).Handle(new GetProyectoQuery(id), CancellationToken.None);
        var entrada = ficha!.Entregables.Select(e => new EntregableInput(
            e.Id, e.Nombre, e.Descripcion, e.FechaPlan, e.Estado, e.ResponsableId, e.Responsable,
            e.Actividades.Select(a => new ActividadInput(
                a.Id, a.Nombre, a.Descripcion, a.FechaInicioPlan, a.FechaFinPlan, a.AvancePct,
                a.EstaCancelada, a.ResponsableId, a.Responsable,
                a.Nombre == "Levantamiento" ? [] : new List<int> { actividades["Levantamiento"] }))
                .ToList())).ToList();

        await new ActualizarProyectoCommandHandler(_ctx, _usuario, _sync).Handle(new ActualizarProyectoCommand(
            id, "Con dependencias", null, null, null, null, null, PrioridadProyecto.Media, null, null, null,
            entrada), CancellationToken.None);

        var d = await TableroAsync();

        d.ActividadesBloqueadas.Should().Be(2);
        d.ArrancaronBloqueadas.Should().Be(1);
        d.Bloqueadas.Select(b => b.Actividad).Should().Equal("Desarrollo", "Piloto");
        d.Bloqueadas.First().Espera.Should().Equal("Levantamiento");
        d.ActividadesVencidas.Should().Be(0, "ninguna tiene fecha: el bloqueo no se ve por fecha");
    }

    public void Dispose() => _ctx.Dispose();
}
