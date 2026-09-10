using System.Text;
using Diger.TramitesEstado.Application.Calendario.Ics;
using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Common.Tiempo;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Calendario;

/// <summary>
/// El formato iCalendar tiene cuatro reglas que se rompen fácil al escribirlo a mano —CRLF, plegado
/// a 75 octetos, escapes y fechas en UTC— y ninguna avisa: el archivo simplemente no se abre, o se
/// abre mal. Estas pruebas son las que hacen razonable no depender de una biblioteca.
/// </summary>
public class IcsWriterTests
{
    private static EventoIcs Evento(string titulo = "Mesa técnica") => new()
    {
        Uid    = "reunion-1@diger.gob.hn",
        Titulo = titulo,
        Inicio = new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.FromHours(-6)),
        Fin    = new DateTimeOffset(2026, 9, 15, 10, 30, 0, TimeSpan.FromHours(-6))
    };

    private static string Escribir(params EventoIcs[] eventos) =>
        IcsWriter.Escribir(eventos, "DIGER//Portal", ahoraUtc: new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Envuelve_los_eventos_en_un_VCALENDAR_valido()
    {
        var ics = Escribir(Evento());

        ics.Should().StartWith("BEGIN:VCALENDAR\r\n");
        ics.Should().EndWith("END:VCALENDAR\r\n");
        ics.Should().Contain("VERSION:2.0\r\n");
        ics.Should().Contain("METHOD:PUBLISH\r\n");
        ics.Should().Contain("BEGIN:VEVENT\r\n").And.Contain("END:VEVENT\r\n");
    }

    /// <summary>RFC 5545 exige CRLF. Un archivo con solo LF lo rechazan varios clientes.</summary>
    [Fact]
    public void Todas_las_lineas_terminan_en_CRLF()
    {
        var ics = Escribir(Evento());

        ics.Replace("\r\n", "").Should().NotContain("\n");
        ics.Replace("\r\n", "").Should().NotContain("\r");
    }

    /// <summary>La hora se emite en UTC con sufijo Z: las 9:00 de Honduras son las 15:00 UTC. Así
    /// el evento no depende de que el cliente sepa interpretar una zona.</summary>
    [Fact]
    public void Las_fechas_se_emiten_en_UTC()
    {
        var ics = Escribir(Evento());

        ics.Should().Contain("DTSTART:20260915T150000Z\r\n");
        ics.Should().Contain("DTEND:20260915T163000Z\r\n");
    }

    [Fact]
    public void Un_evento_de_dia_completo_usa_VALUE_DATE_y_termina_al_dia_siguiente()
    {
        var ics = Escribir(new EventoIcs
        {
            Uid = "x@y", Titulo = "Jornada",
            Dia = new DateOnly(2026, 9, 15), DiaFin = new DateOnly(2026, 9, 16)
        });

        ics.Should().Contain("DTSTART;VALUE=DATE:20260915\r\n");
        ics.Should().Contain("DTEND;VALUE=DATE:20260916\r\n");
        ics.Should().NotContain("DTSTART:2026");
    }

    /// <summary>Los caracteres reservados se escapan; si no, una coma en el título parte el valor
    /// en dos y el cliente lee basura.</summary>
    [Fact]
    public void Escapa_los_caracteres_reservados()
    {
        var ics = Escribir(Evento("Reunión: SEFIN, SEDUC; revisión") with { Descripcion = "Línea 1\nLínea 2" });

        ics.Should().Contain(@"SEFIN\, SEDUC\; revisión");
        ics.Should().Contain(@"Línea 1\nLínea 2");
    }

    /// <summary>La barra invertida se escapa antes que lo demás. Al revés, escaparía las barras que
    /// los otros reemplazos acaban de introducir y el valor llegaría con barras de más.</summary>
    [Fact]
    public void La_barra_invertida_se_escapa_primero_para_no_duplicar_los_demas_escapes()
    {
        var ics = Escribir(Evento(@"ruta\carpeta, final"));

        ics.Should().Contain(@"SUMMARY:ruta\\carpeta\, final");
    }

    /// <summary>
    /// El plegado se cuenta en octetos UTF-8, no en caracteres. Con acentos —que hay en casi todos
    /// los títulos en español— contar caracteres produce líneas que superan el límite.
    /// </summary>
    [Fact]
    public void Pliega_las_lineas_largas_a_75_octetos()
    {
        var titulo = string.Concat(Enumerable.Repeat("áéíóú ", 40));
        var ics = Escribir(Evento(titulo));

        foreach (var linea in ics.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
            Encoding.UTF8.GetByteCount(linea).Should().BeLessThanOrEqualTo(75);
    }

    [Fact]
    public void Las_lineas_plegadas_continuan_con_un_espacio_y_reconstruyen_el_valor_original()
    {
        var titulo = string.Concat(Enumerable.Repeat("áéíóú ", 40)).Trim();
        var ics = Escribir(Evento(titulo));

        // Des-plegar es exactamente lo que hace un cliente: quitar CRLF seguido de un espacio.
        var desplegado = ics.Replace("\r\n ", "");
        desplegado.Should().Contain($"SUMMARY:{titulo}");
    }

    /// <summary>Un evento sin ventana no es representable. Se descarta solo él: emitir un VEVENT
    /// inválido puede hacer que el cliente rechace el archivo entero.</summary>
    [Fact]
    public void Un_evento_sin_fecha_se_descarta_sin_arrastrar_a_los_demas()
    {
        var ics = Escribir(
            new EventoIcs { Uid = "sin-fecha@y", Titulo = "Sin fecha" },
            Evento("Con fecha"));

        ics.Should().NotContain("Sin fecha");
        ics.Should().Contain("SUMMARY:Con fecha");
        System.Text.RegularExpressions.Regex.Matches(ics, "BEGIN:VEVENT").Should().HaveCount(1);
    }

    [Fact]
    public void El_organizador_y_los_asistentes_salen_como_mailto()
    {
        var ics = Escribir(Evento() with
        {
            OrganizadorNombre = "Ana Ortez",
            OrganizadorCorreo = "ana@diger.gob.hn",
            Asistentes = [new AsistenteIcs("Carlos Fuentes", "carlos@sefin.gob.hn")]
        });

        // La línea ATTENDEE pasa de 75 octetos y se pliega, así que se compara des-plegado, que es
        // lo que hace el cliente antes de leerla.
        var desplegado = ics.Replace("\r\n ", "");

        desplegado.Should().Contain("ORGANIZER;CN=\"Ana Ortez\":mailto:ana@diger.gob.hn");
        desplegado.Should().Contain("ATTENDEE;CN=\"Carlos Fuentes\"");
        desplegado.Should().Contain("mailto:carlos@sefin.gob.hn");
    }

    /// <summary>
    /// Un nombre con coma —«Ortez, Ana», como lo escribe medio directorio— no se puede escapar con
    /// barra invertida dentro de un parámetro: hay que entrecomillarlo o el archivo queda inválido.
    /// </summary>
    [Fact]
    public void Un_nombre_con_coma_no_rompe_el_parametro_CN()
    {
        var ics = Escribir(Evento() with
        {
            OrganizadorNombre = "Ortez, Ana",
            OrganizadorCorreo = "ana@diger.gob.hn"
        });

        ics.Should().Contain("ORGANIZER;CN=\"Ortez, Ana\":mailto:");
        ics.Should().NotContain(@"CN=Ortez\,");
    }
}

/// <summary>El paso de reunión a evento: es donde se consume la ventana que dejó la Fase 0.</summary>
public class ReunionIcsTests
{
    private static readonly RelojInstitucional Reloj =
        new(Options.Create(new InstitucionOptions { ZonaHoraria = "America/Tegucigalpa" }));

    private static Reunion Reunion(DateOnly? fecha, string? hora, int? duracion)
    {
        var r = Domain.Entities.Reunion.Crear("Mesa técnica con SEFIN");
        r.Fecha = fecha; r.Hora = hora; r.DuracionMinutos = duracion;
        return r;
    }

    [Fact]
    public void Una_reunion_con_hora_se_publica_con_su_ventana_en_instantes()
    {
        var e = ReunionIcs.Mapear(Reunion(new DateOnly(2026, 9, 15), "09:00", 90), Reloj, "diger.gob.hn");

        e.EsDiaCompleto.Should().BeFalse();
        e.Inicio!.Value.UtcDateTime.Should().Be(new DateTime(2026, 9, 15, 15, 0, 0, DateTimeKind.Utc));
        (e.Fin!.Value - e.Inicio!.Value).Should().Be(TimeSpan.FromMinutes(90));
    }

    /// <summary>Las 44 reuniones importadas no traen duración: se publican con la hora supuesta y
    /// no se caen del calendario.</summary>
    [Fact]
    public void Sin_duracion_declarada_se_publica_igual_con_una_hora()
    {
        var e = ReunionIcs.Mapear(Reunion(new DateOnly(2026, 9, 15), "14:00", null), Reloj, "diger.gob.hn");

        (e.Fin!.Value - e.Inicio!.Value).Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void Sin_hora_se_publica_como_dia_completo()
    {
        var e = ReunionIcs.Mapear(Reunion(new DateOnly(2026, 9, 15), null, null), Reloj, "diger.gob.hn");

        e.EsDiaCompleto.Should().BeTrue();
        e.DiaFin.Should().Be(new DateOnly(2026, 9, 16));
    }

    /// <summary>El UID se arma con el Id y no con el token de registro, que se puede regenerar
    /// desde la pantalla de asistencia: si cambiara, la cita se duplicaría en vez de actualizarse.</summary>
    [Fact]
    public void El_UID_es_estable_entre_descargas()
    {
        var r = Reunion(new DateOnly(2026, 9, 15), "09:00", 60);

        var primera = ReunionIcs.Mapear(r, Reloj, "diger.gob.hn").Uid;
        r.RegenerarToken();
        var segunda = ReunionIcs.Mapear(r, Reloj, "diger.gob.hn").Uid;

        segunda.Should().Be(primera);
    }

    [Fact]
    public void Los_asistentes_se_pueden_omitir_para_no_repartir_correos()
    {
        var r = Reunion(new DateOnly(2026, 9, 15), "09:00", 60);
        r.RegistrarAsistente("Carlos", null, "SEFIN", null, "carlos@sefin.gob.hn", null);

        ReunionIcs.Mapear(r, Reloj, "d", incluirAsistentes: true).Asistentes.Should().HaveCount(1);
        ReunionIcs.Mapear(r, Reloj, "d", incluirAsistentes: false).Asistentes.Should().BeEmpty();
    }
}
