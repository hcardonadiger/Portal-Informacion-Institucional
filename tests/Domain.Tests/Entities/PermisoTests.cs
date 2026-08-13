using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests.Entities;

public class PermisoTests
{
    [Fact]
    public void Crear_DatosValidos_CreaPermisoActivo()
    {
        var p = Permiso.Crear("Expedientes.Crear", "Crear expedientes", "Expedientes", AccionModulo.Crear);

        p.Id.Should().Be("Expedientes.Crear");
        p.Nombre.Should().Be("Crear expedientes");
        p.Modulo.Should().Be("Expedientes");
        p.Accion.Should().Be(AccionModulo.Crear);
        p.Activo.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_ClaveVacia_LanzaArgumentException(string clave)
    {
        var act = () => Permiso.Crear(clave, "Nombre", "Modulo", AccionModulo.Ver);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Desactivar_MarcaComoInactivo()
    {
        var p = Permiso.Crear("Tickets.Editar", "Editar tickets", "Tickets", AccionModulo.Editar);

        p.Desactivar();

        p.Activo.Should().BeFalse();
    }

    [Fact]
    public void Sincronizar_ActualizaDatosYReactiva()
    {
        var p = Permiso.Crear("Tickets.Editar", "Nombre viejo", "Modulo viejo", AccionModulo.Ver);
        p.Desactivar();

        p.Sincronizar("Nombre nuevo", "Modulo nuevo", AccionModulo.Editar);

        p.Nombre.Should().Be("Nombre nuevo");
        p.Modulo.Should().Be("Modulo nuevo");
        p.Accion.Should().Be(AccionModulo.Editar);
        p.Activo.Should().BeTrue();
    }
}
