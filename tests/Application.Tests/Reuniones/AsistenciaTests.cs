using Diger.TramitesEstado.Application.Reuniones.Asistencia;
using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Domain.Common;             // DomainException
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

public class AsistenciaTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ReunionRepository _repo;

    public AsistenciaTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser());
        _repo = new ReunionRepository(_ctx);
    }

    private async Task<Reunion> SembrarAsync(bool abierto = true)
    {
        var r = Reunion.Crear("Taller de prueba");
        r.RegistroAbierto = abierto;
        await _ctx.Reuniones.AddAsync(r);
        await _ctx.SaveChangesAsync();
        return r;
    }

    private static AsistenteAutoInput Datos(string nombre, string? correo = null) =>
        new() { Nombre = nombre, Correo = correo, CodigoPais = "+504", Telefono = "99998888", Institucion = "SENASA" };

    [Fact]
    public async Task Registrar_AgregaAsistenteYActualizaConteo()
    {
        var r = await SembrarAsync();
        var handler = new RegistrarAsistenciaCommandHandler(_repo, _ctx);

        var titulo = await handler.Handle(
            new RegistrarAsistenciaCommand(r.RegistroToken, Datos("Ana", "ana@x.com")), CancellationToken.None);

        titulo.Should().Be("Taller de prueba");
        var saved = await _repo.GetByTokenWithAsistentesAsync(r.RegistroToken);
        saved!.Asistentes.Should().HaveCount(1);
        saved.Asistentes.First().AutoRegistro.Should().BeTrue();
        saved.Asistentes.First().Telefono.Should().Be("+504 99998888");
        saved.NumAsistentes.Should().Be(1);
    }

    [Fact]
    public async Task Registrar_RechazaCorreoDuplicado()
    {
        var r = await SembrarAsync();
        var handler = new RegistrarAsistenciaCommandHandler(_repo, _ctx);
        await handler.Handle(new RegistrarAsistenciaCommand(r.RegistroToken, Datos("Ana", "dup@x.com")), CancellationToken.None);

        var act = () => handler.Handle(
            new RegistrarAsistenciaCommand(r.RegistroToken, Datos("Otra", "DUP@x.com")), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Registrar_RechazaCuandoCerrado()
    {
        var r = await SembrarAsync(abierto: false);
        var handler = new RegistrarAsistenciaCommandHandler(_repo, _ctx);

        var act = () => handler.Handle(
            new RegistrarAsistenciaCommand(r.RegistroToken, Datos("Ana")), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    public void Dispose() => _ctx.Dispose();
}
