using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests;

/// <summary>La reunión que entra desde el calendario de Outlook: privada, de su dueño, y con
/// Outlook mandando solo sobre los Generales y sobre los invitados que trajo.</summary>
public class ReunionCalendarioOutlookTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private const string EventId = "AAMkAGI2TG93AAA=";

    private static GeneralesOutlook G(string titulo = "Mesa técnica", string? hora = "09:00", string? lugar = "Sala 2",
        string? teams = null, bool organizador = false) =>
        new(titulo, new DateOnly(2026, 11, 3), hora, "1:00", lugar, teams, organizador, "ical-1",
            new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Importar_crea_privada_del_usuario_con_sus_generales()
    {
        var r = Reunion.ImportarDeOutlook(EventId, G(), Ana, "DIGER", "AREA1", "UNI1");

        r.Titulo.Should().Be("Mesa técnica");
        r.Fecha.Should().Be(new DateOnly(2026, 11, 3));
        r.Hora.Should().Be("09:00");
        r.Duracion.Should().Be("1:00");
        r.Lugar.Should().Be("Sala 2");
        r.Modalidad.Should().Be("Presencial");
        r.Visibilidad.Should().Be(VisibilidadReunion.Privada);
        r.CreadoPorId.Should().Be(Ana);
        r.OutlookImportadaPorId.Should().Be(Ana);
        r.InstitucionDuenaId.Should().Be("DIGER");
        r.AreaId.Should().Be("AREA1");
        r.UnidadId.Should().Be("UNI1");
        r.OrigenExternoId.Should().Be(Reunion.ClaveOrigenOutlook(EventId));
        r.OutlookImportadoEventId.Should().Be(EventId);
        r.OutlookICalUId.Should().Be("ical-1");
        r.EsImportadaDeOutlook.Should().BeTrue();
        r.EstaEnOutlook.Should().BeFalse("el portal nunca le manda cambios a una cita importada");
    }

    [Fact]
    public void La_clave_de_origen_cabe_en_60_caracteres_y_es_estable()
    {
        var largo = new string('A', 400);
        Reunion.ClaveOrigenOutlook(largo).Should().HaveLength(51).And.StartWith("outlook:");
        Reunion.ClaveOrigenOutlook(largo).Should().Be(Reunion.ClaveOrigenOutlook(largo));
        Reunion.ClaveOrigenOutlook(largo).Should().NotBe(Reunion.ClaveOrigenOutlook(largo + "B"));
    }

    [Fact]
    public void Con_Teams_es_virtual_y_solo_el_organizador_queda_como_tal()
    {
        var org = Reunion.ImportarDeOutlook(EventId, G(teams: "https://teams/x", organizador: true), Ana, "DIGER", null, null);
        var inv = Reunion.ImportarDeOutlook(EventId + "2", G(teams: "https://teams/y"), Ana, "DIGER", null, null);

        org.Modalidad.Should().Be("Virtual");
        org.OutlookEnlaceTeams.Should().Be("https://teams/x");
        org.OutlookOrganizadorId.Should().Be(Ana, "habilita bajar la transcripción con su conexión");
        inv.OutlookOrganizadorId.Should().BeNull();
    }

    [Fact]
    public void Actualizar_cambia_solo_generales_y_dice_si_hubo_cambio()
    {
        var r = Reunion.ImportarDeOutlook(EventId, G(), Ana, "DIGER", null, null);
        r.ObjetivoAgenda = "Escrito en el portal";
        r.Visibilidad = VisibilidadReunion.Publica;

        r.ActualizarDeOutlook(G()).Should().BeFalse();
        r.ActualizarDeOutlook(G(titulo: "Mesa técnica (movida)", hora: "10:30")).Should().BeTrue();

        r.Titulo.Should().Be("Mesa técnica (movida)");
        r.Hora.Should().Be("10:30");
        r.ObjetivoAgenda.Should().Be("Escrito en el portal");
        r.Visibilidad.Should().Be(VisibilidadReunion.Publica);
    }

    [Fact]
    public void Cancelar_y_quitar_cancelacion_son_idempotentes()
    {
        var r = Reunion.ImportarDeOutlook(EventId, G(), Ana, "DIGER", null, null);
        var t = new DateTime(2026, 10, 29, 18, 0, 0, DateTimeKind.Utc);

        r.MarcarCanceladaEnOutlook(t).Should().BeTrue();
        r.MarcarCanceladaEnOutlook(t.AddHours(1)).Should().BeFalse();
        r.CanceladaEnOutlookEl.Should().Be(t);

        r.QuitarCancelacionOutlook().Should().BeTrue();
        r.QuitarCancelacionOutlook().Should().BeFalse();
        r.CanceladaEnOutlookEl.Should().BeNull();
    }

    [Fact]
    public void Los_invitados_entran_como_preregistrados_de_Outlook_sin_duplicar_por_correo()
    {
        var r = Reunion.ImportarDeOutlook(EventId, G(), Ana, "DIGER", null, null);
        r.Agregar(new Asistente { Nombre = "Luis (QR)", Correo = "luis@ihtt.gob.hn", AutoRegistro = true });

        r.SincronizarInvitadosOutlook([new("Marta", "marta@copeco.gob.hn"), new("Luis", "LUIS@ihtt.gob.hn")])
            .Should().BeTrue();

        r.Asistentes.Should().HaveCount(2);
        var marta = r.Asistentes.Single(a => a.Correo == "marta@copeco.gob.hn");
        marta.EsPreregistro.Should().BeTrue();
        marta.DeOutlook.Should().BeTrue();
        marta.Confirmado.Should().BeNull();

        r.SincronizarInvitadosOutlook([new("Marta", "marta@copeco.gob.hn"), new("Luis", "luis@ihtt.gob.hn")])
            .Should().BeFalse("nada cambió");
    }

    [Fact]
    public void Un_retirado_en_Outlook_se_quita_solo_si_vino_de_Outlook_y_sigue_pendiente()
    {
        var r = Reunion.ImportarDeOutlook(EventId, G(), Ana, "DIGER", null, null);
        r.SincronizarInvitadosOutlook([new("Pendiente", "p@x.hn"), new("Asistio", "a@x.hn")]);
        r.Asistentes.Single(a => a.Correo == "a@x.hn").Confirmado = true;
        r.Agregar(new Asistente { Nombre = "Manual", Correo = "m@x.hn", EsPreregistro = true });

        r.SincronizarInvitadosOutlook([]).Should().BeTrue();

        r.Asistentes.Select(a => a.Correo).Should().BeEquivalentTo(["a@x.hn", "m@x.hn"]);
    }

    [Fact]
    public void Una_importada_no_se_puede_enviar_a_Outlook()
    {
        var r = Reunion.ImportarDeOutlook(EventId, G(teams: "https://teams/x"), Ana, "DIGER", null, null);

        var act = () => r.PrepararEnvioOutlook(conTeams: true);

        act.Should().Throw<DomainException>().WithMessage("*viene de su Outlook*");
    }
}
