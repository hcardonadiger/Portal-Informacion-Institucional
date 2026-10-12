using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Accesos;

/// <summary>
/// El recorte de módulos por ámbito (<see cref="PermissionCache.AplicarAmbito"/>).
///
/// <para>Es la compuerta que decide si un módulo aparece en el navbar y si su página deja entrar.
/// Estaba <b>sin ninguna prueba</b>, y es justamente la clase de lógica que se rompe en silencio:
/// cuando falla, no explota —simplemente deja ver de más, y nadie se entera hasta que alguien
/// abre una pestaña que no le tocaba.</para>
///
/// <para>Las filas de prueba son las tres que hay hoy en la base: Expedientes y Siger acotados a
/// DIGER/GOBDIG/DITRA, más Siger abierto a toda el área DIGER/SIGER.</para>
/// </summary>
public class AmbitoDeModulosTests
{
    private static readonly List<ModuloAmbito> Reales =
    [
        ModuloAmbito.Crear("Expedientes", "DIGER", "GOBDIG", "DITRA"),
        ModuloAmbito.Crear("Siger",       "DIGER", "GOBDIG", "DITRA"),
        ModuloAmbito.Crear("Siger",       "DIGER", "SIGER",  null)
    ];

    private static readonly string[] Claves =
    [
        "Expedientes.Ver", "Expedientes.Editar",
        "Siger.Ver", "Siger.Conciliacion.Editar",
        "Reuniones.Ver", "Tickets.Ver"
    ];

    private static HashSet<string> Para(string? inst, string? area, string? unidad) =>
        PermissionCache.AplicarAmbito(
            Claves.ToHashSet(StringComparer.OrdinalIgnoreCase), Reales, inst, area, unidad);

    // ── Quien sí debe verlos ──────────────────────────────────────────────────

    [Fact]
    public void Digitalizacion_de_tramites_ve_expedientes_y_siger()
    {
        var claves = Para("DIGER", "GOBDIG", "DITRA");

        claves.Should().Contain("Expedientes.Ver");
        claves.Should().Contain("Siger.Ver");
    }

    // ── Quien no ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("DIGER", "GOBDIG", "OTRAUNIDAD")]   // misma área, otra unidad
    [InlineData("DIGER", "OTRAAREA", "OTRAUNIDAD")] // otra área
    [InlineData("DIGER", "OTRAAREA", null)]         // jefe de otra área
    [InlineData("DIGER", null, null)]               // jefe de institución, sin área
    [InlineData("SENPRENDE", "GOBDIG", "DITRA")]    // otra institución, mismos ids de área/unidad
    [InlineData(null, null, null)]                  // sin ámbito resuelto
    public void Fuera_de_digitalizacion_no_se_ve_expedientes(string? inst, string? area, string? unidad)
    {
        Para(inst, area, unidad).Should().NotContain("Expedientes.Ver");
    }

    [Theory]
    [InlineData("DIGER", "GOBDIG", "OTRAUNIDAD")]
    [InlineData("DIGER", "OTRAAREA", null)]
    [InlineData("DIGER", null, null)]
    [InlineData(null, null, null)]
    public void Fuera_de_digitalizacion_y_del_area_siger_no_se_ve_siger(string? inst, string? area, string? unidad)
    {
        Para(inst, area, unidad).Should().NotContain("Siger.Ver");
    }

    [Fact]
    public void El_area_siger_completa_ve_siger_pero_no_expedientes()
    {
        // La tercera fila concede Siger a toda el área SIGER, sin acotar unidad. Es más ancha que
        // las otras dos a propósito; si algún día deja de serlo, esta prueba lo dice.
        var claves = Para("DIGER", "SIGER", "CUALQUIERA");

        claves.Should().Contain("Siger.Ver");
        claves.Should().NotContain("Expedientes.Ver", "Expedientes sigue acotado a DITRA");
    }

    // ── Lo que el recorte no debe tocar ───────────────────────────────────────

    [Fact]
    public void Los_modulos_sin_fila_quedan_abiertos_para_todos()
    {
        var claves = Para("SENPRENDE", "OTRAAREA", "OTRAUNIDAD");

        claves.Should().Contain("Reuniones.Ver");
        claves.Should().Contain("Tickets.Ver");
    }

    [Fact]
    public void Restringir_el_padre_restringe_al_submodulo()
    {
        // Siger.Conciliacion no tiene fila propia: cae por ser hijo de Siger.
        Para("DIGER", "OTRAAREA", null).Should().NotContain("Siger.Conciliacion.Editar");
        Para("DIGER", "GOBDIG", "DITRA").Should().Contain("Siger.Conciliacion.Editar");
    }

    [Fact]
    public void Todas_las_acciones_del_modulo_caen_juntas()
    {
        var claves = Para("DIGER", "GOBDIG", "OTRAUNIDAD");

        claves.Should().NotContain("Expedientes.Ver");
        claves.Should().NotContain("Expedientes.Editar", "de nada sirve esconder el enlace si la acción queda");
    }

    [Fact]
    public void Sin_ninguna_fila_no_se_recorta_nada()
    {
        PermissionCache.AplicarAmbito(
            Claves.ToHashSet(StringComparer.OrdinalIgnoreCase), [], "DIGER", "OTRAAREA", null)
            .Should().HaveCount(Claves.Length);
    }

    // ── Sensibilidad a mayúsculas ─────────────────────────────────────────────

    [Fact]
    public void El_ambito_se_compara_sin_distinguir_mayusculas()
    {
        // Los ids vienen de la base y de los claims; una diferencia de capitalización no debería
        // convertirse en una restricción que no se quiso.
        Para("diger", "gobdig", "ditra").Should().Contain("Expedientes.Ver");
    }
}
