using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests;

/// <summary>
/// Reemplazar solo los acuerdos. Lo que se protege: que llenar la memoria desde una transcripción
/// no se lleve de paso a los asistentes ni a los adjuntos, como haría <c>LimpiarHijos</c>.
/// </summary>
public class ReunionAcuerdosTests
{
    [Fact]
    public void Reemplazar_acuerdos_no_toca_asistentes_ni_adjuntos_y_numera_desde_cero()
    {
        var r = Reunion.Crear("Mesa técnica");
        r.PreRegistrar("Invitada", null, "IHTT", null, null, null);
        r.Agregar(ReunionAdjunto.ParaEnlace(SeccionAdjuntoReunion.Documento, 0, "Presentación", null, "https://diger.gob.hn/p"));
        r.Agregar(new AcuerdoReunion { Compromiso = "Viejo", Orden = 7 });

        r.ReemplazarAcuerdos([new AcuerdoReunion { Compromiso = "Uno", Orden = 5 }, new AcuerdoReunion { Compromiso = "Dos" }]);

        r.Acuerdos.Select(a => (a.Compromiso, a.Orden)).Should().Equal(("Uno", 0), ("Dos", 1));
        r.Asistentes.Should().ContainSingle();
        r.Adjuntos.Should().ContainSingle();
    }
}
