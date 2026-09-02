using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

/// <summary>
/// La acción del proyecto: qué hace DIGER en él. Es opcional, así que lo que se prueba no es solo
/// que se guarde, sino que <b>no tenerla siga siendo un estado válido</b> — es la situación de todo
/// el portafolio anterior al campo.
/// </summary>
public class AccionProyectoTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();

    public AccionProyectoTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
        _usuario.Nombre.Returns("Henry Cardona");
    }

    private Task<int> CrearAsync(string nombre, AccionProyecto? accion) =>
        new CrearProyectoCommandHandler(_ctx, _usuario).Handle(
            new CrearProyectoCommand(nombre, Accion: accion, FechaInicioPlan: new DateOnly(2026, 3, 1)),
            CancellationToken.None);

    // ── Alta ──────────────────────────────────────────────────────
    [Fact]
    public async Task Crear_GuardaLaAccionDeclarada()
    {
        var id = await CrearAsync("Digitalización SENAEH", AccionProyecto.Digitalizacion);

        (await _ctx.Proyectos.FindAsync(id))!.Accion.Should().Be(AccionProyecto.Digitalizacion);
    }

    [Fact]
    public async Task Crear_SinAccionElProyectoQuedaSinClasificar()
    {
        var id = await CrearAsync("Proyecto sin clasificar", null);

        (await _ctx.Proyectos.FindAsync(id))!.Accion.Should().BeNull(
            "la acción es opcional: no declararla no puede equivaler a elegir una");
    }

    // ── Edición ───────────────────────────────────────────────────
    [Fact]
    public async Task Actualizar_ClasificarUnProyectoQuedaEnLaBitacora()
    {
        var id = await CrearAsync("SOL — CONSUCOOP", null);

        await ActualizarConAccionAsync(id, "SOL — CONSUCOOP", AccionProyecto.Acompanamiento);

        (await _ctx.Proyectos.FindAsync(id))!.Accion.Should().Be(AccionProyecto.Acompanamiento);

        var ficha = await _ctx.BitacorasProyecto.SingleAsync(b => b.Tipo == TipoEventoProyecto.ModificacionFicha);
        ficha.Detalle.Should().Contain("acción")
             .And.Contain("Sin clasificar")
             .And.Contain("Acompañamiento", "la bitácora escribe el rótulo con tilde, no el nombre del enum");
        ficha.Actor.Should().Be("Henry Cardona");
    }

    [Fact]
    public async Task Actualizar_GuardarSinCambiarLaAccionNoEscribeBitacora()
    {
        var id = await CrearAsync("Soporte SIGER", AccionProyecto.Soporte);

        await ActualizarConAccionAsync(id, "Soporte SIGER", AccionProyecto.Soporte);

        (await _ctx.BitacorasProyecto.CountAsync()).Should().Be(0,
            "un guardado que no cambia nada no debe ensuciar el historial");
    }

    // ── Filtro del listado ────────────────────────────────────────
    [Fact]
    public async Task GetProyectos_FiltraPorAccion()
    {
        await CrearAsync("Acompañamiento A", AccionProyecto.Acompanamiento);
        await CrearAsync("Digitalización B", AccionProyecto.Digitalizacion);
        await CrearAsync("Desarrollo C",     AccionProyecto.Desarrollo);
        await CrearAsync("Sin clasificar D", null);

        var handler = new GetProyectosQueryHandler(_ctx);

        var digitalizacion = await handler.Handle(
            new GetProyectosQuery(Accion: AccionProyecto.Digitalizacion), CancellationToken.None);

        digitalizacion.Select(p => p.Nombre).Should().Equal("Digitalización B");
    }

    [Fact]
    public async Task GetProyectos_SinFiltroTraeTambienLosSinClasificar()
    {
        await CrearAsync("Con acción", AccionProyecto.Soporte);
        await CrearAsync("Sin acción", null);

        var todos = await new GetProyectosQueryHandler(_ctx)
            .Handle(new GetProyectosQuery(), CancellationToken.None);

        todos.Should().HaveCount(2);
        todos.Single(p => p.Nombre == "Sin acción").Accion.Should().BeNull();
    }

    /// <summary>
    /// Guarda la ficha dejando la estructura vacía: acá lo único que se mira es la acción.
    ///
    /// <para>Repite la fecha de inicio con la que nació el proyecto. Sin eso el guardado la borra y
    /// la bitácora registra ese cambio con toda razón — y la prueba de «no cambió nada» estaría
    /// midiendo un cambio que introduce ella misma.</para>
    /// </summary>
    private Task ActualizarConAccionAsync(int id, string nombre, AccionProyecto? accion) =>
        new ActualizarProyectoCommandHandler(_ctx, _usuario).Handle(
            new ActualizarProyectoCommand(
                id, nombre, null, null, null, null, null,
                PrioridadProyecto.Media, accion, new DateOnly(2026, 3, 1), null, []),
            CancellationToken.None);

    public void Dispose() => _ctx.Dispose();
}
