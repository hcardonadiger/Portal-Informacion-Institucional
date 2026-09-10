using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Dashboards.Queries.GetMisProyectosDashboard;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Dashboards;

public class GetMisProyectosDashboardQueryTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly Guid _usuarioId = Guid.NewGuid();

    public GetMisProyectosDashboardQueryTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        // FakeCurrentUser y no un mock: NSubstitute devuelve "" —no null— para los string sin
        // configurar, y la inyección automática de jerarquía de AppDbContext escribiría ese ""
        // en AreaId/UnidadId de cada fila insertada.
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
    }

    private GetMisProyectosDashboardQueryHandler Handler()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(_usuarioId);
        return new GetMisProyectosDashboardQueryHandler(_ctx, currentUser);
    }

    [Fact]
    public async Task SoloTraeProyectosDondeElUsuarioEsInteresadoOResponsable()
    {
        var mio = Proyecto.Crear("PRY-2026-10", "Mío");
        var ajeno = Proyecto.Crear("PRY-2026-11", "Ajeno");
        _ctx.Proyectos.AddRange(mio, ajeno);
        await _ctx.SaveChangesAsync();

        _ctx.ProyectoInteresados.Add(InteresadoProyecto.Crear(
            mio.Id, _usuarioId, "Yo", RolInteresado.Ejecutor, "Prueba"));
        await _ctx.SaveChangesAsync();

        var resultado = await Handler().Handle(new GetMisProyectosDashboardQuery(), CancellationToken.None);

        resultado.TotalProyectos.Should().Be(1);
        resultado.Proyectos.Single().Codigo.Should().Be("PRY-2026-10");
    }

    [Fact]
    public async Task IncluyeElProyectoDelQueElUsuarioEsResponsableAunqueNoSeaInteresado()
    {
        var aCargo = Proyecto.Crear("PRY-2026-12", "A cargo");
        aCargo.ResponsableId = _usuarioId;
        _ctx.Proyectos.AddRange(aCargo, Proyecto.Crear("PRY-2026-13", "Ajeno"));
        await _ctx.SaveChangesAsync();

        var resultado = await Handler().Handle(new GetMisProyectosDashboardQuery(), CancellationToken.None);

        resultado.TotalProyectos.Should().Be(1);
        resultado.Proyectos.Single().Codigo.Should().Be("PRY-2026-12");
    }

    [Fact]
    public async Task MarcaAtrasadoYSinReportarYEncabezaLoAtrasado()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        _ctx.Unidades.Add(Unidad.Crear("UNI1", "AREA1", "Unidad de Sistemas"));

        var vencido = Proyecto.Crear("PRY-2026-20", "Vencido y callado");
        vencido.CambiarEstado(EstadoProyecto.EnEjecucion, "Prueba");
        vencido.FechaFinPlan = hoy.AddDays(-5);
        vencido.UnidadId = "UNI1";

        var alDia = Proyecto.Crear("PRY-2026-21", "Al día");
        alDia.CambiarEstado(EstadoProyecto.EnEjecucion, "Prueba");
        alDia.FechaFinPlan = hoy.AddDays(10);

        // Planificado: vencido igual, pero «sin reportar» solo aplica a lo que está en ejecución.
        var planificado = Proyecto.Crear("PRY-2026-22", "Planificado vencido");
        planificado.FechaFinPlan = hoy.AddDays(-3);

        _ctx.Proyectos.AddRange(vencido, alDia, planificado);
        await _ctx.SaveChangesAsync();

        foreach (var p in new[] { vencido, alDia, planificado })
            _ctx.ProyectoInteresados.Add(InteresadoProyecto.Crear(
                p.Id, _usuarioId, "Yo", RolInteresado.Ejecutor, "Prueba"));
        _ctx.ProyectoAvances.Add(AvanceProyecto.Crear(alDia.Id, null, null, "Reporte de hoy", null, "Yo"));
        await _ctx.SaveChangesAsync();

        var resultado = await Handler().Handle(new GetMisProyectosDashboardQuery(), CancellationToken.None);

        resultado.TotalProyectos.Should().Be(3);
        resultado.Atrasados.Should().Be(2);
        resultado.SinReportar30.Should().Be(1);
        resultado.Proyectos.Select(p => p.Codigo).Should().Equal("PRY-2026-20", "PRY-2026-22", "PRY-2026-21");
        // Unidad.Crear normaliza el nombre a mayúsculas; el tablero muestra lo que hay en el catálogo.
        resultado.Proyectos.Single(p => p.Codigo == "PRY-2026-20").UnidadNombre.Should().Be("UNIDAD DE SISTEMAS");
        resultado.Proyectos.Single(p => p.Codigo == "PRY-2026-21").UnidadNombre.Should().BeNull();
    }

    public void Dispose() => _ctx.Dispose();
}
