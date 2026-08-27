using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Application.Tests.Expedientes;
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
/// Dependencias Fin → Comienzo entre actividades del mismo proyecto.
///
/// <para>Lo que se prueba acá no es que la dependencia se guarde —eso lo cubriría cualquier alta—
/// sino las tres cosas que la hacen usable: que el grafo no admita círculos, que quitar una
/// actividad no deje colgada a la que dependía de ella, y que <b>bloqueada</b> sea un aviso y no
/// un candado.</para>
/// </summary>
public class ProyectosDependenciasTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();

    public ProyectosDependenciasTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
        _usuario.Nombre.Returns("Henry Ortez");
    }

    // ── Andamiaje ─────────────────────────────────────────────────
    // Un proyecto con un entregable y tres actividades: «Levantamiento», «Desarrollo» y «Piloto».
    // Alcanzan para encadenar, para cerrar un círculo y para dejar una suelta.
    private async Task<int> ProyectoConTresActividadesAsync()
    {
        var id = await new CrearProyectoCommandHandler(_ctx, _usuario)
            .Handle(new CrearProyectoCommand("SOL — institución de prueba"), CancellationToken.None);

        await GuardarAsync(id, [Entregable("Integración", "Levantamiento", "Desarrollo", "Piloto")]);
        return id;
    }

    private static EntregableInput Entregable(string nombre, params string[] actividades) =>
        new(0, nombre, null, null, EstadoEntregable.EnProceso, null, null,
            actividades.Select(a => new ActividadInput(0, a, null, null, null, 0, false, null, null)).ToList());

    private Task GuardarAsync(int id, IReadOnlyList<EntregableInput> entregables) =>
        new ActualizarProyectoCommandHandler(_ctx, _usuario).Handle(new ActualizarProyectoCommand(
            id, "SOL — institución de prueba", null, null, null, null, null,
            PrioridadProyecto.Media, null, null, entregables), CancellationToken.None);

    private Task<ProyectoDetailDto?> FichaAsync(int id) =>
        new GetProyectoQueryHandler(_ctx).Handle(new GetProyectoQuery(id), CancellationToken.None);

    /// <summary>La estructura tal como la devolvería el editor, con sus dependencias.</summary>
    private async Task<List<EntregableInput>> EstructuraActualAsync(int id)
    {
        var ficha = await FichaAsync(id);
        return ficha!.Entregables
            .Select(e => new EntregableInput(e.Id, e.Nombre, e.Descripcion, e.FechaPlan, e.Estado,
                                             e.ResponsableId, e.Responsable,
                e.Actividades.Select(a => new ActividadInput(
                    a.Id, a.Nombre, a.Descripcion, a.FechaInicioPlan, a.FechaFinPlan,
                    a.AvancePct, a.EstaCancelada, a.ResponsableId, a.Responsable,
                    a.Predecesoras.Select(p => p.Id).ToList())).ToList()))
            .ToList();
    }

    /// <summary>Reescribe la estructura haciendo que la sucesora dependa de las actividades
    /// nombradas.</summary>
    private async Task DependerAsync(int id, string sucesora, params string[] predecesoras)
    {
        var ficha  = await FichaAsync(id);
        var porNom = ficha!.ActividadesPlanas.ToDictionary(x => x.Actividad.Nombre, x => x.Actividad.Id);
        var ids    = predecesoras.Select(p => porNom[p]).ToList();

        var entrada = (await EstructuraActualAsync(id))
            .Select(e => e with
            {
                Actividades = e.Actividades
                    .Select(a => a.Nombre == sucesora ? a with { Predecesoras = ids } : a)
                    .ToList()
            }).ToList();

        await GuardarAsync(id, entrada);
    }

    private async Task<ActividadProyectoDto> ActividadAsync(int id, string nombre) =>
        (await FichaAsync(id))!.ActividadesPlanas.Single(x => x.Actividad.Nombre == nombre).Actividad;

    /// <summary>Mueve el porcentaje de una actividad como lo haría el editor.</summary>
    private async Task ReportarAsync(int id, string nombre, int pct)
    {
        var entrada = (await EstructuraActualAsync(id))
            .Select(e => e with
            {
                Actividades = e.Actividades
                    .Select(a => a.Nombre == nombre ? a with { AvancePct = pct } : a)
                    .ToList()
            }).ToList();

        await GuardarAsync(id, entrada);
    }

    // ── Alta ──────────────────────────────────────────────────────
    [Fact]
    public async Task Declara_la_dependencia_y_la_ficha_la_devuelve_con_su_entregable()
    {
        var id = await ProyectoConTresActividadesAsync();

        await DependerAsync(id, "Desarrollo", "Levantamiento");

        var desarrollo = await ActividadAsync(id, "Desarrollo");
        desarrollo.Predecesoras.Should().ContainSingle()
            .Which.Should().Match<PredecesoraDto>(p => p.Nombre == "Levantamiento"
                                                    && p.Entregable == "Integración");
    }

    [Fact]
    public async Task Descarta_las_dependencias_repetidas()
    {
        var id = await ProyectoConTresActividadesAsync();

        await DependerAsync(id, "Piloto", "Levantamiento", "Levantamiento", "Desarrollo");

        (await ActividadAsync(id, "Piloto")).Predecesoras.Should().HaveCount(2);
    }

    [Fact]
    public async Task Guardar_sin_tocar_nada_conserva_las_dependencias()
    {
        var id = await ProyectoConTresActividadesAsync();
        await DependerAsync(id, "Desarrollo", "Levantamiento");

        await GuardarAsync(id, await EstructuraActualAsync(id));

        (await ActividadAsync(id, "Desarrollo")).Predecesoras.Should().ContainSingle();
    }

    // ── Grafo ─────────────────────────────────────────────────────
    [Fact]
    public async Task Rechaza_que_una_actividad_dependa_de_si_misma()
    {
        var id = await ProyectoConTresActividadesAsync();

        var act = () => DependerAsync(id, "Desarrollo", "Desarrollo");

        (await act.Should().ThrowAsync<DomainException>())
            .WithMessage("*no puede depender de sí misma*");
    }

    [Fact]
    public async Task Rechaza_el_circulo_y_nombra_a_las_actividades_implicadas()
    {
        var id = await ProyectoConTresActividadesAsync();
        await DependerAsync(id, "Desarrollo", "Levantamiento");
        await DependerAsync(id, "Piloto", "Desarrollo");

        // Cierra el círculo: Levantamiento espera a Piloto, que espera a Desarrollo, que espera
        // a Levantamiento.
        var act = () => DependerAsync(id, "Levantamiento", "Piloto");

        var error = await act.Should().ThrowAsync<DomainException>();
        error.WithMessage("*círculo*");
        error.And.Message.Should().Contain("Levantamiento").And.Contain("Piloto").And.Contain("Desarrollo");
    }

    [Fact]
    public async Task Una_cadena_larga_sin_circulo_se_acepta()
    {
        var id = await ProyectoConTresActividadesAsync();

        await DependerAsync(id, "Desarrollo", "Levantamiento");
        await DependerAsync(id, "Piloto", "Desarrollo");

        (await ActividadAsync(id, "Piloto")).Predecesoras.Should().ContainSingle();
    }

    // ── Quitar la predecesora ─────────────────────────────────────
    [Fact]
    public async Task Quitar_la_predecesora_suelta_la_dependencia_en_vez_de_reventar()
    {
        var id = await ProyectoConTresActividadesAsync();
        await DependerAsync(id, "Desarrollo", "Levantamiento");

        // El editor manda la estructura sin «Levantamiento» y el chip de «Desarrollo» todavía la
        // referencia: es lo que pasa cuando la fila se borró en otra pestaña.
        var entrada = (await EstructuraActualAsync(id))
            .Select(e => e with { Actividades = e.Actividades.Where(a => a.Nombre != "Levantamiento").ToList() })
            .ToList();

        await GuardarAsync(id, entrada);

        var ficha = await FichaAsync(id);
        ficha!.TotalActividades.Should().Be(2);
        ficha.TotalDependencias.Should().Be(0);
    }

    // ── Bloqueo ───────────────────────────────────────────────────
    [Fact]
    public async Task Bloquea_mientras_la_predecesora_sigue_abierta_y_la_libera_al_completarla()
    {
        var id = await ProyectoConTresActividadesAsync();
        await DependerAsync(id, "Desarrollo", "Levantamiento");

        (await ActividadAsync(id, "Desarrollo")).Bloqueada.Should().BeTrue();

        await ReportarAsync(id, "Levantamiento", 100);

        (await ActividadAsync(id, "Desarrollo")).Bloqueada.Should().BeFalse();
    }

    [Fact]
    public async Task La_predecesora_cancelada_deja_de_bloquear()
    {
        var id = await ProyectoConTresActividadesAsync();
        await DependerAsync(id, "Desarrollo", "Levantamiento");

        var entrada = (await EstructuraActualAsync(id))
            .Select(e => e with
            {
                Actividades = e.Actividades
                    .Select(a => a.Nombre == "Levantamiento" ? a with { Cancelada = true } : a)
                    .ToList()
            }).ToList();
        await GuardarAsync(id, entrada);

        (await ActividadAsync(id, "Desarrollo")).Bloqueada.Should().BeFalse(
            "una actividad cancelada dejó de ser parte del plan y no traba a nadie");
    }

    [Fact]
    public async Task Avisa_pero_no_impide_reportar_avance_sobre_una_actividad_bloqueada()
    {
        var id = await ProyectoConTresActividadesAsync();
        await DependerAsync(id, "Desarrollo", "Levantamiento");

        // No lanza: el bloqueo es un aviso. Buena parte del portafolio arrastra estructura
        // incompleta y un candado convertiría el dato que falta en un error del que reporta.
        await ReportarAsync(id, "Desarrollo", 40);

        var desarrollo = await ActividadAsync(id, "Desarrollo");
        desarrollo.AvancePct.Should().Be(40);
        desarrollo.Bloqueada.Should().BeTrue();
        desarrollo.ArrancoBloqueada.Should().BeTrue();
    }

    // ── Choque de fechas ──────────────────────────────────────────
    [Fact]
    public async Task Marca_el_choque_cuando_la_sucesora_empieza_antes_de_que_termine_la_predecesora()
    {
        var id = await ProyectoConTresActividadesAsync();
        await DependerAsync(id, "Desarrollo", "Levantamiento");

        var entrada = (await EstructuraActualAsync(id))
            .Select(e => e with
            {
                Actividades = e.Actividades.Select(a => a.Nombre switch
                {
                    "Levantamiento" => a with { FechaInicioPlan = new DateOnly(2026, 9, 1),
                                                FechaFinPlan    = new DateOnly(2026, 9, 30) },
                    "Desarrollo"    => a with { FechaInicioPlan = new DateOnly(2026, 9, 15),
                                                FechaFinPlan    = new DateOnly(2026, 10, 31) },
                    _               => a
                }).ToList()
            }).ToList();
        await GuardarAsync(id, entrada);

        (await ActividadAsync(id, "Desarrollo")).DesfaseIncoherente.Should().BeTrue();
        (await ActividadAsync(id, "Levantamiento")).DesfaseIncoherente.Should().BeFalse();
    }

    // ── Auditoría ─────────────────────────────────────────────────
    [Fact]
    public async Task La_auditoria_nombra_el_cambio_de_dependencias()
    {
        var id = await ProyectoConTresActividadesAsync();

        await DependerAsync(id, "Desarrollo", "Levantamiento");

        var entradas = await _ctx.BitacorasProyecto
            .Where(b => b.ProyectoId == id && b.Tipo == TipoEventoProyecto.ModificacionEstructura)
            .ToListAsync();

        entradas.Should().Contain(b => b.Detalle.Contains("dependencias"));
    }

    public void Dispose() => _ctx.Dispose();
}
