using Diger.TramitesEstado.Application.Siger.Llenado;
using Diger.TramitesEstado.Application.Siger.Publico;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Llenado;

/// <summary>
/// Las reglas que proponen valores para los huecos de una ficha SIGER.
///
/// Son 1 032 fichas incompletas y cada regla acá decide qué se le va a poner enfrente a quien
/// revise. Lo que más importa de estas pruebas no es que las reglas acierten —la persona corrige
/// lo que fallen— sino que <b>callen cuando no saben</b>: una propuesta inventada tiene la misma
/// apariencia que una derivada, y quien aprueba por tandas no puede distinguirlas.
/// </summary>
public sealed class ReglasLlenadoTests
{
    private static readonly Dictionary<string, int> Catalogo = new()
    {
        ["salud y seguridad social"] = 1,
        ["educacion y cultura"]      = 2,
        ["impuestos y finanzas"]     = 3,
        ["identidad y ciudadania"]   = 4,
        ["empresas y negocios"]      = 5,
        ["vivienda y propiedad"]     = 6,
        ["transporte y vehiculos"]   = 7,
        ["medio ambiente"]           = 8
    };

    private static readonly IReadOnlySet<CampoFicha> Todos =
        new HashSet<CampoFicha> { CampoFicha.Categoria, CampoFicha.Modalidad, CampoFicha.Tiempo, CampoFicha.Costo };

    private static DatosParaLlenado Ficha(
        string nombre = "Trámite de prueba",
        string? descripcion = null,
        string? objetivo = null,
        string?[]? tiempos = null,
        string[]? pasos = null,
        string[]? requisitos = null,
        int lugares = 0) =>
        new(nombre, descripcion, objetivo, tiempos ?? [], pasos ?? [], requisitos ?? [], lugares);

    private static PropuestaCalculada? Solo(DatosParaLlenado datos, CampoFicha campo) =>
        ReglasLlenado.Proponer(datos, new HashSet<CampoFicha> { campo }, Catalogo)
            .FirstOrDefault(p => p.Campo == campo);

    // ── Tiempo ────────────────────────────────────────────────────────────────

    [Fact]
    public void El_tiempo_es_la_suma_de_los_pasos_y_solo_es_de_certeza_alta_si_todos_declararon()
    {
        var p = Solo(Ficha(tiempos: ["1", "2", "5"]), CampoFicha.Tiempo);

        p.Should().NotBeNull();
        p!.Valor.Should().Be("8 días hábiles");
        p.Certeza.Should().Be(CertezaLlenado.Alta);
        p.Justificacion.Should().Contain("1 + 2 + 5", "quien aprueba por tandas necesita ver la cuenta");
    }

    /// <summary>
    /// Si algún paso no declaró tiempo, la suma se queda corta. El valor sigue sirviendo, pero
    /// prometer menos tiempo del real es el error que más molesta a quien hace el trámite, así que
    /// no puede colarse en una aprobación en bloque de lo «alta».
    /// </summary>
    [Fact]
    public void Un_paso_sin_tiempo_baja_la_certeza_y_lo_dice()
    {
        var p = Solo(Ficha(tiempos: ["3", null, "2"]), CampoFicha.Tiempo);

        p!.Certeza.Should().Be(CertezaLlenado.Media);
        p.Justificacion.Should().Contain("corto");
    }

    [Fact]
    public void Sin_ningun_tiempo_declarado_no_se_propone_nada() =>
        Solo(Ficha(tiempos: [null, "", "  "]), CampoFicha.Tiempo).Should().BeNull();

    /// <summary>Entre prometer 3 días y que tarde 4, o prometer 4 y que tarde 4, el segundo error
    /// no existe.</summary>
    [Fact]
    public void El_tiempo_se_redondea_hacia_arriba() =>
        Solo(Ficha(tiempos: ["1.5", "2.2"]), CampoFicha.Tiempo)!.Valor.Should().Be("4 días hábiles");

    [Theory]
    [InlineData(new[] { "0", "0" },     "El mismo día")]
    [InlineData(new[] { "0.1", "0.5" }, "Menos de 1 día hábil")]
    [InlineData(new[] { "1" },          "1 día hábil")]
    public void Los_tiempos_cortos_se_dicen_en_palabras_y_no_en_fracciones(string[] tiempos, string esperado) =>
        Solo(Ficha(tiempos: tiempos), CampoFicha.Tiempo)!.Valor.Should().Be(esperado);

