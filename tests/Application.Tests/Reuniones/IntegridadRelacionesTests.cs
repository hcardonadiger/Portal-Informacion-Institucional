using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

public class IntegridadRelacionesTests
{
    [Fact]
    public void Ticket_EstablecerCreador_UsaSistemaSiNoHayNombre()
    {
        var t = Ticket.Crear("TCK-2026-0001", "Falla");

        var uid = Guid.NewGuid();
        t.EstablecerCreador(uid, "  Ana Pérez  ");
        t.CreadoPorId.Should().Be(uid);
        t.CreadoPor.Should().Be("Ana Pérez");

        t.EstablecerCreador(null, null);
        t.CreadoPorId.Should().BeNull();
        t.CreadoPor.Should().Be("Sistema");
    }

    [Fact]
    public void Ticket_EstablecerReportante_NormalizaDatosDelUsuario()
    {
        var t = Ticket.Crear("TCK-2026-0002", "Falla");

        t.EstablecerReportante("  Ana Pérez  ", "  ANA@X.com  ");
        t.ReportanteNombre.Should().Be("Ana Pérez");
        t.ReportanteCorreo.Should().Be("ana@x.com");
        t.ReportanteTelefono.Should().BeNull();
    }
}
