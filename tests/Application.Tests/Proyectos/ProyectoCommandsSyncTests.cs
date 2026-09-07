using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Services;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

public class ProyectoCommandsSyncTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private readonly IInteresadosAutomaticosSync _sync = Substitute.For<IInteresadosAutomaticosSync>();

    public ProyectoCommandsSyncTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        // FakeCurrentUser (alcance global, área y unidad en null) y no un Substitute: un
        // Substitute devuelve cadena vacía en ActiveAreaId/ActiveUnidadId, y la inyección
        // automática de jerarquía de AppDbContext dejaría el proyecto con AreaId/UnidadId = ""
        // en vez de null. Con eso, «no cambió el alcance» nunca se cumpliría.
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

        _usuario.ActiveInstitucionId.Returns("DIGER");
        _usuario.Nombre.Returns("Henry Cardona");
    }

    [Fact]
    public async Task CrearProyecto_ConArea_DisparaLaSincronizacion()
    {
        var handler = new CrearProyectoCommandHandler(_ctx, _usuario, _sync);

        var id = await handler.Handle(
            new CrearProyectoCommand("Proyecto de prueba", AreaId: "GOBDIGITAL"),
            CancellationToken.None);

        await _sync.Received(1).SincronizarProyectoAsync(id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Un proyecto transversal (sin área ni unidad) no tiene de dónde sacar interesados
    /// automáticos: llamar al sync sería trabajo en vano. Este test cuida el <c>if</c> del handler.
    /// </summary>
    [Fact]
    public async Task CrearProyecto_SinAreaNiUnidad_NoDisparaLaSincronizacion()
    {
        var handler = new CrearProyectoCommandHandler(_ctx, _usuario, _sync);

        await handler.Handle(new CrearProyectoCommand("Proyecto transversal"), CancellationToken.None);

        await _sync.DidNotReceive().SincronizarProyectoAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Cambiar el área cambia quién califica como interesado automático, así que tiene que
    /// disparar la sincronización. La comparación vive ANTES de la mutación en el handler; si
    /// alguien la mueve después queda siempre en falso y este test es lo que lo delata.
    /// </summary>
    [Fact]
    public async Task ActualizarProyecto_CambiaElArea_DisparaLaSincronizacion()
    {
        var id = await CrearAsync(area: "GOBDIGITAL");

        await ActualizarAsync(id, area: "PLANIFICACION", objetivo: null);

        await _sync.Received(1).SincronizarProyectoAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarProyecto_CambiaLaUnidad_DisparaLaSincronizacion()
    {
        var id = await CrearAsync(area: "GOBDIGITAL");

        await ActualizarAsync(id, area: "GOBDIGITAL", unidad: "PMO", objetivo: null);

        await _sync.Received(1).SincronizarProyectoAsync(id, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Guardar la ficha tocando cualquier otra cosa —el objetivo, el responsable— deja el alcance
    /// igual: los interesados automáticos ya son los correctos y no hay nada que recalcular.
    /// </summary>
    [Fact]
    public async Task ActualizarProyecto_SinCambioDeAreaNiUnidad_NoDisparaLaSincronizacion()
    {
        var id = await CrearAsync(area: "GOBDIGITAL");

        await ActualizarAsync(id, area: "GOBDIGITAL", objetivo: "Objetivo nuevo", responsable: "Otra persona");

        var proyecto = await _ctx.Proyectos.FindAsync(id);
        proyecto!.Objetivo.Should().Be("Objetivo nuevo");
        await _sync.DidNotReceive().SincronizarProyectoAsync(id, Arg.Any<CancellationToken>());
    }

    /// <summary>Crea el proyecto y borra las llamadas de esa etapa: cada test de actualización
    /// mide solo lo que dispara SU guardado.</summary>
    private async Task<int> CrearAsync(string? area = null, string? unidad = null)
    {
        var id = await new CrearProyectoCommandHandler(_ctx, _usuario, _sync)
            .Handle(new CrearProyectoCommand("Proyecto de prueba", AreaId: area, UnidadId: unidad),
                    CancellationToken.None);
        _sync.ClearReceivedCalls();
        return id;
    }

    private Task ActualizarAsync(
        int id, string? area = null, string? unidad = null, string? objetivo = null, string? responsable = null) =>
        new ActualizarProyectoCommandHandler(_ctx, _usuario, _sync).Handle(
            new ActualizarProyectoCommand(
                id, "Proyecto de prueba", objetivo, area, unidad, null, responsable,
                PrioridadProyecto.Media, null, null, null, []),
            CancellationToken.None);

    public void Dispose() => _ctx.Dispose();
}
