using Diger.TramitesEstado.Application.Dashboards.Queries;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Dashboards;

public class GetProyectosDashboardQueryAreaTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public GetProyectosDashboardQueryAreaTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        // FakeCurrentUser y no un mock: NSubstitute devuelve "" —no null— para los string sin
        // configurar, y la inyección automática de jerarquía de AppDbContext escribiría ese ""
        // en AreaId/UnidadId de cada fila insertada, justo lo que este test mide.
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
    }

    [Fact]
    public async Task FiltraPorUnaOVariasAreas()
    {
        await SembrarProyectosAsync();

        var resultado = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(AreaIds: ["SIGER", "GOBDIGITAL"]), CancellationToken.None);

        // El proyecto sin área queda fuera: filtrar por área es pedir esas áreas, no «esas o ninguna».
        resultado.Semaforo.Select(s => s.Codigo).Should().BeEquivalentTo("PRY-2026-20", "PRY-2026-21");
    }

    [Fact]
    public async Task SinAreasNoFiltra()
    {
        await SembrarProyectosAsync();

        var sinParametro = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(), CancellationToken.None);
        var listaVacia = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(AreaIds: []), CancellationToken.None);

        sinParametro.Semaforo.Select(s => s.Codigo)
            .Should().BeEquivalentTo("PRY-2026-20", "PRY-2026-21", "PRY-2026-22", "PRY-2026-23");
        listaVacia.Semaforo.Select(s => s.Codigo)
            .Should().BeEquivalentTo("PRY-2026-20", "PRY-2026-21", "PRY-2026-22", "PRY-2026-23");
    }

    [Fact]
    public async Task AreaDesconocidaNoDevuelveNada()
    {
        await SembrarProyectosAsync();

        var resultado = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(AreaIds: ["NOEXISTE"]), CancellationToken.None);

        resultado.Semaforo.Should().BeEmpty();
    }

    /// <summary>Tres proyectos en tres áreas distintas, más uno sin área asignada.</summary>
    private async Task SembrarProyectosAsync()
    {
        var siger = Proyecto.Crear("PRY-2026-20", "SIGER"); siger.AreaId = "SIGER";
        var gobdigital = Proyecto.Crear("PRY-2026-21", "GobDigital"); gobdigital.AreaId = "GOBDIGITAL";
        var otra = Proyecto.Crear("PRY-2026-22", "Otra"); otra.AreaId = "RRHH";
        var sinArea = Proyecto.Crear("PRY-2026-23", "Sin área");
        _ctx.Proyectos.AddRange(siger, gobdigital, otra, sinArea);
        await _ctx.SaveChangesAsync();
    }

    public void Dispose() => _ctx.Dispose();
}
