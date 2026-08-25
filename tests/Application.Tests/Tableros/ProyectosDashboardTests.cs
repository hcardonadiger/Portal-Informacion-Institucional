using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Dashboards.Common;
using Diger.TramitesEstado.Application.Dashboards.Queries;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Common;
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
        var id = await new CrearProyectoCommandHandler(_ctx, _usuario)
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
            .Handle(new RegistrarAvanceCommand(reportado, "Avance de hoy", 40), CancellationToken.None);

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
        await new CrearProyectoCommandHandler(_ctx, _usuario).Handle(new CrearProyectoCommand("Planificado"), CancellationToken.None);

        await new RegistrarAvanceCommandHandler(_ctx, _usuario)
            .Handle(new RegistrarAvanceCommand(a, "Mitad", 50), CancellationToken.None);

        var d = await TableroAsync();

        d.AvancePromedio.Should().Be(25);   // (50 + 0) / 2, sin contar el planificado
    }

    [Fact]
    public async Task Un_bloqueo_que_el_reporte_siguiente_ya_no_menciona_se_da_por_superado()
    {
        var id = await ProyectoEnEjecucionAsync("Con bloqueo");
        var handler = new RegistrarAvanceCommandHandler(_ctx, _usuario);

        await handler.Handle(new RegistrarAvanceCommand(id, "Primer corte", 20, Bloqueo: "Falta la firma"), CancellationToken.None);
        (await TableroAsync()).Bloqueos.Should().ContainSingle().Which.Texto.Should().Be("Falta la firma");

        await handler.Handle(new RegistrarAvanceCommand(id, "Ya se resolvió", 40), CancellationToken.None);
        (await TableroAsync()).Bloqueos.Should().BeEmpty();
    }

    [Fact]
    public async Task Los_hitos_vencidos_y_los_proximos_se_separan_por_la_fecha_de_hoy()
    {
        var id = await ProyectoEnEjecucionAsync("Con hitos");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        await new ActualizarProyectoCommandHandler(_ctx, _usuario).Handle(new ActualizarProyectoCommand(
            id, "Con hitos", null, null, null, null, null, PrioridadProyecto.Media, null, null,
            [
                new HitoInput(0, "Vencido",     null, hoy.AddDays(-5),  null, EstadoHito.EnProceso,  null, null),
                new HitoInput(0, "Por vencer",  null, hoy.AddDays(10),  null, EstadoHito.Pendiente,  null, null),
                new HitoInput(0, "Lejano",      null, hoy.AddDays(120), null, EstadoHito.Pendiente,  null, null),
                new HitoInput(0, "Ya cerrado",  null, hoy.AddDays(-20), null, EstadoHito.Completado, null, null)
            ]), CancellationToken.None);

        var d = await TableroAsync();

        d.HitosVencidos.Should().Be(1);
        d.HitosProximos.Should().Be(1);   // el lejano queda fuera de la ventana de 30 días
        d.Hitos.Select(h => h.Hito).Should().Equal("Vencido", "Por vencer");
        d.Semaforo.Single().HitosVencidos.Should().Be(1);
    }

    [Fact]
    public async Task Los_filtros_recortan_tambien_los_hitos_y_los_bloqueos()
    {
        var mio  = await ProyectoEnEjecucionAsync("Mío");
        var otro = await ProyectoEnEjecucionAsync("Ajeno");

        var handler = new RegistrarAvanceCommandHandler(_ctx, _usuario);
        await handler.Handle(new RegistrarAvanceCommand(mio,  "A", 10, Bloqueo: "Bloqueo del mío"), CancellationToken.None);
        await handler.Handle(new RegistrarAvanceCommand(otro, "B", 10, Bloqueo: "Bloqueo del ajeno"), CancellationToken.None);

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

    public void Dispose() => _ctx.Dispose();
}
