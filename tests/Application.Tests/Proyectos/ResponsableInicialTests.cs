using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Services;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

/// <summary>
/// Quién queda a cargo de un proyecto recién creado. La regla vive en el comando y no en la
/// página porque el alta tiene dos puertas —el modal del listado y la herramienta MCP
/// <c>crear_proyecto</c>— y las dos tienen que comportarse igual.
/// </summary>
public class ResponsableInicialTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private readonly IInteresadosAutomaticosSync _sync = Substitute.For<IInteresadosAutomaticosSync>();

    public ResponsableInicialTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
        PrioridadesDePrueba.Sembrar(_ctx);
    }

    private async Task<Usuario> SembrarUsuarioAsync(string nombre, bool activo = true)
    {
        var u = Usuario.Crear(nombre, $"{Guid.NewGuid()}@diger.gob.hn", "hash");
        if (!activo) u.Desactivar();
        _ctx.Usuarios.Add(u);
        await _ctx.SaveChangesAsync();
        return u;
    }

    private Task<int> CrearAsync(CrearProyectoCommand cmd) =>
        new CrearProyectoCommandHandler(_ctx, _usuario, _sync).Handle(cmd, CancellationToken.None);

    [Fact]
    public async Task Crear_SinResponsableElegido_QuedaAQuienLoCrea()
    {
        var creador = await SembrarUsuarioAsync("Henry Cardona");
        _usuario.UserId.Returns(creador.Id);

        var id = await CrearAsync(new CrearProyectoCommand("Expediente digital"));

        var p = await _ctx.Proyectos.AsNoTracking().SingleAsync(x => x.Id == id);
        p.ResponsableId.Should().Be(creador.Id);
        p.Responsable.Should().Be("Henry Cardona");
    }

    [Fact]
    public async Task Crear_ConResponsableElegido_TomaElNombreDelPadron()
    {
        // Es el caso de la herramienta MCP: manda el Id y nada más. Antes el proyecto quedaba con
        // dueño de verdad y el listado decía «sin asignar».
        var creador = await SembrarUsuarioAsync("Henry Cardona");
        var otro    = await SembrarUsuarioAsync("María López");
        _usuario.UserId.Returns(creador.Id);

        var id = await CrearAsync(new CrearProyectoCommand("Expediente digital", ResponsableId: otro.Id));

        var p = await _ctx.Proyectos.AsNoTracking().SingleAsync(x => x.Id == id);
        p.ResponsableId.Should().Be(otro.Id);
        p.Responsable.Should().Be("María López");
    }

    [Fact]
    public async Task Crear_ConResponsableInactivo_Rechaza()
    {
        var inactivo = await SembrarUsuarioAsync("Usuario de baja", activo: false);

        var act = () => CrearAsync(new CrearProyectoCommand("Expediente digital", ResponsableId: inactivo.Id));

        await act.Should().ThrowAsync<DomainException>();
        (await _ctx.Proyectos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Crear_ConResponsableInexistente_Rechaza()
    {
        var act = () => CrearAsync(new CrearProyectoCommand("Expediente digital", ResponsableId: Guid.NewGuid()));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Crear_SinUsuarioIdentificado_QuedaSinResponsable()
    {
        var id = await CrearAsync(new CrearProyectoCommand("Expediente digital"));

        var p = await _ctx.Proyectos.AsNoTracking().SingleAsync(x => x.Id == id);
        p.ResponsableId.Should().BeNull();
        p.Responsable.Should().BeNull();
    }

    public void Dispose() => _ctx.Dispose();
}
