using Diger.TramitesEstado.Application.Reuniones.Memoria;
using Diger.TramitesEstado.Domain.Common;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones.Memoria;

/// <summary>
/// Las reglas que hasta el 2026-10-23 vivían en el JavaScript del editor. Lo que se protege: que
/// «Generales» nunca se escriba desde un resumen de IA, que el bloque de capacitación y la segunda
/// firma solo caigan donde aplican, y que cada descarte se diga en vez de pasar en silencio.
/// </summary>
public class MemoriaDesdeTranscripcionTests
{
    [Fact]
    public void Las_instrucciones_de_reunion_no_piden_el_bloque_de_capacitacion_ni_la_segunda_firma()
    {
        var i = MemoriaDesdeTranscripcion.Instrucciones(esCapacitacion: false, conEncuesta: false);

        i.Variante.Should().Be("Reunión");
        i.Prompt.Should().StartWith("Eres un asistente que redacta la memoria de una reunión institucional de la DIGER.");
        i.Prompt.Should().Contain(i.Formato);
        i.Formato.Should().NotContain("convocados").And.NotContain("validador_institucion");
        i.Campos.Select(c => c.Clave).Should().Equal("objetivo", "desarrollo", "acuerdos[]", "documentos", "validador_diger");
    }

    [Fact]
    public void Las_instrucciones_de_capacitacion_con_encuesta_piden_todo()
    {
        var i = MemoriaDesdeTranscripcion.Instrucciones(esCapacitacion: true, conEncuesta: true);

        i.Variante.Should().Be("Capacitación");
        i.Prompt.Should().StartWith("Eres un asistente que redacta la memoria de una CAPACITACIÓN impartida por la DIGER.");
        i.Prompt.Should().Contain("7. Esta actividad es una CAPACITACIÓN");
        i.Formato.Should().Contain("\"convocados\": 30,").And.Contain("\"validador_institucion\"");
        i.Campos.Select(c => c.Clave).Should().Equal(
            "objetivo", "desarrollo", "acuerdos[]",
            "convocados", "num_asistentes", "pct_asistencia", "compromisos",
            "documentos", "validador_diger", "validador_institucion");
    }

    /// <summary>
    /// La guardia del traslado: el prompt armado a mano con las mismas líneas que
    /// CW_BLOQUE_*, CW_REGLAS_* y cwPrompt de Editor.cshtml (al 2026-10-23). Si alguien reescribe
    /// un texto del servidor, este test lo dice — el prompt ya está probado con la IA.
    /// </summary>
    [Fact]
    public void El_prompt_es_el_mismo_que_el_del_editor()
    {
        var esperado = string.Join("\n",
            "Eres un asistente que redacta la memoria de una CAPACITACIÓN impartida por la DIGER.",
            "Te comparto la transcripción de la sesión. Devuelve ÚNICAMENTE un objeto JSON válido:",
            "sin texto antes ni después, sin bloques de código y sin comentarios, con esta forma:",
            "",
            "{",
            "  \"objetivo\": \"Objetivo principal de la sesión\",",
            "  \"desarrollo\": \"<p>Resumen del desarrollo…</p><ul><li>Punto tratado</li></ul>\",",
            "  \"acuerdos\": [",
            "    { \"compromiso\": \"Qué se hará\", \"responsable\": \"Nombre o institución\", \"plazo\": \"YYYY-MM-DD\" }",
            "  ],",
            "  \"convocados\": 30,",
            "  \"num_asistentes\": 24,",
            "  \"pct_asistencia\": 80,",
            "  \"compromisos\": [\"Compromiso de seguimiento posterior a la capacitación\"],",
            "  \"documentos\": [\"Presentación institucional.pdf\", \"Lista de asistencia firmada\"],",
            "  \"validador_diger\": \"Nombre de quien valida por la DIGER\",",
            "  \"validador_institucion\": \"Nombre de quien valida por la institución\"",
            "}",
            "",
            "Reglas:",
            "1. No inventes nada. Si un dato no aparece en la transcripción, omite esa clave.",
            "2. NO incluyas titulo, fecha, hora, duracion, modalidad, tipo, institucion, lugar ni",
            "   visibilidad. Esos datos ya están capturados en el sistema y no deben modificarse;",
            "   si los devuelves, se descartan.",
            "3. \"desarrollo\" admite HTML simple (<p>, <ul>, <li>, <strong>, <em>). Redáctalo en",
            "   español, en tercera persona y en pasado: discusión, decisiones y conclusiones.",
            "4. \"acuerdos\" son compromisos concretos y accionables. \"responsable\" es el nombre de la",
            "   persona o institución tal como se menciona; \"plazo\" va en formato YYYY-MM-DD y se",
            "   omite si no se acordó una fecha.",
            "5. \"documentos\" lista los archivos, presentaciones o enlaces mencionados.",
            "6. Los \"validador_*\" son nombres de personas, no cargos ni instituciones. Omítelos si la",
            "   transcripción no dice quién valida.",
            "7. Esta actividad es una CAPACITACIÓN: llena también su bloque propio. NO devuelvas un",
            "   objetivo ni un contenido aparte para la capacitación: lo formativo va en \"objetivo\" y",
            "   los temas impartidos en \"desarrollo\", que es donde los lee el acta.",
            "8. \"convocados\" y \"num_asistentes\" son enteros; \"pct_asistencia\" es un entero de 0 a 100",
            "   y solo se calcula si tienes ambas cifras. \"compromisos\" son los acuerdos de",
            "   seguimiento posteriores a la capacitación (replicar, entregar material, evaluar).",
            "",
            "Transcripción:",
            "\"\"\"",
            "(pegue aquí la transcripción)",
            "\"\"\"");

        MemoriaDesdeTranscripcion.Instrucciones(true, true).Prompt.Should().Be(esperado);
    }