    /// <summary>El dato se capturó desde varias configuraciones regionales y conviven «0.5» y
    /// «0,5». Sin esto, media Honduras suma cinco días donde hay medio.</summary>
    [Fact]
    public void La_coma_decimal_se_entiende_igual_que_el_punto() =>
        Solo(Ficha(tiempos: ["0,5", "0,5"]), CampoFicha.Tiempo)!.Valor.Should().Be("1 día hábil");

    // ── Costo ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// La prueba más importante del archivo. Que una ficha no mencione ningún pago no prueba que
    /// sea gratuita: puede estar mal capturada. Si esta regla alguna vez decide que el silencio
    /// significa «gratis», el portal público le va a decir al ciudadano que un trámite no cuesta
    /// nada con la misma cara con la que se lo diría un dato verificado.
    /// </summary>
    [Fact]
    public void El_silencio_sobre_el_costo_no_se_interpreta_como_gratuito()
    {
        var p = Solo(Ficha(pasos: ["Presentar la solicitud en ventanilla."]), CampoFicha.Costo);

        p.Should().BeNull("no mencionar un pago no es lo mismo que declararse gratuito");
    }

    [Fact]
    public void Mencionar_un_pago_propone_que_tiene_costo_y_cita_el_texto()
    {
        var p = Solo(Ficha(requisitos: ["Original del recibo de pago emitido por la TGR"]), CampoFicha.Costo);

        p!.Valor.Should().Be("false");
        p.Certeza.Should().Be(CertezaLlenado.Media);
        p.Justificacion.Should().Contain("recibo de pago", "sin la cita nadie puede aprobar sin abrir la ficha");
    }

    [Fact]
    public void Declararse_sin_costo_propone_gratuito() =>
        Solo(Ficha(pasos: ["Este trámite es gratuito."]), CampoFicha.Costo)!.Valor.Should().Be("true");

    /// <summary>«Es gratuito, adjunte el recibo de pago del timbre» aparece de verdad en el
    /// inventario. Cobrar algo no es ser gratuito, pero la duda tiene que verse.</summary>
    [Fact]
    public void Si_el_texto_dice_las_dos_cosas_gana_el_costo_pero_baja_la_certeza()
    {
        var p = Solo(Ficha(
            pasos:      ["El trámite es gratuito."],
            requisitos: ["Adjuntar recibo de pago del timbre."]), CampoFicha.Costo);

        p!.Valor.Should().Be("false");
        p.Certeza.Should().Be(CertezaLlenado.Baja);
        p.Justificacion.Should().Contain("revisarlo");
    }

    // ── Categoría ─────────────────────────────────────────────────────────────

    [Fact]
    public void La_categoria_sale_del_nombre_del_tramite()
    {
        var p = Solo(Ficha("Emisión de licencia de conducir"), CampoFicha.Categoria);

        p!.Valor.Should().Be("7");
        p.Certeza.Should().Be(CertezaLlenado.Media);
    }

    /// <summary>
    /// La descripción arrastra contexto de la institución y despista: la de un instituto forestal
    /// habla de bosques aunque el trámite sea una constancia de identidad. Por eso el nombre gana
    /// siempre que diga algo.
    /// </summary>
    [Fact]
    public void El_nombre_pesa_mas_que_la_descripcion()
    {
        var p = Solo(Ficha(
            nombre:      "Constancia de identidad",
            descripcion: "Emitida por el instituto forestal para trámites de bosque y madera."),
            CampoFicha.Categoria);

        p!.Valor.Should().Be("4", "el nombre habla de identidad; el bosque es ruido de la institución");
        p.Certeza.Should().Be(CertezaLlenado.Media);
    }

    [Fact]
    public void Si_solo_la_descripcion_dice_algo_se_propone_con_certeza_baja()
    {
        var p = Solo(Ficha(
            nombre:      "Solicitud de constancia",
            descripcion: "Constancia de cotizante para efectos de seguridad social."),
            CampoFicha.Categoria);

        p!.Valor.Should().Be("1");
        p.Certeza.Should().Be(CertezaLlenado.Baja);
        p.Justificacion.Should().Contain("confirmarlo");
    }

    /// <summary>
    /// Una categoría equivocada manda el trámite a la sección que no es y nadie lo encuentra;
    /// una vacía al menos se ve vacía. Ante el empate se calla.
    /// </summary>
    [Fact]
    public void Un_empate_entre_categorias_no_se_resuelve_inventando() =>
        Solo(Ficha("Licencia ambiental para transporte"), CampoFicha.Categoria)
            .Should().BeNull("«ambient» y «transporte» aciertan igual; elegir sería inventar");

