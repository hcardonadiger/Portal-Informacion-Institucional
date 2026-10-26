using System.Text.Json;
using Diger.TramitesEstado.Web.Pages.Expedientes;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El equipo institucional en el viaje entre el JSON del editor (expediente.js) y el DTO de
/// aplicación. El editor no manda la prioridad como número: la prioridad es el orden de las
/// filas, y es este mapeo el que la convierte.
/// </summary>
public class EquipoInstitucionalEditorTests
{
    [Fact]
    public void El_orden_de_las_filas_es_la_prioridad()
    {
        var o = Leer("""
            { "inst": "SALUD", "analista": "Analista",
              "equipo": [ { "nombre": "Luis Paz", "cargo": "Enlace" },
                          { "nombre": "Ana Rivera", "correo": "ana@inst.hn", "telefono": "2222-0000" } ] }
            """);

        var d = OriginalShapeMapper.ToInput(o, "SALUD");

        d.Equipo!.Select(m => (m.Prioridad, m.Nombre)).Should().Equal((1, "Luis Paz"), (2, "Ana Rivera"));
        d.Equipo![1].Correo.Should().Be("ana@inst.hn");
        d.Equipo![1].Telefono.Should().Be("2222-0000");
    }

    /// <summary>Un borrador guardado en el navegador antes del equipo no trae la clave
    /// <c>equipo</c>. Eso debe llegar como null —«no lo sé»—, no como lista vacía, que
    /// borraría el equipo guardado.</summary>
    [Fact]
    public void Un_JSON_sin_equipo_no_vacia_el_equipo()
    {
        var o = Leer("""{ "inst": "SALUD", "analista": "Analista", "contacto_nombre": "Ana Rivera" }""");

        OriginalShapeMapper.ToInput(o, "SALUD").Equipo.Should().BeNull();
    }

    /// <summary>Un expediente de antes del equipo solo tiene el contacto: el editor lo
    /// recibe como prioridad 1 para no mostrar el equipo vacío.</summary>
    [Fact]
    public void Un_expediente_con_solo_contacto_abre_el_equipo_con_el()
    {
        var d = OriginalShapeMapper.ToInput(
            Leer("""{ "inst": "SALUD", "analista": "Analista" }"""), "SALUD") with
        {
            Equipo = [], ContactoNombre = "Ana Rivera", ContactoCargo = "Jefa de TI"
        };

        var o = OriginalShapeMapper.FromInput(d);

        o.Equipo.Should().ContainSingle();
        o.Equipo![0].Nombre.Should().Be("Ana Rivera");
        o.Equipo![0].Cargo.Should().Be("Jefa de TI");
    }

    private static OriginalExpedienteDto Leer(string json) =>
        JsonSerializer.Deserialize<OriginalExpedienteDto>(json)!;
}
