using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Services;
using Diger.TramitesEstado.Application.Tests.Permisos;
using Diger.TramitesEstado.Application.Usuarios.Commands.AsignarInstitucionesUsuario;
using Diger.TramitesEstado.Application.Usuarios.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Usuarios;

/// <summary>
/// Reasignar a alguien cambia su área/unidad y, con eso, en qué proyectos califica como interesado
/// automático. El handler tiene que avisarle al sync después de guardar; si no, el padrón de
/// interesados queda con la foto vieja hasta que alguien vuelva a tocar el proyecto.
/// </summary>
public class AsignarJerarquiaUsuarioSyncTests : IDisposable
{
    private const string Empleado = "Empleado";

    private readonly AppDbContext _ctx;
    private readonly IUsuarioRepository _repo = Substitute.For<IUsuarioRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IInteresadosAutomaticosSync _sync = Substitute.For<IInteresadosAutomaticosSync>();
    private readonly IRolCatalogo _catalogo = CatalogoFake.Con(
        CatalogoFake.Rol("Administrador", esAdministrador: true),
        CatalogoFake.Rol(Empleado));

    public AsignarJerarquiaUsuarioSyncTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.EsGlobal.Returns(true);
        _ctx = new AppDbContext(opts, currentUser, Substitute.For<MediatR.IPublisher>());
    }

    [Fact]
    public async Task AsignarJerarquia_SincronizaLosInteresadosAutomaticosDelUsuario()
    {
        var usuario = Usuario.Crear("Empleado de prueba", $"{Guid.NewGuid():N}@diger.gob.hn", "hash");
        _repo.GetByIdAsync(usuario.Id, Arg.Any<CancellationToken>()).Returns(usuario);

        var handler = new AsignarJerarquiaUsuarioCommandHandler(_repo, _uow, _ctx, _catalogo, _sync);
        var asignaciones = new List<AsignacionDto> { new("DIGER", "GOBDIGITAL", null) };

        await handler.Handle(
            new AsignarJerarquiaUsuarioCommand(usuario.Id, Empleado, asignaciones),
            CancellationToken.None);

        await _repo.Received(1).ReemplazarAsignacionesAsync(
            usuario.Id, Empleado, Arg.Any<IEnumerable<AsignacionDto>>(), Arg.Any<CancellationToken>());
        await _sync.Received(1).SincronizarUsuarioAsync(usuario.Id, Arg.Any<CancellationToken>());
    }

    public void Dispose() => _ctx.Dispose();
}
