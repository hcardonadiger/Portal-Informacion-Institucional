using Diger.TramitesEstado.Application.Reuniones.Common;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

/// <summary>
/// El editor reemplaza los asistentes en bloque al guardar. Antes de este arreglo, el formulario no
/// llevaba el origen de cada uno y guardar convertía a todos en «manuales»: se perdía el
/// pre-registro, la asistencia marcada, el auto-registro por QR y la marca de invitado de Outlook.
/// La ida y vuelta ToForm → Aplicar tiene que dejarlos como estaban.
/// </summary>
public class EditorConservaAsistentesTests
{
    [Fact]
    public void Guardar_desde_el_editor_conserva_origen_y_asistencia()
    {
        var registrado = new DateTime(2026, 10, 20, 9, 15, 0);
        var r = Reunion.Crear("Mesa");
        r.Agregar(new Asistente { Nombre = "Pre", Correo = "pre@x.hn", EsPreregistro = true, Confirmado = true });
        r.Agregar(new Asistente { Nombre = "Ausente", Correo = "aus@x.hn", EsPreregistro = true, Confirmado = false });
        r.Agregar(new Asistente { Nombre = "QR", Correo = "qr@x.hn", AutoRegistro = true, RegistradoEl = registrado });
        r.Agregar(new Asistente { Nombre = "Outlook", Correo = "o@x.hn", EsPreregistro = true, DeOutlook = true });
        r.Agregar(new Asistente { Nombre = "Manual", Correo = "m@x.hn" });

        var (datos, asistentes, acuerdos) = ReunionMapper.ToForm(r);
        ReunionMapper.Aplicar(r, datos, asistentes, acuerdos);

        var porCorreo = r.Asistentes.ToDictionary(a => a.Correo!);
        porCorreo["pre@x.hn"].Should().Match<Asistente>(a => a.EsPreregistro && a.Confirmado == true);
        porCorreo["aus@x.hn"].Should().Match<Asistente>(a => a.EsPreregistro && a.Confirmado == false);
        porCorreo["qr@x.hn"].Should().Match<Asistente>(a => a.AutoRegistro && a.RegistradoEl == registrado);
        porCorreo["o@x.hn"].Should().Match<Asistente>(a => a.DeOutlook && a.EsPreregistro && a.Confirmado == null);
        porCorreo["m@x.hn"].Should().Match<Asistente>(a => !a.EsPreregistro && !a.AutoRegistro && !a.DeOutlook && a.Confirmado == null);
    }
}
