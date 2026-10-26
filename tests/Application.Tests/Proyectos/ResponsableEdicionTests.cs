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
/// Quién queda a cargo al guardar la ficha. Igual que en el alta, el nombre del responsable lo
/// resuelve el comando contra el padrón: la edición también tiene dos puertas —el editor y la
/// herramienta MCP <c>actualizar_ficha_proyecto</c>— y la de MCP solo manda el Id.
/// </summary>
public class ResponsableEdicionTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly CatalogoPrioridades _prio;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private readonly IInteresadosAutomaticosSync _sync = Substitute.For<IInteresadosAutomaticosSync>();

    public ResponsableEdicionTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
        _prio = PrioridadesDePrueba.Sembrar(_ctx);
        _usuario.Nombre.Returns("Henry Cardona");
    }

    private async Task<Usuario> SembrarUsuarioAsync(string nombre)
    {
        var u = Usuario.Crear(nombre, $"{Guid.NewGuid()}@diger.gob.hn", "hash");
        _ctx.Usuarios.Add(u);
        await _ctx.SaveChangesAsync();
        return u;
    }

    private async Task<int> CrearConResponsableAsync(Usuario responsable)
    {
        _usuario.UserId.Returns(responsable.Id);
        return await new CrearProyectoCommandHandler(_ctx, _usuario, _sync)
            .Handle(new CrearProyectoCommand("Expediente digital"), CancellationToken.None);
    }

    private Task GuardarAsync(int id, Guid? responsableId) =>
        new ActualizarProyectoCommandHandler(_ctx, _usuario, _sync).Handle(new ActualizarProyectoCommand(
            id, "Expediente digital", null, null, null, responsableId,
            _prio.Media, null, null, null, null, []), CancellationToken.None);

    private Task<Proyecto> LeerAsync(int id) =>
        _ctx.Proyectos.AsNoTracking().SingleAsync(p => p.Id == id);

    [Fact]
    public async Task Guardar_ConOtroResponsable_TomaElNombreDelPadron()
    {
        // El caso de MCP: antes guardaba el Id nuevo con el nombre del responsable anterior.
        var henry = await SembrarUsuarioAsync("Henry Cardona");
        var maria = await SembrarUsuarioAsync("María López");
        var id    = await CrearConResponsableAsync(henry);

        await GuardarAsync(id, maria.Id);

        var p = await LeerAsync(id);
        p.ResponsableId.Should().Be(maria.Id);
        p.Responsable.Should().Be("María López");
    }

    [Fact]
    public async Task Guardar_SinResponsable_BorraTambienElNombre()
    {
        // El editor ofrece «Sin asignar»: antes dejaba el Id en null y el nombre viejo puesto.
        var henry = await SembrarUsuarioAsync("Henry Cardona");
        var id    = await CrearConResponsableAsync(henry);

        await GuardarAsync(id, null);

        var p = await LeerAsync(id);
        p.ResponsableId.Should().BeNull();
        p.Responsable.Should().BeNull();
    }

    [Fact]
    public async Task Guardar_ConResponsableNuevoInactivo_RechazaYNoToca()
    {
        var henry = await SembrarUsuarioAsync("Henry Cardona");
        var baja  = await SembrarUsuarioAsync("Usuario de baja");
        baja.Desactivar();
        await _ctx.SaveChangesAsync();
        var id = await CrearConResponsableAsync(henry);

        var act = () => GuardarAsync(id, baja.Id);

        await act.Should().ThrowAsync<DomainException>();
        (await LeerAsync(id)).ResponsableId.Should().Be(henry.Id);
    }

    [Fact]
    public async Task Guardar_ConResponsableNuevoInexistente_Rechaza()
    {
        var henry = await SembrarUsuarioAsync("Henry Cardona");
        var id    = await CrearConResponsableAsync(henry);

        var act = () => GuardarAsync(id, Guid.NewGuid());

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Guardar_ConElMismoResponsableYaDadoDeBaja_ConservaAlResponsable()
    {
        // Se valida solo lo que cambia: que el responsable se haya ido después no puede convertir
        // cada guardado de la ficha en un error que el usuario no provocó.
        var henry = await SembrarUsuarioAsync("Henry Cardona");
        var id    = await CrearConResponsableAsync(henry);
        henry.Desactivar();
        await _ctx.SaveChangesAsync();

        await GuardarAsync(id, henry.Id);

        var p = await LeerAsync(id);
        p.ResponsableId.Should().Be(henry.Id);
        p.Responsable.Should().Be("Henry Cardona");
    }

    [Fact]
    public async Task Guardar_ConElMismoResponsableSinNombre_LoRellenaDelPadron()
    {
        // Proyectos creados por MCP antes del arreglo del alta: Id puesto, nombre vacío.
        var henry = await SembrarUsuarioAsync("Henry Cardona");
        var id    = await CrearConResponsableAsync(henry);
        var fila  = await _ctx.Proyectos.SingleAsync(p => p.Id == id);
        fila.Responsable = null;
        await _ctx.SaveChangesAsync();
        _ctx.ChangeTracker.Clear();

        await GuardarAsync(id, henry.Id);

        (await LeerAsync(id)).Responsable.Should().Be("Henry Cardona");
    }

    [Fact]
    public async Task Guardar_ConOtroResponsable_LaBitacoraDiceLosDosNombres()
    {
        var henry = await SembrarUsuarioAsync("Henry Cardona");
        var maria = await SembrarUsuarioAsync("María López");
        var id    = await CrearConResponsableAsync(henry);

        await GuardarAsync(id, maria.Id);

        var bitacora = await _ctx.BitacorasProyecto.AsNoTracking()
            .Where(b => b.ProyectoId == id)
            .Select(b => b.Detalle)
            .ToListAsync();
        bitacora.Should().Contain(d => d.Contains("responsable: Henry Cardona → María López"));
    }

    public void Dispose() => _ctx.Dispose();
}
