using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Chat;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Chat;

/// <summary>
/// La bandeja del chat le mostraba al técnico solo la cola y sus propios chats; el Global no
/// tenía cómo ver los que atendían los demás ni los ya cerrados. Supervisar es leer: responder
/// y resolver sigue siendo de quien atiende, y quitarle el chat a otro es solo del Global.
/// </summary>
public sealed class SupervisionChatTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ChatService  _svc;

    private static readonly Guid Ana  = Guid.NewGuid();
    private static readonly Guid Beto = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();

    public SupervisionChatTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), NSubstitute.Substitute.For<MediatR.IPublisher>());
        _svc = new ChatService(_ctx, _ctx);
    }

    public void Dispose() => _ctx.Dispose();

    private async Task<int> SesionAsync(string usuario, Guid? tecnico = null, string? tecnicoNombre = null,
        ChatEstado? cierre = null)
    {
        var s = ChatSesion.Iniciar(Guid.NewGuid(), usuario);
        if (tecnico is Guid t) s.AsignarTecnico(t, tecnicoNombre ?? "Técnico");
        if (cierre is ChatEstado e) s.Cerrar(e);
        _ctx.ChatSesiones.Add(s);
        await _ctx.SaveChangesAsync();
        return s.Id;
    }

    [Fact]
    public async Task El_Global_ve_los_activos_de_los_demas_con_quien_los_atiende()
    {
        await SesionAsync("Usuario en cola");
        await SesionAsync("Usuario de Ana",  Ana,   "Ana");
        await SesionAsync("Usuario de Beto", Beto,  "Beto");
        await SesionAsync("Usuario mío",     Admin, "Admin");
        await SesionAsync("Usuario cerrado", Ana,   "Ana", ChatEstado.Resuelto);

        var otros = await _svc.GetActivasDeOtrosAsync(Admin);

        otros.Select(s => s.UsuarioNombre).Should().BeEquivalentTo("Usuario de Ana", "Usuario de Beto");
        otros.Select(s => s.TecnicoNombre).Should().BeEquivalentTo("Ana", "Beto");
    }

    [Fact]
    public async Task Los_cerrados_salen_todos_los_estados_de_cierre_y_con_tope()
    {
        await SesionAsync("Activo", Ana, "Ana");
        await SesionAsync("Resuelto",   Ana, "Ana", ChatEstado.Resuelto);
        await SesionAsync("Abandonado", null, null, ChatEstado.Abandonado);
        await SesionAsync("Expirado",   Beto, "Beto", ChatEstado.Expirado);

        var cerradas = await _svc.GetCerradasRecientesAsync();
        cerradas.Select(s => s.UsuarioNombre).Should().BeEquivalentTo("Resuelto", "Abandonado", "Expirado");

        (await _svc.GetCerradasRecientesAsync(maximo: 2)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Solo_quien_atiende_un_chat_abierto_lo_atiende()
    {
        var deAna   = await SesionAsync("Usuario de Ana", Ana, "Ana");
        var cerrado = await SesionAsync("Cerrado", Ana, "Ana", ChatEstado.Resuelto);

        (await _svc.AtiendeSesionAsync(deAna, Ana)).Should().BeTrue();
        (await _svc.AtiendeSesionAsync(deAna, Admin)).Should().BeFalse("el Global lo lee, no lo atiende");
        (await _svc.AtiendeSesionAsync(cerrado, Ana)).Should().BeFalse("un chat cerrado ya no se responde");
    }

    [Fact]
    public async Task De_la_cola_lo_toma_cualquiera_sin_reasignacion()
    {
        var id = await SesionAsync("En cola");

        var anterior = await _svc.TomarSesionAsync(id, Beto, "Beto", puedeReasignar: false);

        anterior.Should().BeNull();
        (await _svc.AtiendeSesionAsync(id, Beto)).Should().BeTrue();
    }

    [Fact]
    public async Task Un_tecnico_comun_no_le_quita_el_chat_a_otro()
    {
        var id = await SesionAsync("Usuario de Ana", Ana, "Ana");

        var act = () => _svc.TomarSesionAsync(id, Beto, "Beto", puedeReasignar: false);

        await act.Should().ThrowAsync<DomainException>();
        (await _svc.AtiendeSesionAsync(id, Ana)).Should().BeTrue();
    }

    [Fact]
    public async Task El_Global_toma_el_chat_de_otro_y_queda_dicho_a_quien_se_lo_quito()
    {
        var id = await SesionAsync("Usuario de Ana", Ana, "Ana");

        var anterior = await _svc.TomarSesionAsync(id, Admin, "Admin", puedeReasignar: true);

        anterior.Should().Be("Ana");
        (await _svc.AtiendeSesionAsync(id, Admin)).Should().BeTrue();
        (await _svc.AtiendeSesionAsync(id, Ana)).Should().BeFalse();
    }

    [Fact]
    public async Task Un_chat_cerrado_no_se_toma_ni_siendo_Global()
    {
        var id = await SesionAsync("Cerrado", Ana, "Ana", ChatEstado.Resuelto);

        var act = () => _svc.TomarSesionAsync(id, Admin, "Admin", puedeReasignar: true);

        await act.Should().ThrowAsync<DomainException>();
    }

    // ── La cola compartida: contar, repartir ─────────────────────────────

    private async Task<(Guid Id, string Nombre)> UsuarioAsync(string nombre, string rol, bool activo = true, int? temaId = null)
    {
        var u = Usuario.Crear(nombre, $"{Guid.NewGuid():N}@pruebas.gob.hn", "hash");
        if (!activo) u.Desactivar();
        _ctx.Usuarios.Add(u);
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(u.Id, "DIGER", null, null, rol));
        if (temaId is int t) _ctx.UsuarioTemas.Add(UsuarioTema.Crear(u.Id, t));
        await _ctx.SaveChangesAsync();
        return (u.Id, nombre);
    }

    private async Task RolesAsync()
    {
        _ctx.Roles.AddRange(
            Rol.Crear("Empleado",  "Empleado",  NivelAlcance.Unidad, esTecnicoSoporte: true),
            Rol.Crear("Consultor", "Consultor", NivelAlcance.Unidad, esSoloLectura: true));
        await _ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task El_contador_cuenta_solo_lo_que_espera_en_cola()
    {
        await SesionAsync("En cola 1");
        await SesionAsync("En cola 2");
        await SesionAsync("Activo", Ana, "Ana");
        await SesionAsync("Cerrado", null, null, ChatEstado.Abandonado);

        (await _svc.ContarColaAsync()).Should().Be(2);
    }

    private async Task<TemaTicket> TemaAsync(string nombre)
    {
        var tema = TemaTicket.Crear(nombre, 0);
        _ctx.TemasTicket.Add(tema);
        await _ctx.SaveChangesAsync();
        return tema;
    }

    private async Task<int> SesionDelTemaAsync(TemaTicket tema)
    {
        var s = ChatSesion.Iniciar(Guid.NewGuid(), "Usuario con tema");
        s.AsignarTema(tema.Id, tema.Nombre);
        _ctx.ChatSesiones.Add(s);
        await _ctx.SaveChangesAsync();
        return s.Id;
    }

    // Técnico es quien tiene temas asignados. Antes entraba a la lista todo rol con
    // EsTecnicoSoporte, y como casi todos los roles lo tienen, el «Asignar a…» era el directorio.
    [Fact]
    public async Task Los_asignables_de_un_chat_son_solo_quienes_atienden_su_tema()
    {
        await RolesAsync();
        var accesos = await TemaAsync("Accesos");
        var datos   = await TemaAsync("Datos");

        await UsuarioAsync("Zoila de accesos", "Empleado", temaId: accesos.Id);
        await UsuarioAsync("Bruno de datos", "Empleado", temaId: datos.Id);
        await UsuarioAsync("Alberto sin tema", "Empleado");
        await UsuarioAsync("Consultora", "Consultor", temaId: accesos.Id); // su rol no atiende soporte
        await UsuarioAsync("Inactivo", "Empleado", activo: false, temaId: accesos.Id);

        var lista = await _svc.GetTecnicosAsignablesAsync(accesos.Id);

        lista.Select(t => (t.Nombre, t.DelTema)).Should().Equal(("Zoila de accesos", true));
    }

    [Fact]
    public async Task Sin_tema_o_si_nadie_lo_atiende_se_reparte_entre_todos_los_que_tienen_temas()
    {
        await RolesAsync();
        var accesos = await TemaAsync("Accesos");
        var datos   = await TemaAsync("Datos");
        var huerfano = await TemaAsync("Nadie lo atiende");

        await UsuarioAsync("Zoila de accesos", "Empleado", temaId: accesos.Id);
        await UsuarioAsync("Bruno de datos", "Empleado", temaId: datos.Id);
        await UsuarioAsync("Alberto sin tema", "Empleado");

        foreach (var temaId in new int?[] { null, huerfano.Id })
            (await _svc.GetTecnicosAsignablesAsync(temaId)).Select(t => (t.Nombre, t.DelTema)).Should().Equal(
                [("Bruno de datos", false), ("Zoila de accesos", false)],
                "sin quien atienda el tema, el chat igual tiene que poder repartirse, pero no a cualquiera");
    }

    [Fact]
    public async Task Asignar_desde_la_cola_deja_el_chat_activo_con_ese_tecnico()
    {
        await RolesAsync();
        var tema = await TemaAsync("Accesos");
        var (id, nombre) = await UsuarioAsync("Empleado DEV", "Empleado", temaId: tema.Id);
        var sesion = await SesionDelTemaAsync(tema);

        var asignado = await _svc.AsignarDesdeColaAsync(sesion, id);

        asignado.Should().Be(nombre);
        (await _svc.AtiendeSesionAsync(sesion, id)).Should().BeTrue();
        (await _svc.ContarColaAsync()).Should().Be(0);
    }

    [Fact]
    public async Task No_se_asigna_un_chat_a_quien_no_atiende_su_tema()
    {
        await RolesAsync();
        var accesos = await TemaAsync("Accesos");
        var datos   = await TemaAsync("Datos");
        await UsuarioAsync("Zoila de accesos", "Empleado", temaId: accesos.Id);
        var (bruno, _) = await UsuarioAsync("Bruno de datos", "Empleado", temaId: datos.Id);
        var sesion = await SesionDelTemaAsync(accesos);

        var act = () => _svc.AsignarDesdeColaAsync(sesion, bruno);

        await act.Should().ThrowAsync<DomainException>("el POST no puede saltarse la lista que ofrece la pantalla");
        (await _svc.ContarColaAsync()).Should().Be(1);
    }

    [Fact]
    public async Task No_se_asigna_a_quien_no_tiene_temas()
    {
        await RolesAsync();
        var tema = await TemaAsync("Accesos");
        await UsuarioAsync("Zoila de accesos", "Empleado", temaId: tema.Id);
        var (alberto, _) = await UsuarioAsync("Alberto sin tema", "Empleado");
        var sesion = await SesionAsync("En cola sin tema");

        var act = () => _svc.AsignarDesdeColaAsync(sesion, alberto);

        await act.Should().ThrowAsync<DomainException>();
        (await _svc.ContarColaAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Solo_se_asigna_lo_que_sigue_en_cola()
    {
        await RolesAsync();
        var (id, _) = await UsuarioAsync("Empleado DEV", "Empleado");
        var activo = await SesionAsync("Ya tomado", Ana, "Ana");

        var act = () => _svc.AsignarDesdeColaAsync(activo, id);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*ya no está en la cola*");
        (await _svc.AtiendeSesionAsync(activo, Ana)).Should().BeTrue();
    }

    [Fact]
    public async Task No_se_asigna_a_quien_no_atiende_soporte()
    {
        await RolesAsync();
        var (consultor, _) = await UsuarioAsync("Consultora", "Consultor");
        var sesion = await SesionAsync("En cola");

        var act = () => _svc.AsignarDesdeColaAsync(sesion, consultor);

        await act.Should().ThrowAsync<DomainException>();
        (await _svc.ContarColaAsync()).Should().Be(1);
    }
}
