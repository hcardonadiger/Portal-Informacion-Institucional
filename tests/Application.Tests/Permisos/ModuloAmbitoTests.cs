using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Permisos;

/// <summary>
/// Composición del ámbito por unidad/área con la matriz rol×permiso.
/// Lo que se prueba acá es <c>PermissionCache.AplicarAmbito</c>, el único punto donde se
/// resuelve: el navbar y el bloqueo real llaman los dos ahí, así que si esto está bien, no
/// pueden contradecirse.
/// </summary>
public class ModuloAmbitoTests
{
    private const string Inst = "DIGER";

    private static HashSet<string> Claves(params string[] c) =>
        c.ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static ModuloAmbito Ambito(string modulo, string? area = null, string? unidad = null) =>
        ModuloAmbito.Crear(modulo, Inst, area, unidad);

    [Fact]
    public void SinFilas_NoQuitaNada()
    {
        var claves = Claves("Expedientes.Ver", "Siger.Ver", "Proyectos.Ver");

        var r = PermissionCache.AplicarAmbito(claves, [], Inst, "GOBDIG", "DPD");

        r.Should().BeEquivalentTo(claves, "un módulo sin filas queda abierto para todos");
    }

    [Fact]
    public void ModuloLimitadoAUnaUnidad_SoloLoVeEsaUnidad()
    {
        var claves  = Claves("Expedientes.Ver", "Proyectos.Ver");
        var ambitos = new[] { Ambito("Expedientes", "GOBDIG", "DITRA") };

        var enDitra = PermissionCache.AplicarAmbito(claves, ambitos, Inst, "GOBDIG", "DITRA");
        var enDpd   = PermissionCache.AplicarAmbito(claves, ambitos, Inst, "GOBDIG", "DPD");

        enDitra.Should().Contain("Expedientes.Ver");
        enDpd.Should().NotContain("Expedientes.Ver", "DPD no está en la concesión");
        enDpd.Should().Contain("Proyectos.Ver", "los módulos sin filas no se tocan");
    }

    [Fact]
    public void ConcesionPorArea_AlcanzaATodasSusUnidades()
    {
        var claves  = Claves("Siger.Ver");
        var ambitos = new[] { Ambito("Siger", "SIGER") };   // sin unidad = toda el área

        PermissionCache.AplicarAmbito(claves, ambitos, Inst, "SIGER", "MSD")
                       .Should().Contain("Siger.Ver");
        PermissionCache.AplicarAmbito(claves, ambitos, Inst, "GOBDIG", "DITRA")
                       .Should().NotContain("Siger.Ver");
    }

    [Fact]
    public void VariasConcesiones_BastaConQueUnaAlcance()
    {
        // El caso real: SIGER para la unidad de Digitalización y para toda el área SIGER.
        var claves  = Claves("Siger.Ver");
        var ambitos = new[] { Ambito("Siger", "GOBDIG", "DITRA"), Ambito("Siger", "SIGER") };

        PermissionCache.AplicarAmbito(claves, ambitos, Inst, "GOBDIG", "DITRA")
                       .Should().Contain("Siger.Ver");
        PermissionCache.AplicarAmbito(claves, ambitos, Inst, "SIGER", "EID")
                       .Should().Contain("Siger.Ver");
        PermissionCache.AplicarAmbito(claves, ambitos, Inst, "GOBDIG", "DPD")
                       .Should().NotContain("Siger.Ver");
    }

    [Fact]
    public void LimitarElPadre_TambienLimitaAlSubmodulo()
    {
        var claves  = Claves("Siger.Ver", "Siger.Conciliacion.Editar");
        var ambitos = new[] { Ambito("Siger", "GOBDIG", "DITRA") };

        var enDpd = PermissionCache.AplicarAmbito(claves, ambitos, Inst, "GOBDIG", "DPD");

        enDpd.Should().BeEmpty("restringir Siger arrastra a Siger.Conciliacion");
    }

    [Fact]
    public void SubmoduloLimitado_NoAfectaAlPadre()
    {
        var claves  = Claves("Tableros.Ver", "Tableros.Digitalizacion.Ver");
        var ambitos = new[] { Ambito("Tableros.Digitalizacion", "GOBDIG", "DITRA") };

        var enDpd = PermissionCache.AplicarAmbito(claves, ambitos, Inst, "GOBDIG", "DPD");

        enDpd.Should().Contain("Tableros.Ver");
        enDpd.Should().NotContain("Tableros.Digitalizacion.Ver");
    }

    [Fact]
    public void OtraInstitucion_NoAlcanza()
    {
        var claves  = Claves("Expedientes.Ver");
        var ambitos = new[] { Ambito("Expedientes", "GOBDIG", "DITRA") };

        PermissionCache.AplicarAmbito(claves, ambitos, "CONSUCOOP", "GOBDIG", "DITRA")
                       .Should().BeEmpty("el ancla institucional se evalúa primero");
    }

    [Fact]
    public void UnidadSinArea_SeRechaza()
    {
        var crear = () => ModuloAmbito.Crear("Expedientes", Inst, null, "DITRA");

        crear.Should().Throw<DomainException>();
    }
}
