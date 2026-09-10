using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Common.Tiempo;
using Diger.TramitesEstado.Application.Reuniones.Common;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

public class RelojInstitucionalTests
{
    private static RelojInstitucional Reloj(string zona) =>
        new(Options.Create(new InstitucionOptions { ZonaHoraria = zona }));

    [Theory]
    [InlineData("America/Tegucigalpa")]
    [InlineData("Central America Standard Time")]
    public void Resuelve_la_zona_de_Honduras_por_cualquiera_de_sus_dos_nombres(string zona)
    {
        var reloj = Reloj(zona);

        reloj.Zona.BaseUtcOffset.Should().Be(TimeSpan.FromHours(-6));
    }

    [Fact]
    public void Una_hora_de_pared_se_convierte_al_instante_con_desfase_menos_seis()
    {
        var reloj = Reloj("America/Tegucigalpa");

        var instante = reloj.AInstante(new DateTime(2026, 9, 15, 9, 0, 0));

        instante.Offset.Should().Be(TimeSpan.FromHours(-6));
        instante.UtcDateTime.Should().Be(new DateTime(2026, 9, 15, 15, 0, 0, DateTimeKind.Utc));
    }

    /// <summary>Honduras no tiene horario de verano: la misma hora de pared da el mismo desfase en
    /// enero y en julio. Si esto falla, alguien cambió la zona configurada por una que sí lo tiene.</summary>
    [Fact]
    public void No_hay_horario_de_verano_en_ninguna_epoca_del_ano()
    {
        var reloj = Reloj("America/Tegucigalpa");

        reloj.AInstante(new DateTime(2026, 1, 15, 9, 0, 0)).Offset
            .Should().Be(reloj.AInstante(new DateTime(2026, 7, 15, 9, 0, 0)).Offset);
    }

    /// <summary>Una zona mal escrita en appsettings no debe impedir que el portal arranque.</summary>
    [Fact]
    public void Una_zona_invalida_cae_al_respaldo_en_vez_de_reventar()
    {
        var reloj = Reloj("Zona/Inventada");

        reloj.ZonaResuelta.Should().NotBe("Zona/Inventada");
        reloj.Zona.BaseUtcOffset.Should().Be(TimeSpan.FromHours(-6));
    }

    /// <summary>La pieza que faltaba para publicar en un calendario: de los campos sueltos de la
    /// reunión a un par de instantes absolutos.</summary>
    [Fact]
    public void La_ventana_de_una_reunion_llega_a_instantes_absolutos()
    {
        var reloj = Reloj("America/Tegucigalpa");
        var r = Reunion.Crear("Mesa técnica");
        r.Fecha = new DateOnly(2026, 9, 15);
        r.Hora = "09:00";
        r.DuracionMinutos = 90;

        var inicio = reloj.AInstante(r.InicioLocal!.Value);
        var fin    = reloj.AInstante(r.FinLocal!.Value);

        inicio.UtcDateTime.Should().Be(new DateTime(2026, 9, 15, 15, 0, 0, DateTimeKind.Utc));
        (fin - inicio).Should().Be(TimeSpan.FromMinutes(90));
    }
}

public class DuracionTextoTests
{
    [Theory]
    [InlineData(30, "30 min")]
    [InlineData(45, "45 min")]
    [InlineData(60, "1 h")]
    [InlineData(90, "1 h 30 min")]
    [InlineData(120, "2 h")]
    [InlineData(480, "8 h")]
    public void Formatea_la_duracion_en_horas_y_minutos(int minutos, string esperado)
    {
        DuracionTexto.Formatear(minutos).Should().Be(esperado);
    }

    /// <summary>Sin duración declarada devuelve null y no «1 h»: el valor supuesto sirve para
    /// calcular la ventana, no para mostrarlo como si la reunión lo hubiera dicho.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Sin_duracion_declarada_no_hay_texto(int? minutos)
    {
        DuracionTexto.Formatear(minutos).Should().BeNull();
    }

    [Fact]
    public void Todas_las_opciones_del_selector_se_formatean_igual_que_su_etiqueta()
    {
        // La etiqueta de «jornada completa» agrega texto, así que se compara que lo contenga.
        foreach (var (minutos, texto) in DuracionTexto.Opciones)
            texto.Should().Contain(DuracionTexto.Formatear(minutos));
    }
}
