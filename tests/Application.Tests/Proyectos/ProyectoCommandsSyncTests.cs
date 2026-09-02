using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Services;
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
    private readonly IInteresadosAutomaticosSync _sync = Substitute.For<IInteresadosAutomaticosSync>();

    public ProyectoCommandsSyncTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.EsGlobal.Returns(true);
        currentUser.ActiveInstitucionId.Returns("DIGER");
        _ctx = new AppDbContext(opts, currentUser, Substitute.For<MediatR.IPublisher>());
    }

    [Fact]
    public async Task CrearProyecto_ConArea_DisparaLaSincronizacion()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.ActiveInstitucionId.Returns("DIGER");
        var handler = new CrearProyectoCommandHandler(_ctx, currentUser, _sync);

        var id = await handler.Handle(
            new CrearProyectoCommand("Proyecto de prueba", AreaId: "GOBDIGITAL"),
            CancellationToken.None);

        await _sync.Received(1).SincronizarProyectoAsync(id, Arg.Any<CancellationToken>());
    }

    public void Dispose() => _ctx.Dispose();
}
