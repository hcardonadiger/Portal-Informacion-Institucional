using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Notificaciones;

/// <summary>El catálogo es lo que pone cada aviso bajo el interruptor de su módulo. Un tipo nuevo sin
/// módulo quedaría fuera de todos los interruptores, y lanzar en tiempo de ejecución sería peor que
/// fallar acá.</summary>
public sealed class CatalogoNotificacionesTests
{
    [Fact]
    public void Todos_los_tipos_tienen_modulo()
    {
        foreach (var tipo in Enum.GetValues<TipoNotificacion>())
            FluentActions.Invoking(() => CatalogoNotificaciones.De(tipo))
                .Should().NotThrow($"{tipo} tiene que tener módulo en CatalogoNotificaciones");
    }

    [Fact]
    public void El_chat_no_admite_correo()
    {
        CatalogoNotificaciones.De(TipoNotificacion.ChatRecibido).AdmiteCorreo.Should().BeFalse();
        CatalogoNotificaciones.ModuloAdmiteCorreo(ModuloNotificacion.Soporte).Should().BeFalse();
        CatalogoNotificaciones.ModuloAdmiteCorreo(ModuloNotificacion.Reuniones).Should().BeTrue();
    }

    [Fact]
    public void Solo_los_cinco_del_revisor_son_diarios()
    {
        CatalogoNotificaciones.Todas.Where(d => d.Diario).Select(d => d.Tipo).Should().BeEquivalentTo(new[]
        {
            TipoNotificacion.ReuniónMañana, TipoNotificacion.CompromisoVencido, TipoNotificacion.CompromisoProximo,
            TipoNotificacion.EtapaCronogramaVencida, TipoNotificacion.EtapaCronogramaProxima,
        });
    }

    [Fact]
    public void Los_recordatorios_manuales_mandan_correo_al_instante()
    {
        CatalogoNotificaciones.Todas.Where(d => d.CorreoInmediato).Select(d => d.Tipo).Should().BeEquivalentTo(new[]
        {
            TipoNotificacion.RecordatorioManualTicket, TipoNotificacion.RecordatorioManualExpediente,
            TipoNotificacion.RecordatorioManualReunion, TipoNotificacion.RecordatorioManualCompromiso,
        });
    }

    [Fact]
    public void El_plural_dice_la_cantidad()
    {
        CatalogoNotificaciones.De(TipoNotificacion.CompromisoVencido).Plural(6).Should().Be("Tiene 6 compromisos vencidos");
    }
}
