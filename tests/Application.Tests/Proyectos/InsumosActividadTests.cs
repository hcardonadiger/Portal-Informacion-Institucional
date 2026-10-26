using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Application.Proyectos.Services;
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
/// Entrega 4 (2026-10-22): los insumos de cada actividad viajan con la estructura del proyecto,
/// igual que las dependencias. Se prueba la regla de «solo lo que cambia» —un insumo nuevo tiene
/// que estar disponible y uno ya asignado se conserva aunque se desactive— y que no se pueda
/// asignar nada a una actividad de un proyecto fuera de alcance, aunque se arme la petición a mano.
/// </summary>
public sealed class InsumosActividadTests : IDisposable
{
    private const string MiInstitucion   = "INST-A";
    private const string OtraInstitucion = "INST-B";

    private readonly string _base = Guid.NewGuid().ToString();
    private readonly AppDbContext _ctx;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private readonly IInteresadosAutomaticosSync _sync = Substitute.For<IInteresadosAutomaticosSync>();

    private readonly int _cemento, _varilla, _retirado, _deCategoriaRetirada;
    private readonly int _proyecto, _ajeno;

    public InsumosActividadTests()
    {
        _ctx = Contexto(new FakeCurrentUser());
        _usuario.Nombre.Returns("Henry Ortez");

        var materiales = CategoriaInsumo.Crear("Materiales", 1);
        var obras      = CategoriaInsumo.Crear("Obras", 2);
        _ctx.CategoriasInsumo.AddRange(materiales, obras);
        _ctx.SaveChanges();

        var cemento  = Insumo.Crear(materiales.Id, "Cemento", null);
        var varilla  = Insumo.Crear(materiales.Id, "Varilla", null);
        var retirado = Insumo.Crear(materiales.Id, "Asbesto", null);
        var andamio  = Insumo.Crear(obras.Id, "Andamio", null);
        _ctx.Insumos.AddRange(cemento, varilla, retirado, andamio);
        _ctx.SaveChanges();

        retirado.Actualizar(materiales.Id, "Asbesto", null, activo: false);
        obras.Actualizar("Obras", 2, activo: false);
        _ctx.SaveChanges();

        (_cemento, _varilla, _retirado, _deCategoriaRetirada) = (cemento.Id, varilla.Id, retirado.Id, andamio.Id);

        _proyecto = Sembrar("PRY-2026-861", MiInstitucion, "Instalar el servidor");
        _ajeno    = Sembrar("PRY-2026-862", OtraInstitucion, "Actividad ajena");
        _ctx.ChangeTracker.Clear();
    }

    public void Dispose() => _ctx.Dispose();

