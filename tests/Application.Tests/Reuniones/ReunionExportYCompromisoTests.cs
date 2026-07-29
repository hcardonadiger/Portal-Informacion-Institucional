using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Reuniones.Queries.GetCompromisos;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

public class ReunionExportYCompromisoTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public ReunionExportYCompromisoTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser());
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task GetCompromisos_DetectaCompromisosProximosAVencer()
    {
        // Arrange
        var r = Reunion.Crear("Reunión Seguimiento");
        _ctx.Reuniones.Add(r);

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        r.Agregar(new AcuerdoReunion
        {
            Compromiso = "Entregar informe borrador",
            Responsable = "Ana Analista",
            Plazo = hoy.AddDays(2), // Faltan 2 días (dentro de la ventana de 3)
            Estado = EstadoCompromiso.Pendiente
        });
        await _ctx.SaveChangesAsync();

        var handler = new GetCompromisosQueryHandler(_ctx);

        // Act
        var res = await handler.Handle(new GetCompromisosQuery(), CancellationToken.None);

        // Assert
        res.Pagina.Items.Should().HaveCount(1);
        res.Pagina.Items[0].ProximoAVencer.Should().BeTrue();
    }
}
