using Diger.TramitesEstado.Application.Siger.Promocion;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Promocion;

/// <summary>
/// El código de una ficha es único y de 20 caracteres como máximo. Una ficha nacida en el
/// portal no tiene código de SIGER, así que se genera uno con el prefijo que esa institución
/// ya usa (400 = Aduanas) más una P de portal, para que se distinga a simple vista de las
/// 1.057 importadas.
/// </summary>
public sealed class CodigoPromovidoTests
{
    [Theory]
    [InlineData("400-001", "400")]
    [InlineData("24-104",  "24")]
    [InlineData("950-66",  "950")]
    public void Saca_el_prefijo_del_codigo_de_SIGER(string codigo, string esperado) =>
        CodigoPromovido.PrefijoDe(codigo).Should().Be(esperado);

    [Fact]
    public void Codigo_sin_guion_se_toma_entero_como_prefijo() =>
        CodigoPromovido.PrefijoDe("400").Should().Be("400");

    [Fact]
    public void Primera_ficha_promovida_de_la_institucion_es_P01() =>
        CodigoPromovido.Siguiente("400", []).Should().Be("400-P01");

    [Fact]
    public void Correlativo_continua_desde_el_mayor_existente() =>
        CodigoPromovido.Siguiente("400", ["400-P01", "400-P03", "400-012"])
            .Should().Be("400-P04");

    [Fact]
    public void El_correlativo_es_por_institucion_no_global() =>
        CodigoPromovido.Siguiente("24", ["400-P07", "400-P08"]).Should().Be("24-P01");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Institucion_sin_fichas_en_SIGER_usa_el_prefijo_DGR(string? prefijo) =>
        CodigoPromovido.Siguiente(prefijo, []).Should().Be("DGR-P01");

    [Fact]
    public void El_codigo_generado_cabe_en_la_columna()
    {
        // Codigo es nvarchar(20). Con el prefijo más largo que existe hoy y tres cifras
        // de correlativo sigue sobrando espacio; la prueba lo deja fijado.
        var codigo = CodigoPromovido.Siguiente("9999", ["9999-P998"]);
        codigo.Should().Be("9999-P999");
        codigo.Length.Should().BeLessThanOrEqualTo(20);
    }
}
