using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Application.Proyectos.Services;
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
    private readonly IRolCatalogo _catalogo = Substitute.For<IRolCatalogo>();
    private readonly ICurrentUserService _actor = Substitute.For<ICurrentUserService>();

    private InteresadosAutomaticosSyncService Sync() => new(_ctx, _catalogo);
    private QuitarInteresadoCommandHandler Handler() => new(_ctx, _actor, Sync());

    public InteresadoCommandsTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.EsGlobal.Returns(true);
        currentUser.Nombre.Returns("Prueba");
        _ctx = new AppDbContext(opts, currentUser, Substitute.For<MediatR.IPublisher>());
        _actor.Nombre.Returns("Prueba");
    }

    [Fact]
    public async Task Quitar_FilaAutomaticaDeQuienSigueTeniendoElDerecho_SeRechaza()
    {
        var (jefe, proyecto) = await JefeConProyectoDeSuAreaAsync("PRY-2026-99");

        var interesado = InteresadoProyecto.CrearAutomatico(
            proyecto.Id, jefe.Id, jefe.Nombre, RolInteresado.Patrocinador, null);
        _ctx.ProyectoInteresados.Add(interesado);
        await _ctx.SaveChangesAsync();

        var accion = async () => await Handler().Handle(
            new QuitarInteresadoCommand(interesado.Id), CancellationToken.None);

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

        await Handler().Handle(new QuitarInteresadoCommand(interesado.Id), CancellationToken.None);

        (await _ctx.ProyectoInteresados.AnyAsync(i => i.Id == interesado.Id)).Should().BeFalse();
    }

    // ── Guarda por derecho vigente (I2) ───────────────────────────────────
    // La guarda no mira la bandera Automatico sino si la persona HOY está habilitada por su
    // capacidad de rol para ese proyecto. Es lo que cierra el hueco de la fila manual: quien ya
    // era interesado manual antes de ser jefe de área nunca recibe la fila automática (el sync
    // salta a quien ya figura), así que su acceso cuelga de la manual — y si esa se puede quitar,
    // pierde permanentemente el acceso que su capacidad debería garantizarle.

    private static RolInfo RolJefeDeArea => new(
        "JefeArea", "Jefe de Área", NivelAlcance.Area, false, false, false, false,
        EsJefeDeArea: true, EsPmo: false, Color: null);

    private async Task<(Usuario Jefe, Proyecto Proyecto)> JefeConProyectoDeSuAreaAsync(string codigo)
    {
        var jefe = Usuario.Crear("Jefa del área", $"{Guid.NewGuid():N}@diger.gob.hn", "hash");
        _ctx.Usuarios.Add(jefe);
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefe.Id, "DIGER", "GOBDIGITAL", null, "JefeArea"));
        _catalogo.Obtener("JefeArea").Returns(RolJefeDeArea);

        var proyecto = Proyecto.Crear(codigo, "Proyecto de prueba");
        proyecto.AreaId = "GOBDIGITAL";
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();
        return (jefe, proyecto);
    }

    [Fact]
    public async Task Quitar_FilaManualDeQuienHoyEsJefeDeEsaArea_SeRechaza()
    {
        var (jefe, proyecto) = await JefeConProyectoDeSuAreaAsync("PRY-2026-97");

        var manual = InteresadoProyecto.Crear(
            proyecto.Id, jefe.Id, jefe.Nombre, RolInteresado.ContraparteTecnica, "Alguien");
        _ctx.ProyectoInteresados.Add(manual);
        await _ctx.SaveChangesAsync();

        var accion = async () => await Handler().Handle(
            new QuitarInteresadoCommand(manual.Id), CancellationToken.None);

        await accion.Should().ThrowAsync<DomainException>();
        (await _ctx.ProyectoInteresados.AnyAsync(i => i.Id == manual.Id)).Should().BeTrue();
    }

    /// <summary>La contracara: la fila automática que quedó huérfana —su dueño ya no tiene la
    /// capacidad— sí se puede quitar. Antes la bandera la volvía irremovible para siempre y no
    /// había ninguna salida desde el portal.</summary>
    [Fact]
    public async Task Quitar_FilaAutomaticaDeQuienYaNoTieneElDerecho_SePermite()
    {
        var proyecto = Proyecto.Crear("PRY-2026-96", "Proyecto de prueba");
        proyecto.AreaId = "GOBDIGITAL";
        _ctx.Proyectos.Add(proyecto);
        await _ctx.SaveChangesAsync();

        // Sin asignación ni capacidad: la fila quedó de una capacidad ya revocada.
        var huerfana = InteresadoProyecto.CrearAutomatico(
            proyecto.Id, Guid.NewGuid(), "Ex jefe", RolInteresado.Patrocinador, null);
        _ctx.ProyectoInteresados.Add(huerfana);
        await _ctx.SaveChangesAsync();

        await Handler().Handle(new QuitarInteresadoCommand(huerfana.Id), CancellationToken.None);

        (await _ctx.ProyectoInteresados.AnyAsync(i => i.Id == huerfana.Id)).Should().BeFalse();
    }

    // ── El DTO dice si la fila se puede quitar (I4) ────────────────────────
    [Fact]
    public async Task Consulta_MarcaComoNoRemovibleAQuienEstaHabilitadoPorSuCapacidad()
    {
        var (jefe, proyecto) = await JefeConProyectoDeSuAreaAsync("PRY-2026-95");

        var manual = InteresadoProyecto.Crear(
            proyecto.Id, jefe.Id, jefe.Nombre, RolInteresado.ContraparteTecnica, "Alguien");
        var otro = InteresadoProyecto.Crear(
            proyecto.Id, Guid.NewGuid(), "Contraparte cualquiera", RolInteresado.Beneficiario, "Alguien");
        _ctx.ProyectoInteresados.AddRange(manual, otro);
        await _ctx.SaveChangesAsync();

        var filas = await new GetInteresadosProyectoQueryHandler(_ctx, Sync())
            .Handle(new GetInteresadosProyectoQuery(proyecto.Id), CancellationToken.None);

        filas.Single(f => f.Id == manual.Id).Removible.Should().BeFalse(
            "la vista tiene que salir de la misma fuente de verdad que la guarda del comando");
        filas.Single(f => f.Id == otro.Id).Removible.Should().BeTrue();
    }

    public void Dispose() => _ctx.Dispose();
}
