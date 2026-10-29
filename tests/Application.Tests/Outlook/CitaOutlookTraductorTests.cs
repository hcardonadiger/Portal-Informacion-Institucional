using Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Outlook;

public class CitaOutlookTraductorTests
{
    private static CitaOutlook Cita(string? asunto = "Mesa técnica", bool todoElDia = false, string? ubicacion = " Sala 2 ",
        string? teams = null, int minutos = 90) =>
        new("AAMk-1", "ical-1", asunto, new DateTime(2026, 11, 3, 9, 5, 0), new DateTime(2026, 11, 3, 9, 5, 0).AddMinutes(minutos),
            todoElDia, ubicacion, teams, EsOrganizador: true, Cancelada: false,
            ModificadaElUtc: new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Traduce_fecha_hora_duracion_y_lugar()
    {
        var g = CitaOutlookTraductor.Traducir(Cita());

        g.Titulo.Should().Be("Mesa técnica");
        g.Fecha.Should().Be(new DateOnly(2026, 11, 3));
        g.Hora.Should().Be("09:05");
        g.Duracion.Should().Be("1:30", "HorarioReunion lee «H:mm»");
        g.Lugar.Should().Be("Sala 2");
        g.EnlaceTeams.Should().BeNull();
        g.EsOrganizador.Should().BeTrue();
        g.ICalUId.Should().Be("ical-1");
    }

    [Fact]
    public void Todo_el_dia_no_lleva_hora_ni_duracion()
    {
        var g = CitaOutlookTraductor.Traducir(Cita(todoElDia: true, minutos: 24 * 60));

        g.Hora.Should().BeNull();
        g.Duracion.Should().BeNull();
    }

    [Fact]
    public void Asunto_vacio_y_textos_largos_se_ajustan_a_la_base()
    {
        CitaOutlookTraductor.Traducir(Cita(asunto: "  ")).Titulo.Should().Be("(Sin asunto)");
        CitaOutlookTraductor.Traducir(Cita(asunto: new string('x', 300))).Titulo.Should().HaveLength(250);
        CitaOutlookTraductor.Traducir(Cita(ubicacion: new string('y', 300))).Lugar.Should().HaveLength(250);
        CitaOutlookTraductor.Traducir(Cita(ubicacion: "")).Lugar.Should().BeNull();
    }

    [Fact]
    public void Invitados_sin_recursos_ni_el_dueno_y_con_nombre_de_respaldo()
    {
        var c = Cita() with
        {
            Invitados =
            [
                new("Marta López", "marta@copeco.gob.hn", false),
                new(null, "  luis@ihtt.gob.hn ", false),
                new("Sala 2", "sala2@diger.gob.hn", true),
                new("Henry", "HCARDONA@diger.gob.hn", false),
                new("Sin correo", null, false),
                new(new string('n', 200), "largo@x.hn", false),
            ]
        };

        var inv = CitaOutlookTraductor.Invitados(c, "hcardona@diger.gob.hn");

        inv.Select(i => i.Correo).Should().Equal("marta@copeco.gob.hn", "luis@ihtt.gob.hn", "largo@x.hn");
        inv[1].Nombre.Should().Be("luis@ihtt.gob.hn", "sin nombre, se usa el correo");
        inv[2].Nombre.Should().HaveLength(150);
    }

    [Fact]
    public void Sin_invitados_es_lista_vacia()
    {
        CitaOutlookTraductor.Invitados(Cita(), "x@diger.gob.hn").Should().BeEmpty();
    }

    [Fact]
    public void El_enlace_de_Teams_pasa_tal_cual()
    {
        CitaOutlookTraductor.Traducir(Cita(teams: "https://teams.microsoft.com/l/x")).EnlaceTeams
            .Should().Be("https://teams.microsoft.com/l/x");
    }
}
