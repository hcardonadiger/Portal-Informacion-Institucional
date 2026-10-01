using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests;

public class TokenAccesoMcpTests
{
    private static TokenAccesoMcp Crear(DateTime? expira = null) =>
        TokenAccesoMcp.Crear(Guid.NewGuid(), Guid.NewGuid(), "Claude Code", "abc123", "hash", expira);

    [Fact]
    public void Crear_exige_usuario_y_asignacion()
    {
        var accionSinUsuario = () => TokenAccesoMcp.Crear(
            Guid.Empty, Guid.NewGuid(), "x", "p", "h", null);
        accionSinUsuario.Should().Throw<ArgumentException>();

        var accionSinAsignacion = () => TokenAccesoMcp.Crear(
            Guid.NewGuid(), Guid.Empty, "x", "p", "h", null);
        accionSinAsignacion.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_exige_nombre(string nombre)
    {
        var accion = () => TokenAccesoMcp.Crear(Guid.NewGuid(), Guid.NewGuid(), nombre, "p", "h", null);
        accion.Should().Throw<ArgumentException>();
    }

    /// <summary>Un token que nace vencido le daría al usuario una credencial que su primera
    /// petición rechaza; es mejor no entregarla.</summary>
    [Fact]
    public void Crear_rechaza_un_vencimiento_ya_pasado()
    {
        var accion = () => Crear(DateTime.UtcNow.AddMinutes(-1));
        accion.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Un_token_recien_creado_es_utilizable()
    {
        var token = Crear();

        token.EsUtilizable(DateTime.UtcNow).Should().BeTrue();
        token.EstaRevocado.Should().BeFalse();
        token.RevocadoUtc.Should().BeNull();
    }

    [Fact]
    public void Sin_vencimiento_sigue_siendo_utilizable_dentro_de_diez_anios()
    {
        var token = Crear(expira: null);

        token.EsUtilizable(DateTime.UtcNow.AddYears(10)).Should().BeTrue();
    }

    [Fact]
    public void Deja_de_ser_utilizable_al_pasar_su_vencimiento()
    {
        var expira = DateTime.UtcNow.AddDays(1);
        var token  = Crear(expira);

        token.EsUtilizable(expira.AddSeconds(-1)).Should().BeTrue();
        token.EsUtilizable(expira).Should().BeFalse("el vencimiento es el instante en que ya no sirve");
        token.EsUtilizable(expira.AddSeconds(1)).Should().BeFalse();
    }

    [Fact]
    public void Revocar_lo_inutiliza_aunque_no_haya_vencido()
    {
        var token = Crear(DateTime.UtcNow.AddYears(1));

        token.Revocar();

        token.EstaRevocado.Should().BeTrue();
        token.EsUtilizable(DateTime.UtcNow).Should().BeFalse();
    }

    /// <summary>La primera fecha es la que cuenta: volver a revocar no debe correr el sello
    /// hacia adelante y perder cuándo pasó de verdad.</summary>
    [Fact]
    public void Revocar_dos_veces_conserva_la_primera_fecha()
    {
        var token = Crear();
        token.Revocar();
        var primera = token.RevocadoUtc;

        token.Revocar();

        token.RevocadoUtc.Should().Be(primera);
    }

    [Fact]
    public void RegistrarUso_guarda_el_sello()
    {
        var token = Crear();
        var ahora = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

        token.UltimoUsoUtc.Should().BeNull();
        token.RegistrarUso(ahora);

        token.UltimoUsoUtc.Should().Be(ahora);
    }
}
