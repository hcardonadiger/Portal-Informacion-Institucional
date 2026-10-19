using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests.Entities;

/// <summary>
/// El cambio obligatorio de contraseña se decide con una sola columna, <c>PasswordCambiadoEl</c>,
/// y lo único que la hace confiable es quién la estampa: si un restablecimiento hecho por un
/// administrador contara como «ya la cambió», la cuenta se saltaría el portón con una contraseña
/// que su dueño nunca eligió.
/// </summary>
public class UsuarioPasswordTests
{
    /// <summary>Un corte ya cumplido: el portón está en vigor. No se usa el 01/11/2026 literal
    /// porque la prueba tiene que valer igual antes y después de esa fecha.</summary>
    private static readonly DateTime CorteEnVigor = DateTime.UtcNow.AddDays(-1);

    private static Usuario NuevoUsuario() =>
        Usuario.Crear("Ana Prueba", "ana@diger.gob.hn", "hash-temporal");

    [Fact]
    public void UsuarioRecienCreadoNaceSinFechaDeCambio()
    {
        var u = NuevoUsuario();

        u.PasswordCambiadoEl.Should().BeNull();
        u.RequiereCambioPassword(CorteEnVigor).Should().BeTrue(
            "la cuenta nueva arranca con una contraseña temporal que su dueño todavía no eligió");
    }

    [Fact]
    public void CambiarPasswordEstampaLaFechaYSueltaElPorton()
    {
        var u = NuevoUsuario();

        u.CambiarPassword("hash-elegido-por-ella");

        u.PasswordCambiadoEl.Should().NotBeNull();
        u.PasswordCambiadoEl!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        u.RequiereCambioPassword(CorteEnVigor).Should().BeFalse("la fijó ella misma, después del corte");
    }

    [Fact]
    public void AsignarPasswordTemporalNoEstampaYDejaElCambioPendiente()
    {
        var u = NuevoUsuario();
        u.CambiarPassword("hash-elegido-por-ella");

        u.AsignarPasswordTemporal("hash-que-puso-el-admin");

        u.PasswordCambiadoEl.Should().BeNull("la puso un tercero, así que vuelve a contar como temporal");
        u.RequiereCambioPassword(CorteEnVigor).Should().BeTrue();
    }

    [Fact]
    public void UnCambioAnteriorAlCorteNoCuentaParaLaTandaUnica()
    {
        var u = NuevoUsuario();
        u.CambiarPassword("hash-viejo");

        // El corte todavía no llega: la contraseña se cambió antes que él.
        u.RequiereCambioPassword(DateTime.UtcNow.AddYears(1)).Should().BeTrue(
            "la tanda única exige que el cambio ocurra a partir del corte, no antes");
    }

    [Fact]
    public void CambiarlaElMismoInstanteDelCorteYaCumple()
    {
        var u = NuevoUsuario();
        u.CambiarPassword("hash");

        u.RequiereCambioPassword(u.PasswordCambiadoEl!.Value).Should().BeFalse(
            "el corte es el límite inclusivo: no se le puede exigir dos veces");
    }

    [Fact]
    public void AsignarPasswordTemporalRechazaUnHashVacio()
    {
        var u = NuevoUsuario();

        var accion = () => u.AsignarPasswordTemporal("  ");

        accion.Should().Throw<ArgumentException>();
    }
}
