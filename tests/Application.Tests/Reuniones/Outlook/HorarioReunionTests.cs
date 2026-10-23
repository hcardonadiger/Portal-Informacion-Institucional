using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Domain.Common;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones.Outlook;

/// <summary>
/// Hora y Duracion son texto libre en la base. Medido en GestionGD_new el 2026-10-22: Hora siempre
/// HH:mm; Duracion vacía en 79 de 81, y las otras dos «1 hora» y «1». Lo que no se pueda leer se
/// rechaza nombrando el valor: una cita a la hora equivocada es peor que ninguna.
/// </summary>
public class HorarioReunionTests
{
    private static readonly DateOnly Dia = new(2026, 10, 23);

    [Theory]
    [InlineData("09:00", 9, 0)]
    [InlineData("14:00", 14, 0)]
    [InlineData("13:59", 13, 59)]
    [InlineData("02:00", 2, 0)]
    [InlineData(" 9:30 ", 9, 30)]
    public void Lee_la_hora_HH_mm(string hora, int h, int m)
    {
        var r = HorarioReunion.Resolver(Dia, hora, null);

        r.Inicio.Should().Be(new DateTime(2026, 10, 23, h, m, 0));
    }

    [Theory]
    [InlineData(null, 60)]
    [InlineData("", 60)]
    [InlineData("1", 60)]
    [InlineData("1 hora", 60)]
    [InlineData("2 horas", 120)]
    [InlineData("1.5 horas", 90)]
    [InlineData("1,5 h", 90)]
    [InlineData("90 min", 90)]
    [InlineData("45 minutos", 45)]
    [InlineData("1:30", 90)]
    public void Lee_la_duracion(string? duracion, int minutos)
    {
        var r = HorarioReunion.Resolver(Dia, "09:00", duracion);

        (r.Fin - r.Inicio).TotalMinutes.Should().Be(minutos);
    }

    [Fact]
    public void Sin_fecha_se_rechaza()
    {
        var acto = () => HorarioReunion.Resolver(null, "09:00", null);

        acto.Should().Throw<DomainException>().WithMessage("*fecha y hora*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sin_hora_se_rechaza(string? hora)
    {
        var acto = () => HorarioReunion.Resolver(Dia, hora, null);

        acto.Should().Throw<DomainException>().WithMessage("*fecha y hora*");
    }

    [Theory]
    [InlineData("9 am")]
    [InlineData("25:00")]
    [InlineData("10:75")]
    [InlineData("mañana")]
    public void Una_hora_ilegible_se_rechaza_nombrando_el_valor(string hora)
    {
        var acto = () => HorarioReunion.Resolver(Dia, hora, null);

        acto.Should().Throw<DomainException>().WithMessage($"*«{hora}»*");
    }

    [Theory]
    [InlineData("toda la mañana")]
    [InlineData("0")]
    [InlineData("0 min")]
    [InlineData("30 horas")]
    public void Una_duracion_ilegible_o_absurda_se_rechaza(string duracion)
    {
        var acto = () => HorarioReunion.Resolver(Dia, "09:00", duracion);

        acto.Should().Throw<DomainException>().WithMessage($"*«{duracion}»*");
    }
}
