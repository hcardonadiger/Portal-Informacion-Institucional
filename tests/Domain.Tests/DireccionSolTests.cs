using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests;

/// <summary>
/// Cómo se arma una dirección de SOL.
///
/// Estas pruebas valen más de lo que aparenta una concatenación de cadenas. Desde la Fase 7 la
/// dirección de cada trámite <b>se compone</b> en vez de escribirse: una barra de más no rompe
/// un enlace, rompe todos a la vez, y en el portal que ve el ciudadano. Y un enlace mal armado no
/// da error en ninguna parte — se ve perfecto y lleva a un 404 que nadie reporta.
/// </summary>
public sealed class DireccionSolTests
{
    private const string Host = "https://sol.pdihonduras.gob.hn";

    // ── Composición ───────────────────────────────────────────────────────────

    [Fact]
    public void Con_tramo_la_direccion_se_arma_con_la_ruta_de_la_institucion()
    {
        DireccionSol.Componer(Host, "CONSUCOOP", "licencia-de-operacion", urlHeredada: null)
            .Should().Be("https://sol.pdihonduras.gob.hn/CONSUCOOP/licencia-de-operacion");
    }

    /// <summary>
    /// El caso que justifica que esto viva en un solo lugar: cada quien escribe las barras a su
    /// manera y todas las formas tienen que dar la misma dirección. Repartido por cuatro
    /// pantallas, bastaría con que una olvidara recortar para producir <c>//</c>.
    /// </summary>
    [Theory]
    [InlineData("https://sol.pdihonduras.gob.hn/", "CONSUCOOP",  "licencia")]
    [InlineData("https://sol.pdihonduras.gob.hn",  "/CONSUCOOP", "licencia")]
    [InlineData("https://sol.pdihonduras.gob.hn",  "CONSUCOOP/", "/licencia")]
    [InlineData("https://sol.pdihonduras.gob.hn/", "/CONSUCOOP/", "/licencia/")]
    [InlineData("https://sol.pdihonduras.gob.hn",  "CONSUCOOP",  "  licencia  ")]
    public void Las_barras_de_sobra_dan_igual(string host, string ruta, string tramo)
    {
        DireccionSol.Componer(host, ruta, tramo, urlHeredada: null)
            .Should().Be("https://sol.pdihonduras.gob.hn/CONSUCOOP/licencia");
    }

    [Fact]
    public void Un_tramo_de_varios_niveles_se_respeta()
    {
        DireccionSol.Componer(Host, "IHTT", "permisos/explotacion", urlHeredada: null)
            .Should().Be("https://sol.pdihonduras.gob.hn/IHTT/permisos/explotacion");
    }

    // ── La URL heredada (D-14) ────────────────────────────────────────────────

    /// <summary>Sin tramo manda la heredada, y se devuelve intacta: D-14 dice que no se toca.</summary>
    [Fact]
    public void Sin_tramo_vale_la_url_heredada_tal_cual()
    {
        DireccionSol.Componer(Host, "CONSUCOOP", tramo: null, urlHeredada: "https://otro.sitio.hn/x?y=1")
            .Should().Be("https://otro.sitio.hn/x?y=1");
    }

    /// <summary>
    /// Cuando hay las dos, gana el tramo. Es lo que hace que capturar el tramo de una ficha vieja
    /// sirva de algo: si mandara la heredada, corregir una dirección mala sería imposible sin
    /// borrarla antes.
    /// </summary>
    [Fact]
    public void Con_las_dos_manda_el_tramo()
    {
        DireccionSol.Componer(Host, "CONSUCOOP", "nuevo", urlHeredada: "https://google.com")
            .Should().Be("https://sol.pdihonduras.gob.hn/CONSUCOOP/nuevo");
    }

    [Fact]
    public void Sin_tramo_y_sin_heredada_no_hay_direccion()
    {
        DireccionSol.Componer(Host, "CONSUCOOP", tramo: null, urlHeredada: null)
            .Should().BeNull();
    }

    /// <summary>
    /// Sin host configurado no se inventa una dirección relativa. Un <c>/CONSUCOOP/licencia</c>
    /// suelto lo resolvería el navegador contra el portal equivocado y el enlace apuntaría a un
    /// sitio que no es SOL — peor que no tener enlace.
    /// </summary>
    [Fact]
    public void Sin_host_no_se_devuelve_una_direccion_a_medias()
    {
        DireccionSol.Componer(urlBase: "", "CONSUCOOP", "licencia", urlHeredada: null)
            .Should().BeNull();

        DireccionSol.Componer(urlBase: null, "CONSUCOOP", "licencia", urlHeredada: "https://viejo.hn/x")
            .Should().Be("https://viejo.hn/x", "si no se puede componer, al menos queda la heredada");
    }

