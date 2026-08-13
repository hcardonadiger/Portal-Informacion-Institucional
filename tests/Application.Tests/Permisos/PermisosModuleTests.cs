using System.Security.Claims;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Permisos;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Permisos;

/// <summary>Catálogo de roles falso: cualquier código que no se registre acá devuelve null,
/// que es como el catálogo real reporta un rol inexistente o inactivo.</summary>
internal static class CatalogoFake
{
    public static RolInfo Rol(string codigo, bool esAdministrador = false, bool esSoloLectura = false) =>
        new(codigo, codigo, NivelAlcance.Unidad, esAdministrador, esSoloLectura, false, false, null);

    public static IRolCatalogo Con(params RolInfo[] roles)
    {
        var catalogo = Substitute.For<IRolCatalogo>();
        catalogo.Activos().Returns(roles);
        foreach (var r in roles)
            catalogo.Obtener(r.Codigo).Returns(r);
        return catalogo;
    }
}

public class GuardarMatrizPermisosCommandTests : IDisposable
{
    private const string JefeArea  = "JefeArea";
    private const string Empleado  = "Empleado";
    private const string Admin     = "Administrador";
    private const string Consultor = "Consultor";

    private readonly AppDbContext _ctx;
    private readonly IPermissionCache _cache = Substitute.For<IPermissionCache>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IRolCatalogo _catalogo = CatalogoFake.Con(
        CatalogoFake.Rol(Admin, esAdministrador: true),
        CatalogoFake.Rol(JefeArea),
        CatalogoFake.Rol(Empleado),
        CatalogoFake.Rol(Consultor, esSoloLectura: true));

    public GuardarMatrizPermisosCommandTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var fakeCurrentUserParaContexto = Substitute.For<ICurrentUserService>();
        fakeCurrentUserParaContexto.EsGlobal.Returns(true);
        _ctx = new AppDbContext(opts, fakeCurrentUserParaContexto, Substitute.For<MediatR.IPublisher>());

