using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests;

/// <summary>
/// Estado de una reunión respecto de Outlook. Lo que se protege: que el Teams nazca encendido,
/// que el reintento de un envío reuse la misma transacción —Graph la usa para no duplicar la
/// cita a los invitados— y que un fallo deje la marca de pendiente con su motivo.
/// </summary>
public class ReunionOutlookTests
{
    [Fact]
    public void Una_reunion_nueva_no_esta_en_Outlook_y_trae_Teams_encendido()
    {
        var r = Reunion.Crear("Mesa técnica");
        r.Modalidad = "Virtual";

        r.EstaEnOutlook.Should().BeFalse();
        r.OutlookConTeams.Should().BeTrue("las virtuales e híbridas se graban para la transcripción");
        r.OutlookPendiente.Should().BeFalse();
    }

    [Fact]
    public void Preparar_dos_veces_devuelve_la_misma_transaccion()
    {
        var r = Reunion.Crear("Mesa técnica");
        r.Modalidad = "Virtual";

        var primera = r.PrepararEnvioOutlook(conTeams: true);
        var segunda = r.PrepararEnvioOutlook(conTeams: true);

        segunda.Should().Be(primera, "un reintento tras un fallo no puede crear una segunda cita");
        primera.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Preparar_sin_Teams_lo_deja_apagado()
    {
        var r = Reunion.Crear("Mesa técnica");
        r.Modalidad = "Virtual";

        r.PrepararEnvioOutlook(conTeams: false);

        r.OutlookConTeams.Should().BeFalse();
    }

    [Fact]
    public void No_se_puede_preparar_un_envio_de_una_reunion_ya_enviada()
    {
        var r = Reunion.Crear("Mesa técnica");
        r.Modalidad = "Virtual";
        r.PrepararEnvioOutlook(true);
        r.RegistrarEnvioOutlook("AAMk-1", Guid.NewGuid(), "https://teams/x", new string('a', 64), DateTime.UtcNow);

        var acto = () => r.PrepararEnvioOutlook(true);

        acto.Should().Throw<DomainException>();
    }

    [Fact]
    public void Registrar_el_envio_deja_la_reunion_en_Outlook_y_sin_pendiente()
    {
        var r = Reunion.Crear("Mesa técnica");
        r.Modalidad = "Virtual";
        var organizador = Guid.NewGuid();
        r.PrepararEnvioOutlook(true);
        r.MarcarOutlookPendiente("Outlook no respondió");

        r.RegistrarEnvioOutlook("AAMk-1", organizador, "https://teams/x", new string('a', 64), new DateTime(2026, 10, 23, 15, 0, 0, DateTimeKind.Utc));

        r.EstaEnOutlook.Should().BeTrue();
        r.OutlookOrganizadorId.Should().Be(organizador);
        r.OutlookEnlaceTeams.Should().Be("https://teams/x");
        r.OutlookHuella.Should().Be(new string('a', 64));
        r.OutlookPendiente.Should().BeFalse();
        r.OutlookError.Should().BeNull();
    }

    [Fact]
    public void Marcar_pendiente_guarda_el_motivo_recortado_a_500()
    {
        var r = Reunion.Crear("Mesa técnica");
        r.Modalidad = "Virtual";

        r.MarcarOutlookPendiente(new string('x', 900));

        r.OutlookPendiente.Should().BeTrue();
        r.OutlookError.Should().HaveLength(500);
    }

    [Fact]
    public void Sincronizar_limpia_el_pendiente_y_actualiza_la_huella()
    {
        var r = Reunion.Crear("Mesa técnica");
        r.Modalidad = "Virtual";
        r.PrepararEnvioOutlook(true);
        r.RegistrarEnvioOutlook("AAMk-1", Guid.NewGuid(), null, new string('a', 64), DateTime.UtcNow);
        r.MarcarOutlookPendiente("falló");

        r.RegistrarSincronizacionOutlook("https://teams/y", new string('b', 64), DateTime.UtcNow);

        r.OutlookPendiente.Should().BeFalse();
        r.OutlookError.Should().BeNull();
        r.OutlookHuella.Should().Be(new string('b', 64));
        r.OutlookEnlaceTeams.Should().Be("https://teams/y");
    }

    [Fact]
    public void Descartar_el_pendiente_no_toca_la_huella_ni_la_fecha_de_sincronizacion()
    {
        var r = Reunion.Crear("Mesa técnica");
        r.Modalidad = "Virtual";
        var enviadaEl = new DateTime(2026, 10, 23, 15, 0, 0, DateTimeKind.Utc);
        r.PrepararEnvioOutlook(true);
        r.RegistrarEnvioOutlook("AAMk-1", Guid.NewGuid(), null, new string('a', 64), enviadaEl);
        r.MarcarOutlookPendiente("falló");

        r.DescartarOutlookPendiente();

        r.OutlookPendiente.Should().BeFalse();
        r.OutlookError.Should().BeNull();
        r.OutlookHuella.Should().Be(new string('a', 64), "no se mandó nada: la cita sigue como se envió");
        r.OutlookSincronizadaEl.Should().Be(enviadaEl);
    }

    [Fact]
    public void Una_conexion_vencida_conserva_el_motivo_y_reconectar_la_limpia()
    {
        var usuario = Guid.NewGuid();
        var c = ConexionMicrosoft.Crear(usuario, "ana@diger.gob.hn", "oid.tid", [1, 2, 3], DateTime.UtcNow);

        c.MarcarVencida("invalid_grant");
        c.RequiereReconexion.Should().BeTrue();
        c.UltimoError.Should().Be("invalid_grant");

        c.Reconectar("ana@diger.gob.hn", "oid.tid", [4, 5], DateTime.UtcNow);
        c.RequiereReconexion.Should().BeFalse();
        c.UltimoError.Should().BeNull();
        c.CacheTokens.Should().Equal(4, 5);
        c.Id.Should().Be(usuario);
    }

    [Theory]
    [InlineData("Virtual")]
    [InlineData("Híbrida")]
    [InlineData("hibrida")]
    [InlineData(" Virtual (Teams) ")]
    public void Las_virtuales_e_hibridas_admiten_Outlook(string modalidad)
    {
        var r = Reunion.Crear("Mesa técnica");
        r.Modalidad = modalidad;

        r.AdmiteOutlook.Should().BeTrue();
    }

    [Theory]
    [InlineData("Presencial")]
    [InlineData("Otro")]
    [InlineData("")]
    [InlineData(null)]
    public void Las_presenciales_o_sin_modalidad_no_se_envian_a_Outlook(string? modalidad)
    {
        var r = Reunion.Crear("Mesa técnica");
        r.Modalidad = modalidad;

        r.AdmiteOutlook.Should().BeFalse();
        var acto = () => r.PrepararEnvioOutlook(true);
        acto.Should().Throw<DomainException>().WithMessage("*virtuales o híbridas*");
        r.OutlookTransaccionId.Should().BeNull("no se prepara nada para una reunión que no va");
    }
}
