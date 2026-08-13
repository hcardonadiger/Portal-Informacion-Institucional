using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests.Entities;

public class RolTests
{
    [Fact]
    public void Crear_DatosValidos_NormalizaYQuedaActivo()
    {
        var rol = Rol.Crear("  JefeArea  ", "  Jefe de Área  ", NivelAlcance.Area, "  Manda en su área  ", "  #1a5fa0  ");

        rol.Id.Should().Be("JefeArea");
        rol.Nombre.Should().Be("Jefe de Área");
        rol.Descripcion.Should().Be("Manda en su área");
        rol.Color.Should().Be("#1a5fa0");
        rol.NivelAlcance.Should().Be(NivelAlcance.Area);
        rol.Activo.Should().BeTrue();
        rol.EsSistema.Should().BeFalse();
    }

    [Fact]
    public void Crear_ColorEnBlanco_QuedaNulo()
    {
        var rol = Rol.Crear("Auditor", "Auditor", NivelAlcance.Institucion, color: "   ");

        rol.Color.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_CodigoVacio_LanzaArgumentException(string codigo)
    {
        var act = () => Rol.Crear(codigo, "Nombre", NivelAlcance.Unidad);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("Jefe Area")]   // el código viaja como claim: sin espacios
    [InlineData("Jefe.Area")]
    [InlineData("Jefe/Area")]
    public void Crear_CodigoConCaracteresInvalidos_LanzaDomainException(string codigo)
    {
        var act = () => Rol.Crear(codigo, "Nombre", NivelAlcance.Unidad);
        act.Should().Throw<DomainException>().WithMessage("*letras, números*");
    }

    [Fact]
    public void Crear_CodigoDemasiadoLargo_LanzaDomainException()
    {
        var act = () => Rol.Crear(new string('a', 61), "Nombre", NivelAlcance.Unidad);
        act.Should().Throw<DomainException>().WithMessage("*60 caracteres*");
    }

    [Fact]
    public void Crear_AdministradorYSoloLectura_LanzaDomainException()
    {
        var act = () => Rol.Crear("Raro", "Raro", NivelAlcance.Global, esAdministrador: true, esSoloLectura: true);
        act.Should().Throw<DomainException>().WithMessage("*solo lectura*");
    }

    [Fact]
    public void Actualizar_CambiaCamposYCapacidades()
    {
        var rol = Rol.Crear("Auditor", "Auditor", NivelAlcance.Unidad);

        rol.Actualizar("Auditor interno", NivelAlcance.Institucion, "Revisa expedientes", "#6d4c00",
            esAdministrador: false, esSoloLectura: true, esSupervisor: true, esTecnicoSoporte: false);

        rol.Nombre.Should().Be("Auditor interno");
        rol.NivelAlcance.Should().Be(NivelAlcance.Institucion);
        rol.Descripcion.Should().Be("Revisa expedientes");
        rol.EsSoloLectura.Should().BeTrue();
        rol.EsSupervisor.Should().BeTrue();
    }

    [Fact]
    public void Actualizar_AdministradorYSoloLectura_LanzaDomainException()
    {
        var rol = Rol.Crear("Auditor", "Auditor", NivelAlcance.Unidad);

        var act = () => rol.Actualizar("Auditor", NivelAlcance.Unidad, null, null,
            esAdministrador: true, esSoloLectura: true, esSupervisor: false, esTecnicoSoporte: false);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void DesactivarYActivar_AlternanElEstado()
    {
        var rol = Rol.Crear("Auditor", "Auditor", NivelAlcance.Unidad);

        rol.Desactivar();
        rol.Activo.Should().BeFalse();

        rol.Activar();
        rol.Activo.Should().BeTrue();
    }
}
