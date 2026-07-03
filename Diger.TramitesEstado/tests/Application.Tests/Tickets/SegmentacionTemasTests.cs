using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Application.Tickets.Queries.GetTickets;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Tickets;

public class SegmentacionTemasTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public SegmentacionTemasTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser());
    }

    private async Task<TemaTicket> SeedTemaAsync(string nombre, int horas = 0)
    {
        var t = TemaTicket.Crear(nombre, horas);
        await _ctx.TemasTicket.AddAsync(t);
        await _ctx.SaveChangesAsync();
        return t;
    }

    private async Task SeedTicketAsync(string num, int temaId)
    {
        var t = Ticket.Crear(num, "T " + num);
        t.TemaId = temaId;
        await _ctx.Tickets.AddAsync(t);
        await _ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task GetTickets_FiltraPorTemas()
    {
        var error = await SeedTemaAsync("Error");
        var config = await SeedTemaAsync("Config");
        var acceso = await SeedTemaAsync("Acceso");
        await SeedTicketAsync("TCK-1", error.Id);
        await SeedTicketAsync("TCK-2", config.Id);
        await SeedTicketAsync("TCK-3", acceso.Id);
        var h = new GetTicketsQueryHandler(_ctx);

        (await h.Handle(new GetTicketsQuery(), CancellationToken.None)).Total.Should().Be(3);

        var soloDos = await h.Handle(
            new GetTicketsQuery(TemaIds: [error.Id, acceso.Id]),
            CancellationToken.None);
        soloDos.Total.Should().Be(2);

        // Filtro activo con lista vacía (especialista sin temas) → sin resultados.
        (await h.Handle(new GetTicketsQuery(TemaIds: []), CancellationToken.None)).Total.Should().Be(0);
    }

    [Fact]
    public async Task Repo_ReemplazaTemasDelUsuario()
    {
        var t1 = await SeedTemaAsync("Error");
        var t2 = await SeedTemaAsync("Config");
        var t3 = await SeedTemaAsync("Acceso");
        var u = Usuario.Crear("Ana", "ana@x.com", "hash", RolUsuario.Tecnico);
        await _ctx.Usuarios.AddAsync(u);
        await _ctx.SaveChangesAsync();
        var repo = new UsuarioRepository(_ctx);

        await repo.ReemplazarTemasAsync(u.Id, [t1.Id, t2.Id]);
        await _ctx.SaveChangesAsync();
        (await repo.GetTemaIdsAsync(u.Id)).Should().BeEquivalentTo([t1.Id, t2.Id]);

        // Reemplazo en bloque.
        await repo.ReemplazarTemasAsync(u.Id, [t3.Id]);
        await _ctx.SaveChangesAsync();
        (await repo.GetTemaIdsAsync(u.Id)).Should().BeEquivalentTo([t3.Id]);
    }

    public void Dispose() => _ctx.Dispose();
}
