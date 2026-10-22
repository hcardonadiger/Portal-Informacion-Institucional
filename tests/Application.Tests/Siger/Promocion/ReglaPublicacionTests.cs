using Diger.TramitesEstado.Application.Siger.Promocion;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Promocion;

/// <summary>
/// El consejo —ya no la decisión— sobre si una ficha está lista para llegar al ciudadano.
/// Desde la Fase 4 la publicación es manual: esta regla solo separa las candidatas del resto y
/// decide si la pantalla muestra un aviso. Vive en Application y no en la página porque la
/// promoción desde un expediente necesita la misma respuesta, y dos copias acabarían
/// discrepando a la vista del público.
/// </summary>
public sealed class ReglaPublicacionTests
{
    [Theory]
    [InlineData("Aprobado")]
    [InlineData("Completo")]
    public void Aprobado_y_Completo_estan_listos(string estado) =>
        ReglaPublicacion.EstadoListoParaPublicar(estado).Should().BeTrue();

    [Theory]
    [InlineData("Registrado")]
    [InlineData("En revisión")]
    [InlineData("")]
    [InlineData(null)]
    public void Cualquier_otro_estado_no_esta_listo(string? estado) =>
        ReglaPublicacion.EstadoListoParaPublicar(estado).Should().BeFalse();

    /// <summary>Una ficha promovida nace Registrado, así que ni siquiera aparece como candidata
    /// hasta que alguien la apruebe. Promover y publicar son dos actos distintos.</summary>
    [Fact]
    public void Una_ficha_recien_promovida_no_es_candidata() =>
        ReglaPublicacion.EstadoListoParaPublicar(ReglaPublicacion.Registrado).Should().BeFalse();

    /// <summary>
    /// La completitud no entra en esta regla y no debe entrar (P-09, opción 1). Una ficha
    /// aprobada a la que le falta la categoría o el costo sigue siendo candidata: la pantalla la
    /// marca con un aviso, y quien administra decide. El ciudadano ve el trámite con un guion
    /// donde falta el dato, en vez de no encontrarlo. Si alguien vuelve a atar publicación con
    /// completitud, esta prueba lo detiene.
    /// </summary>
    [Fact]
    public void La_regla_no_mira_la_completitud_de_la_ficha() =>
        ReglaPublicacion.EstadoListoParaPublicar(ReglaPublicacion.Aprobado).Should().BeTrue();
}