    private AppDbContext Contexto(ICurrentUserService quien) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_base).Options,
            quien, Substitute.For<MediatR.IPublisher>());

    private int Sembrar(string codigo, string institucion, string actividad)
    {
        var p = Proyecto.Crear(codigo, $"Proyecto {codigo}");
        p.PrioridadId   = PrioridadesDePrueba.Media(_ctx);
        p.InstitucionId = institucion;
        var e = EntregableProyecto.Crear("Infraestructura", 1);
        e.Agregar(ActividadProyecto.Crear(actividad, 1));
        p.Agregar(e);
        _ctx.Proyectos.Add(p);
        _ctx.SaveChanges();
        return p.Id;
    }

    private Task<ProyectoDetailDto?> FichaAsync(int id, AppDbContext? ctx = null) =>
        new GetProyectoQueryHandler(ctx ?? _ctx).Handle(new GetProyectoQuery(id), CancellationToken.None);

    /// <summary>La estructura tal como la devolvería el editor, con los insumos que se le pidan a
    /// cada actividad. <paramref name="insumos"/> null = no los manda, como la herramienta MCP.</summary>
    private async Task<List<EntregableInput>> EstructuraAsync(int id, IReadOnlyList<int>? insumos)
    {
        var ficha = await FichaAsync(id);
        return ficha!.Entregables
            .Select(e => new EntregableInput(e.Id, e.Nombre, e.Descripcion, e.FechaPlan, e.Estado,
                                             e.ResponsableId, e.Responsable,
                e.Actividades.Select(a => new ActividadInput(
                    a.Id, a.Nombre, a.Descripcion, a.FechaInicioPlan, a.FechaFinPlan,
                    a.AvancePct, a.EstaCancelada, a.ResponsableId, a.Responsable,
                    a.Predecesoras.Select(p => p.Id).ToList(), insumos)).ToList()))
            .ToList();
    }

    private Task GuardarAsync(int id, IReadOnlyList<EntregableInput> entregables, AppDbContext? ctx = null) =>
        new ActualizarProyectoCommandHandler(ctx ?? _ctx, _usuario, _sync).Handle(new ActualizarProyectoCommand(
            id, $"Proyecto", null, null, null, null,
            PrioridadesDePrueba.Media(_ctx), null, null, null, null, entregables), CancellationToken.None);

    private async Task AsignarAsync(params int[] insumos)
    {
        await GuardarAsync(_proyecto, await EstructuraAsync(_proyecto, insumos));
        _ctx.ChangeTracker.Clear();
    }

    private async Task<IReadOnlyList<InsumoAsignadoDto>> InsumosAsync(int proyecto = 0) =>
        (await FichaAsync(proyecto == 0 ? _proyecto : proyecto))!.ActividadesPlanas.Single().Actividad.Insumos;

    private async Task DesactivarAsync(int insumoId)
    {
        var i = await _ctx.Insumos.SingleAsync(x => x.Id == insumoId);
        i.Actualizar(i.CategoriaId, i.Nombre, i.Descripcion, activo: false);
        await _ctx.SaveChangesAsync();
        _ctx.ChangeTracker.Clear();
    }

    // ── Asignar ───────────────────────────────────────────────────

    [Fact]
    public async Task Los_insumos_elegidos_quedan_en_la_actividad_y_la_ficha_los_devuelve()
    {
        await AsignarAsync(_varilla, _cemento);

        var insumos = await InsumosAsync();
        insumos.Select(x => x.Nombre).Should().Equal("Cemento", "Varilla");
        insumos.Should().AllSatisfy(x =>
        {
            x.Categoria.Should().Be("Materiales");
            x.Disponible.Should().BeTrue();
        });
    }

    [Fact]
    public async Task La_bitacora_registra_quien_cambio_los_insumos()
    {
        await AsignarAsync(_cemento);

        var asiento = await _ctx.BitacorasProyecto.AsNoTracking()
            .Where(b => b.ProyectoId == _proyecto && b.Tipo == TipoEventoProyecto.ModificacionEstructura)
            .SingleAsync();
        asiento.Detalle.Should().Contain("con insumos cambiados");
    }

    [Fact]
    public async Task Un_insumo_desactivado_no_se_puede_agregar_aunque_se_mande_a_mano()
    {
        var act = () => AsignarAsync(_cemento, _retirado);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Instalar el servidor*");
        _ctx.ChangeTracker.Clear();
        (await InsumosAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Un_insumo_de_una_categoria_desactivada_tampoco()
    {
        var act = () => AsignarAsync(_deCategoriaRetirada);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Un_insumo_que_no_existe_tampoco()
    {
        var act = () => AsignarAsync(987654);

        await act.Should().ThrowAsync<DomainException>();
    }

    // ── Lo que ya estaba ──────────────────────────────────────────

    [Fact]
    public async Task Uno_ya_asignado_que_despues_se_desactivo_se_conserva_y_se_marca()
    {
        await AsignarAsync(_cemento, _varilla);
        await DesactivarAsync(_cemento);

        // El editor lo vuelve a mandar tal como estaba, más uno nuevo.
        await AsignarAsync(_cemento, _varilla);

        var insumos = await InsumosAsync();
        insumos.Select(x => x.Nombre).Should().Equal("Cemento", "Varilla");
        insumos.Single(x => x.Nombre == "Cemento").Disponible.Should().BeFalse();
    }

    [Fact]
    public async Task Uno_desactivado_se_puede_quitar()
    {
        await AsignarAsync(_cemento, _varilla);
        await DesactivarAsync(_cemento);

        await AsignarAsync(_varilla);

        (await InsumosAsync()).Select(x => x.Nombre).Should().Equal("Varilla");
    }

    [Fact]
    public async Task Si_la_estructura_llega_sin_insumos_no_se_tocan()
    {
        // Es lo que manda la herramienta MCP de estructura, que no conoce los insumos.
        await AsignarAsync(_cemento);

        await GuardarAsync(_proyecto, await EstructuraAsync(_proyecto, insumos: null));
        _ctx.ChangeTracker.Clear();

        (await InsumosAsync()).Select(x => x.Nombre).Should().Equal("Cemento");
    }

    [Fact]
    public async Task Quitar_la_actividad_se_lleva_sus_insumos_sin_error()
    {
        await AsignarAsync(_cemento);

        var sinActividades = (await EstructuraAsync(_proyecto, null))
            .Select(e => e with { Actividades = [] }).ToList();
        await GuardarAsync(_proyecto, sinActividades);

        (await FichaAsync(_proyecto))!.ActividadesPlanas.Should().BeEmpty();
    }

    // ── Alcance ───────────────────────────────────────────────────

    private ICurrentUserService UsuarioDeMiInstitucion()
    {
        var u = Substitute.For<ICurrentUserService>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns(Guid.NewGuid());
        u.Nombre.Returns("Usuario de institución");
        u.EsGlobal.Returns(false);
        u.NivelAlcance.Returns(NivelAlcance.Institucion);
        u.ActiveInstitucionId.Returns(MiInstitucion);
        return u;
    }

    [Fact]
    public async Task No_se_asignan_insumos_a_una_actividad_de_un_proyecto_fuera_de_alcance()
    {
        var entrada = await EstructuraAsync(_ajeno, [_cemento]);
        using var acotado = Contexto(UsuarioDeMiInstitucion());

        var act = () => GuardarAsync(_ajeno, entrada, acotado);

        await act.Should().ThrowAsync<NotFoundException>();
        (await InsumosAsync(_ajeno)).Should().BeEmpty();
    }

    [Fact]
    public async Task La_actividad_de_otro_proyecto_metida_en_el_formulario_del_propio_se_ignora()
    {
        // Mi proyecto, pero con el Id de la actividad del proyecto ajeno colado en su estructura.
        var ajena   = (await FichaAsync(_ajeno))!.ActividadesPlanas.Single().Actividad.Id;
        var entrada = (await EstructuraAsync(_proyecto, null))
            .Select(e => e with
            {
                Actividades = [.. e.Actividades, new ActividadInput(ajena, "Actividad ajena", null, null, null,
                                                                    0, false, null, null, null, [_cemento])]
            }).ToList();
        using var acotado = Contexto(UsuarioDeMiInstitucion());

        await GuardarAsync(_proyecto, entrada, acotado);

        (await InsumosAsync(_ajeno)).Should().BeEmpty();
    }
}
