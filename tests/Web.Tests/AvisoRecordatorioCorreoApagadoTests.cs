using Diger.TramitesEstado.Application.Notificaciones.Commands.EnviarRecordatorioManual;
using Diger.TramitesEstado.Web.Models;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

public sealed class AvisoRecordatorioCorreoApagadoTests
{
    [Fact]
    public void Con_campana_avisa_que_el_correo_esta_apagado_en_el_panel()
    {
        var (nivel, texto) = AvisoRecordatorio.Para(new ResultadoRecordatorio(1, 0, 0, false, CorreoApagado: true), "ok");

        nivel.Should().Be(AvisoRecordatorio.Nivel.Advertencia);
        texto.Should().Contain("Panel de control");
    }

    [Fact]
    public void Sin_campana_ni_correo_es_error()
    {
        var (nivel, _) = AvisoRecordatorio.Para(new ResultadoRecordatorio(0, 0, 0, false, CorreoApagado: true), "ok");
        nivel.Should().Be(AvisoRecordatorio.Nivel.Error);
    }
}
