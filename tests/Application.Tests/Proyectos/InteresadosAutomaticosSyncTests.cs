using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Services;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

public class InteresadosAutomaticosSyncTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly IRolCatalogo _catalogo = Substitute.For<IRolCatalogo>();
    private readonly InteresadosAutomaticosSyncService _sync;

    public InteresadosAutomaticosSyncTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.EsGlobal.Returns(true);
        _ctx = new AppDbContext(opts, currentUser, Substitute.For<MediatR.IPublisher>());
        _sync = new InteresadosAutomaticosSyncService(_ctx, _catalogo);
    }

    private async Task<Usuario> SembrarUsuarioAsync(string nombre)
    {
        var usuario = Usuario.Crear(nombre, $"{Guid.NewGuid()}@diger.gob.hn", "hash");
        _ctx.Usuarios.Add(usuario);
        await _ctx.SaveChangesAsync();
        return usuario;
    }

    [Fact]
    public async Task SincronizarProyecto_AgregaAlJefeDeAreaComoInteresadoAutomatico()
    {
        var jefe = await SembrarUsuarioAsync("Jefe de Área");
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefe.Id, "DIGER", "GOBDIGITAL", null, "JefeArea"));
        _catalogo.Obtener("JefeArea").Returns(new RolInfo(
            "JefeArea", "Jefe de Área", NivelAlcance.Area, false, false, false, false,
            EsJefeDeArea: true, EsPmo: false, Color: null));

        var proyecto = Proyecto.Crear("PRY-2026-01", "Proyecto de prueba");
        proyecto.AreaId = "GOBDIGITAL";
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        await _sync.SincronizarProyectoAsync(proyecto.Id);

        var interesados = await _ctx.ProyectoInteresados.Where(i => i.ProyectoId == proyecto.Id).ToListAsync();
        interesados.Should().ContainSingle(i => i.UsuarioId == jefe.Id && i.Automatico && i.Rol == RolInteresado.Patrocinador);
    }

    [Fact]
    public async Task SincronizarProyecto_QuitaAlQueYaNoCalifica()
    {
        var exJefe = await SembrarUsuarioAsync("Ex Jefe");
        var proyecto = Proyecto.Crear("PRY-2026-02", "Proyecto de prueba");
        proyecto.AreaId = "GOBDIGITAL";
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        _ctx.ProyectoInteresados.Add(InteresadoProyecto.CrearAutomatico(
            proyecto.Id, exJefe.Id, exJefe.Nombre, RolInteresado.Patrocinador, null));
        await _ctx.SaveChangesAsync();

        // Sin AsignacionUsuario para exJefe en GOBDIGITAL: ya no califica.
        await _sync.SincronizarProyectoAsync(proyecto.Id);

        (await _ctx.ProyectoInteresados.AnyAsync(i => i.ProyectoId == proyecto.Id && i.UsuarioId == exJefe.Id))
            .Should().BeFalse();
    }

    [Fact]
    public async Task SincronizarProyecto_NoTocaUnInteresadoManualDelMismoUsuario()
    {
        var jefe = await SembrarUsuarioAsync("Jefe de Área");
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefe.Id, "DIGER", "GOBDIGITAL", null, "JefeArea"));
        _catalogo.Obtener("JefeArea").Returns(new RolInfo(
            "JefeArea", "Jefe de Área", NivelAlcance.Area, false, false, false, false,
            EsJefeDeArea: true, EsPmo: false, Color: null));

        var proyecto = Proyecto.Crear("PRY-2026-03", "Proyecto de prueba");
        proyecto.AreaId = "GOBDIGITAL";
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        var manual = InteresadoProyecto.Crear(proyecto.Id, jefe.Id, jefe.Nombre, RolInteresado.Ejecutor, "Alguien");
        _ctx.ProyectoInteresados.Add(manual);
        await _ctx.SaveChangesAsync();

        await _sync.SincronizarProyectoAsync(proyecto.Id);

        var fila = await _ctx.ProyectoInteresados.SingleAsync(i => i.ProyectoId == proyecto.Id && i.UsuarioId == jefe.Id);
        fila.Automatico.Should().BeFalse();
        fila.Rol.Should().Be(RolInteresado.Ejecutor);
    }

    [Fact]
    public async Task SincronizarUsuario_AgregaATodosLosProyectosDeSuArea()
    {
        var jefe = await SembrarUsuarioAsync("Jefe de Área");
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefe.Id, "DIGER", "SIGER", null, "JefeArea"));
        _catalogo.Obtener("JefeArea").Returns(new RolInfo(
            "JefeArea", "Jefe de Área", NivelAlcance.Area, false, false, false, false,
            EsJefeDeArea: true, EsPmo: false, Color: null));

        var p1 = Proyecto.Crear("PRY-2026-04", "Uno"); p1.AreaId = "SIGER";
        var p2 = Proyecto.Crear("PRY-2026-05", "Dos"); p2.AreaId = "SIGER";
        var p3 = Proyecto.Crear("PRY-2026-06", "Otra área"); p3.AreaId = "GOBDIGITAL";
        _ctx.Proyectos.AddRange(p1, p2, p3);
        await _ctx.SaveChangesAsync();

        await _sync.SincronizarUsuarioAsync(jefe.Id);

        var proyectosDelJefe = await _ctx.ProyectoInteresados
            .Where(i => i.UsuarioId == jefe.Id).Select(i => i.ProyectoId).ToListAsync();
        proyectosDelJefe.Should().BeEquivalentTo([p1.Id, p2.Id]);
    }

    [Fact]
    public async Task SincronizarUsuario_IgnoraElAlcanceDelUsuarioQueDisparaLaSincronizacion()
    {
        // El actor que dispara la sincronización (p.ej. quien edita la jerarquía de otro usuario)
        // puede no tener alcance global. La sincronización tiene que ver TODOS los proyectos del
        // área/unidad en cuestión, no solo los que el actor podría ver — de lo contrario un
        // administrador de área deja fuera, en silencio, los proyectos de otras instituciones.
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var actorAcotado = Substitute.For<ICurrentUserService>();
        actorAcotado.EsGlobal.Returns(false);
        actorAcotado.ActiveInstitucionId.Returns("OTRA-INST");
        actorAcotado.NivelAlcance.Returns(NivelAlcance.Institucion);
        using var ctxAcotado = new AppDbContext(opts, actorAcotado, Substitute.For<MediatR.IPublisher>());
        var syncAcotado = new InteresadosAutomaticosSyncService(ctxAcotado, _catalogo);

        var jefe = Usuario.Crear("Jefe de Área", $"{Guid.NewGuid()}@diger.gob.hn", "hash");
        ctxAcotado.Usuarios.Add(jefe);
        ctxAcotado.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefe.Id, "DIGER", "SIGER", null, "JefeArea"));
        _catalogo.Obtener("JefeArea").Returns(new RolInfo(
            "JefeArea", "Jefe de Área", NivelAlcance.Area, false, false, false, false,
            EsJefeDeArea: true, EsPmo: false, Color: null));

        var proyecto = Proyecto.Crear("PRY-2026-07", "Proyecto fuera del alcance del actor");
        proyecto.InstitucionId = "DIGER";
        proyecto.AreaId = "SIGER";
        ctxAcotado.Proyectos.Add(proyecto);
        await ctxAcotado.SaveChangesAsync();

        await syncAcotado.SincronizarUsuarioAsync(jefe.Id);

        (await ctxAcotado.ProyectoInteresados
            .AnyAsync(i => i.ProyectoId == proyecto.Id && i.UsuarioId == jefe.Id && i.Automatico))
            .Should().BeTrue();
    }

    public void Dispose() => _ctx.Dispose();
}
