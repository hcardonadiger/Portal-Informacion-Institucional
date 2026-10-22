using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests;

public sealed class AjustesNotificacionesTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(1441)]
    public void El_intervalo_fuera_de_rango_se_rechaza(int minutos) =>
        AjustesNotificaciones.CrearPorDefecto().Invoking(a => a.CambiarIntervalo(minutos)).Should().Throw<DomainException>();

    [Fact]
    public void Quince_minutos_es_valido()
    {
        var a = AjustesNotificaciones.CrearPorDefecto();
        a.CambiarIntervalo(15);
        a.IntervaloMinutos.Should().Be(15);
        a.Id.Should().Be(AjustesNotificaciones.IdUnico);
    }

    [Fact]
    public void Configurar_el_correo_lo_marca_como_configurado()
    {
        var a = AjustesNotificaciones.CrearPorDefecto();
        a.ConfigurarSmtp(" smtp.office365.com ", 587, true, "no-reply@diger.gob.hn", "no-reply@diger.gob.hn", null);
        a.SmtpConfigurado.Should().BeTrue();
        a.SmtpHost.Should().Be("smtp.office365.com");
    }

    [Fact]
    public void El_interruptor_avisa_si_cambio()
    {
        var i = InterruptorNotificacion.Crear(ModuloNotificacion.Reuniones);
        i.Cambiar(true, false, true).Should().BeFalse();
        i.Cambiar(true, true, true).Should().BeTrue();
    }

    [Fact]
    public void Al_tercer_fallo_el_correo_queda_fallido()
    {
        var c = CorreoPendiente.Crear(Guid.NewGuid(), ModuloNotificacion.Reuniones, TipoNotificacion.CompromisoVencido,
            "x", "/c/1", "/c/1", DateTime.UtcNow);
        c.RegistrarFallo("e", 3); c.RegistrarFallo("e", 3);
        c.Estado.Should().Be(EstadoCorreoPendiente.Pendiente);
        c.RegistrarFallo("e", 3);
        c.Estado.Should().Be(EstadoCorreoPendiente.Fallido);
        c.Intentos.Should().Be(3);
    }
}
