using System.Globalization;
using Diger.TramitesEstado.Application.Common.Extensions;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Comun;

/// <summary>
/// Fechas en palabras (<c>ToFechaLarga</c> / <c>ToFechaLargaConDia</c>), las que lee el afiche
/// del QR. El punto de estas pruebas es la cultura: el host no configura localización, así que
/// si los ayudantes no la fijaran, el mismo afiche saldría en español o en inglés según el
/// idioma del servidor que lo genere.
/// </summary>
public class FechaEnPalabrasTests
{
    [Fact]
    public void La_fecha_larga_va_en_espanol()
    {
        new DateOnly(2026, 9, 24).ToFechaLarga().Should().Be("24 de septiembre de 2026");
    }

    [Fact]
    public void La_fecha_larga_con_dia_empieza_con_el_dia_de_la_semana_en_mayuscula()
    {
        new DateOnly(2026, 9, 24).ToFechaLargaConDia().Should().Be("Jueves 24 de septiembre de 2026");
    }

    [Fact]
    public void No_depende_de_la_cultura_del_servidor()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            new DateOnly(2026, 10, 5).ToFechaLargaConDia()
                .Should().Be("Lunes 5 de octubre de 2026",
                    "un servidor en inglés no debe producir un afiche en inglés");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Una_fecha_nula_no_produce_texto()
    {
        DateOnly? sinFecha = null;

        sinFecha.ToFechaLarga().Should().BeNull();
        sinFecha.ToFechaLargaConDia().Should().BeNull();
    }
}