    // ── Qué se acepta ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("licencia")]
    [InlineData("licencia-de-operacion")]
    [InlineData("tramite_01")]
    [InlineData("permisos/explotacion/renovacion")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Un_tramo_bien_escrito_se_acepta(string? valor) =>
        DireccionSol.EsSegmentoValido(valor).Should().BeTrue();

    /// <summary>
    /// Se rechaza en vez de escaparse. Un espacio o una tilde acá casi nunca es una dirección
    /// exótica: es un descuido de captura, y escaparlo produciría un enlace que existe, se ve
    /// bien y no lleva a ninguna parte.
    /// </summary>
    [Theory]
    [InlineData("licencia de operacion")]
    [InlineData("licencía")]
    [InlineData("https://sol.pdihonduras.gob.hn/CONSUCOOP/licencia")]
    [InlineData("licencia?id=3")]
    [InlineData("licencia#ancla")]
    public void Un_tramo_que_no_puede_ir_en_una_direccion_se_rechaza(string valor) =>
        DireccionSol.EsSegmentoValido(valor).Should().BeFalse();

    // ── El prefijo que ve la persona (D-13) ───────────────────────────────────

    [Fact]
    public void El_prefijo_es_lo_que_va_delante_de_lo_que_la_persona_escribe()
    {
        DireccionSol.Prefijo(Host, "CONSUCOOP").Should().Be("https://sol.pdihonduras.gob.hn/CONSUCOOP/");
    }

    /// <summary>El prefijo tiene que ser exactamente lo que se va a componer. Si enseñara una
    /// cosa y se guardara otra, la pantalla estaría mintiendo sobre el resultado.</summary>
    [Fact]
    public void El_prefijo_y_la_composicion_no_pueden_discrepar()
    {
        var prefijo = DireccionSol.Prefijo(Host, "IHTT");
        var completa = DireccionSol.Componer(Host, "IHTT", "permiso", urlHeredada: null);

        completa.Should().Be(prefijo + "permiso");
    }

    // ── La ruta de la institución (D-20) ──────────────────────────────────────

    [Fact]
    public void Sin_corregir_nada_la_ruta_de_una_institucion_es_su_llave()
    {
        var inst = Institucion.Crear("CONSUCOOP", "Consejo Nacional Supervisor de Cooperativas");

        inst.RutaSol.Should().BeNull("nadie la ha corregido");
        inst.RutaSolEfectiva.Should().Be("CONSUCOOP");
    }

    [Fact]
    public void Corregir_la_ruta_cambia_lo_que_se_compone()
    {
        var inst = Institucion.Crear("CANATURHIHT", "CANATURH / IHT");
        inst.FijarRutaSol("canaturh");

        inst.RutaSol.Should().Be("canaturh");
        inst.RutaSolEfectiva.Should().Be("canaturh");
    }

    /// <summary>
    /// Vaciar el campo la devuelve al valor por defecto en vez de dejarla sin ruta. Borrar en un
    /// formulario se lee como «vuelve a lo de siempre», no como «esta institución se queda sin
    /// dirección».
    /// </summary>
    [Fact]
    public void Vaciar_la_ruta_la_devuelve_a_la_llave()
    {
        var inst = Institucion.Crear("IHTT", "Instituto Hondureño de Transporte Terrestre");
        inst.FijarRutaSol("transporte");
        inst.FijarRutaSol("   ");

        inst.RutaSol.Should().BeNull();
        inst.RutaSolEfectiva.Should().Be("IHTT");
    }

    /// <summary>
    /// Ponerla igual a la llave es no haberla corregido. Guardarla como valor propio haría que
    /// una institución que cambie de llave arrastrara la vieja para siempre, sin que nadie
    /// recuerde que ese valor no lo eligió una persona.
    /// </summary>
    [Fact]
    public void Fijarla_igual_a_la_llave_es_lo_mismo_que_no_fijarla()
    {
        var inst = Institucion.Crear("IHTT", "Instituto Hondureño de Transporte Terrestre");
        inst.FijarRutaSol("IHTT");

        inst.RutaSol.Should().BeNull();
    }

    [Fact]
    public void Una_ruta_que_no_puede_ir_en_una_direccion_se_rechaza()
    {
        var inst = Institucion.Crear("IHTT", "Instituto Hondureño de Transporte Terrestre");

        var acto = () => inst.FijarRutaSol("transporte terrestre");

        acto.Should().Throw<DomainException>();
        inst.RutaSolEfectiva.Should().Be("IHTT", "un valor rechazado no deja la ruta a medias");
    }

    /// <summary>
    /// La llave de una institución siempre puede ir en una dirección: la factoría solo admite
    /// mayúsculas, números, guion y guion bajo. Es lo que hace que D-20 funcione sin escapar
    /// nada — y si algún día se aflojara esa validación, esta prueba lo diría.
    /// </summary>
    [Fact]
    public void La_llave_de_una_institucion_siempre_sirve_como_ruta()
    {
        foreach (var llave in new[] { "CONSUCOOP", "IHTT", "ALCALDIACOMAYAGUA", "SDE-01", "X_2" })
        {
            var inst = Institucion.Crear(llave, $"Institución {llave}");
            DireccionSol.EsSegmentoValido(inst.RutaSolEfectiva).Should().BeTrue($"«{llave}» tiene que servir");
        }
    }
}
