using Diger.TramitesEstado.Application.Dashboards.Common;
using Diger.TramitesEstado.Application.Dashboards.Queries.GetTicketsDashboard;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Application.Tickets.Prioridades;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Tickets;

/// <summary>
/// El catálogo de prioridades de ticket, y sobre todo la marca «cuenta como crítica».
///
/// <para>Esa marca es la parte que puede romperse en silencio. El indicador de «críticos
/// abiertos» de los tableros preguntaba <c>Prioridad == PrioridadTicket.Critica</c>; con un
/// catálogo el nombre lo edita quien administra, así que la condición pasó a mirar una marca
/// explícita. Si alguien la volviera a atar al nombre, el indicador seguiría compilando y daría
/// cero en cuanto renombraran la fila.</para>
/// </summary>
public class PrioridadesTicketCatalogoTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly CatalogoPrioridadesTicket _prio;

    public PrioridadesTicketCatalogoTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
        _prio = PrioridadesTicketDePrueba.Sembrar(_ctx);
    }

    private async Task<int> TicketAsync(string numero, int prioridadId)
    {
        var t = Ticket.Crear(numero, $"Ticket {numero}");
        t.PrioridadId = prioridadId;
        _ctx.Tickets.Add(t);
        await _ctx.SaveChangesAsync();
        return t.Id;
    }

    private Task<TicketsDashboardDto> TableroAsync() =>
        new GetTicketsDashboardQueryHandler(_ctx)
            .Handle(new GetTicketsDashboardQuery(), CancellationToken.None);

    // ── La marca de crítica ───────────────────────────────────────
    [Fact]
    public async Task El_indicador_cuenta_los_tickets_con_prioridad_marcada_como_critica()
    {
        await TicketAsync("TCK-2026-0001", _prio.Critica);
        await TicketAsync("TCK-2026-0002", _prio.Media);

        (await TableroAsync()).CriticosAbiertos.Should().Be(1);
    }

    [Fact]
    public async Task Renombrar_la_prioridad_no_altera_el_indicador()
    {
        // Es la prueba que justifica que exista la marca: antes la condición miraba el nombre.
        await TicketAsync("TCK-2026-0001", _prio.Critica);

        await new ActualizarPrioridadTicketCommandHandler(_ctx).Handle(
            new ActualizarPrioridadTicketCommand(
                _prio.Critica, "Bloqueante total", 1, ColorEtiqueta.Rojo, EsCritica: true, Activo: true),
            CancellationToken.None);

        (await TableroAsync()).CriticosAbiertos.Should().Be(1);
    }

    [Fact]
    public async Task Marcar_otra_prioridad_como_critica_la_suma_al_indicador()
    {
        await TicketAsync("TCK-2026-0001", _prio.Critica);
        await TicketAsync("TCK-2026-0002", _prio.Alta);

        (await TableroAsync()).CriticosAbiertos.Should().Be(1, "todavía solo Crítica está marcada");

        await new ActualizarPrioridadTicketCommandHandler(_ctx).Handle(
            new ActualizarPrioridadTicketCommand(
                _prio.Alta, "Alta", 2, ColorEtiqueta.Naranja, EsCritica: true, Activo: true),
            CancellationToken.None);

        (await TableroAsync()).CriticosAbiertos.Should().Be(2);
    }

    [Fact]
    public async Task Desmarcarla_lo_baja()
    {
        await TicketAsync("TCK-2026-0001", _prio.Critica);

        await new ActualizarPrioridadTicketCommandHandler(_ctx).Handle(
            new ActualizarPrioridadTicketCommand(
                _prio.Critica, "Critica", 1, ColorEtiqueta.Rojo, EsCritica: false, Activo: true),
            CancellationToken.None);

        (await TableroAsync()).CriticosAbiertos.Should().Be(0);
    }

    // ── El gráfico por prioridad ──────────────────────────────────
    [Fact]
    public async Task El_grafico_lista_todas_las_prioridades_aunque_no_tengan_tickets()
    {
        // Con el enum, las categorías vacías salían en cero. Si ahora solo se listaran las que
        // tienen tickets, el gráfico cambiaría de forma según el mes.
        await TicketAsync("TCK-2026-0001", _prio.Media);

        var porPrioridad = (await TableroAsync()).PorPrioridad;

        porPrioridad.Should().HaveCount(4);
        porPrioridad.Single(p => p.Id == _prio.Media).Cantidad.Should().Be(1);
        porPrioridad.Single(p => p.Id == _prio.Baja).Cantidad.Should().Be(0);
    }

    [Fact]
    public async Task El_grafico_sale_en_el_orden_del_catalogo()
    {
        var porPrioridad = (await TableroAsync()).PorPrioridad;

        porPrioridad.Select(p => p.Etiqueta)
            .Should().ContainInOrder("Critica", "Alta", "Media", "Baja");
    }

    // ── Reglas del catálogo ───────────────────────────────────────
    [Fact]
    public async Task No_se_puede_eliminar_una_prioridad_con_tickets()
    {
        await TicketAsync("TCK-2026-0001", _prio.Alta);

        var acto = () => new EliminarPrioridadTicketCommandHandler(_ctx)
            .Handle(new EliminarPrioridadTicketCommand(_prio.Alta), CancellationToken.None);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*1 ticket*");
    }

    [Fact]
    public async Task No_se_puede_eliminar_la_predeterminada()
    {
        var acto = () => new EliminarPrioridadTicketCommandHandler(_ctx)
            .Handle(new EliminarPrioridadTicketCommand(_prio.Media), CancellationToken.None);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*predeterminada*");
    }

    [Fact]
    public async Task Crear_rechaza_un_nombre_repetido()
    {
        var acto = () => new CrearPrioridadTicketCommandHandler(_ctx)
            .Handle(new CrearPrioridadTicketCommand("Alta", 9, ColorEtiqueta.Rojo, EsCritica: false),
                    CancellationToken.None);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*Alta*");
    }

    [Fact]
    public async Task Marcar_una_predeterminada_desmarca_la_anterior()
    {
        await new MarcarPrioridadTicketPredeterminadaCommandHandler(_ctx)
            .Handle(new MarcarPrioridadTicketPredeterminadaCommand(_prio.Alta), CancellationToken.None);

        var marcadas = await _ctx.PrioridadesTicket
            .Where(p => p.EsPredeterminada).Select(p => p.Id).ToListAsync();

        marcadas.Should().ContainSingle().Which.Should().Be(_prio.Alta);
    }

    // ── Resolución al crear un ticket ─────────────────────────────
    [Fact]
    public async Task Sin_prioridad_elegida_el_ticket_toma_la_predeterminada()
    {
        var id = await PrioridadTicketResolver.ResolverAsync(_ctx, null, CancellationToken.None);
        id.Should().Be(_prio.Media);
    }

    [Fact]
    public async Task Una_prioridad_inexistente_se_rechaza()
    {
        var acto = () => PrioridadTicketResolver.ResolverAsync(_ctx, 9999, CancellationToken.None);
        await acto.Should().ThrowAsync<DomainException>().WithMessage("*no existe*");
    }

    public void Dispose() => _ctx.Dispose();
}
