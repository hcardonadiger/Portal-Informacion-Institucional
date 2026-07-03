using Diger.TramitesEstado.Application.Expedientes.Seguimiento;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Expedientes;

public class MetodologiaDigitalizacionTests
{
    [Fact]
    public void PesosDeSubs_SumanUnoPorEtapa()
    {
        foreach (var e in MetodologiaDigitalizacion.Etapas)
            e.Subs.Sum(s => s.Peso).Should().BeApproximately(1.0, 0.001, $"etapa {e.Num}");
    }

    [Fact]
    public void EtapaPct_PonderaCompletadoYEnProceso()
    {
        var e = MetodologiaDigitalizacion.Etapas.First(x => x.Num == "II"); // subs 2.1..2.6
        var estados = new Dictionary<string, int> { ["2.4"] = 2, ["2.2"] = 1 }; // 2.4 peso .25 (×1), 2.2 peso .20 (×.5)
        MetodologiaDigitalizacion.EtapaPct(e, estados)
            .Should().BeApproximately(0.25 * 1 + 0.20 * 0.5, 0.001);
    }

    [Fact]
    public void Global_NormalizaAUnoConTodoCompleto()
    {
        var estados = MetodologiaDigitalizacion.Etapas
            .SelectMany(e => e.Subs).ToDictionary(s => s.Id, _ => 2);
        MetodologiaDigitalizacion.Global(estados, new Dictionary<string, bool>())
            .Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void Global_ExcluyeEtapaToggleCuandoNoAplica()
    {
        // Solo la etapa IV (toggle) completa; sin aplicar, debe quedar en 0 (excluida del denominador).
        var estados = new Dictionary<string, int> { ["4.1"] = 2 };
        var aplica  = new Dictionary<string, bool> { ["IV"] = false };
        MetodologiaDigitalizacion.Global(estados, aplica).Should().Be(0);
    }

    [Theory]
    [InlineData(0.0, "No iniciado")]
    [InlineData(0.5, "En proceso")]
    [InlineData(1.0, "En operación")]
    public void EstadoGlobal_MapeaUmbrales(double pct, string esperado) =>
        MetodologiaDigitalizacion.EstadoGlobal(pct).Should().Be(esperado);
}
