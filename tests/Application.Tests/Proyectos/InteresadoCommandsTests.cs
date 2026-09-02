using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

public class InteresadoCommandsTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public InteresadoCommandsTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.EsGlobal.Returns(true);
        currentUser.Nombre.Returns("Prueba");
        _ctx = new AppDbContext(opts, currentUser, Substitute.For<MediatR.IPublisher>());
    }

    [Fact]
    public async Task Quitar_InteresadoAutomatico_SeRechaza()
    {
        var proyecto = Proyecto.Crear("PRY-2026-99", "Proyecto de prueba");
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        var interesado = InteresadoProyecto.CrearAutomatico(
            proyecto.Id, Guid.NewGuid(), "Jefe de Área", RolInteresado.Patrocinador, null);
        _ctx.ProyectoInteresados.Add(interesado);
        await _ctx.SaveChangesAsync();

        var handler = new QuitarInteresadoCommandHandler(_ctx, Substitute.For<ICurrentUserService>());

        var accion = async () => await handler.Handle(new QuitarInteresadoCommand(interesado.Id), CancellationToken.None);

        await accion.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Quitar_InteresadoManual_SePermite()
    {
        var proyecto = Proyecto.Crear("PRY-2026-98", "Proyecto de prueba");
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        var interesado = InteresadoProyecto.Crear(
            proyecto.Id, Guid.NewGuid(), "Interesado manual", RolInteresado.Beneficiario, "Prueba");
        _ctx.ProyectoInteresados.Add(interesado);
        await _ctx.SaveChangesAsync();

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.Nombre.Returns("Prueba");
        var handler = new QuitarInteresadoCommandHandler(_ctx, currentUser);

        await handler.Handle(new QuitarInteresadoCommand(interesado.Id), CancellationToken.None);

        (await _ctx.ProyectoInteresados.AnyAsync(i => i.Id == interesado.Id)).Should().BeFalse();
    }

    public void Dispose() => _ctx.Dispose();
}
