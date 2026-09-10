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

    [Fact]
    public async Task DeUsuarioIdAcotaAResponsableOInteresado()
    {
        await SembrarProyectosAsync();

        var yo = Guid.NewGuid();
        var siger = await _ctx.Proyectos.SingleAsync(p => p.Codigo == "PRY-2026-20");
        siger.ResponsableId = yo;

        var gobdigital = await _ctx.Proyectos.SingleAsync(p => p.Codigo == "PRY-2026-21");
        _ctx.ProyectoInteresados.Add(InteresadoProyecto.Crear(
            gobdigital.Id, yo, "Yo", RolInteresado.Ejecutor, "Pruebas"));
        await _ctx.SaveChangesAsync();

        var resultado = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(DeUsuarioId: yo), CancellationToken.None);

        // Las dos maneras de ser «mío», y nada más: es el mismo predicado que usa el nivel Unidad.
        resultado.Semaforo.Select(s => s.Codigo).Should().BeEquivalentTo("PRY-2026-20", "PRY-2026-21");
    }

    [Fact]
    public async Task SinDeUsuarioIdNoAcota()
    {
        await SembrarProyectosAsync();

        var resultado = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(), CancellationToken.None);

        // null significa «sin acotar», no «de nadie»: si se confundieran, el tablero de
        // institución saldría vacío.
        resultado.Semaforo.Should().HaveCount(4);
    }

    [Fact]
    public async Task LaUnidadViajaEnElSemaforo()
    {
        await SembrarProyectosAsync();

        var siger = await _ctx.Proyectos.SingleAsync(p => p.Codigo == "PRY-2026-20");
        siger.UnidadId = "UNI-A";
        _ctx.Unidades.Add(Unidad.Crear("UNI-A", "SIGER", "Unidad A"));
        await _ctx.SaveChangesAsync();

        var resultado = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(), CancellationToken.None);

        var conUnidad = resultado.Semaforo.Single(s => s.Codigo == "PRY-2026-20");
        conUnidad.UnidadId.Should().Be("UNI-A");
        conUnidad.UnidadNombre.Should().NotBeNull("el catálogo la resuelve");

        // El que no tiene unidad la lleva en null por los dos lados: es lo que permite al tablero
        // de área separarlo del que sí tiene unidad pero cuyo nombre no resuelve.
        var sinUnidad = resultado.Semaforo.Single(s => s.Codigo == "PRY-2026-23");
        sinUnidad.UnidadId.Should().BeNull();
        sinUnidad.UnidadNombre.Should().BeNull();
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
