using Diger.TramitesEstado.Application.Siger;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger;

public class ConciliacionMatcherTests
{
    // ── Normalización ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Modificación de Licencia", "MODIFICACION DE LICENCIA")]
    [InlineData("Prórroga de estadía",      "PRORROGA DE ESTADIA")]
    [InlineData("Inscripción de Gerente",   "INSCRIPCION DE GERENTE")]
    public void Normalizar_QuitaTildesYPasaAMayusculas(string entrada, string esperado)
        => ConciliacionMatcher.Normalizar(entrada).Should().Be(esperado);

    [Theory]
    [InlineData("  Registro   de   vehiculo  ", "REGISTRO DE VEHICULO")]
    [InlineData("Registro\tde\tvehiculo",       "REGISTRO DE VEHICULO")]
    public void Normalizar_ColapsaEspaciosYTabulaciones(string entrada, string esperado)
        => ConciliacionMatcher.Normalizar(entrada).Should().Be(esperado);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalizar_TextoVacio_DevuelveCadenaVacia(string? entrada)
        => ConciliacionMatcher.Normalizar(entrada).Should().BeEmpty();

    /// <summary>
    /// Caso real del inventario: dos fichas de IP que solo difieren en mayúsculas y tildes.
    /// Sin normalizar no se detectan como duplicadas.
    /// </summary>
    [Fact]
    public void Normalizar_FichasQueSoloDifierenEnMayusculasYTildes_QuedanIguales()
    {
        var a = ConciliacionMatcher.Normalizar("Traspaso o cambio de Propietario de vehículo de persona Natural a persona Jurídica.");
        var b = ConciliacionMatcher.Normalizar("Traspaso o cambio de propietario de vehiculo de persona natural a persona juridica.");

        a.Should().Be(b);
    }

    // ── Clasificación ─────────────────────────────────────────────────────────

    [Fact]
    public void Clasificar_UnExactoConMismaSigla_EsAltaConfianza()
        => ConciliacionMatcher.Clasificar(exactos: 1, exactosMismaSigla: 1, parciales: 0)
            .Should().Be(CubetaConciliacion.AltaConfianza);

    [Fact]
    public void Clasificar_UnExactoConSiglaDistinta_EsMediaConfianza()
        => ConciliacionMatcher.Clasificar(exactos: 1, exactosMismaSigla: 0, parciales: 0)
            .Should().Be(CubetaConciliacion.MediaConfianza);

    [Fact]
    public void Clasificar_VariosExactos_EsAmbiguo()
        => ConciliacionMatcher.Clasificar(exactos: 2, exactosMismaSigla: 1, parciales: 0)
            .Should().Be(CubetaConciliacion.Ambiguo);

    [Fact]
    public void Clasificar_SinExactosPeroConParciales_EsParcial()
        => ConciliacionMatcher.Clasificar(exactos: 0, exactosMismaSigla: 0, parciales: 3)
            .Should().Be(CubetaConciliacion.Parcial);

    [Fact]
    public void Clasificar_SinNingunCandidato_EsSinCandidato()
        => ConciliacionMatcher.Clasificar(exactos: 0, exactosMismaSigla: 0, parciales: 0)
            .Should().Be(CubetaConciliacion.SinCandidato);

    /// <summary>
    /// Un nombre ambiguo no debe degradarse a alta confianza solo porque una de las fichas
    /// comparta la sigla: si hay dos candidatos, la elección es humana.
    /// </summary>
    [Fact]
    public void Clasificar_DosExactosAmbosDeLaMismaInstitucion_SigueSiendoAmbiguo()
        => ConciliacionMatcher.Clasificar(exactos: 2, exactosMismaSigla: 2, parciales: 0)
            .Should().Be(CubetaConciliacion.Ambiguo);
}
