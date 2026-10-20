using Diger.TramitesEstado.Application.Siger.Promocion;
using Diger.TramitesEstado.Application.Siger.Publico;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Promocion;

/// <summary>
/// El expediente guarda la modalidad como texto libre; SIGER tiene un CHECK cerrado de tres
/// valores. Los casos de abajo no son inventados: son las diez variantes que existen hoy en
/// ExpedienteTramites, con su conteo real. Si esta conversión falla, la promoción revienta
/// contra CK_TramitesSiger_Modalidad en vez de guardar.
/// </summary>
public sealed class ModalidadNormalizadorTests
{
    [Theory]
    [InlineData("En línea")]                    // 166 filas
    [InlineData("En linea")]                    // 1  — sin tilde
    [InlineData("En línea (total)")]            // 12
    [InlineData("Trámite en línea")]            // 3
    [InlineData("En línea Tipo de solicitud")]  // 1  — dato sucio, pero es en línea
    public void Texto_de_en_linea_da_Virtual(string texto) =>
        ModalidadNormalizador.Normalizar(texto).Should().Be(ModalidadPublica.Virtual);

    [Theory]
    [InlineData("En línea / Presencial")]  // 2
    [InlineData("En línea, Presencial")]   // 14
    public void Texto_con_ambas_da_Hibrido(string texto) =>
        ModalidadNormalizador.Normalizar(texto).Should().Be(ModalidadPublica.Hibrido);

    [Fact]
    public void Presencial_da_Presencial() =>
        ModalidadNormalizador.Normalizar("Presencial").Should().Be(ModalidadPublica.Presencial);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Vacio_no_inventa_modalidad(string? texto) =>
        ModalidadNormalizador.Normalizar(texto).Should().BeNull();

    [Fact]
    public void Texto_que_no_dice_nada_no_inventa_modalidad() =>
        ModalidadNormalizador.Normalizar("Tipo de solicitud").Should().BeNull();

    /// <summary>
    /// El caso que más fácil se rompe: «Hibrido» no contiene ni «linea» ni «presencial», así que
    /// una conversión que solo mire palabras clave lo convertiría en null. Como el normalizador
    /// corre en cada guardado, eso borraría la modalidad de los trámites híbridos cada vez que
    /// alguien tocara el expediente.
    /// </summary>
    [Theory]
    [InlineData(ModalidadPublica.Virtual)]
    [InlineData(ModalidadPublica.Presencial)]
    [InlineData(ModalidadPublica.Hibrido)]
    public void Un_valor_que_ya_es_del_catalogo_pasa_intacto(string valor) =>
        ModalidadNormalizador.Normalizar(valor).Should().Be(valor);
}
