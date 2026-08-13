using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Usuarios.Common;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Permisos;

/// <summary>
/// El portal no puede quedarse sin nadie capaz de administrarlo. RolesModule ya garantiza que
/// exista un ROL administrador; esto cubre el otro lado: que exista un USUARIO ACTIVO asignado
/// a alguno de esos roles.
/// </summary>
public class AdministradoresInvarianteTests : IDisposable
{
    private const string Admin    = "Administrador";
    private const string Empleado = "Empleado";

    private readonly AppDbContext _ctx;
    private readonly IRolCatalogo _catalogo = CatalogoFake.Con(
        CatalogoFake.Rol(Admin, esAdministrador: true),
        CatalogoFake.Rol(Empleado));

    public AdministradoresInvarianteTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.EsGlobal.Returns(true);
        _ctx = new AppDbContext(opts, currentUser, Substitute.For<MediatR.IPublisher>());
    }

    private async Task<Guid> SembrarUsuarioAsync(string rol, bool activo = true)
    {
        var u = Usuario.Crear($"U{Guid.NewGuid():N}", $"{Guid.NewGuid():N}@diger.gob.hn", "hash");
        if (!activo) u.Desactivar();

        _ctx.Usuarios.Add(u);
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(u.Id, "DIGER", null, null, rol));
        await _ctx.SaveChangesAsync();
        return u.Id;
    }

    [Fact]
    public async Task TieneRolAdministrador_DistingueSegunLaCapacidadDelRol()
    {
        var admin = await SembrarUsuarioAsync(Admin);
        var otro  = await SembrarUsuarioAsync(Empleado);

        (await AdministradoresInvariante.TieneRolAdministradorAsync(_ctx, _catalogo, admin, default))
            .Should().BeTrue();
        (await AdministradoresInvariante.TieneRolAdministradorAsync(_ctx, _catalogo, otro, default))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Validar_UnicoAdministrador_Lanza()
    {
        var admin = await SembrarUsuarioAsync(Admin);
        await SembrarUsuarioAsync(Empleado);

        var act = async () => await AdministradoresInvariante
            .ValidarNoEsElUltimoAdministradorAsync(_ctx, _catalogo, admin, default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*único usuario activo*");
    }

    [Fact]
    public async Task Validar_ConOtroAdministradorActivo_NoLanza()
    {
        var admin = await SembrarUsuarioAsync(Admin);
        await SembrarUsuarioAsync(Admin);

        var act = async () => await AdministradoresInvariante
            .ValidarNoEsElUltimoAdministradorAsync(_ctx, _catalogo, admin, default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Validar_OtroAdministradorInactivoNoCuenta_Lanza()
    {
        var admin = await SembrarUsuarioAsync(Admin);
        await SembrarUsuarioAsync(Admin, activo: false);

        // Un administrador desactivado no puede iniciar sesión, así que no sirve de respaldo.
        var act = async () => await AdministradoresInvariante
            .ValidarNoEsElUltimoAdministradorAsync(_ctx, _catalogo, admin, default);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Validar_SinNingunRolAdministradorEnElCatalogo_NoLanza()
    {
        var alguien = await SembrarUsuarioAsync(Empleado);
        var sinAdmin = CatalogoFake.Con(CatalogoFake.Rol(Empleado));

        // Ese caso lo cubre RolesModule (no deja quedarse sin rol administrador); acá no hay
        // nada que preservar y la guardia no debe estorbar.
        var act = async () => await AdministradoresInvariante
            .ValidarNoEsElUltimoAdministradorAsync(_ctx, sinAdmin, alguien, default);

        await act.Should().NotThrowAsync();
    }

    public void Dispose() => _ctx.Dispose();
}
