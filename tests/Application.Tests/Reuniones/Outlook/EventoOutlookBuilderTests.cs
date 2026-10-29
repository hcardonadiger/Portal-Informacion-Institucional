using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones.Outlook;

public class EventoOutlookBuilderTests
{
    private const string Url = "https://portal/Reuniones/Acta/7";

    private static Reunion Base(string modalidad = "Presencial")
    {
        var r = Reunion.Crear("Mesa técnica SIGER");
        r.Fecha = new DateOnly(2026, 10, 23);
        r.Hora = "09:00";
        r.Modalidad = modalidad;
        r.Lugar = "Sala 2, DIGER";
        r.ObjetivoAgenda = "Revisar avances & <pendientes>";
        return r;
    }

    [Fact]
    public void Arma_asunto_horario_lugar_y_cuerpo()
    {
        var e = EventoOutlookBuilder.Construir(Base(), "org@diger.gob.hn", Url);

        e.Asunto.Should().Be("Mesa técnica SIGER");
        e.Inicio.Should().Be(new DateTime(2026, 10, 23, 9, 0, 0));
        e.Fin.Should().Be(new DateTime(2026, 10, 23, 10, 0, 0));
        e.Ubicacion.Should().Be("Sala 2, DIGER");
        e.CuerpoHtml.Should().Contain("Revisar avances &amp; &lt;pendientes&gt;", "el objetivo es texto del usuario y va escapado");
        e.CuerpoHtml.Should().Contain(Url);
    }

    [Theory]
    [InlineData("Presencial")]
    [InlineData("Virtual")]
    [InlineData("Híbrida")]
    [InlineData(null)]
    public void Teams_va_en_toda_modalidad_mientras_no_se_apague(string? modalidad)
    {
        var e = EventoOutlookBuilder.Construir(Base(modalidad!), null, Url);

        e.EsTeams.Should().BeTrue();
    }

    [Fact]
    public void Sin_Teams_cuando_se_desmarco_al_enviar()
    {
        var r = Base("Híbrida"); // solo las virtuales e híbridas se envían
        r.PrepararEnvioOutlook(conTeams: false);

        EventoOutlookBuilder.Construir(r, null, Url).EsTeams.Should().BeFalse();
    }

    [Fact]
    public void Invita_preregistrados_con_correo_enlace_y_facilitador_sin_repetir_ni_al_organizador()
    {
        var r = Base();
        r.PreRegistrar("Ana Paz", null, "IHTT", null, "ana@ihtt.gob.hn", null);
        r.PreRegistrar("Ana Paz (dup)", null, "IHTT", null, "ANA@ihtt.gob.hn", null);
        r.PreRegistrar("Sin correo", null, "IHTT", null, null, null);
        r.PreRegistrar("Correo roto", null, "IHTT", null, "no-es-correo", null);
        r.PreRegistrar("El organizador", null, "DIGER", null, "org@diger.gob.hn", null);
        r.RegistrarAsistente("Llegó por QR", null, "IHTT", null, "qr@ihtt.gob.hn", null);
        r.EpNombre = "Luis Enlace"; r.EpCorreo = "luis@ihtt.gob.hn";
        r.FacNombre = "Carla Facilitadora"; r.FacCorreo = "carla@diger.gob.hn";

        var e = EventoOutlookBuilder.Construir(r, "ORG@diger.gob.hn", Url);

        e.Invitados.Select(i => i.Correo).Should().BeEquivalentTo(
            ["ana@ihtt.gob.hn", "luis@ihtt.gob.hn", "carla@diger.gob.hn"],
            "el auto-registro por QR es asistencia, no invitación");
    }

    [Fact]
    public void Una_hora_ilegible_se_propaga_como_rechazo()
    {
        var r = Base();
        r.Hora = "9 am";

        var acto = () => EventoOutlookBuilder.Construir(r, null, Url);

        acto.Should().Throw<Diger.TramitesEstado.Domain.Common.DomainException>();
    }

    [Fact]
    public void La_huella_es_estable_e_ignora_el_orden_y_las_mayusculas_de_los_invitados()
    {
        var a = new EventoOutlook("X", new DateTime(2026, 1, 1, 9, 0, 0), new DateTime(2026, 1, 1, 10, 0, 0), "Sala", "<p>x</p>",
            [new("b@x.hn", "B"), new("a@x.hn", "A")], true);
        var b = a with { Invitados = [new("A@X.HN", "A"), new("b@x.hn", "B")] };

        a.Huella().Should().Be(b.Huella()).And.HaveLength(64);
    }

    [Theory]
    [InlineData("asunto")]
    [InlineData("inicio")]
    [InlineData("lugar")]
    [InlineData("cuerpo")]
    [InlineData("invitado")]
    [InlineData("teams")]
    public void La_huella_cambia_con_cualquier_campo_visible(string campo)
    {
        var a = new EventoOutlook("X", new DateTime(2026, 1, 1, 9, 0, 0), new DateTime(2026, 1, 1, 10, 0, 0), "Sala", "<p>x</p>",
            [new("a@x.hn", "A")], true);
        var b = campo switch
        {
            "asunto"   => a with { Asunto = "Y" },
            "inicio"   => a with { Inicio = a.Inicio.AddMinutes(30) },
            "lugar"    => a with { Ubicacion = "Otra sala" },
            "cuerpo"   => a with { CuerpoHtml = "<p>y</p>" },
            "invitado" => a with { Invitados = [new("a@x.hn", "A"), new("c@x.hn", "C")] },
            _          => a with { EsTeams = false }
        };

        b.Huella().Should().NotBe(a.Huella());
    }
}
