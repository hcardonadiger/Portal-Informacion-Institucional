using Diger.TramitesEstado.Application.Dashboards.Queries.GetMiTablero;
using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Tableros;

public class MiTableroTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public MiTableroTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser());
    }

    [Fact]
    public async Task GetMiTablero_RetornaSoloElementosAsociadosAlUsuario()
    {
        var fakeUser = new FakeCurrentUser();
        var handler = new GetMiTableroQueryHandler(_ctx, fakeUser);

        // Sembrar reunión donde fakeUser participa por correo
        var r = Reunion.Crear("Taller con Usuario");
        r.RegistrarAsistente("User Name", "Cargo", "DIGER", "Tecnología", fakeUser.Correo, "+504 99998888");
        _ctx.Reuniones.Add(r);

        // Sembrar ticket donde fakeUser es reportante
        var t = Ticket.Crear("TCK-2026-0001", "Falla en sistema");
        t.EstablecerReportante("User Name", fakeUser.Correo, "+504 99998888");
        _ctx.Tickets.Add(t);

        await _ctx.SaveChangesAsync();

        var result = await handler.Handle(new GetMiTableroQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result.Reuniones.Should().HaveCount(1);
        result.Reuniones.First().Titulo.Should().Be("Taller con Usuario");
        result.Tickets.Should().HaveCount(1);
        result.Tickets.First().Titulo.Should().Be("Falla en sistema");
    }

    public void Dispose() => _ctx.Dispose();
}
