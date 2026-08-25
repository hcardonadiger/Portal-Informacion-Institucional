using Diger.TramitesEstado.Application.Siger.Publico;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Publico;

/// <summary>
/// La regla que decide si una ficha se publica y, ahora, qué le falta para publicarse. Se prueba
/// aparte de las consultas porque la alerta que ve el técnico en PortalDigital y el filtro
/// <c>?soloFichasCompletas=true</c> que ve el ciudadano dependen los dos de ella: si discrepan,
/// el portal muestra fichas que el inventario declara incompletas.
/// </summary>
public sealed class FichaPublicaCompletitudTests
{
    // Una ficha a la que no le falta nada. Cada prueba de abajo le quita exactamente un dato.
    private const int    Categoria = 3;
    private const string Modalidad = ModalidadPublica.Presencial;
    private const string Tiempo    = "5 días hábiles";

    [Fact]
    public void Ficha_con_todo_capturado_no_tiene_faltantes()
    {
        var faltantes = FichaPublicaCompletitud.CamposFaltantes(
            Categoria, Modalidad, Tiempo, costoEsGratuito: true, estaEnSol: false, solUrl: null, solTramo: null);

        faltantes.Should().BeEmpty();
        FichaPublicaCompletitud.Evaluar(
            Categoria, Modalidad, Tiempo, true, false, null, null).Should().BeTrue();
    }

    [Fact]
    public void Sin_categoria_la_reporta_como_faltante()
    {
        FichaPublicaCompletitud.CamposFaltantes(null, Modalidad, Tiempo, true, false, null, null)
            .Should().Equal("categoría");
    }

    [Fact]
    public void Sin_modalidad_la_reporta_como_faltante()
    {
        FichaPublicaCompletitud.CamposFaltantes(Categoria, null, Tiempo, true, false, null, null)
            .Should().Equal("modalidad");
    }

    [Fact]
    public void Sin_tiempo_lo_reporta_como_faltante()
    {
        FichaPublicaCompletitud.CamposFaltantes(Categoria, Modalidad, null, true, false, null, null)
            .Should().Equal("tiempo");
    }

    [Fact]
    public void Sin_costo_lo_reporta_como_faltante()
    {
        FichaPublicaCompletitud.CamposFaltantes(Categoria, Modalidad, Tiempo, null, false, null, null)
            .Should().Equal("costo");
    }

    /// <summary>El costo es "gratuito sí/no", no el monto: una ficha marcada como gratuita está
    /// completa aunque no tenga texto de costo. Es la razón por la que las 49 fichas del piloto
    /// pueden publicarse sin que nadie escriba un monto.</summary>
    [Fact]
    public void Marcar_gratuito_completa_el_costo_aunque_no_haya_monto()
    {
        FichaPublicaCompletitud.CamposFaltantes(Categoria, Modalidad, Tiempo, true, false, null, null)
            .Should().NotContain("costo");
    }

    [Fact]
    public void El_enlace_a_SOL_solo_se_exige_cuando_el_tramite_esta_en_SOL()
    {
        FichaPublicaCompletitud.CamposFaltantes(Categoria, Modalidad, Tiempo, true, estaEnSol: true, solUrl: null, solTramo: null)
            .Should().Equal("enlace a SOL");

        FichaPublicaCompletitud.CamposFaltantes(Categoria, Modalidad, Tiempo, true, estaEnSol: false, solUrl: null, solTramo: null)
            .Should().BeEmpty();
    }

    /// <summary>
    /// Desde la Fase 7 el enlace puede venir de dos sitios: el tramo que se captura hoy o la URL
    /// completa heredada. Cualquiera de los dos completa la ficha.
    ///
    /// Si esta regla siguiera mirando solo la heredada, <b>toda</b> ficha capturada como manda
    /// D-13 quedaría declarada incompleta y desaparecería del catálogo en cuanto el portal
    /// filtrara por completas — justo las fichas recién trabajadas.
    /// </summary>
    [Fact]
    public void El_tramo_capturado_completa_el_enlace_igual_que_la_url_heredada()
    {
        FichaPublicaCompletitud.CamposFaltantes(
            Categoria, Modalidad, Tiempo, true, estaEnSol: true,
            solUrl: null, solTramo: "licencia-de-operacion")
            .Should().BeEmpty("el tramo es el enlace, solo que sin el prefijo");

        FichaPublicaCompletitud.CamposFaltantes(
            Categoria, Modalidad, Tiempo, true, estaEnSol: true,
            solUrl: "https://sol.gob.hn/CONSUCOOP/algo", solTramo: null)
            .Should().BeEmpty("la dirección heredada sigue valiendo (D-14)");
    }

    /// <summary>El orden importa: es el que sigue el editor, y la alerta se lee como una lista de
    /// trabajo de arriba abajo.</summary>
    [Fact]
    public void Los_faltantes_salen_en_el_orden_del_editor()
    {
        FichaPublicaCompletitud.CamposFaltantes(null, null, null, null, estaEnSol: true, solUrl: null, solTramo: null)
            .Should().Equal("categoría", "modalidad", "tiempo", "costo", "enlace a SOL");
    }

    /// <summary>Evaluar no puede decir "completa" mientras CamposFaltantes tenga algo, porque la
    /// primera decide qué se publica y la segunda qué se le pide al técnico.</summary>
    [Theory]
    [InlineData(null,  Modalidad, Tiempo, true,  false)]
    [InlineData(3,     null,      Tiempo, true,  false)]
    [InlineData(3,     Modalidad, null,   true,  false)]
    [InlineData(3,     Modalidad, Tiempo, null,  false)]
    [InlineData(3,     Modalidad, Tiempo, true,  true)]
    [InlineData(3,     Modalidad, Tiempo, true,  false)]
    public void Evaluar_y_CamposFaltantes_nunca_se_contradicen(
        int? categoriaId, string? modalidad, string? tiempo, bool? gratuito, bool estaEnSol)
    {
        var faltantes = FichaPublicaCompletitud.CamposFaltantes(
            categoriaId, modalidad, tiempo, gratuito, estaEnSol, solUrl: null, solTramo: null);
        var completa = FichaPublicaCompletitud.Evaluar(
            categoriaId, modalidad, tiempo, gratuito, estaEnSol, solUrl: null, solTramo: null);

        completa.Should().Be(faltantes.Count == 0);
    }

    [Fact]
    public void La_frase_enumera_lo_que_falta_y_reconoce_la_ficha_completa()
    {
        FichaPublicaCompletitud.Frase([])
            .Should().Be("La ficha pública está completa.");

        FichaPublicaCompletitud.Frase(["categoría", "tiempo"])
            .Should().Be("Falta capturar: categoría, tiempo.");
    }
}
