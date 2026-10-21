using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Application.Tickets.Queries.GetOperadoresSoporte;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Tickets;

/// <summary>
/// Verifica la fuente de operadores para la asignación de tickets: primero los especialistas del
/// tema (<see cref="UsuarioTema"/>) y, cuando el tema no tiene ninguno, el <i>fallback</i> a los
/// usuarios cuyo rol tiene la capacidad <c>EsTecnicoSoporte</c>.
/// </summary>
public class OperadoresSoporteTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly GetOperadoresSoporteQueryHandler _handler;

    public OperadoresSoporteTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), NSubstitute.Substitute.For<MediatR.IPublisher>());
        _handler = new GetOperadoresSoporteQueryHandler(_ctx);
    }

    private async Task<Usuario> SeedUsuarioAsync(string nombre)
    {
        var u = Usuario.Crear(nombre, $"{nombre.Replace(" ", "").ToLowerInvariant()}@x.com", "hash");
        await _ctx.Usuarios.AddAsync(u);
        await _ctx.SaveChangesAsync();
        return u;
    }

    private async Task SeedRolAsync(string codigo, bool esTecnicoSoporte)
    {
        await _ctx.Roles.AddAsync(Rol.Crear(codigo, codigo, NivelAlcance.Unidad, esTecnicoSoporte: esTecnicoSoporte));
        await _ctx.SaveChangesAsync();
    }

    private async Task SeedAsignacionAsync(Guid usuarioId, string rol)
    {
        await _ctx.AsignacionesUsuario.AddAsync(AsignacionUsuario.Crear(usuarioId, "DIGER", null, null, rol));
        await _ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Prefiere_especialistas_del_tema()
    {
        var tema = TemaTicket.Crear("Acceso", 0);
        await _ctx.TemasTicket.AddAsync(tema);
        await _ctx.SaveChangesAsync();

        var especialista = await SeedUsuarioAsync("Ana Especialista");
        var otro         = await SeedUsuarioAsync("Beto Otro");
        await _ctx.UsuarioTemas.AddAsync(UsuarioTema.Crear(especialista.Id, tema.Id));
        await _ctx.SaveChangesAsync();

        var ops = await _handler.Handle(new GetOperadoresSoporteQuery(tema.Id), CancellationToken.None);

        ops.Select(o => o.Id).Should().ContainSingle().Which.Should().Be(especialista.Id);
        ops.Select(o => o.Id).Should().NotContain(otro.Id);
    }

    [Fact]
    public async Task Sin_especialistas_cae_a_tecnicos_de_soporte()
    {
        var tema = TemaTicket.Crear("Config", 0); // sin especialistas
        await _ctx.TemasTicket.AddAsync(tema);
        await _ctx.SaveChangesAsync();

        await SeedRolAsync("SOPORTE", esTecnicoSoporte: true);
        await SeedRolAsync("EMPLEADO", esTecnicoSoporte: false);

        var soporte  = await SeedUsuarioAsync("Carla Soporte");
        var empleado = await SeedUsuarioAsync("Dario Empleado");
        await SeedAsignacionAsync(soporte.Id, "SOPORTE");
        await SeedAsignacionAsync(empleado.Id, "EMPLEADO");

        var porTema = await _handler.Handle(new GetOperadoresSoporteQuery(tema.Id), CancellationToken.None);
        porTema.Select(o => o.Id).Should().BeEquivalentTo([soporte.Id]);

        // Sin tema indicado también cae al conjunto de técnicos de soporte.
        var sinTema = await _handler.Handle(new GetOperadoresSoporteQuery(null), CancellationToken.None);
        sinTema.Select(o => o.Id).Should().BeEquivalentTo([soporte.Id]);
    }

    public void Dispose() => _ctx.Dispose();
}
