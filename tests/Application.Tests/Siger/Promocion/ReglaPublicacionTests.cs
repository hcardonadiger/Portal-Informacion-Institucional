using Diger.TramitesEstado.Application.Siger.Promocion;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Promocion;

/// <summary>
/// La única regla que decide si una ficha llega al ciudadano. Vive en Application y no en la
/// página del editor porque la promoción la necesita igual: dos copias de esta regla acabarían
/// discrepando, y la discrepancia se vería en el portal público.
/// </summary>
public sealed class ReglaPublicacionTests
{
    [Theory]
    [InlineData("Aprobado")]
    [InlineData("Completo")]
    public void Aprobado_y_Completo_se_publican(string estado) =>
        ReglaPublicacion.SePublica(estado).Should().BeTrue();

    [Theory]
    [InlineData("Registrado")]
    [InlineData("En revisión")]
    [InlineData("")]
    [InlineData(null)]
    public void Cualquier_otro_estado_no_se_publica(string? estado) =>
        ReglaPublicacion.SePublica(estado).Should().BeFalse();

    [Fact]
    public void Una_ficha_promovida_nace_sin_publicar() =>
        ReglaPublicacion.SePublica(ReglaPublicacion.Registrado).Should().BeFalse();

    /// <summary>
    /// La completitud dejó de censurar (P-09, opción 1). Una ficha aprobada a la que le falta
    /// la categoría o el costo se publica igual: el ciudadano ve el trámite con un guion donde
    /// falta el dato, en vez de no encontrarlo. Si alguien vuelve a atar publicación con
    /// completitud, esta prueba lo detiene.
    /// </summary>
    [Fact]
    public void Una_ficha_aprobada_pero_incompleta_se_publica_igual() =>
        ReglaPublicacion.SePublica(ReglaPublicacion.Aprobado).Should().BeTrue();
}