    /// <summary>Los ids de categoría salen de datos sembrados. Si la base no tiene esa categoría,
    /// escribir el número igual metería un id que apunta a otra cosa.</summary>
    [Fact]
    public void Si_la_categoria_no_existe_en_esta_base_no_se_propone() =>
        ReglasLlenado.Proponer(Ficha("Emisión de licencia de conducir"),
                new HashSet<CampoFicha> { CampoFicha.Categoria }, new Dictionary<string, int>())
            .Should().BeEmpty();

    // ── Modalidad ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Hoy ninguna ficha del inventario marca EstaEnSol ni DisponibleEnLinea, y ningún paso
    /// declara modalidad: no hay señal dura. Todo lo que salga de acá es un supuesto y ninguna
    /// respuesta puede pasar de certeza Baja, porque una regla que contesta lo mismo para mil
    /// fichas no está clasificando.
    /// </summary>
    [Theory]
    [InlineData("Presentarse en la ventanilla con los documentos.", ModalidadPublica.Presencial)]
    [InlineData("Llenar el formulario en línea en el sitio web.",   ModalidadPublica.Virtual)]
    public void La_modalidad_sale_del_texto_y_nunca_pasa_de_certeza_baja(string paso, string esperada)
    {
        var p = Solo(Ficha(pasos: [paso]), CampoFicha.Modalidad);

        p!.Valor.Should().Be(esperada);
        p.Certeza.Should().Be(CertezaLlenado.Baja);
        p.Justificacion.Should().Contain("Supuesto", "la pantalla tiene que poder decir que esto no es un dato");
    }

    [Fact]
    public void Un_canal_en_linea_junto_a_oficinas_de_atencion_es_hibrido() =>
        Solo(Ficha(pasos: ["Solicitarlo en línea en el sitio web."], lugares: 3), CampoFicha.Modalidad)!
            .Valor.Should().Be(ModalidadPublica.Hibrido);

    [Fact]
    public void Con_lugares_de_atencion_y_ningun_indicio_mas_se_propone_presencial_por_descarte()
    {
        var p = Solo(Ficha(lugares: 2), CampoFicha.Modalidad);

        p!.Valor.Should().Be(ModalidadPublica.Presencial);
        p.Justificacion.Should().Contain("descarte");
    }

    [Fact]
    public void Sin_texto_ni_lugares_no_se_propone_modalidad() =>
        Solo(Ficha(), CampoFicha.Modalidad).Should().BeNull();

    // ── Alcance ───────────────────────────────────────────────────────────────

    /// <summary>El llenado solo toca huecos. Un campo que alguien ya llenó no se vuelve a
    /// proponer, aunque la regla tuviera una opinión distinta.</summary>
    [Fact]
    public void Solo_se_propone_sobre_los_campos_que_se_piden()
    {
        var datos = Ficha("Emisión de licencia de conducir", tiempos: ["2"], lugares: 1,
            requisitos: ["Recibo de pago"]);

        var propuestas = ReglasLlenado.Proponer(datos, new HashSet<CampoFicha> { CampoFicha.Tiempo }, Catalogo);

        propuestas.Should().ContainSingle().Which.Campo.Should().Be(CampoFicha.Tiempo);
    }

    [Fact]
    public void Una_ficha_completa_no_genera_nada() =>
        ReglasLlenado.Proponer(Ficha(), new HashSet<CampoFicha>(), Catalogo).Should().BeEmpty();

    /// <summary>La columna Justificacion tope en 400. Una ficha con treinta pasos no puede tumbar
    /// el guardado del lote entero.</summary>
    [Fact]
    public void La_justificacion_nunca_desborda_la_columna()
    {
        var muchos = Enumerable.Range(0, 40).Select(_ => (string?)"1.25").ToArray();

        Solo(Ficha(tiempos: muchos), CampoFicha.Tiempo)!.Justificacion.Length.Should().BeLessThanOrEqualTo(400);
    }

    /// <summary>En el inventario conviven «Migración» y «migracion». Una regla que busque la
    /// palabra tal cual acierta o falla según cómo alguien escribió el título, y ese fallo es
    /// invisible: simplemente no se propone nada.</summary>
    [Fact]
    public void Las_tildes_y_las_mayusculas_no_cambian_el_resultado() =>
        Solo(Ficha("SOLICITUD DE RESIDENCIA PARA EXTRANJEROS"), CampoFicha.Categoria)!
            .Valor.Should().Be("4");
}
