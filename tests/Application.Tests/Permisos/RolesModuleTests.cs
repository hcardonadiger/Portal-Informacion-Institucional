using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Roles;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Permisos;

public class RolesModuleTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly IRolCatalogo _catalogo = Substitute.For<IRolCatalogo>();

    public RolesModuleTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.EsGlobal.Returns(true);
        _ctx = new AppDbContext(opts, currentUser, Substitute.For<MediatR.IPublisher>());
    }

    private async Task SembrarAsync(params Rol[] roles)
    {
        _ctx.Roles.AddRange(roles);
        await _ctx.SaveChangesAsync();
    }

    private static Rol Administrador() =>
        Rol.Crear("Administrador", "Administrador", NivelAlcance.Global, esAdministrador: true, esSistema: true);

    // ── Crear ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task Crear_CodigoDuplicado_LanzaDomainException()
    {
        await SembrarAsync(Rol.Crear("Auditor", "Auditor", NivelAlcance.Unidad));
        var handler = new CrearRolCommandHandler(_ctx, _catalogo);

        var act = async () => await handler.Handle(
            new CrearRolCommand("Auditor", "Otro auditor", NivelAlcance.Area, null, null, false, false, false, false),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Auditor*");
    }

    [Fact]
    public async Task Crear_RolNuevo_PersisteYRecargaElCatalogo()
    {
        var handler = new CrearRolCommandHandler(_ctx, _catalogo);

        var codigo = await handler.Handle(
            new CrearRolCommand("Auditor", "Auditor interno", NivelAlcance.Institucion, "Revisa", "#123456",
                false, EsSoloLectura: true, false, false),
            CancellationToken.None);

        codigo.Should().Be("Auditor");
        var guardado = await _ctx.Roles.SingleAsync(r => r.Id == "Auditor");
        guardado.NivelAlcance.Should().Be(NivelAlcance.Institucion);
        guardado.EsSoloLectura.Should().BeTrue();
        guardado.EsSistema.Should().BeFalse();

        await _catalogo.Received(1).RecargarAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_RolConJefeDeAreaYPmo_LasPersiste()
    {
        var handler = new CrearRolCommandHandler(_ctx, _catalogo);

        await handler.Handle(
            new CrearRolCommand("JefeGobDigital", "Jefe Gobierno Digital", NivelAlcance.Area, null, null,
                false, false, false, false, EsJefeDeArea: true, EsPmo: false),
            CancellationToken.None);

        var guardado = await _ctx.Roles.SingleAsync(r => r.Id == "JefeGobDigital");
        guardado.EsJefeDeArea.Should().BeTrue();
        guardado.EsPmo.Should().BeFalse();
    }

    // ── Actualizar ────────────────────────────────────────────────────────
    [Fact]
    public async Task Actualizar_QuitarAdministradorAlUltimo_LanzaDomainException()
    {
        await SembrarAsync(Administrador());
        var handler = new ActualizarRolCommandHandler(_ctx, _catalogo);

        var act = async () => await handler.Handle(
            new ActualizarRolCommand("Administrador", "Administrador", NivelAlcance.Global, null, null,
                EsAdministrador: false, false, false, false, Activo: true),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*al menos un rol activo*");
    }

    [Fact]
    public async Task Actualizar_DesactivarAlUltimoAdministrador_LanzaDomainException()
    {
        await SembrarAsync(Administrador());
        var handler = new ActualizarRolCommandHandler(_ctx, _catalogo);

        var act = async () => await handler.Handle(
            new ActualizarRolCommand("Administrador", "Administrador", NivelAlcance.Global, null, null,
                EsAdministrador: true, false, false, false, Activo: false),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*al menos un rol activo*");
    }

    [Fact]
    public async Task Actualizar_ConOtroAdministradorActivo_PermiteQuitarLaCapacidad()
    {
        await SembrarAsync(
            Administrador(),
            Rol.Crear("Superusuario", "Superusuario", NivelAlcance.Global, esAdministrador: true));

        var handler = new ActualizarRolCommandHandler(_ctx, _catalogo);

        await handler.Handle(
            new ActualizarRolCommand("Superusuario", "Ex superusuario", NivelAlcance.Area, null, null,
                EsAdministrador: false, false, EsSupervisor: true, false, Activo: true),
            CancellationToken.None);

        var guardado = await _ctx.Roles.SingleAsync(r => r.Id == "Superusuario");
        guardado.EsAdministrador.Should().BeFalse();
        guardado.EsSupervisor.Should().BeTrue();
        guardado.NivelAlcance.Should().Be(NivelAlcance.Area);
    }

    [Fact]
    public async Task Actualizar_RolInexistente_LanzaNotFound()
    {
        var handler = new ActualizarRolCommandHandler(_ctx, _catalogo);

        var act = async () => await handler.Handle(
            new ActualizarRolCommand("NoExiste", "X", NivelAlcance.Unidad, null, null, false, false, false, false, true),
            CancellationToken.None);

        await act.Should().ThrowAsync<Diger.TramitesEstado.Application.Common.Exceptions.NotFoundException>();
    }

    // ── Eliminar ──────────────────────────────────────────────────────────
    [Fact]
    public async Task Eliminar_RolDeSistema_LanzaDomainException()
    {
        await SembrarAsync(Administrador());
        var handler = new EliminarRolCommandHandler(_ctx, _catalogo);

        var act = async () => await handler.Handle(new EliminarRolCommand("Administrador"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*del sistema*");
    }

    [Fact]
    public async Task Eliminar_RolConUsuariosAsignados_LanzaDomainException()
    {
        await SembrarAsync(Rol.Crear("Auditor", "Auditor", NivelAlcance.Unidad));
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(Guid.NewGuid(), "DIGER", null, null, "Auditor"));
        await _ctx.SaveChangesAsync();

        var handler = new EliminarRolCommandHandler(_ctx, _catalogo);

        var act = async () => await handler.Handle(new EliminarRolCommand("Auditor"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*1 usuario*");
    }

    [Fact]
    public async Task Eliminar_RolLibre_LoBorraYRecargaElCatalogo()
    {
        await SembrarAsync(Rol.Crear("Auditor", "Auditor", NivelAlcance.Unidad));
        var handler = new EliminarRolCommandHandler(_ctx, _catalogo);

        await handler.Handle(new EliminarRolCommand("Auditor"), CancellationToken.None);

        (await _ctx.Roles.AnyAsync(r => r.Id == "Auditor")).Should().BeFalse();
        await _catalogo.Received(1).RecargarAsync(Arg.Any<CancellationToken>());
    }

    // ── Query ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetRoles_CuentaUsuariosAsignadosYOrdenaPorAlcance()
    {
        await SembrarAsync(
            Rol.Crear("Empleado", "Empleado", NivelAlcance.Unidad),
            Administrador());

        var uid = Guid.NewGuid();
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(uid, "DIGER", null, null, "Empleado"));
        await _ctx.SaveChangesAsync();

        var roles = await new GetRolesQueryHandler(_ctx).Handle(new GetRolesQuery(), CancellationToken.None);

        roles.Select(r => r.Codigo).Should().ContainInOrder("Administrador", "Empleado");
        roles.Single(r => r.Codigo == "Empleado").UsuariosAsignados.Should().Be(1);
        roles.Single(r => r.Codigo == "Administrador").UsuariosAsignados.Should().Be(0);
    }

    public void Dispose() => _ctx.Dispose();
}
