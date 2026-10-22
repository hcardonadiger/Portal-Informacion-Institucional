using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones.Outlook;

/// <summary>
/// Lo que se protege: guardar nunca falla por Outlook, guardar sin cambios visibles no molesta a
/// los invitados, el reintento no duplica la cita, actualizar usa la conexión del ORGANIZADOR, y
/// borrar nunca queda bloqueado por Microsoft.
/// </summary>
public class OutlookCommandsTests : IDisposable
{
    private const string Url = "https://portal/Reuniones/Acta/1";

    private readonly AppDbContext _ctx;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private readonly IOutlookCalendario _outlook = Substitute.For<IOutlookCalendario>();
    private readonly IConexionMicrosoft _conexion = Substitute.For<IConexionMicrosoft>();
    private readonly Usuario _organizador = Usuario.Crear("Ana Organizadora", "ana@diger.gob.hn", "hash");
    private readonly Usuario _editor = Usuario.Crear("Beto Editor", "beto@diger.gob.hn", "hash");

    public OutlookCommandsTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _usuario.EsGlobal.Returns(true);
        _usuario.UserId.Returns(_organizador.Id);
        _ctx = new AppDbContext(opts, _usuario, Substitute.For<MediatR.IPublisher>());
        _ctx.Usuarios.AddRange(_organizador, _editor);
        _ctx.SaveChanges();
        _conexion.EstaConfigurada.Returns(true);
        _outlook.CrearAsync(default, default!, default, default)
            .ReturnsForAnyArgs(new ResultadoOutlook("AAMk-1", "https://teams/abc"));
        _outlook.ActualizarAsync(default, default!, default!, default)
            .ReturnsForAnyArgs(new ResultadoOutlook("AAMk-1", "https://teams/abc"));
    }

    public void Dispose() => _ctx.Dispose();

    private void Conectar(Usuario u, bool vencida = false)
    {
        var c = ConexionMicrosoft.Crear(u.Id, u.Correo, "oid.tid", [1], DateTime.UtcNow);
        if (vencida) c.MarcarVencida("invalid_grant");
        _ctx.ConexionesMicrosoft.Add(c);
        _ctx.SaveChanges();
    }

    private Reunion Sembrar(string hora = "09:00")
    {
        var r = Reunion.Crear("Mesa técnica");
        r.Fecha = new DateOnly(2026, 10, 23);
        r.Hora = hora;
        r.PreRegistrar("Invitada", null, "IHTT", null, "invitada@ihtt.gob.hn", null);
        _ctx.Reuniones.Add(r);
        _ctx.SaveChanges();
        return r;
    }

    private EnviarReunionAOutlookCommandHandler Enviar() => new(_ctx, new ReunionRepository(_ctx), _usuario, _outlook, _ctx);
    private SincronizarReunionOutlookCommandHandler Sincronizar() => new(_ctx, new ReunionRepository(_ctx), _outlook, _ctx);
    private CancelarReunionOutlookCommandHandler Cancelar() => new(new ReunionRepository(_ctx), _outlook);

    private async Task<Reunion> EnviadaAsync()
    {
        Conectar(_organizador);
        var r = Sembrar();
        await Enviar().Handle(new EnviarReunionAOutlookCommand(r.Id, true, Url), default);
        _outlook.ClearReceivedCalls();
        return r;
    }

    // ── Enviar ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Enviar_crea_la_cita_y_guarda_id_organizador_enlace_y_huella()
    {
        Conectar(_organizador);
        var r = Sembrar();

        await Enviar().Handle(new EnviarReunionAOutlookCommand(r.Id, true, Url), default);

        var fila = await _ctx.Reuniones.AsNoTracking().SingleAsync();
        fila.OutlookEventId.Should().Be("AAMk-1");
        fila.OutlookOrganizadorId.Should().Be(_organizador.Id);
        fila.OutlookEnlaceTeams.Should().Be("https://teams/abc");
        fila.OutlookHuella.Should().HaveLength(64);
        await _outlook.Received(1).CrearAsync(_organizador.Id,
            Arg.Is<EventoOutlook>(e => e.EsTeams && e.Invitados.Single().Correo == "invitada@ihtt.gob.hn"),
            fila.OutlookTransaccionId!.Value, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_conexion_no_se_envia_y_se_dice_donde_conectar()
    {
        var r = Sembrar();

        var acto = () => Enviar().Handle(new EnviarReunionAOutlookCommand(r.Id, true, Url), default);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*Mi perfil*");
        await _outlook.DidNotReceiveWithAnyArgs().CrearAsync(default, default!, default, default);
    }

    [Fact]
    public async Task Una_hora_ilegible_no_llega_a_Graph()
    {
        Conectar(_organizador);
        var r = Sembrar(hora: "9 am");

        var acto = () => Enviar().Handle(new EnviarReunionAOutlookCommand(r.Id, true, Url), default);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*«9 am»*");
        await _outlook.DidNotReceiveWithAnyArgs().CrearAsync(default, default!, default, default);
    }

    [Fact]
    public async Task Si_Graph_falla_el_reintento_reusa_la_misma_transaccion()
    {
        Conectar(_organizador);
        var r = Sembrar();
        _outlook.CrearAsync(default, default!, default, default)
            .ThrowsAsyncForAnyArgs(new OutlookNoDisponibleException("503"));

        var primero = () => Enviar().Handle(new EnviarReunionAOutlookCommand(r.Id, true, Url), default);
        await primero.Should().ThrowAsync<DomainException>().WithMessage("*no se duplicará*");
        var transaccion = (await _ctx.Reuniones.AsNoTracking().SingleAsync()).OutlookTransaccionId;
        transaccion.Should().NotBeNull("se guarda ANTES de llamar a Graph");

        _outlook.CrearAsync(default, default!, default, default)
            .ReturnsForAnyArgs(new ResultadoOutlook("AAMk-1", null));
        await Enviar().Handle(new EnviarReunionAOutlookCommand(r.Id, true, Url), default);

        await _outlook.Received().CrearAsync(_organizador.Id, Arg.Any<EventoOutlook>(), transaccion!.Value, Arg.Any<CancellationToken>());
    }

    // ── Sincronizar ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Guardar_sin_cambios_visibles_no_llama_a_Graph()
    {
        var r = await EnviadaAsync();

        await Sincronizar().Handle(new SincronizarReunionOutlookCommand(r.Id, Url), default);

        await _outlook.DidNotReceiveWithAnyArgs().ActualizarAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task Un_cambio_de_hora_actualiza_con_la_conexion_del_organizador_aunque_edite_otro()
    {
        var r = await EnviadaAsync();
        _usuario.UserId.Returns(_editor.Id);
        r.Hora = "10:30";
        await _ctx.SaveChangesAsync();

        await Sincronizar().Handle(new SincronizarReunionOutlookCommand(r.Id, Url), default);

        await _outlook.Received(1).ActualizarAsync(_organizador.Id, "AAMk-1",
            Arg.Is<EventoOutlook>(e => e.Inicio.Hour == 10 && e.Inicio.Minute == 30), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_Graph_falla_al_actualizar_queda_pendiente_y_no_lanza()
    {
        var r = await EnviadaAsync();
        r.Hora = "11:00";
        await _ctx.SaveChangesAsync();
        _outlook.ActualizarAsync(default, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new OutlookNoDisponibleException("timeout"));

        await Sincronizar().Handle(new SincronizarReunionOutlookCommand(r.Id, Url), default);

        var fila = await _ctx.Reuniones.AsNoTracking().SingleAsync();
        fila.OutlookPendiente.Should().BeTrue();
        fila.OutlookError.Should().Contain("timeout");
    }

    [Fact]
    public async Task Con_la_conexion_del_organizador_vencida_el_pendiente_lo_nombra()
    {
        var r = await EnviadaAsync();
        r.Hora = "11:00";
        await _ctx.SaveChangesAsync();
        _outlook.ActualizarAsync(default, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new OutlookConexionVencidaException("invalid_grant"));

        await Sincronizar().Handle(new SincronizarReunionOutlookCommand(r.Id, Url), default);

        (await _ctx.Reuniones.AsNoTracking().SingleAsync()).OutlookError
            .Should().Contain("Ana Organizadora").And.Contain("reconectarse");
    }

    [Fact]
    public async Task Un_pendiente_se_reintenta_aunque_la_huella_no_haya_cambiado()
    {
        var r = await EnviadaAsync();
        r.MarcarOutlookPendiente("falló antes");
        await _ctx.SaveChangesAsync();

        await Sincronizar().Handle(new SincronizarReunionOutlookCommand(r.Id, Url), default);

        await _outlook.ReceivedWithAnyArgs(1).ActualizarAsync(default, default!, default!, default);
        (await _ctx.Reuniones.AsNoTracking().SingleAsync()).OutlookPendiente.Should().BeFalse();
    }

    [Fact]
    public async Task Si_le_borran_la_hora_queda_pendiente_con_el_motivo_y_no_llama_a_Graph()
    {
        var r = await EnviadaAsync();
        r.Hora = null;
        await _ctx.SaveChangesAsync();

        await Sincronizar().Handle(new SincronizarReunionOutlookCommand(r.Id, Url), default);

        var fila = await _ctx.Reuniones.AsNoTracking().SingleAsync();
        fila.OutlookPendiente.Should().BeTrue();
        fila.OutlookError.Should().Contain("fecha y hora");
        await _outlook.DidNotReceiveWithAnyArgs().ActualizarAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task Una_reunion_que_no_esta_en_Outlook_no_se_sincroniza()
    {
        var r = Sembrar();

        await Sincronizar().Handle(new SincronizarReunionOutlookCommand(r.Id, Url), default);

        _outlook.ReceivedCalls().Should().BeEmpty();
    }

    // ── Cancelar ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancelar_avisa_a_Graph_con_la_conexion_del_organizador()
    {
        var r = await EnviadaAsync();

        await Cancelar().Handle(new CancelarReunionOutlookCommand(r.Id), default);

        await _outlook.Received(1).CancelarAsync(_organizador.Id, "AAMk-1", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_no_se_puede_cancelar_se_dice_que_se_cancele_a_mano()
    {
        var r = await EnviadaAsync();
        _outlook.CancelarAsync(default, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new OutlookNoDisponibleException("503"));

        var acto = () => Cancelar().Handle(new CancelarReunionOutlookCommand(r.Id), default);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*a mano*");
    }

    // ── Estado ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task El_estado_dice_si_el_usuario_puede_enviar_y_quien_organiza()
    {
        var r = await EnviadaAsync();

        var e = await new GetEstadoOutlookQueryHandler(_ctx, _usuario, _conexion)
            .Handle(new GetEstadoOutlookQuery(r.Id), default);

        e.Configurado.Should().BeTrue();
        e.UsuarioConectado.Should().BeTrue();
        e.EnOutlook.Should().BeTrue();
        e.Organizador.Should().Be("Ana Organizadora");
        e.EnlaceTeams.Should().Be("https://teams/abc");
    }
}