        _currentUser.Nombre.Returns("Admin de Prueba");
    }

    private GuardarMatrizPermisosCommandHandler CrearHandler() =>
        new(_ctx, _cache, _currentUser, _catalogo);

    /// <summary>Siembra el catálogo tomando la acción del sufijo de la clave, igual que hace
    /// PermissionAttribute: "Tickets.Ver" ⇒ AccionModulo.Ver.</summary>
    private async Task SembrarPermisosAsync(params string[] claves)
    {
        foreach (var clave in claves)
        {
            var accion = Enum.TryParse<AccionModulo>(clave.Split('.').Last(), out var a) ? a : AccionModulo.Editar;
            _ctx.Permisos.Add(Permiso.Crear(clave, clave, clave.Split('.')[0], accion));
        }
        await _ctx.SaveChangesAsync();
    }

    private static GuardarMatrizPermisosCommand Comando(params (string Rol, string[] Claves)[] filas) =>
        new(filas.ToDictionary(f => f.Rol, f => (IReadOnlyList<string>)f.Claves));

    [Fact]
    public async Task Handle_RolAdministradorEnGrants_LanzaDomainException()
    {
        await SembrarPermisosAsync(PermisosConstants.GestionarPermisos);

        var act = async () => await CrearHandler().Handle(
            Comando((Admin, [PermisosConstants.GestionarPermisos])), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*administrador*");
    }

    [Fact]
    public async Task Handle_RolFueraDelCatalogo_LanzaDomainException()
    {
        await SembrarPermisosAsync(PermisosConstants.GestionarPermisos);

        var act = async () => await CrearHandler().Handle(
            Comando(("RolInventado", [PermisosConstants.GestionarPermisos])), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*no existe o está inactivo*");
    }

    [Fact]
    public async Task Handle_ConAdministradorActivo_NoExigeQueUnRolConserveGestionarPermisos()
    {
        // Este es el caso normal y el que rompía la pantalla: ningún rol configurable tiene
        // Accesos.Permisos.Editar porque los administradores aprueban por código. Si la
        // guardia lo exigiera igual, NINGÚN guardado sería posible.
        await SembrarPermisosAsync(PermisosConstants.GestionarPermisos, "Tickets.Editar");

        var act = async () => await CrearHandler().Handle(
            Comando((JefeArea, ["Tickets.Editar"])), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_SinAdministradorActivoYSinNadieQueGestione_LanzaDomainException()
    {
        await SembrarPermisosAsync(PermisosConstants.GestionarPermisos, "Tickets.Editar");

        // Catálogo sin ningún rol administrador: acá la única forma de volver a administrar
        // permisos es que un rol configurable conserve la clave.
        var sinAdmin = CatalogoFake.Con(CatalogoFake.Rol(JefeArea), CatalogoFake.Rol(Empleado));
        var handler = new GuardarMatrizPermisosCommandHandler(_ctx, _cache, _currentUser, sinAdmin);

        var act = async () => await handler.Handle(
            Comando((JefeArea, ["Tickets.Editar"])), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*último rol*");
    }

    [Fact]
    public async Task Handle_SinAdministradorPeroOtroRolFueraDelPayloadLaConserva_NoLanza()
    {
        await SembrarPermisosAsync(PermisosConstants.GestionarPermisos, "Tickets.Editar");
        _ctx.RolPermisos.Add(RolPermiso.Crear(Empleado, PermisosConstants.GestionarPermisos));
        await _ctx.SaveChangesAsync();

        var sinAdmin = CatalogoFake.Con(CatalogoFake.Rol(JefeArea), CatalogoFake.Rol(Empleado));
        var handler = new GuardarMatrizPermisosCommandHandler(_ctx, _cache, _currentUser, sinAdmin);

        // El post solo trae la columna de JefeArea; Empleado no viene y mantiene la clave.
        var act = async () => await handler.Handle(
            Comando((JefeArea, ["Tickets.Editar"])), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_OtorgaPermiso_PersisteAuditaEInvalidaCache()
    {
        await SembrarPermisosAsync(PermisosConstants.GestionarPermisos, "Tickets.Editar");

        await CrearHandler().Handle(
            Comando((JefeArea, [PermisosConstants.GestionarPermisos, "Tickets.Editar"])),
            CancellationToken.None);

        var otorgados = await _ctx.RolPermisos.Where(p => p.RolId == JefeArea).ToListAsync();
        otorgados.Select(o => o.PermisoClave).Should().BeEquivalentTo(
            [PermisosConstants.GestionarPermisos, "Tickets.Editar"]);

        var auditoria = await _ctx.PermisosAuditoria.ToListAsync();
        auditoria.Should().HaveCount(2);
        auditoria.Should().OnlyContain(a => a.Accion == AccionPermiso.Otorgado && a.Actor == "Admin de Prueba");

        _cache.Received(1).Invalidar(JefeArea);
    }

    [Fact]
    public async Task Handle_RevocaPermisoExistente_LoQuitaYAudita()
    {
        await SembrarPermisosAsync(PermisosConstants.GestionarPermisos, "Tickets.Editar");
        _ctx.RolPermisos.Add(RolPermiso.Crear(Empleado, "Tickets.Editar"));
        await _ctx.SaveChangesAsync();

        await CrearHandler().Handle(
            Comando((Empleado, []),                                       // se quita Tickets.Editar
                    (JefeArea, [PermisosConstants.GestionarPermisos])),   // sigue cubierto
            CancellationToken.None);

        (await _ctx.RolPermisos.Where(p => p.RolId == Empleado).ToListAsync()).Should().BeEmpty();

        var auditoria = await _ctx.PermisosAuditoria.Where(a => a.RolId == Empleado).ToListAsync();
        auditoria.Should().ContainSingle(a => a.Accion == AccionPermiso.Revocado && a.PermisoClave == "Tickets.Editar");
    }

    [Fact]
    public async Task Handle_RolSoloLecturaConClaveDeMutacion_LanzaDomainException()
    {
        await SembrarPermisosAsync(PermisosConstants.GestionarPermisos, "Tickets.Editar", "Tickets.Ver");

        // La pantalla deshabilita esas casillas, pero eso es UI: el comando tiene que
        // rechazarlas igual ante un POST armado a mano.
        var act = async () => await CrearHandler().Handle(
            Comando((Consultor, ["Tickets.Ver", "Tickets.Editar"]),
                    (JefeArea,  [PermisosConstants.GestionarPermisos])),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*solo lectura*");
    }

    [Fact]
    public async Task Handle_RolSoloLecturaSoloConClavesDeConsulta_LasOtorga()
    {
        await SembrarPermisosAsync(PermisosConstants.GestionarPermisos, "Tickets.Ver");

        await CrearHandler().Handle(
            Comando((Consultor, ["Tickets.Ver"]),
                    (JefeArea,  [PermisosConstants.GestionarPermisos])),
            CancellationToken.None);

        (await _ctx.RolPermisos.Where(p => p.RolId == Consultor).Select(p => p.PermisoClave).ToListAsync())
            .Should().BeEquivalentTo(["Tickets.Ver"]);
    }

    [Fact]
    public void GestionarPermisos_CoincideConLaClaveQueEmiteElAtributo()
    {
        // Si alguien cambia el módulo o la acción de la página de permisos, la guardia
        // anti-bloqueo dejaría de proteger la clave real sin que nada más lo note.
        new PermissionAttribute(PermisosConstants.ModuloPermisos, AccionModulo.Editar)
            .Clave.Should().Be(PermisosConstants.GestionarPermisos);
    }

    public void Dispose() => _ctx.Dispose();
}

public class PermissionAuthorizationHandlerTests
{
    private static async Task<AuthorizationHandlerContext> EjecutarAsync(
        PermissionAuthorizationHandler handler, string clave)
    {
        var requirement = new PermissionRequirement(clave);
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(), null);
        await handler.HandleAsync(context);
        return context;
    }

    [Fact]
    public async Task RolAdministrador_SiempreAprueba_AunConCacheVacia()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.Rol.Returns("Administrador");
        currentUser.EsGlobal.Returns(true);
        var cache = Substitute.For<IPermissionCache>();

        var handler = new PermissionAuthorizationHandler(currentUser, cache);

        var resultado = await EjecutarAsync(handler, "Cualquier.Permiso");

        resultado.HasSucceeded.Should().BeTrue();
        await cache.DidNotReceive().ObtenerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RolConPermisoEnCache_Aprueba()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.Rol.Returns("JefeArea");
        var cache = Substitute.For<IPermissionCache>();
        cache.ObtenerAsync("JefeArea", Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(new HashSet<string> { "Tickets.Editar" }));

        var handler = new PermissionAuthorizationHandler(currentUser, cache);

        var resultado = await EjecutarAsync(handler, "Tickets.Editar");

        resultado.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task RolSinPermisoEnCache_NoAprueba()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.Rol.Returns("Consultor");
        var cache = Substitute.For<IPermissionCache>();
        cache.ObtenerAsync("Consultor", Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(new HashSet<string>()));

        var handler = new PermissionAuthorizationHandler(currentUser, cache);

        var resultado = await EjecutarAsync(handler, "Tickets.Editar");

        resultado.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task SinRolResuelto_NoAprueba()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.Rol.Returns((string?)null);
        var cache = Substitute.For<IPermissionCache>();

        var handler = new PermissionAuthorizationHandler(currentUser, cache);

        var resultado = await EjecutarAsync(handler, "Tickets.Editar");

        resultado.HasSucceeded.Should().BeFalse();
    }
}
