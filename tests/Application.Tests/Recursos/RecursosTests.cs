using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Recursos.Commands.CrearRecurso;
using Diger.TramitesEstado.Application.Recursos.Commands.EliminarRecurso;
using Diger.TramitesEstado.Application.Recursos.Commands.RegistrarDescarga;
using Diger.TramitesEstado.Application.Recursos.Queries.GetRecursos;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Recursos;

public class RecursosTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public RecursosTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser());
    }

    [Fact]
    public async Task CrearRecurso_SiEsAdmin_CreaExitosamente()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.Rol.Returns("Administrador");

        var handler = new CrearRecursoCommandHandler(_ctx, currentUser);
        var cmd = new CrearRecursoCommand(
            "Plantilla Trámite SOL",
            "Plantilla estándar",
            "Plantilla",
            "plantilla_sol.docx",
            "/uploads/recursos/plantilla_sol.docx",
            204800
        );

        var id = await handler.Handle(cmd, CancellationToken.None);

        var rec = await _ctx.Recursos.FindAsync(id);
        rec.Should().NotBeNull();
        rec!.Titulo.Should().Be("Plantilla Trámite SOL");
        rec.Categoria.Should().Be("Plantilla");
        rec.DescargasCount.Should().Be(0);
    }

    [Fact]
    public async Task CrearRecurso_SiNoEsAdmin_LanzaExcepcion()
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.Rol.Returns("Empleado");

        var handler = new CrearRecursoCommandHandler(_ctx, currentUser);
        var cmd = new CrearRecursoCommand(
            "Guía de Usuario",
            "Guía rápida",
            "Guía / Manual",
            "guia.pdf",
            "/uploads/recursos/guia.pdf",
            1048576
        );

        var act = async () => await handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetRecursosQuery_FiltraPorCategoriaYBusqueda()
    {
        var r1 = Recurso.Crear("Plantilla Ficha", "Ficha técnica", "Plantilla", "ficha.docx", "/uploads/recursos/ficha.docx", 500);
        var r2 = Recurso.Crear("Manual SOL", "Guía del usuario SOL", "Guía / Manual", "manual.pdf", "/uploads/recursos/manual.pdf", 1200);
        _ctx.Recursos.AddRange(r1, r2);
        await _ctx.SaveChangesAsync();

        var queryHandler = new GetRecursosQueryHandler(_ctx);

        var resTodos = await queryHandler.Handle(new GetRecursosQuery(), CancellationToken.None);
        resTodos.Should().HaveCount(2);

        var resPlantilla = await queryHandler.Handle(new GetRecursosQuery(null, "Plantilla"), CancellationToken.None);
        resPlantilla.Should().HaveCount(1);
        resPlantilla[0].Titulo.Should().Be("Plantilla Ficha");

        var resSearch = await queryHandler.Handle(new GetRecursosQuery("SOL", null), CancellationToken.None);
        resSearch.Should().HaveCount(1);
        resSearch[0].Titulo.Should().Be("Manual SOL");
    }

    [Fact]
    public async Task RegistrarDescarga_IncrementaContadorDescargas()
    {
        var r = Recurso.Crear("Formato Solicitud", "Formato oficial", "Formulario", "form.pdf", "/uploads/recursos/form.pdf", 800);
        _ctx.Recursos.Add(r);
        await _ctx.SaveChangesAsync();

        var handler = new RegistrarDescargaRecursoCommandHandler(_ctx);
        await handler.Handle(new RegistrarDescargaRecursoCommand(r.Id), CancellationToken.None);

        var updated = await _ctx.Recursos.FindAsync(r.Id);
        updated!.DescargasCount.Should().Be(1);
    }

    public void Dispose()
    {
        _ctx.Dispose();
    }
}
