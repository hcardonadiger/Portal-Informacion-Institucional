using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests.Entities;

public class ExpedienteTests
{
    private static Expediente CrearValido() =>
        Expediente.Crear("EXP-CNBS-2026-01", "CNBS", null, null, "CNBS", "Ana Analista");

    [Fact]
    public void Crear_DatosValidos_CreaExpediente()
    {
        var e = CrearValido();

        e.Codigo.Should().Be("EXP-CNBS-2026-01");
        e.InstitucionId.Should().Be("CNBS");
        e.Institucion.Should().Be("CNBS");
        e.Analista.Should().Be("Ana Analista");
        e.EstadoExpediente.Should().Be(EstadoExpediente.EnExploracion);
    }

    [Fact]
    public void Crear_EmiteEventoDeDominio()
    {
        var e = CrearValido();

        e.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ExpedienteCreatedEvent>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_CodigoVacio_LanzaArgumentException(string codigo)
    {
        var act = () => Expediente.Crear(codigo, "CNBS", null, null, "CNBS", "Ana");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Crear_InstitucionInvalida_LanzaArgumentException()
    {
        var act = () => Expediente.Crear("EXP-X-2026-01", "", null, null, "CNBS", "Ana");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Agregar_TramiteYRequisito_IncrementaColecciones()
    {
        var e = CrearValido();
        e.Agregar(new ExpedienteTramite { TramiteIndex = 0, NombreTramite = "Constancia" });
        e.Agregar(new TramiteRequisito { TramiteIndex = 0, Orden = 0, Requisito = "Cédula" });

        e.Tramites.Should().HaveCount(1);
        e.Requisitos.Should().HaveCount(1);
        e.Tramites.First().NombreTramite.Should().Be("Constancia");
    }

    [Fact]
    public void LimpiarHijos_VaciaTodasLasColecciones()
    {
        var e = CrearValido();
        e.Agregar(new ExpedienteTramite { TramiteIndex = 0, NombreTramite = "T1" });
        e.Agregar(new FundamentoLegal { Orden = 0, Instrumento = "Ley X" });

        e.LimpiarHijos();

        e.Tramites.Should().BeEmpty();
        e.Legal.Should().BeEmpty();
    }
}