    [Fact]
    public void Interpreta_memoria_cierre_y_acuerdos()
    {
        var p = MemoriaDesdeTranscripcion.Interpretar("""
            { "objetivo": "Revisar avances", "desarrollo": "<p>Se revisó.</p>",
              "acuerdos": [ { "compromiso": "Enviar informe", "responsable": "IHTT", "plazo": "2026-11-05" },
                            { "compromiso": "Sin plazo" } ],
              "documentos": ["Presentación.pdf", "", "Lista firmada"], "validador_diger": "Ana Paz" }
            """, esCapacitacion: false, conEncuesta: false);

        p.Objetivo.Should().Be("Revisar avances");
        p.Desarrollo.Should().Be("<p>Se revisó.</p>");
        p.Documentos.Should().Be("Presentación.pdf\nLista firmada", "uno por línea, sin vacíos, como cwLineas");
        p.ValidadorDiger.Should().Be("Ana Paz");
        p.Acuerdos.Should().Equal(
            new AcuerdoPropuesto("Enviar informe", "IHTT", new DateOnly(2026, 11, 5)),
            new AcuerdoPropuesto("Sin plazo", null, null));
        p.CamposAplicables.Should().Be(6, "objetivo, desarrollo, documentos, validador y dos acuerdos");
        p.Mensaje.Should().Be("Se llenaron 6 campo(s) de Memoria y Cierre.");
    }

    [Fact]
    public void Generales_se_descarta_y_se_dice()
    {
        var p = MemoriaDesdeTranscripcion.Interpretar(
            """{ "titulo": "Otro", "fecha": "2026-01-01", "lugar": "X", "objetivo": "O" }""", false, false);

        p.Objetivo.Should().Be("O");
        p.Mensaje.Should().Be("Se llenaron 1 campo(s) de Memoria y Cierre. Se ignoró titulo, fecha, lugar: «Generales» no se modifica desde el JSON.");
    }

    [Fact]
    public void El_bloque_de_capacitacion_solo_cae_en_una_capacitacion()
    {
        const string json = """{ "convocados": 30, "num_asistentes": "24", "pct_asistencia": 80, "compromisos": ["Replicar", "Evaluar"] }""";

        var reunion = MemoriaDesdeTranscripcion.Interpretar(json, esCapacitacion: false, conEncuesta: false);
        reunion.Convocados.Should().BeNull();
        reunion.Compromisos.Should().BeNull();
        reunion.Mensaje.Should().Be("El JSON no traía ningún campo de Memoria, Capacitación ni Cierre. Se ignoró convocados, num_asistentes, pct_asistencia, compromisos: el tipo de esta reunión no es Capacitación.");

        var cap = MemoriaDesdeTranscripcion.Interpretar(json, esCapacitacion: true, conEncuesta: false);
        cap.Convocados.Should().Be(30);
        cap.NumAsistentes.Should().Be(24, "un número como texto también vale");
        cap.PctAsistencia.Should().Be(80);
        cap.Compromisos.Should().Be("Replicar\nEvaluar");
        cap.Mensaje.Should().Be("Se llenaron 4 campo(s) de Memoria, Capacitación y Cierre.");
    }

