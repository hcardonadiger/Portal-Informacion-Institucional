using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Application.Tickets.Common;
using Diger.TramitesEstado.Application.Tickets.Queries.GetUsuariosAsignables;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Tickets;

/// <summary>
/// El «Responsable del Acta» de un expediente sale de esta consulta. Como Expedientes está
/// limitado a DITRA en <c>ModuloAmbitos</c>, la lista solo debe traer a las personas asignadas a
/// esa unidad.
/// </summary>
public sealed class UsuariosAsignablesPorModuloTests : IDisposable
{
    private readonly AppDbContext _ctx = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

    public void Dispose() => _ctx.Dispose();

    private async Task<Usuario> Persona(string nombre, string inst, string? area, string? unidad)
    {
        var u = Usuario.Crear(nombre, $"{nombre}@prueba.local", "hash");
        _ctx.Usuarios.Add(u);
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(u.Id, inst, area, unidad, "Empleado"));
        await _ctx.SaveChangesAsync();
        return u;
    }

    private Task<IReadOnlyList<UsuarioAsignableDto>> Consultar(string? modulo) =>
        new GetUsuariosAsignablesQueryHandler(_ctx).Handle(new GetUsuariosAsignablesQuery(modulo), CancellationToken.None);

    [Fact]
    public async Task Modulo_limitado_a_DITRA_solo_trae_a_su_personal()
    {
        await Persona("Ana",   "DIGER", "GOBDIG", "DITRA");
        await Persona("Beto",  "DIGER", "GOBDIG", "DAI");
        await Persona("Carla", "OTRA",  "GOBDIG", "DITRA");
        _ctx.ModuloAmbitos.Add(ModuloAmbito.Crear("Expedientes", "DIGER", "GOBDIG", "DITRA"));
        await _ctx.SaveChangesAsync();

        var lista = await Consultar("Expedientes");

        lista.Select(u => u.Nombre).Should().Equal("Ana");
    }

    [Fact]
    public async Task Modulo_sin_ambito_o_sin_modulo_trae_a_todos()
    {
        await Persona("Ana",  "DIGER", "GOBDIG", "DITRA");
        await Persona("Beto", "DIGER", "GOBDIG", "DAI");
        _ctx.ModuloAmbitos.Add(ModuloAmbito.Crear("Siger", "DIGER", "GOBDIG", "DITRA"));
        await _ctx.SaveChangesAsync();

        (await Consultar("Expedientes")).Should().HaveCount(2);
        (await Consultar(null)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Submodulo_hereda_la_restriccion_del_padre()
    {
        await Persona("Ana",  "DIGER", "GOBDIG", "DITRA");
        await Persona("Beto", "DIGER", "GOBDIG", "DAI");
        _ctx.ModuloAmbitos.Add(ModuloAmbito.Crear("Siger", "DIGER", "GOBDIG", "DITRA"));
        await _ctx.SaveChangesAsync();

        (await Consultar("Siger.Conciliacion")).Select(u => u.Nombre).Should().Equal("Ana");
    }
}
