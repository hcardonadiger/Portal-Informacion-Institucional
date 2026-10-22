using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

/// <summary>
/// Entrega 3 (2026-10-22): el responsable de un riesgo es un interesado del proyecto o su
/// responsable, igual que en entregables y actividades. Antes el servidor aceptaba a cualquier
/// usuario: bastaba con cambiar el valor del selector en la petición. Solo se valida lo que cambia,
/// así que un riesgo anterior a la regla se sigue editando sin tocarle el responsable.
/// </summary>
public sealed class RiesgosResponsableTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ICurrentUserService _usuario = new FakeCurrentUser();

    private readonly Usuario _interesado = Usuario.Crear("Ines Interesada", "ines@prueba.local", "hash");
    private readonly Usuario _duenio     = Usuario.Crear("Raul Responsable", "raul@prueba.local", "hash");
    private readonly Usuario _ajeno      = Usuario.Crear("Ana Ajena", "ana@prueba.local", "hash");

    private readonly int _proyecto;

    public RiesgosResponsableTests()
    {
        _ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            _usuario, Substitute.For<MediatR.IPublisher>());

        _ctx.Usuarios.AddRange(_interesado, _duenio, _ajeno);

        // El responsable a propósito NO figura entre los interesados: así quedaron los proyectos
        // creados antes de que se registrara solo.
        var p = Proyecto.Crear("PRY-2026-901", "Proyecto con equipo");
        p.PrioridadId   = PrioridadesDePrueba.Media(_ctx);
        p.ResponsableId = _duenio.Id;
        p.Responsable   = _duenio.Nombre;
        _ctx.Proyectos.Add(p);
        _ctx.SaveChanges();

        _ctx.ProyectoInteresados.Add(InteresadoProyecto.Crear(
            p.Id, _interesado.Id, _interesado.Nombre, RolInteresado.Ejecutor, "siembra"));
        _ctx.SaveChanges();
        _proyecto = p.Id;
    }

    public void Dispose() => _ctx.Dispose();

    private Task<int> RegistrarAsync(Guid? responsable) =>
        new RegistrarRiesgoCommandHandler(_ctx, _usuario).Handle(
            new RegistrarRiesgoCommand(_proyecto, "Falta designar contraparte", CategoriaRiesgo.Institucional,
                NivelCualitativo.Media, NivelCualitativo.Alta, EstrategiaRiesgo.Mitigar, ResponsableId: responsable),
            CancellationToken.None);

    private Task ActualizarAsync(int riesgoId, Guid? responsable, string texto = "Texto corregido") =>
        new ActualizarRiesgoCommandHandler(_ctx, _usuario).Handle(
            new ActualizarRiesgoCommand(riesgoId, texto, CategoriaRiesgo.Institucional, NivelCualitativo.Alta,
                NivelCualitativo.Alta, EstrategiaRiesgo.Mitigar, null, responsable, null, null, ProyectoId: _proyecto),
            CancellationToken.None);

    /// <summary>Un riesgo de antes de la regla, con un responsable que hoy no sería elegible. Se
    /// siembra directo porque el comando ya no lo deja crear.</summary>
    private int RiesgoHeredado()
    {
        var r = RiesgoProyecto.Crear(_proyecto, "Riesgo de antes de la regla", CategoriaRiesgo.Institucional,
            NivelCualitativo.Media, NivelCualitativo.Media, EstrategiaRiesgo.Mitigar, "siembra",
            responsableId: _ajeno.Id, responsable: _ajeno.Nombre);
        _ctx.ProyectoRiesgos.Add(r);
        _ctx.SaveChanges();
        return r.Id;
    }

    private Task<RiesgoProyecto> LeerAsync(int riesgoId) =>
        _ctx.ProyectoRiesgos.AsNoTracking().SingleAsync(r => r.Id == riesgoId);

    [Fact]
    public async Task Registrar_rechaza_un_responsable_que_no_es_interesado_ni_responsable()
    {
        var act = () => RegistrarAsync(_ajeno.Id);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*interesado del proyecto o de su responsable*");
        (await _ctx.ProyectoRiesgos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Registrar_acepta_a_un_interesado()
    {
        var id = await RegistrarAsync(_interesado.Id);

        (await LeerAsync(id)).ResponsableId.Should().Be(_interesado.Id);
    }

    [Fact]
    public async Task Registrar_acepta_al_responsable_del_proyecto_aunque_no_figure_como_interesado()
    {
        var id = await RegistrarAsync(_duenio.Id);

        (await LeerAsync(id)).ResponsableId.Should().Be(_duenio.Id);
    }

    [Fact]
    public async Task Registrar_sin_responsable_sigue_funcionando()
    {
        var id = await RegistrarAsync(null);

        (await LeerAsync(id)).ResponsableId.Should().BeNull();
    }

    [Fact]
    public async Task Actualizar_rechaza_pasar_el_riesgo_a_alguien_que_no_es_elegible()
    {
        var id = await RegistrarAsync(_interesado.Id);
        _ctx.ChangeTracker.Clear();

        var act = () => ActualizarAsync(id, _ajeno.Id);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*interesado del proyecto o de su responsable*");
        (await LeerAsync(id)).ResponsableId.Should().Be(_interesado.Id);
    }

    [Fact]
    public async Task Actualizar_acepta_pasarlo_al_responsable_del_proyecto()
    {
        var id = await RegistrarAsync(_interesado.Id);

        await ActualizarAsync(id, _duenio.Id);

        (await LeerAsync(id)).ResponsableId.Should().Be(_duenio.Id);
    }

    [Fact]
    public async Task Un_riesgo_antiguo_se_edita_sin_tocar_su_responsable()
    {
        var id = RiesgoHeredado();

        await ActualizarAsync(id, _ajeno.Id);

        var r = await LeerAsync(id);
        r.Descripcion.Should().Be("Texto corregido");
        r.ResponsableId.Should().Be(_ajeno.Id);
    }

    [Fact]
    public async Task Un_riesgo_antiguo_puede_pasar_a_alguien_del_equipo_o_quedar_sin_responsable()
    {
        var aInteresado = RiesgoHeredado();
        var sinNadie    = RiesgoHeredado();

        await ActualizarAsync(aInteresado, _interesado.Id);
        await ActualizarAsync(sinNadie, null);

        (await LeerAsync(aInteresado)).ResponsableId.Should().Be(_interesado.Id);
        (await LeerAsync(sinNadie)).ResponsableId.Should().BeNull();
    }
}
