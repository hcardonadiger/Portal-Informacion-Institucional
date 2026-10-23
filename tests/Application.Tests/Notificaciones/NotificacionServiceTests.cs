using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Notificaciones;

public sealed class NotificacionServiceTests : IDisposable
{
    private readonly AppDbContext _ctx = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

    private readonly Guid _ana = Guid.NewGuid();
    private readonly AjustesDePrueba _ajustes = new();
    private RelojFijo _reloj = new(RecordatorioBackgroundServiceTests.NueveDeLaManana);

    private Task<bool> AvisarAsync(TipoNotificacion tipo, string url) =>
        NotificacionesDePrueba.Servicio(_ctx, _ajustes, _reloj)
            .CrearYGuardarAsync(_ana, tipo, $"Aviso {url}", url);

    [Fact]
    public async Task Con_la_campana_apagada_no_hay_aviso()
    {
        _ajustes.Con(ModuloNotificacion.Reuniones, campana: false);

        (await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1")).Should().BeFalse();
        (await _ctx.Notificaciones.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Dos_del_mismo_tipo_se_juntan_en_un_aviso()
    {
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/2");

        var n = await _ctx.Notificaciones.Include(x => x.Elementos).SingleAsync();
        n.Cantidad.Should().Be(2);
        n.Titulo.Should().Be("Tiene 2 compromisos vencidos");
        n.Elementos.Should().HaveCount(2);
    }

    [Fact]
    public async Task Leido_el_aviso_lo_siguiente_abre_uno_nuevo()
    {
        await AvisarAsync(TipoNotificacion.TicketAsignado, "/t/1");
        (await _ctx.Notificaciones.SingleAsync()).MarcarLeida();
        await _ctx.SaveChangesAsync();

        await AvisarAsync(TipoNotificacion.TicketAsignado, "/t/2");

        (await _ctx.Notificaciones.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Con_agrupar_apagado_cada_uno_es_un_aviso()
    {
        _ajustes.Con(ModuloNotificacion.Reuniones, agrupar: false);
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/2");

        (await _ctx.Notificaciones.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Un_diario_ya_avisado_hoy_no_se_repite_pero_manana_si()
    {
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");
        (await _ctx.Notificaciones.SingleAsync()).MarcarLeida();
        await _ctx.SaveChangesAsync();

        (await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1")).Should().BeFalse();
        (await _ctx.Notificaciones.CountAsync()).Should().Be(1);

        _reloj = new RelojFijo(RecordatorioBackgroundServiceTests.NueveDeLaManana.AddDays(1));
        (await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1")).Should().BeTrue();
        (await _ctx.Notificaciones.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Un_diario_sin_leer_desde_ayer_no_sube_en_cada_revision_de_hoy()
    {
        var hoy = RecordatorioBackgroundServiceTests.NueveDeLaManana;
        _reloj = new RelojFijo(hoy.AddDays(-1));
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");

        _reloj = new RelojFijo(hoy);
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");
        _reloj = new RelojFijo(hoy.AddMinutes(15));
        (await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1")).Should().BeFalse();

        var n = await _ctx.Notificaciones.SingleAsync();
        n.FechaActualizacion.Should().Be(hoy.UtcDateTime, "la segunda revisión del día ya no lo sube");
        n.Cantidad.Should().Be(1);
    }

    [Fact]
    public async Task Dos_avisos_del_mismo_chat_no_se_pierden_ni_cuentan_doble()
    {
        await AvisarAsync(TipoNotificacion.ChatRecibido, "/Chat#4");
        await AvisarAsync(TipoNotificacion.ChatRecibido, "/Chat#4");

        var n = await _ctx.Notificaciones.SingleAsync();
        n.Cantidad.Should().Be(1);
    }

    [Fact]
    public async Task Con_el_correo_encendido_queda_en_la_bandeja()
    {
        _ajustes.Con(ModuloNotificacion.Reuniones, correo: true);
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");

        (await _ctx.CorreosPendientes.SingleAsync()).Estado.Should().Be(EstadoCorreoPendiente.Pendiente);
    }

    [Fact]
    public async Task Con_el_correo_apagado_no_hay_bandeja()
    {
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");
        (await _ctx.CorreosPendientes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_recordatorio_manual_no_pasa_por_la_bandeja()
    {
        _ajustes.Con(ModuloNotificacion.Tickets, correo: true);
        await AvisarAsync(TipoNotificacion.RecordatorioManualTicket, "/t/1");
        (await _ctx.CorreosPendientes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Solo_correo_sin_campana_igual_no_repite_en_el_dia()
    {
        _ajustes.Con(ModuloNotificacion.Reuniones, campana: false, correo: true);
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");

        (await _ctx.CorreosPendientes.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task La_campana_lista_lo_ultimo_primero_y_el_grupo_lleva_a_su_detalle()
    {
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/1");
        await AvisarAsync(TipoNotificacion.CompromisoVencido, "/c/2");

        var lista = await NotificacionesDePrueba.Servicio(_ctx, _ajustes, _reloj).GetNoLeidasAsync(_ana);
        lista.Should().ContainSingle();
        lista[0].Url.Should().Be($"/Notificaciones/{lista[0].Id}");
        lista[0].Cantidad.Should().Be(2);
    }

    public void Dispose() => _ctx.Dispose();
}
