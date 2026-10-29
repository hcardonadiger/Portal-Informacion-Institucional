using Diger.TramitesEstado.Application.Reuniones.Common;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

/// <summary>Outlook manda sobre los Generales de una reunión importada: guardar desde el editor no
/// puede pisarlos, aunque el formulario los traiga (no basta con deshabilitar el input).</summary>
public class ReunionImportadaDeOutlookTests
{
    private static Reunion Importada() => Reunion.ImportarDeOutlook("AAMk-1",
        new GeneralesOutlook("Mesa", new DateOnly(2026, 11, 3), "09:00", "1:00", "Sala 2", null, false, "ical", null),
        Guid.NewGuid(), "DIGER", null, null);

    [Fact]
    public void Aplicar_no_pisa_los_generales_de_una_importada_pero_si_lo_demas()
    {
        var r = Importada();
        var (datos, asistentes, acuerdos) = ReunionMapper.ToForm(r);
        datos.Titulo = "Cambiado en el portal";
        datos.Fecha = new DateOnly(2027, 1, 1);
        datos.Hora = "18:00";
        datos.Duracion = "3 horas";
        datos.Lugar = "Otra sala";
        datos.ObjetivoAgenda = "Objetivo nuevo";

        ReunionMapper.Aplicar(r, datos, asistentes, acuerdos);

        r.Titulo.Should().Be("Mesa");
        r.Fecha.Should().Be(new DateOnly(2026, 11, 3));
        r.Hora.Should().Be("09:00");
        r.Duracion.Should().Be("1:00");
        r.Lugar.Should().Be("Sala 2");
        r.ObjetivoAgenda.Should().Be("Objetivo nuevo");
    }

    [Fact]
    public void ToForm_dice_que_es_importada_y_si_esta_cancelada()
    {
        var r = Importada();
        r.MarcarCanceladaEnOutlook(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));

        var (datos, _, _) = ReunionMapper.ToForm(r);

        datos.EsImportadaDeOutlook.Should().BeTrue();
        datos.CanceladaEnOutlookEl.Should().Be(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
    }
}
