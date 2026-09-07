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
    public async Task SincronizarProyecto_QuitaLaFilaDeUnUsuarioDesactivado()
    {
        // Desactivar a alguien no le quitaba su fila automática: la baja solo miraba si seguía
        // calificando por su asignación, y esa no se toca al desactivar la cuenta. La fila
        // quedaba huérfana y —desde que la guarda de QuitarInteresado pregunta por el derecho
        // vigente— tampoco se podía quitar a mano. Contradecía además a AgregarInteresadoCommand,
        // que sí rechaza a un usuario inactivo.
        var jefe = await SembrarUsuarioAsync("Jefe Desactivado");
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefe.Id, "DIGER", "GOBDIGITAL", null, "JefeArea"));
        _catalogo.Obtener("JefeArea").Returns(new RolInfo(
            "JefeArea", "Jefe de Área", NivelAlcance.Area, false, false, false, false,
            EsJefeDeArea: true, EsPmo: false, Color: null));

        var proyecto = Proyecto.Crear("PRY-2026-90", "Proyecto de prueba");
        proyecto.AreaId = "GOBDIGITAL";
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        await _sync.SincronizarProyectoAsync(proyecto.Id);
        (await _ctx.ProyectoInteresados.AnyAsync(i => i.UsuarioId == jefe.Id)).Should().BeTrue("estando activo si le toca");

        jefe.Desactivar();
        await _ctx.SaveChangesAsync();

        await _sync.SincronizarProyectoAsync(proyecto.Id);

        (await _ctx.ProyectoInteresados.AnyAsync(i => i.UsuarioId == jefe.Id))
            .Should().BeFalse("una cuenta desactivada no conserva el acceso que le daba su capacidad");

        (await _sync.CalcularDerechoVigenteAsync(proyecto.Id))
            .Should().NotContainKey(jefe.Id, "y tampoco puede seguir bloqueando el borrado manual");
    }

    [Fact]
    public async Task SincronizarUsuario_QuitaSusFilasAlQuedarDesactivado()
    {
        var jefe = await SembrarUsuarioAsync("Jefe Desactivado");
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefe.Id, "DIGER", "GOBDIGITAL", null, "JefeArea"));
        _catalogo.Obtener("JefeArea").Returns(new RolInfo(
            "JefeArea", "Jefe de Área", NivelAlcance.Area, false, false, false, false,
            EsJefeDeArea: true, EsPmo: false, Color: null));

        var proyecto = Proyecto.Crear("PRY-2026-91", "Proyecto de prueba");
        proyecto.AreaId = "GOBDIGITAL";
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        await _sync.SincronizarUsuarioAsync(jefe.Id);
        (await _ctx.ProyectoInteresados.AnyAsync(i => i.UsuarioId == jefe.Id)).Should().BeTrue();

        jefe.Desactivar();
        await _ctx.SaveChangesAsync();

        await _sync.SincronizarUsuarioAsync(jefe.Id);

        (await _ctx.ProyectoInteresados.AnyAsync(i => i.UsuarioId == jefe.Id))
            .Should().BeFalse("el camino por usuario tiene que cerrar igual que el camino por proyecto");
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

    [Fact]
    public async Task SincronizarProyectoYSincronizarUsuario_CoincidenEnElRolCuandoCalificaPorAmbosCaminos()
    {
        // Usuario que es a la vez JefeDeArea de AREA-X y Pmo de UNIDAD-Y, y un proyecto que cae
        // dentro de ambas. Los dos métodos de sincronización mantienen la misma tabla
        // ProyectoInteresados, así que tienen que coincidir en el Rol que le asignan a este par
        // (usuario, proyecto) — sin importar cuál de los dos se invoque, ni cuántas veces.
        var jefePmo = await SembrarUsuarioAsync("Jefe y Pmo a la vez");
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefePmo.Id, "DIGER", "AREA-X", null, "JefeArea"));
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefePmo.Id, "DIGER", null, "UNIDAD-Y", "Pmo"));
        _catalogo.Obtener("JefeArea").Returns(new RolInfo(
            "JefeArea", "Jefe de Área", NivelAlcance.Area, false, false, false, false,
            EsJefeDeArea: true, EsPmo: false, Color: null));
        _catalogo.Obtener("Pmo").Returns(new RolInfo(
            "Pmo", "PMO", NivelAlcance.Unidad, false, false, false, false,
            EsJefeDeArea: false, EsPmo: true, Color: null));

        var proyecto = Proyecto.Crear("PRY-2026-08", "Proyecto en ambos caminos");
        proyecto.AreaId = "AREA-X";
        proyecto.UnidadId = "UNIDAD-Y";
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        // Vía SincronizarProyectoAsync, dos veces seguidas (idempotencia).
        await _sync.SincronizarProyectoAsync(proyecto.Id);
        await _sync.SincronizarProyectoAsync(proyecto.Id);
        var filaPorProyecto = await _ctx.ProyectoInteresados
            .SingleAsync(i => i.ProyectoId == proyecto.Id && i.UsuarioId == jefePmo.Id);
        filaPorProyecto.Rol.Should().Be(RolInteresado.Ejecutor);

        // Se limpia la fila para poder observar de forma aislada lo que produce el otro método.
        _ctx.ProyectoInteresados.Remove(filaPorProyecto);
        await _ctx.SaveChangesAsync();

        // Vía SincronizarUsuarioAsync, dos veces seguidas (idempotencia — no debería alternar Rol).
        await _sync.SincronizarUsuarioAsync(jefePmo.Id);
        await _sync.SincronizarUsuarioAsync(jefePmo.Id);
        var filaPorUsuario = await _ctx.ProyectoInteresados
            .SingleAsync(i => i.ProyectoId == proyecto.Id && i.UsuarioId == jefePmo.Id);
        filaPorUsuario.Rol.Should().Be(RolInteresado.Ejecutor);
    }

    // ── Bitácora (I3) ─────────────────────────────────────────────
    // Los dos caminos manuales escriben en BitacorasProyecto porque conceder o quitar un
    // interesado es un cambio de ACCESO, no una anotación de gestión. El camino automático hace
    // exactamente lo mismo y es el que más filas va a mover: si es el único invisible, la
    // pregunta «¿por qué esta persona ve este proyecto?» se queda sin nada que leer.

    private const string ActorDelSistema = "Sistema (sincronización automática)";

    [Fact]
    public async Task SincronizarProyecto_DejaConstanciaEnLaBitacoraAlDarDeAlta()
    {
        var jefe = await SembrarUsuarioAsync("Jefe de Área");
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefe.Id, "DIGER", "GOBDIGITAL", null, "JefeArea"));
        _catalogo.Obtener("JefeArea").Returns(new RolInfo(
            "JefeArea", "Jefe de Área", NivelAlcance.Area, false, false, false, false,
            EsJefeDeArea: true, EsPmo: false, Color: null));

        var proyecto = Proyecto.Crear("PRY-2026-20", "Proyecto de prueba");
        proyecto.AreaId = "GOBDIGITAL";
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        await _sync.SincronizarProyectoAsync(proyecto.Id);

        var entradas = await _ctx.BitacorasProyecto.Where(b => b.ProyectoId == proyecto.Id).ToListAsync();
        entradas.Should().ContainSingle();
        entradas[0].Tipo.Should().Be(TipoEventoProyecto.Interesado);
        entradas[0].Actor.Should().Be(ActorDelSistema);
        entradas[0].Detalle.Should().Contain("Jefe de Área").And.Contain("jefe de área",
            "el motivo tiene que estar escrito, no solo el hecho");
    }

    [Fact]
    public async Task SincronizarProyecto_DejaConstanciaEnLaBitacoraAlQuitar()
    {
        var exJefe = await SembrarUsuarioAsync("Ex Jefe");
        var proyecto = Proyecto.Crear("PRY-2026-21", "Proyecto de prueba");
        proyecto.AreaId = "GOBDIGITAL";
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        _ctx.ProyectoInteresados.Add(InteresadoProyecto.CrearAutomatico(
            proyecto.Id, exJefe.Id, exJefe.Nombre, RolInteresado.Patrocinador, null));
        await _ctx.SaveChangesAsync();

        await _sync.SincronizarProyectoAsync(proyecto.Id);

        var entradas = await _ctx.BitacorasProyecto.Where(b => b.ProyectoId == proyecto.Id).ToListAsync();
        entradas.Should().ContainSingle();
        entradas[0].Actor.Should().Be(ActorDelSistema);
        entradas[0].Detalle.Should().Contain("Ex Jefe").And.Contain("jefe de área");
    }

    [Fact]
    public async Task SincronizarUsuario_DejaConstanciaEnLaBitacoraEnLasDosDirecciones()
    {
        var jefe = await SembrarUsuarioAsync("Jefe de Área");
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefe.Id, "DIGER", "SIGER", null, "JefeArea"));
        _catalogo.Obtener("JefeArea").Returns(new RolInfo(
            "JefeArea", "Jefe de Área", NivelAlcance.Area, false, false, false, false,
            EsJefeDeArea: true, EsPmo: false, Color: null));

        var proyecto = Proyecto.Crear("PRY-2026-22", "Proyecto de prueba");
        proyecto.AreaId = "SIGER";
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        await _sync.SincronizarUsuarioAsync(jefe.Id);
        (await _ctx.BitacorasProyecto.CountAsync(b => b.ProyectoId == proyecto.Id))
            .Should().Be(1, "el alta automática se registra");

        // Se le retira la capacidad al rol: la siguiente pasada tiene que dar de baja Y registrarlo.
        _catalogo.Obtener("JefeArea").Returns(new RolInfo(
            "JefeArea", "Jefe de Área", NivelAlcance.Area, false, false, false, false,
            EsJefeDeArea: false, EsPmo: false, Color: null));

        await _sync.SincronizarUsuarioAsync(jefe.Id);

        var entradas = await _ctx.BitacorasProyecto
            .Where(b => b.ProyectoId == proyecto.Id).OrderBy(b => b.Id).ToListAsync();
        entradas.Should().HaveCount(2, "la baja automática también se registra");
        entradas[1].Actor.Should().Be(ActorDelSistema);
        entradas[1].Detalle.Should().Contain("Jefe de Área");
    }

    public void Dispose() => _ctx.Dispose();
}
