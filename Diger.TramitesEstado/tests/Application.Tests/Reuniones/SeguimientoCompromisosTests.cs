using Diger.TramitesEstado.Application.Reuniones.Commands.ActualizarSeguimiento;
using Diger.TramitesEstado.Application.Reuniones.Queries.GetCompromisos;
using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

public class SeguimientoCompromisosTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public SeguimientoCompromisosTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser());
    }

    private async Task<AcuerdoReunion> SembrarAcuerdoAsync(DateOnly? plazo, EstadoCompromiso estado)
    {
        var r = Reunion.Crear("Reunión de prueba");
        r.InstitucionId = 7; r.Institucion = "CNBS";
        r.Agregar(new AcuerdoReunion { Orden = 0, Compromiso = "Entregar informe", Responsable = "Ana", Plazo = plazo, Estado = estado });
        await _ctx.Reuniones.AddAsync(r);
        await _ctx.SaveChangesAsync();
        return r.Acuerdos.First();
    }

    [Fact]
    public async Task Comando_MarcarCumplido_RegistraFechaCuandoFaltaba()
    {
        var ac = await SembrarAcuerdoAsync(new DateOnly(2026, 1, 10), EstadoCompromiso.Pendiente);
        var handler = new ActualizarSeguimientoCompromisoCommandHandler(_ctx, new FakeCurrentUser());

        await handler.Handle(
            new ActualizarSeguimientoCompromisoCommand(ac.Id, EstadoCompromiso.Cumplido, null, "Listo"),
            CancellationToken.None);

        var actualizado = await _ctx.Acuerdos.FindAsync(ac.Id);
        actualizado!.Estado.Should().Be(EstadoCompromiso.Cumplido);
        actualizado.FechaCumplimiento.Should().NotBeNull();
        actualizado.NotaSeguimiento.Should().Be("Listo");
        actualizado.SeguimientoActualizadoPor.Should().Be("test");
    }

    [Fact]
    public async Task Query_DetectaVencidos_YResumen()
    {
        await SembrarAcuerdoAsync(new DateOnly(2026, 1, 1), EstadoCompromiso.Pendiente);   // vencido
        await SembrarAcuerdoAsync(new DateOnly(2999, 1, 1), EstadoCompromiso.EnProgreso);  // a futuro
        await SembrarAcuerdoAsync(new DateOnly(2026, 1, 1), EstadoCompromiso.Cumplido);     // vencido pero cumplido → no cuenta

        var handler = new GetCompromisosQueryHandler(_ctx);
        var res = await handler.Handle(new GetCompromisosQuery(), CancellationToken.None);

        res.Resumen.Total.Should().Be(3);
        res.Resumen.Vencidos.Should().Be(1);
        res.Resumen.Cumplidos.Should().Be(1);

        var soloVencidos = await handler.Handle(new GetCompromisosQuery(SoloVencidos: true), CancellationToken.None);
        soloVencidos.Pagina.Total.Should().Be(1);
        soloVencidos.Pagina.Items[0].Vencido.Should().BeTrue();
    }

    public void Dispose() => _ctx.Dispose();
}