    [Fact]
    public void La_segunda_firma_solo_cae_con_la_encuesta_encendida()
    {
        const string json = """{ "validador_institucion": "Luis" }""";

        MemoriaDesdeTranscripcion.Interpretar(json, false, conEncuesta: true).ValidadorInstitucion.Should().Be("Luis");

        var sin = MemoriaDesdeTranscripcion.Interpretar(json, false, conEncuesta: false);
        sin.ValidadorInstitucion.Should().BeNull();
        sin.Mensaje.Should().EndWith("Se ignoró validador_institucion: la encuesta de esta reunión está apagada.");
    }

    [Fact]
    public void Satisfaccion_y_detalle_viejo_de_capacitacion_se_descartan_con_su_aviso()
    {
        var p = MemoriaDesdeTranscripcion.Interpretar(
            """{ "satisfaccion": "buena", "contenido": "x", "objetivo": "O" }""", true, false);

        p.Mensaje.Should().Contain("Se ignoró satisfaccion: la satisfacción ya no se captura en el cierre.");
        p.Mensaje.Should().Contain("Se ignoró contenido: el detalle de capacitación se quitó; use «objetivo» y «desarrollo».");
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("[1,2]")]
    [InlineData("")]
    [InlineData("   ")]
    public void Lo_que_no_es_un_objeto_JSON_se_rechaza(string json)
    {
        var acto = () => MemoriaDesdeTranscripcion.Interpretar(json, false, false);

        acto.Should().Throw<DomainException>().WithMessage("El texto pegado no es un JSON válido.");
    }

    /// <summary>
    /// La IA no siempre escribe «80»: a veces «80.0», «3e1», «"24.0"» o «"80%"». Son el mismo
    /// entero y se aplican; una fracción o un negativo no son una cifra de asistencia y no.
    /// </summary>
    [Theory]
    [InlineData("80.0", 80)]
    [InlineData("\"24.0\"", 24)]
    [InlineData("3e1", 30)]
    [InlineData("\"80%\"", 80)]
    [InlineData("\" 24 \"", 24)]
    [InlineData("24.5", null)]
    [InlineData("\"24.5\"", null)]
    [InlineData("\"-3\"", null)]
    [InlineData("-3", null)]
    [InlineData("3000000000", null)]
    public void Las_cifras_de_capacitacion_aceptan_decimales_enteros_y_el_signo_de_porcentaje(string valor, int? esperado)
    {
        var p = MemoriaDesdeTranscripcion.Interpretar(
            "{ \"convocados\": " + valor + ", \"pct_asistencia\": " + valor + " }", esCapacitacion: true, conEncuesta: false);

        p.Convocados.Should().Be(esperado);
        p.PctAsistencia.Should().Be(esperado is <= 100 ? esperado : null, "el rango 0..100 del porcentaje sigue igual");
    }

    [Theory]
    [InlineData("""{ "pct_asistencia": 140 }""")]
    [InlineData("""{ "pct_asistencia": "mucho" }""")]
    public void Un_porcentaje_imposible_no_se_aplica(string json)
    {
        MemoriaDesdeTranscripcion.Interpretar(json, esCapacitacion: true, conEncuesta: false)
            .PctAsistencia.Should().BeNull();
    }

    [Fact]
    public void Un_plazo_ilegible_deja_el_acuerdo_sin_plazo()
    {
        var p = MemoriaDesdeTranscripcion.Interpretar(
            """{ "acuerdos": [ { "compromiso": "X", "plazo": "el viernes" }, { "responsable": "sin compromiso" } ] }""", false, false);

        p.Acuerdos.Should().Equal(new AcuerdoPropuesto("X", null, null));
    }

    [Theory]
    [InlineData("Capacitación", true)]
    [InlineData("Taller", false)]
    [InlineData(null, false)]
    public void Es_capacitacion_solo_con_el_tipo_exacto(string? tipo, bool esperado) =>
        MemoriaDesdeTranscripcion.EsCapacitacion(tipo).Should().Be(esperado);
}
