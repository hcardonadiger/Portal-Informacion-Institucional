using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests.Entities;

/// <summary>
/// La ventana inicio/fin de una reunión: lo que después consume cualquier calendario. Se prueba en
/// el dominio porque la regla —incluida la duración supuesta— tiene que dar lo mismo para el .ics,
/// para Outlook y para la pantalla del portal.
/// </summary>
public class ReunionVentanaTests
{
    private static Reunion Reunion(DateOnly? fecha, string? hora, int? duracion = null)
    {
        var r = Domain.Entities.Reunion.Crear("Mesa técnica");
        r.Fecha = fecha;
        r.Hora = hora;
        r.DuracionMinutos = duracion;
        return r;
    }

    [Fact]
    public void Sin_fecha_no_hay_ventana()
    {
        var r = Reunion(null, "09:00", 60);

        r.InicioLocal.Should().BeNull();
        r.FinLocal.Should().BeNull();
    }

    [Fact]
    public void Con_hora_y_duracion_el_fin_es_el_inicio_mas_la_duracion()
    {
        var r = Reunion(new DateOnly(2026, 9, 15), "09:30", 90);

        r.InicioLocal.Should().Be(new DateTime(2026, 9, 15, 9, 30, 0));
        r.FinLocal.Should().Be(new DateTime(2026, 9, 15, 11, 0, 0));
        r.EsTodoElDia.Should().BeFalse();
    }

    /// <summary>El caso de las 44 reuniones importadas: hay hora, no hay duración.</summary>
    [Fact]
    public void Sin_duracion_declarada_se_supone_una_hora()
    {
        var r = Reunion(new DateOnly(2026, 9, 15), "14:00");

        r.DuracionEfectivaMinutos.Should().Be(Domain.Entities.Reunion.DuracionPredeterminadaMinutos);
        r.FinLocal.Should().Be(new DateTime(2026, 9, 15, 15, 0, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Una_duracion_no_positiva_no_se_usa(int minutos)
    {
        var r = Reunion(new DateOnly(2026, 9, 15), "14:00", minutos);

        r.DuracionEfectivaMinutos.Should().Be(60);
    }

    /// <summary>Sin hora es un evento de día completo, no uno a medianoche: son cosas distintas y
    /// los calendarios las representan distinto.</summary>
    [Fact]
    public void Sin_hora_es_evento_de_dia_completo()
    {
        var r = Reunion(new DateOnly(2026, 9, 15), null, 90);

        r.EsTodoElDia.Should().BeTrue();
        r.InicioLocal.Should().Be(new DateTime(2026, 9, 15, 0, 0, 0));
        r.FinLocal.Should().Be(new DateTime(2026, 9, 16, 0, 0, 0));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("por definir")]
    public void Una_hora_ilegible_se_trata_como_dia_completo(string hora)
    {
        var r = Reunion(new DateOnly(2026, 9, 15), hora);

        r.HoraInicio.Should().BeNull();
        r.EsTodoElDia.Should().BeTrue();
    }

    /// <summary>La ventana no lleva zona: es hora de pared. Convertirla es de quien conoce la zona
    /// configurada, y que el Kind sea Unspecified es lo que impide que alguien la trate como UTC.</summary>
    [Fact]
    public void La_ventana_es_hora_de_pared_sin_zona()
    {
        var r = Reunion(new DateOnly(2026, 9, 15), "09:00", 60);

        r.InicioLocal!.Value.Kind.Should().Be(DateTimeKind.Unspecified);
        r.FinLocal!.Value.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void Una_reunion_que_cruza_la_medianoche_termina_al_dia_siguiente()
    {
        var r = Reunion(new DateOnly(2026, 9, 15), "23:00", 120);

        r.FinLocal.Should().Be(new DateTime(2026, 9, 16, 1, 0, 0));
    }
}
