using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.EventHandlers;
using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Application.Proyectos.Services;
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

public class ProyectosTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private readonly IInteresadosAutomaticosSync _sync = Substitute.For<IInteresadosAutomaticosSync>();

    public ProyectosTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
        _usuario.Nombre.Returns("Henry Cardona");

        // Sin esto, el doble devuelve una tarea con null para CalcularDerechoVigenteAsync y todo
        // lo que consulta el derecho vigente —la guarda de quitar, la consulta de la ficha—
        // revienta con NRE. Un diccionario vacío es lo que corresponde acá: en esta suite nadie
        // tiene capacidad de jefe de área ni de PMO, así que todas las filas son removibles.
        _sync.CalcularDerechoVigenteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, RolInteresado>());
    }

    private Task<int> CrearAsync(string nombre = "Proyecto de prueba") =>
        new CrearProyectoCommandHandler(_ctx, _usuario, _sync)
            .Handle(new CrearProyectoCommand(nombre, FechaInicioPlan: new DateOnly(2026, 3, 1)), CancellationToken.None);

    // ── Código correlativo ────────────────────────────────────────
    [Fact]
    public async Task Crear_NumeraElCodigoPorAnio()
    {
        var id1 = await CrearAsync("Primero");
        var id2 = await CrearAsync("Segundo");

        (await _ctx.Proyectos.FindAsync(id1))!.Codigo.Should().Be("PRY-2026-01");
        (await _ctx.Proyectos.FindAsync(id2))!.Codigo.Should().Be("PRY-2026-02");
    }

    [Fact]
    public async Task Crear_RechazaCierreAnteriorAlInicio()
    {
        var cmd = new CrearProyectoCommand("Fechas al revés",
            FechaInicioPlan: new DateOnly(2026, 6, 1),
            FechaFinPlan: new DateOnly(2026, 5, 1));

        var act = () => new CrearProyectoCommandHandler(_ctx, _usuario, _sync).Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    // ── Máquina de estados ────────────────────────────────────────
    [Fact]
    public async Task CambiarEstado_NoPermiteSaltarDePlanificadoACerrado()
    {
        var id = await CrearAsync();
        var handler = new CambiarEstadoProyectoCommandHandler(_ctx, _usuario);

        var act = () => handler.Handle(new CambiarEstadoProyectoCommand(id, EstadoProyecto.Cerrado), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        (await _ctx.Proyectos.FindAsync(id))!.Estado.Should().Be(EstadoProyecto.Planificado);
    }

    [Fact]
    public async Task CambiarEstado_AEnEjecucion_MarcaLaFechaRealDeInicio()
    {
        var id = await CrearAsync();
        var handler = new CambiarEstadoProyectoCommandHandler(_ctx, _usuario);

        await handler.Handle(new CambiarEstadoProyectoCommand(id, EstadoProyecto.EnEjecucion), CancellationToken.None);

        var p = await _ctx.Proyectos.FindAsync(id);
        p!.Estado.Should().Be(EstadoProyecto.EnEjecucion);
        p.FechaInicioReal.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Fact]
    public async Task CambiarEstado_UnProyectoCerradoYaNoAdmiteCambios()
    {
        var id = await CrearAsync();
        var handler = new CambiarEstadoProyectoCommandHandler(_ctx, _usuario);
        await handler.Handle(new CambiarEstadoProyectoCommand(id, EstadoProyecto.EnEjecucion), CancellationToken.None);
        await handler.Handle(new CambiarEstadoProyectoCommand(id, EstadoProyecto.Cerrado), CancellationToken.None);

        var act = () => handler.Handle(new CambiarEstadoProyectoCommand(id, EstadoProyecto.EnEjecucion), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    // ── Avance calculado: actividad → entregable → proyecto ───────
    [Fact]
    public async Task Avance_ElPorcentajeDelProyectoEsElPromedioDeSusActividades()
    {
        var id = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);

        // Primer entregable con dos actividades al 100 y 50; los otros dos sin desglosar y
        // pendientes, así que valen 0 por la regla 0/50/100.
        await ConActividadesAsync(id, ids[0], (100, "Levantamiento"), (50, "Diseño"));

        // (75 + 0 + 0) / 3 = 25
        (await _ctx.Proyectos.FindAsync(id))!.AvancePct.Should().Be(25);
    }

    [Fact]
    public async Task Avance_UnEntregableSinActividadesValePorSuEstado()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);

        var entrada = (await EntregablesActualesAsync(id)).Select(e =>
            e.Id == ids[0] ? e with { Estado = EstadoEntregable.Completado }
          : e.Id == ids[1] ? e with { Estado = EstadoEntregable.EnProceso }
          : e).ToList();
        await GuardarFichaAsync(id, entrada);

        // 100 + 50 + 0 = 150 / 3 = 50. Es la regla de valor fijo del PMI, la que sostiene a los
        // entregables que vienen de la carga inicial y todavía no se desglosaron.
        (await _ctx.Proyectos.FindAsync(id))!.AvancePct.Should().Be(50);
    }

    [Fact]
    public async Task Avance_LoCanceladoSaleDelPromedioEnVezDeContarComoCero()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);

        var entrada = (await EntregablesActualesAsync(id)).Select(e =>
            e.Id == ids[0] ? e with { Estado = EstadoEntregable.Completado }
          : e with { Estado = EstadoEntregable.Cancelado }).ToList();
        await GuardarFichaAsync(id, entrada);

        (await _ctx.Proyectos.FindAsync(id))!.AvancePct.Should().Be(100,
            "los cancelados no cuentan ni a favor ni en contra: queda un solo entregable vigente y está cumplido");
    }

    [Fact]
    public async Task Avance_UnaActividadCanceladaTampocoArrastraElPromedioDelEntregable()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await ConActividadesAsync(id, ids[0], (100, "Hecha"), (0, "Descartada"));

        var entrada = (await EntregablesActualesAsync(id)).Select(e => e.Id != ids[0] ? e : e with
        {
            Actividades = e.Actividades
                .Select(a => a.Nombre == "Descartada" ? a with { Cancelada = true } : a).ToList()
        }).ToList();
        await GuardarFichaAsync(id, entrada);

        var entregable = await _ctx.ProyectoEntregables.Include(e => e.Actividades)
            .SingleAsync(e => e.Id == ids[0]);
        entregable.AvanceCalculado.Should().Be(100);
    }

    [Fact]
    public async Task Avance_SinNingunEntregableVigenteElProyectoQuedaEnCero()
    {
        var id = await ConDuenioYEntregablesAsync();

        await GuardarFichaAsync(id, []);   // el editor los quitó todos

        (await _ctx.Proyectos.FindAsync(id))!.AvancePct.Should().Be(0,
            "sin estructura no hay contra qué medir, y ese cero es la señal de que falta cargarla");
    }

    // ── Actividades ───────────────────────────────────────────────
    [Fact]
    public async Task Actividad_ElPorcentajeFijaElEstadoYLasFechasReales()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await ConActividadesAsync(id, ids[0], (60, "En curso"), (100, "Terminada"));

        var actividades = await _ctx.ProyectoActividades.OrderBy(a => a.Orden).ToListAsync();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        actividades[0].Estado.Should().Be(EstadoActividad.EnProceso);
        actividades[0].FechaInicioReal.Should().Be(hoy, "pasar de cero sella el arranque");
        actividades[0].FechaFinReal.Should().BeNull();

        actividades[1].Estado.Should().Be(EstadoActividad.Completada);
        actividades[1].FechaFinReal.Should().Be(hoy);
    }

    [Fact]
    public async Task Actividad_BajarDelCienSueltaLaFechaDeCierre()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await ConActividadesAsync(id, ids[0], (100, "Se creía terminada"));

        var entrada = (await EntregablesActualesAsync(id)).Select(e => e.Id != ids[0] ? e : e with
        {
            Actividades = e.Actividades.Select(a => a with { AvancePct = 70 }).ToList()
        }).ToList();
        await GuardarFichaAsync(id, entrada);

        var a = await _ctx.ProyectoActividades.SingleAsync();
        a.Estado.Should().Be(EstadoActividad.EnProceso);
        a.FechaFinReal.Should().BeNull("si volvió a estar abierta, no terminó");
    }

    [Fact]
    public async Task Actividad_RechazaUnaVentanaQueTerminaAntesDeEmpezar()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);

        var entrada = (await EntregablesActualesAsync(id)).Select(e => e.Id != ids[0] ? e : e with
        {
            Actividades = [new ActividadInput(0, "Imposible", null,
                new DateOnly(2026, 9, 1), new DateOnly(2026, 8, 1), 0, false, null, null)]
        }).ToList();

        var act = () => GuardarFichaAsync(id, entrada);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*anterior a la de inicio*");
    }

    [Fact]
    public async Task Actividad_ElEntregableSeCierraSoloCuandoTodasLleganAlCien()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await ConActividadesAsync(id, ids[0], (100, "Una"), (40, "Otra"));

        var pendiente = await _ctx.ProyectoActividades.SingleAsync(a => a.Nombre == "Otra");
        (await _ctx.ProyectoEntregables.FindAsync(ids[0]))!.Estado
            .Should().Be(EstadoEntregable.Pendiente, "todavía falta una");

        await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio)).Handle(
            new RegistrarAvanceCommand(id, "Cerramos la última", ids[0], pendiente.Id, 100),
            CancellationToken.None);

        var entregable = await _ctx.ProyectoEntregables.FindAsync(ids[0]);
        entregable!.Estado.Should().Be(EstadoEntregable.Completado,
            "esperar a que alguien lo marque a mano es lo que dejaba el cronograma quieto");
        entregable.FechaReal.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));

        var detalle = await _ctx.BitacorasProyecto
            .Where(b => b.Tipo == TipoEventoProyecto.ModificacionEstructura)
            .OrderByDescending(b => b.Id).Select(b => b.Detalle).FirstAsync();
        detalle.Should().Contain("100 %");
    }

    // ── Bitácora de ejecución ─────────────────────────────────────
    [Fact]
    public async Task RegistrarAvance_ReportarSobreUnaActividadMueveElAvanceDelProyecto()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await ConActividadesAsync(id, ids[0], (0, "Levantamiento"));

        var actividad = await _ctx.ProyectoActividades.SingleAsync();

        await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio)).Handle(
            new RegistrarAvanceCommand(id, "Se cerró el levantamiento", ids[0], actividad.Id, 60,
                Bloqueo: "Falta firma"),
            CancellationToken.None);

        (await _ctx.ProyectoActividades.SingleAsync()).AvancePct.Should().Be(60);
        // (60 + 0 + 0) / 3 = 20
        (await _ctx.Proyectos.FindAsync(id))!.AvancePct.Should().Be(20);

        var avance = await _ctx.ProyectoAvances.SingleAsync();
        avance.PorcentajeReportado.Should().Be(60);
        avance.ActividadId.Should().Be(actividad.Id);
        avance.EntregableId.Should().Be(ids[0], "la actividad viaja siempre con su entregable");
        avance.Autor.Should().Be("Henry Cardona");
        avance.Bloqueo.Should().Be("Falta firma");
    }

    [Fact]
    public async Task RegistrarAvance_UnaNotaSinActividadNoMueveNingunNumero()
    {
        var id = await ConDuenioYEntregablesAsync();

        await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio)).Handle(
            new RegistrarAvanceCommand(id, "Se sostuvo la reunión de arranque"), CancellationToken.None);

        var avance = await _ctx.ProyectoAvances.SingleAsync();
        avance.PorcentajeReportado.Should().BeNull();
        (await _ctx.Proyectos.FindAsync(id))!.AvancePct.Should().Be(0);
    }

    [Fact]
    public async Task RegistrarAvance_UnPorcentajeSinActividadEsError()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);

        var act = () => new RegistrarAvanceCommandHandler(_ctx, Como(Duenio)).Handle(
            new RegistrarAvanceCommand(id, "Avance general", ids[0], null, 40), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*actividad*");
    }

    [Fact]
    public async Task RegistrarAvance_RechazaUnaActividadQueNoEsDeEseEntregable()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await ConActividadesAsync(id, ids[0], (0, "De otro entregable"));
        var actividad = await _ctx.ProyectoActividades.SingleAsync();

        var act = () => new RegistrarAvanceCommandHandler(_ctx, Como(Duenio)).Handle(
            new RegistrarAvanceCommand(id, "Cruzada", ids[1], actividad.Id, 10), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*no pertenece*");
    }

    // ── Cerrar el entregable desde el reporte de avance ───────────
    [Fact]
    public async Task RegistrarAvance_PuedeDarPorCumplidoElEntregableAlQueSeImputa()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);

        await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio)).Handle(
            new RegistrarAvanceCommand(id, "Entregado y validado", ids[0], CompletarEntregable: true),
            CancellationToken.None);

        var actualizado = await _ctx.ProyectoEntregables.SingleAsync(e => e.Id == ids[0]);
        actualizado.Estado.Should().Be(EstadoEntregable.Completado);
        actualizado.FechaReal.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow),
            "cerrar el entregable sin fecha real dejaría el cronograma a medias");
    }

    [Fact]
    public async Task RegistrarAvance_SinMarcarNoTocaElEntregable()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);

        await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio)).Handle(
            new RegistrarAvanceCommand(id, "Sigue en curso", ids[0]), CancellationToken.None);

        (await _ctx.ProyectoEntregables.SingleAsync(e => e.Id == ids[0])).Estado
            .Should().Be(EstadoEntregable.Pendiente);
    }

    [Fact]
    public async Task RegistrarAvance_NoSePuedeCerrarUnEntregableSinImputarleElReporte()
    {
        var id = await ConDuenioYEntregablesAsync();

        var act = () => new RegistrarAvanceCommandHandler(_ctx, Como(Duenio)).Handle(
            new RegistrarAvanceCommand(id, "Avance general", CompletarEntregable: true),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task RegistrarAvance_CerrarElEntregableQuedaEnLaAuditoria()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await LimpiarAuditoriaAsync();

        await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio)).Handle(
            new RegistrarAvanceCommand(id, "Entregado", ids[0], CompletarEntregable: true),
            CancellationToken.None);

        var detalle = await _ctx.BitacorasProyecto
            .Where(b => b.Tipo == TipoEventoProyecto.ModificacionEstructura)
            .Select(b => b.Detalle).SingleAsync();

        detalle.Should().Contain("Primero").And.Contain("cumplido");
    }

    [Fact]
    public async Task RegistrarAvance_VolverACerrarUnEntregableYaCumplidoNoDuplicaLaAuditoria()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);

        var handler = new RegistrarAvanceCommandHandler(_ctx, Como(Duenio));
        await handler.Handle(new RegistrarAvanceCommand(id, "Entregado", ids[0], CompletarEntregable: true), CancellationToken.None);
        await LimpiarAuditoriaAsync();
        await handler.Handle(new RegistrarAvanceCommand(id, "Ajuste posterior", ids[0], CompletarEntregable: true), CancellationToken.None);

        (await _ctx.BitacorasProyecto.CountAsync(b => b.Tipo == TipoEventoProyecto.ModificacionEstructura))
            .Should().Be(0, "el entregable ya estaba cumplido: no hubo cambio que registrar");
    }

    // ── Bloqueo que materializa un riesgo ─────────────────────────
    private async Task<int> RiesgoAsync(int proyectoId, string descripcion = "Falta designar contraparte")
    {
        var id = await new RegistrarRiesgoCommandHandler(_ctx, _usuario).Handle(
            new RegistrarRiesgoCommand(proyectoId, descripcion, CategoriaRiesgo.Institucional,
                NivelCualitativo.Media, NivelCualitativo.Alta, EstrategiaRiesgo.Mitigar),
            CancellationToken.None);
        return id;
    }

    [Fact]
    public async Task RegistrarAvance_VincularUnRiesgoLoPasaAMaterializado()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var rid = await RiesgoAsync(id);

        await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio)).Handle(
            new RegistrarAvanceCommand(id, "Avance parcial",
                Bloqueo: "La contraparte sigue sin designarse", RiesgoId: rid),
            CancellationToken.None);

        var riesgo = await _ctx.ProyectoRiesgos.SingleAsync(r => r.Id == rid);
        riesgo.Estado.Should().Be(EstadoRiesgo.Materializado,
            "un bloqueo que confirma un riesgo deja de ser algo que podría pasar");

        (await _ctx.ProyectoAvances.SingleAsync()).RiesgoId.Should().Be(rid);
    }

    [Fact]
    public async Task RegistrarAvance_VincularRiesgoSinBloqueoEsError()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var rid = await RiesgoAsync(id);

        var act = () => new RegistrarAvanceCommandHandler(_ctx, Como(Duenio)).Handle(
            new RegistrarAvanceCommand(id, "Avance", RiesgoId: rid), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>(
            "lo que materializa el riesgo es el bloqueo, no la referencia suelta");
    }

    [Fact]
    public async Task RegistrarAvance_NoSePuedeVincularUnRiesgoDeOtroProyecto()
    {
        var id    = await ConDuenioYEntregablesAsync();
        var otro  = await CrearAsync("Otro proyecto");
        var ajeno = await RiesgoAsync(otro);

        var act = () => new RegistrarAvanceCommandHandler(_ctx, Como(Duenio)).Handle(
            new RegistrarAvanceCommand(id, "Avance", Bloqueo: "Trabado", RiesgoId: ajeno),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task RegistrarAvance_UnRiesgoYaCerradoNoSeReabreSolo()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var rid = await RiesgoAsync(id);
        await new CambiarEstadoRiesgoCommandHandler(_ctx, _usuario).Handle(
            new CambiarEstadoRiesgoCommand(rid, EstadoRiesgo.Cerrado), CancellationToken.None);

        await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio)).Handle(
            new RegistrarAvanceCommand(id, "Avance", Bloqueo: "Volvió a trabarse", RiesgoId: rid),
            CancellationToken.None);

        (await _ctx.ProyectoRiesgos.SingleAsync(r => r.Id == rid)).Estado
            .Should().Be(EstadoRiesgo.Cerrado, "reabrir un riesgo cerrado es una decisión, no un efecto secundario");
    }

    [Fact]
    public async Task CorregirElAvance_BorrarElBloqueoSueltaElVinculoConElRiesgo()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var rid = await RiesgoAsync(id);
        var aid = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio)).Handle(
            new RegistrarAvanceCommand(id, "Avance", Bloqueo: "Trabado", RiesgoId: rid),
            CancellationToken.None);

        await new ActualizarAvanceCommandHandler(_ctx, Como(Duenio)).Handle(
            new ActualizarAvanceCommand(aid, "Avance corregido", null, null), CancellationToken.None);

        (await _ctx.ProyectoAvances.SingleAsync(a => a.Id == aid)).RiesgoId
            .Should().BeNull("sin bloqueo escrito, el riesgo quedaría materializado por un hecho que ya no está");
    }

    // ── Reapertura ────────────────────────────────────────────────
    [Fact]
    public async Task Reabrir_DevuelveAEjecucionYLimpiaLaFechaDeCierre()
    {
        var id = await CrearAsync();
        var handler = new CambiarEstadoProyectoCommandHandler(_ctx, _usuario);
        await handler.Handle(new CambiarEstadoProyectoCommand(id, EstadoProyecto.EnEjecucion), CancellationToken.None);
        await handler.Handle(new CambiarEstadoProyectoCommand(id, EstadoProyecto.Cerrado), CancellationToken.None);

        (await _ctx.Proyectos.FindAsync(id))!.FechaFinReal.Should().NotBeNull("el cierre la puso");

        await new ReabrirProyectoCommandHandler(_ctx, _usuario).Handle(
            new ReabrirProyectoCommand(id, "Quedó pendiente la capacitación"), CancellationToken.None);

        var p = await _ctx.Proyectos.FindAsync(id);
        p!.Estado.Should().Be(EstadoProyecto.EnEjecucion);
        p.FechaFinReal.Should().BeNull("si el proyecto sigue, no terminó");
    }

    [Fact]
    public async Task Reabrir_ExigeMotivo()
    {
        var id = await CrearAsync();
        var handler = new CambiarEstadoProyectoCommandHandler(_ctx, _usuario);
        await handler.Handle(new CambiarEstadoProyectoCommand(id, EstadoProyecto.Cancelado), CancellationToken.None);

        var act = () => new ReabrirProyectoCommandHandler(_ctx, _usuario)
            .Handle(new ReabrirProyectoCommand(id, "   "), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Reabrir_NoAplicaAUnProyectoQueSigueAbierto()
    {
        var id = await CrearAsync();

        var act = () => new ReabrirProyectoCommandHandler(_ctx, _usuario)
            .Handle(new ReabrirProyectoCommand(id, "Motivo cualquiera"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Reabrir_DejaElMotivoEnLaAuditoria()
    {
        var id = await CrearAsync();
        await new CambiarEstadoProyectoCommandHandler(_ctx, _usuario)
            .Handle(new CambiarEstadoProyectoCommand(id, EstadoProyecto.Cancelado), CancellationToken.None);
        await LimpiarAuditoriaAsync();

        await new ReabrirProyectoCommandHandler(_ctx, _usuario).Handle(
            new ReabrirProyectoCommand(id, "Se consiguió el financiamiento"), CancellationToken.None);

        var detalle = await _ctx.BitacorasProyecto
            .Where(b => b.Tipo == TipoEventoProyecto.CambioEstado)
            .Select(b => b.Detalle).FirstAsync();

        detalle.Should().Contain("reabierto").And.Contain("financiamiento");
    }

    [Fact]
    public async Task RegistrarAvance_ConservaElHistoricoAunqueElPorcentajeBaje()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await ConActividadesAsync(id, ids[0], (0, "Única"));
        var actividad = await _ctx.ProyectoActividades.SingleAsync();

        var handler = new RegistrarAvanceCommandHandler(_ctx, Como(Duenio));
        await handler.Handle(new RegistrarAvanceCommand(id, "Primer corte", ids[0], actividad.Id, 60), CancellationToken.None);
        // Un replanteo puede bajar el porcentaje: la actividad sigue al último reporte, pero los
        // reportes anteriores no se tocan.
        await handler.Handle(new RegistrarAvanceCommand(id, "Se replanteó el alcance", ids[0], actividad.Id, 25), CancellationToken.None);

        (await _ctx.ProyectoActividades.SingleAsync()).AvancePct.Should().Be(25);
        (await _ctx.ProyectoAvances.CountAsync()).Should().Be(2);
        (await _ctx.ProyectoAvances.OrderBy(a => a.Id).Select(a => a.PorcentajeReportado).ToListAsync())
            .Should().Equal(60, 25);
    }

    [Fact]
    public async Task RegistrarAvance_RechazaUnProyectoCerrado()
    {
        var id = await CrearAsync();
        await new CambiarEstadoProyectoCommandHandler(_ctx, _usuario)
            .Handle(new CambiarEstadoProyectoCommand(id, EstadoProyecto.Cancelado), CancellationToken.None);

        var act = () => new RegistrarAvanceCommandHandler(_ctx, _usuario)
            .Handle(new RegistrarAvanceCommand(id, "Tarde"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task RegistrarAvance_RechazaUnEntregableDeOtroProyecto()
    {
        var idA = await CrearAsync("A");
        var idB = await CrearAsync("B");

        await new ActualizarProyectoCommandHandler(_ctx, _usuario, _sync).Handle(new ActualizarProyectoCommand(
            idB, "B", null, null, null, null, null, PrioridadProyecto.Media, null, null, null,
            [new EntregableInput(0, "Entregable de B", null, null, EstadoEntregable.Pendiente, null, null, [])]),
            CancellationToken.None);

        var ajeno = await _ctx.ProyectoEntregables.SingleAsync();

        var act = () => new RegistrarAvanceCommandHandler(_ctx, _usuario)
            .Handle(new RegistrarAvanceCommand(idA, "Imputación cruzada", ajeno.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task RegistrarAvance_RechazaUnPorcentajeFueraDeRango()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await ConActividadesAsync(id, ids[0], (0, "Única"));
        var actividad = await _ctx.ProyectoActividades.SingleAsync();

        var act = () => new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(id, "Imposible", ids[0], actividad.Id, 140), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    // ── Estructura ────────────────────────────────────────────────
    [Fact]
    public async Task Actualizar_NumeraLosEntregablesNuevosYDescartaLosVacios()
    {
        var id = await CrearAsync();

        await new ActualizarProyectoCommandHandler(_ctx, _usuario, _sync).Handle(new ActualizarProyectoCommand(
            id, "Proyecto de prueba", null, null, null, null, null, PrioridadProyecto.Alta, null, null, null,
            [
                new EntregableInput(0, "Segundo", null, null, EstadoEntregable.Pendiente,  null, null, []),
                new EntregableInput(0, "   ",     null, null, EstadoEntregable.Pendiente,  null, null, []), // fila vacía del editor
                new EntregableInput(0, "Tercero", null, null, EstadoEntregable.Completado, null, null, [])
            ]), CancellationToken.None);

        var entregables = await _ctx.ProyectoEntregables.OrderBy(e => e.Orden).ToListAsync();
        entregables.Select(e => e.Nombre).Should().Equal("Segundo", "Tercero");
        entregables.Select(e => e.Orden).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Actualizar_NumeraLasActividadesNuevasYDescartaLasVacias()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);

        var entrada = (await EntregablesActualesAsync(id)).Select(e => e.Id != ids[0] ? e : e with
        {
            Actividades =
            [
                new ActividadInput(0, "Primera", null, null, null, 0, false, null, null),
                new ActividadInput(0, "  ",      null, null, null, 0, false, null, null),
                new ActividadInput(0, "Segunda", null, null, null, 0, false, null, null)
            ]
        }).ToList();
        await GuardarFichaAsync(id, entrada);

        var actividades = await _ctx.ProyectoActividades.OrderBy(a => a.Orden).ToListAsync();
        actividades.Select(a => a.Nombre).Should().Equal("Primera", "Segunda");
        actividades.Select(a => a.Orden).Should().Equal(1, 2);
    }

    // ── Reconciliación: la imputación de la bitácora no se pierde ──
    /// <summary>
    /// La que habría atrapado el bug: antes el guardado hacía LimpiarHitos() + Agregar(), las filas
    /// renacían con Id nuevo y la FK en SetNull dejaba el avance sin imputar.
    /// </summary>
    [Fact]
    public async Task Actualizar_ConservaLaImputacionDelAvanceAlGuardarLaFicha()
    {
        var id = await ConDuenioYEntregablesAsync();
        var entregableId = (await IdsPorOrdenAsync(id))[1];               // "Segundo"

        var avanceId = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(id, "Avance imputado", entregableId), CancellationToken.None);

        await GuardarFichaAsync(id, await EntregablesActualesAsync(id));

        var avance = await _ctx.ProyectoAvances.FindAsync(avanceId);
        avance!.EntregableId.Should().Be(entregableId, "el entregable conserva su identidad al guardar la ficha");
        (await _ctx.ProyectoEntregables.FindAsync(entregableId)).Should().NotBeNull();
    }

    [Fact]
    public async Task Actualizar_QuitarUnaActividadDesimputaSuAvanceEnVezDeReventar()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await ConActividadesAsync(id, ids[0], (30, "La que se va"));
        var actividad = await _ctx.ProyectoActividades.SingleAsync();

        var avanceId = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(id, "Imputado a la que se va", ids[0], actividad.Id, 50),
                    CancellationToken.None);

        var entrada = (await EntregablesActualesAsync(id))
            .Select(e => e.Id != ids[0] ? e : e with { Actividades = [] }).ToList();
        await GuardarFichaAsync(id, entrada);

        var avance = await _ctx.ProyectoAvances.FindAsync(avanceId);
        avance!.ActividadId.Should().BeNull(
            "su FK es NoAction: sin soltarla a mano el borrado fallaría con la entrada apuntándola");
        avance.EntregableId.Should().Be(ids[0], "la imputación al entregable sí sobrevive");
        avance.PorcentajeReportado.Should().Be(50, "lo que se reportó ese día no cambia");
        (await _ctx.ProyectoActividades.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// La tabla del editor no muestra la descripción, así que si el formulario no la devuelve llega
    /// null y el comando la interpreta como «vaciar». Cada guardado de la ficha borraba las
    /// descripciones, en silencio. La vista las manda en un campo oculto; esta prueba fija que lo
    /// que viaja de vuelta se conserva.
    /// </summary>
    [Fact]
    public async Task Actualizar_ConservaLaDescripcionQueElFormularioDevuelve()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);

        // Un script cargó descripciones ricas que el editor no muestra.
        foreach (var e in await _ctx.ProyectoEntregables.Where(x => x.ProyectoId == id).ToListAsync())
            e.Definir(e.Nombre, $"Descripción larga de {e.Nombre}", e.FechaPlan, e.ResponsableId, e.Responsable);
        await _ctx.SaveChangesAsync(CancellationToken.None);

        await GuardarFichaAsync(id, await EntregablesActualesAsync(id));

        var descripciones = await _ctx.ProyectoEntregables.Where(x => x.ProyectoId == id)
            .OrderBy(x => x.Orden).Select(x => x.Descripcion).ToArrayAsync();
        descripciones.Should().AllSatisfy(d => d.Should().StartWith("Descripción larga de"));
        ids.Should().HaveCount(3);
    }

    [Fact]
    public async Task Actualizar_EditaElEntregableEnSuLugarSinCambiarleElId()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);

        var entrada = (await EntregablesActualesAsync(id))
            .Select(e => e.Id == ids[0] ? e with { Nombre = "Primero renombrado" } : e).ToList();
        await GuardarFichaAsync(id, entrada);

        (await IdsPorOrdenAsync(id)).Should().Equal(ids, "no se recrea ninguno");
        (await _ctx.ProyectoEntregables.FindAsync(ids[0]))!.Nombre.Should().Be("Primero renombrado");
    }

    [Fact]
    public async Task Actualizar_QuitaSoloElEntregableAusenteYDesimputaSuAvance()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);

        var avanceId = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(id, "Imputado al que se va", ids[2]), CancellationToken.None);

        var entrada = (await EntregablesActualesAsync(id)).Where(e => e.Id != ids[2]).ToList();
        await GuardarFichaAsync(id, entrada);

        (await IdsPorOrdenAsync(id)).Should().Equal(ids[0], ids[1]);
        (await _ctx.ProyectoAvances.FindAsync(avanceId))!.EntregableId.Should().BeNull(
            "quitar un entregable sí desimputa: la entrada queda, deja de estar imputada");
    }

    [Fact]
    public async Task Actualizar_NoAlteraElOrdenExistenteYMandaLosNuevosAlFinal()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);

        // El responsable reordena; después alguien guarda la ficha.
        await new ReordenarEntregablesCommandHandler(_ctx, Como(Duenio))
            .Handle(new ReordenarEntregablesCommand(id, [ids[2], ids[0], ids[1]]), CancellationToken.None);

        var entrada = (await EntregablesActualesAsync(id)).ToList();
        entrada.Add(new EntregableInput(0, "Cuarto", null, null, EstadoEntregable.Pendiente, null, null, []));
        await GuardarFichaAsync(id, entrada);

        var final = await _ctx.ProyectoEntregables.Where(e => e.ProyectoId == id)
            .OrderBy(e => e.Orden).Select(e => e.Nombre).ToArrayAsync();
        // Guardar la ficha respeta el orden que fijó el responsable y agrega al final.
        final.Should().Equal("Tercero", "Primero", "Segundo", "Cuarto");
    }

    // ── Responsables: salen de los interesados ────────────────────
    [Fact]
    public async Task Responsable_UnEntregableSoloSeAsignaAUnInteresadoDelProyecto()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        var ajeno = await UsuarioAsync("Alguien de otra mesa");

        var entrada = (await EntregablesActualesAsync(id))
            .Select(e => e.Id == ids[0] ? e with { ResponsableId = ajeno, Responsable = "Alguien de otra mesa" } : e)
            .ToList();

        var act = () => GuardarFichaAsync(id, entrada);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*interesado*");
    }

    [Fact]
    public async Task Responsable_AlRegistrarloComoInteresadoLaAsignacionPasa()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        var uid = await UsuarioAsync("Brizzio Zelaya");
        await InteresadoAsync(id, uid, RolInteresado.Ejecutor);

        var entrada = (await EntregablesActualesAsync(id)).Select(e => e.Id != ids[0] ? e : e with
        {
            ResponsableId = uid,
            Responsable   = "Brizzio Zelaya",
            Actividades   = [new ActividadInput(0, "Con dueño", null, null, null, 0, false, uid, "Brizzio Zelaya")]
        }).ToList();
        await GuardarFichaAsync(id, entrada);

        (await _ctx.ProyectoEntregables.FindAsync(ids[0]))!.ResponsableId.Should().Be(uid);
        (await _ctx.ProyectoActividades.SingleAsync()).ResponsableId.Should().Be(uid);
    }

    /// <summary>
    /// Los entregables que vienen de la carga inicial tienen responsables que nunca se registraron
    /// como interesados. Exigirles la regla al guardar convertiría cada edición de la ficha en un
    /// error que el usuario no provocó.
    /// </summary>
    [Fact]
    public async Task Responsable_ElQueYaEstabaYNoEsInteresadoNoBloqueaElGuardado()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        var heredado = await UsuarioAsync("Responsable heredado");

        // Como lo dejó un script: asignado sin pasar por el registro de interesados.
        var entregable = await _ctx.ProyectoEntregables.FindAsync(ids[0]);
        entregable!.Definir(entregable.Nombre, entregable.Descripcion, entregable.FechaPlan,
                            heredado, "Responsable heredado");
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var entrada = (await EntregablesActualesAsync(id))
            .Select(e => e.Id == ids[0] ? e with { Nombre = "Primero, con otro nombre" } : e).ToList();
        await GuardarFichaAsync(id, entrada);

        (await _ctx.ProyectoEntregables.FindAsync(ids[0]))!.ResponsableId.Should().Be(heredado);
    }

    /// <summary>Los entregables vigentes como los mandaría el editor: con su Id y sus actividades.</summary>
    private async Task<List<EntregableInput>> EntregablesActualesAsync(int proyectoId) =>
        await _ctx.ProyectoEntregables.Where(e => e.ProyectoId == proyectoId).OrderBy(e => e.Orden)
            .Select(e => new EntregableInput(e.Id, e.Nombre, e.Descripcion, e.FechaPlan,
                                             e.Estado, e.ResponsableId, e.Responsable,
                e.Actividades.OrderBy(a => a.Orden)
                    .Select(a => new ActividadInput(a.Id, a.Nombre, a.Descripcion,
                                                    a.FechaInicioPlan, a.FechaFinPlan, a.AvancePct,
                                                    a.Estado == EstadoActividad.Cancelada,
                                                    a.ResponsableId, a.Responsable,
                                                    // El editor real también las postea: sin esto el helper simularía un
                                                    // formulario que borra las dependencias en cada guardado.
                                                    a.Predecesoras.Select(d => d.PredecesoraId).ToList())).ToList()))
            .ToListAsync();

    private Task GuardarFichaAsync(int id, IReadOnlyList<EntregableInput> entregables) =>
        new ActualizarProyectoCommandHandler(_ctx, _usuario, _sync).Handle(new ActualizarProyectoCommand(
            id, "Proyecto de prueba", null, null, null, Duenio, "Dueño del proyecto",
            PrioridadProyecto.Media, null, null, null, entregables), CancellationToken.None);

    /// <summary>Le cuelga actividades a un entregable, con su porcentaje ya reportado.</summary>
    private async Task ConActividadesAsync(int proyectoId, int entregableId, params (int Pct, string Nombre)[] actividades)
    {
        var entrada = (await EntregablesActualesAsync(proyectoId)).Select(e => e.Id != entregableId ? e : e with
        {
            Actividades = actividades
                .Select(a => new ActividadInput(0, a.Nombre, null, null, null, a.Pct, false, null, null))
                .ToList()
        }).ToList();
        await GuardarFichaAsync(proyectoId, entrada);
    }

    // ── Listado ───────────────────────────────────────────────────
    [Fact]
    public async Task Listado_MarcaComoSinReportarLoQueLlevaMasDeTreintaDiasSinAvance()
    {
        var id = await CrearAsync();
        await new CambiarEstadoProyectoCommandHandler(_ctx, _usuario)
            .Handle(new CambiarEstadoProyectoCommand(id, EstadoProyecto.EnEjecucion), CancellationToken.None);

        var enEjecucionSinReportes = (await new GetProyectosQueryHandler(_ctx)
            .Handle(new GetProyectosQuery(), CancellationToken.None)).Single();

        enEjecucionSinReportes.SinReportar.Should().BeTrue();
        enEjecucionSinReportes.SinDesglose.Should().BeTrue("no tiene ni un entregable cargado");

        await new RegistrarAvanceCommandHandler(_ctx, _usuario)
            .Handle(new RegistrarAvanceCommand(id, "Reporte de hoy"), CancellationToken.None);

        var conReporteReciente = (await new GetProyectosQueryHandler(_ctx)
            .Handle(new GetProyectosQuery(), CancellationToken.None)).Single();

        conReporteReciente.SinReportar.Should().BeFalse();
    }

    [Fact]
    public async Task Listado_CuentaLosDosNivelesDelArbol()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await ConActividadesAsync(id, ids[0], (100, "Una"), (0, "Otra"));

        var fila = (await new GetProyectosQueryHandler(_ctx)
            .Handle(new GetProyectosQuery(), CancellationToken.None)).Single();

        fila.TotalEntregables.Should().Be(3);
        fila.TotalActividades.Should().Be(2);
        fila.AvancePct.Should().Be(17, "(50 + 0 + 0) / 3, redondeado");
        fila.SinDesglose.Should().BeFalse();
    }

    // ── Reordenar y corregir bitácora (solo el propietario) ───────
    private static readonly Guid Duenio = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ajeno  = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Proyecto con responsable y tres entregables, que es el escenario de estas acciones.</summary>
    private Task<int> ConDuenioYEntregablesAsync() => ConEntregablesAsync(Duenio);

    /// <summary>Igual, pero permite dejar el proyecto sin responsable — el caso en que la guarda
    /// de propiedad no tiene contra quién comparar.</summary>
    private async Task<int> ConEntregablesAsync(Guid? responsable)
    {
        var id = await CrearAsync();
        await new ActualizarProyectoCommandHandler(_ctx, _usuario, _sync).Handle(new ActualizarProyectoCommand(
            id, "Proyecto de prueba", null, null, null,
            responsable, responsable is null ? null : "Dueño del proyecto",
            PrioridadProyecto.Media, null, null, null,
            [
                new EntregableInput(0, "Primero", null, null, EstadoEntregable.Pendiente, null, null, []),
                new EntregableInput(0, "Segundo", null, null, EstadoEntregable.Pendiente, null, null, []),
                new EntregableInput(0, "Tercero", null, null, EstadoEntregable.Pendiente, null, null, [])
            ]), CancellationToken.None);
        return id;
    }

    private ICurrentUserService Como(Guid? uid)
    {
        var u = Substitute.For<ICurrentUserService>();
        u.Nombre.Returns("Henry Cardona");
        u.UserId.Returns(uid);
        return u;
    }

    /// <summary>Un administrador que <b>no</b> es el responsable. La capacidad EsAdministrador del
    /// rol es lo que ICurrentUserService expone como EsGlobal; el resto de los dobles de esta
    /// suite lo dejan en false, que es el valor por omisión de NSubstitute.</summary>
    private ICurrentUserService ComoAdministrador()
    {
        var u = Como(Ajeno);
        u.EsGlobal.Returns(true);
        return u;
    }

    private Task<int[]> IdsPorOrdenAsync(int proyectoId) =>
        _ctx.ProyectoEntregables.Where(e => e.ProyectoId == proyectoId)
            .OrderBy(e => e.Orden).Select(e => e.Id).ToArrayAsync();

    [Fact]
    public async Task Reordenar_ElPropietarioMueveElUltimoEntregableAlPrincipio()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        int[] nuevo = [ids[2], ids[0], ids[1]];

        await new ReordenarEntregablesCommandHandler(_ctx, Como(Duenio))
            .Handle(new ReordenarEntregablesCommand(id, nuevo), CancellationToken.None);

        (await IdsPorOrdenAsync(id)).Should().Equal(nuevo);
        _ctx.ProyectoEntregables.Where(e => e.ProyectoId == id).OrderBy(e => e.Orden)
            .Select(e => e.Orden).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Reordenar_LoRechazaAQuienNoEsElResponsable()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);

        var act = () => new ReordenarEntregablesCommandHandler(_ctx, Como(Ajeno))
            .Handle(new ReordenarEntregablesCommand(id, [ids[2], ids[0], ids[1]]), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*responsable del proyecto*");
        (await IdsPorOrdenAsync(id)).Should().Equal(ids);   // no se movió nada
    }

    [Fact]
    public async Task Reordenar_UnProyectoSinResponsableNoAdmiteLaAccion()
    {
        // Sin responsable no hay contra quién comparar: a quien no es administrador se le rechaza
        // aunque haya creado el proyecto. El administrador sí pasa — ver la prueba de más abajo.
        var id  = await ConEntregablesAsync(responsable: null);
        var ids = await IdsPorOrdenAsync(id);

        var act = () => new ReordenarEntregablesCommandHandler(_ctx, Como(Duenio))
            .Handle(new ReordenarEntregablesCommand(id, [ids[2], ids[0], ids[1]]), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*no tiene responsable*");
    }

    [Fact]
    public async Task Reordenar_ExigeLaListaCompletaDeEntregables()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);

        var act = () => new ReordenarEntregablesCommandHandler(_ctx, Como(Duenio))
            .Handle(new ReordenarEntregablesCommand(id, [ids[1], ids[0]]), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*no corresponde*");
        (await IdsPorOrdenAsync(id)).Should().Equal(ids);
    }

    [Fact]
    public async Task ReordenarActividades_ElPropietarioLasMueveDentroDeSuEntregable()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await ConActividadesAsync(id, ids[0], (0, "Una"), (0, "Otra"), (0, "Tercera"));

        var actuales = await _ctx.ProyectoActividades.OrderBy(a => a.Orden).Select(a => a.Id).ToArrayAsync();

        await new ReordenarActividadesCommandHandler(_ctx, Como(Duenio)).Handle(
            new ReordenarActividadesCommand(id, ids[0], [actuales[2], actuales[0], actuales[1]]),
            CancellationToken.None);

        (await _ctx.ProyectoActividades.OrderBy(a => a.Orden).Select(a => a.Nombre).ToArrayAsync())
            .Should().Equal("Tercera", "Una", "Otra");
    }

    [Fact]
    public async Task ReordenarActividades_RechazaUnEntregableDeOtroProyecto()
    {
        var id    = await ConDuenioYEntregablesAsync();
        var otro  = await ConDuenioYEntregablesAsync();
        var ajeno = (await IdsPorOrdenAsync(otro))[0];

        var act = () => new ReordenarActividadesCommandHandler(_ctx, Como(Duenio))
            .Handle(new ReordenarActividadesCommand(id, ajeno, [1]), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*no pertenece*");
    }

    // ── Reordenar: el administrador es la excepción a la guarda de propiedad ──
    // Reordenar es cosmético y reversible, así que admite el bypass. Corregir la bitácora reescribe
    // un registro histórico y NO lo admite: es lo que separa a estas cuatro pruebas de la última.

    [Fact]
    public async Task ReordenarActividades_UnAdministradorLasMueveAunqueNoSeaElResponsable()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await ConActividadesAsync(id, ids[0], (0, "Una"), (0, "Otra"), (0, "Tercera"));

        var actuales = await _ctx.ProyectoActividades.OrderBy(a => a.Orden).Select(a => a.Id).ToArrayAsync();

        await new ReordenarActividadesCommandHandler(_ctx, ComoAdministrador()).Handle(
            new ReordenarActividadesCommand(id, ids[0], [actuales[2], actuales[0], actuales[1]]),
            CancellationToken.None);

        (await _ctx.ProyectoActividades.OrderBy(a => a.Orden).Select(a => a.Nombre).ToArrayAsync())
            .Should().Equal("Tercera", "Una", "Otra");
    }

    [Fact]
    public async Task Reordenar_UnAdministradorMueveLosEntregablesDeUnProyectoAjeno()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        int[] nuevo = [ids[2], ids[0], ids[1]];

        await new ReordenarEntregablesCommandHandler(_ctx, ComoAdministrador())
            .Handle(new ReordenarEntregablesCommand(id, nuevo), CancellationToken.None);

        (await IdsPorOrdenAsync(id)).Should().Equal(nuevo);
    }

    [Fact]
    public async Task Reordenar_UnAdministradorDesatascaUnProyectoSinResponsable()
    {
        // El caso que motivó abrir la guarda: sin responsable asignado no había quien reordenara,
        // y el proyecto quedaba con su estructura congelada hasta que alguien editara la ficha.
        var id  = await ConEntregablesAsync(responsable: null);
        var ids = await IdsPorOrdenAsync(id);
        int[] nuevo = [ids[2], ids[0], ids[1]];

        await new ReordenarEntregablesCommandHandler(_ctx, ComoAdministrador())
            .Handle(new ReordenarEntregablesCommand(id, nuevo), CancellationToken.None);

        (await IdsPorOrdenAsync(id)).Should().Equal(nuevo);
    }

    [Fact]
    public async Task ReordenarActividades_SigueRechazandoAlAjenoQueNoEsAdministrador()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await ConActividadesAsync(id, ids[0], (0, "Una"), (0, "Otra"));

        var actuales = await _ctx.ProyectoActividades.OrderBy(a => a.Orden).Select(a => a.Id).ToArrayAsync();

        var act = () => new ReordenarActividadesCommandHandler(_ctx, Como(Ajeno))
            .Handle(new ReordenarActividadesCommand(id, ids[0], [actuales[1], actuales[0]]),
                    CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*responsable del proyecto*");
        (await _ctx.ProyectoActividades.OrderBy(a => a.Orden).Select(a => a.Nombre).ToArrayAsync())
            .Should().Equal("Una", "Otra");
    }

    [Fact]
    public async Task CorregirAvance_NoLaHabilitaSerAdministrador()
    {
        // La contracara de las tres de arriba: el bypass es solo para reordenar. Si esta prueba
        // empieza a fallar, el bypass se filtró a la corrección de la bitácora.
        var id = await ConDuenioYEntregablesAsync();
        var avanceId = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(id, "Original"), CancellationToken.None);

        var act = () => new ActualizarAvanceCommandHandler(_ctx, ComoAdministrador())
            .Handle(new ActualizarAvanceCommand(avanceId, "Intento del administrador", null),
                    CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*responsable del proyecto*");
        (await _ctx.ProyectoAvances.FindAsync(avanceId))!.Descripcion.Should().Be("Original");
    }

    [Fact]
    public async Task CorregirAvance_GuardaElCambioYSellaQuienLoEdito()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await ConActividadesAsync(id, ids[0], (0, "Única"));
        var actividad = await _ctx.ProyectoActividades.SingleAsync();

        var avanceId = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(id, "Texto con herror", ids[0], actividad.Id, 30,
                Bloqueo: "algo traba"), CancellationToken.None);

        await new ActualizarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new ActualizarAvanceCommand(avanceId, "Texto corregido", null, ids[0], actividad.Id),
                    CancellationToken.None);

        var a = await _ctx.ProyectoAvances.FindAsync(avanceId);
        a!.Descripcion.Should().Be("Texto corregido");
        a.Bloqueo.Should().BeNull();
        a.EditadoPor.Should().Be("Henry Cardona");
        a.EditadoEn.Should().NotBeNull();
        a.Autor.Should().Be("Henry Cardona");           // el autor original no se toca
        a.PorcentajeReportado.Should().Be(30);          // el porcentaje tampoco
    }

    [Fact]
    public async Task CorregirAvance_UnaEntradaQueFijoUnPorcentajeNoSePuedeReimputar()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await ConActividadesAsync(id, ids[0], (0, "Única"));
        var actividad = await _ctx.ProyectoActividades.SingleAsync();

        var avanceId = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(id, "Original", ids[0], actividad.Id, 40), CancellationToken.None);

        var act = () => new ActualizarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new ActualizarAvanceCommand(avanceId, "Reimputado", null, ids[1]), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*reporte nuevo*");
    }

    [Fact]
    public async Task CorregirAvance_LoRechazaAQuienNoEsElResponsable()
    {
        var id = await ConDuenioYEntregablesAsync();
        var avanceId = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(id, "Original"), CancellationToken.None);

        var act = () => new ActualizarAvanceCommandHandler(_ctx, Como(Ajeno))
            .Handle(new ActualizarAvanceCommand(avanceId, "Intento ajeno", null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*responsable del proyecto*");
        (await _ctx.ProyectoAvances.FindAsync(avanceId))!.Descripcion.Should().Be("Original");
    }

    [Fact]
    public async Task CorregirAvance_NoAceptaUnEntregableDeOtroProyecto()
    {
        var propio = await ConDuenioYEntregablesAsync();
        var otro   = await ConDuenioYEntregablesAsync();
        var ajeno  = (await IdsPorOrdenAsync(otro))[0];

        var avanceId = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(propio, "Original"), CancellationToken.None);

        var act = () => new ActualizarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new ActualizarAvanceCommand(avanceId, "Reimputado", null, ajeno), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*no pertenece*");
    }

    // ── Auditoría del proyecto ────────────────────────────────────
    /// <summary>El armado del escenario pasa por el comando de actualizar, que ya deja auditoría.
    /// Estas pruebas miden lo que viene después, así que arrancan con el historial en cero.
    /// (ExecuteDeleteAsync no lo soporta el proveedor en memoria.)</summary>
    private async Task LimpiarAuditoriaAsync()
    {
        _ctx.BitacorasProyecto.RemoveRange(await _ctx.BitacorasProyecto.ToListAsync());
        await _ctx.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Auditoria_RegistraElCambioDeEstadoYAvisaAlResponsable()
    {
        var id = await ConDuenioYEntregablesAsync();
        await LimpiarAuditoriaAsync();
        var e  = new ProyectoEstadoCambiadoEvent(id, "PRY-2026-01", "Planificado", "EnEjecucion", "Otra persona");

        await new ProyectoEstadoCambiadoEventHandler(_ctx).Handle(e, CancellationToken.None);

        var entrada = await _ctx.BitacorasProyecto.SingleAsync();
        entrada.Tipo.Should().Be(TipoEventoProyecto.CambioEstado);
        entrada.Detalle.Should().Contain("Planificado → EnEjecucion");
        entrada.Actor.Should().Be("Otra persona");

        var aviso = await _ctx.Notificaciones.SingleAsync();
        aviso.DestinatarioId.Should().Be(Duenio);
        aviso.Titulo.Should().Contain("EnEjecucion");
    }

    [Fact]
    public async Task Auditoria_NoLeAvisaAlResponsableDeSuPropioCambio()
    {
        var id = await ConDuenioYEntregablesAsync();
        await LimpiarAuditoriaAsync();
        var e  = new ProyectoEstadoCambiadoEvent(id, "PRY-2026-01", "Planificado", "EnEjecucion", "Dueño del proyecto");

        await new ProyectoEstadoCambiadoEventHandler(_ctx).Handle(e, CancellationToken.None);

        (await _ctx.BitacorasProyecto.CountAsync()).Should().Be(1, "la auditoría se escribe igual");
        (await _ctx.Notificaciones.CountAsync()).Should().Be(0, "notificarle su propia acción es ruido");
    }

    [Fact]
    public async Task Auditoria_ResumeQueCambioEnLaFichaYEnLaEstructura()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await LimpiarAuditoriaAsync();

        var entrada = (await EntregablesActualesAsync(id))
            .Where(e => e.Id != ids[2])                                     // quita "Tercero"
            .Select(e => e.Id == ids[0] ? e with { Nombre = "Renombrado" } : e)
            .Append(new EntregableInput(0, "Nuevo", null, null, EstadoEntregable.Pendiente, null, null, []))
            .ToList();

        await new ActualizarProyectoCommandHandler(_ctx, _usuario, _sync).Handle(new ActualizarProyectoCommand(
            id, "Proyecto con otro nombre", null, null, null, Duenio, "Dueño del proyecto",
            PrioridadProyecto.Alta, null, null, null, entrada), CancellationToken.None);

        var auditoria = await _ctx.BitacorasProyecto.OrderBy(b => b.Id).ToListAsync();

        var ficha = auditoria.Single(b => b.Tipo == TipoEventoProyecto.ModificacionFicha);
        ficha.Detalle.Should().Contain("nombre").And.Contain("prioridad");
        ficha.Actor.Should().Be("Henry Cardona");

        var estructura = auditoria.Single(b => b.Tipo == TipoEventoProyecto.ModificacionEstructura);
        estructura.Detalle.Should().Contain("«Nuevo»").And.Contain("«Tercero»").And.Contain("1 entregable modificado");
    }

    [Fact]
    public async Task Auditoria_ResumeElMovimientoDeActividades()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        await LimpiarAuditoriaAsync();

        await ConActividadesAsync(id, ids[0], (0, "Una"), (0, "Otra"));

        var detalle = await _ctx.BitacorasProyecto
            .Where(b => b.Tipo == TipoEventoProyecto.ModificacionEstructura)
            .Select(b => b.Detalle).SingleAsync();

        detalle.Should().Contain("«Primero»").And.Contain("2 actividades agregadas");
    }

    [Fact]
    public async Task Auditoria_UnGuardadoSinCambiosNoEnsuciaElHistorial()
    {
        var id = await ConDuenioYEntregablesAsync();
        await LimpiarAuditoriaAsync();

        await GuardarFichaAsync(id, await EntregablesActualesAsync(id));

        (await _ctx.BitacorasProyecto.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Auditoria_DejaRastroDelReordenamientoYDeLaCorreccion()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var ids = await IdsPorOrdenAsync(id);
        var avanceId = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(id, "Original"), CancellationToken.None);
        await LimpiarAuditoriaAsync();

        await new ReordenarEntregablesCommandHandler(_ctx, Como(Duenio))
            .Handle(new ReordenarEntregablesCommand(id, [ids[2], ids[0], ids[1]]), CancellationToken.None);
        await new ActualizarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new ActualizarAvanceCommand(avanceId, "Corregido", null), CancellationToken.None);

        var tipos = await _ctx.BitacorasProyecto.OrderBy(b => b.Id).Select(b => b.Tipo).ToListAsync();
        tipos.Should().Equal(TipoEventoProyecto.ModificacionEstructura, TipoEventoProyecto.CorreccionBitacora);
    }

    // ── Riesgos ───────────────────────────────────────────────────
    private Task<int> RiesgoAsync(int proyectoId, NivelCualitativo prob, NivelCualitativo imp,
                                  EstrategiaRiesgo est = EstrategiaRiesgo.Mitigar, string? mitigacion = null,
                                  DateOnly? revision = null) =>
        new RegistrarRiesgoCommandHandler(_ctx, _usuario).Handle(new RegistrarRiesgoCommand(
            proyectoId, "El equipo institucional no designa administrador", CategoriaRiesgo.Institucional,
            prob, imp, est, mitigacion, null, null, revision), CancellationToken.None);

    [Fact]
    public async Task Riesgo_LaSeveridadEsElProductoYClasificaElSemaforo()
    {
        var id = await ConDuenioYEntregablesAsync();

        var alto  = await RiesgoAsync(id, NivelCualitativo.Alta, NivelCualitativo.Alta);   // 9
        var medio = await RiesgoAsync(id, NivelCualitativo.Alta, NivelCualitativo.Baja);   // 3
        var bajo  = await RiesgoAsync(id, NivelCualitativo.Baja, NivelCualitativo.Media);  // 2

        var r = await _ctx.ProyectoRiesgos.ToListAsync();
        r.Single(x => x.Id == alto).Severidad.Should().Be(9);
        r.Single(x => x.Id == alto).NivelSeveridad.Should().Be(NivelCualitativo.Alta);
        r.Single(x => x.Id == medio).NivelSeveridad.Should().Be(NivelCualitativo.Media);
        r.Single(x => x.Id == bajo).NivelSeveridad.Should().Be(NivelCualitativo.Baja);
    }

    [Fact]
    public async Task Riesgo_ElListadoOrdenaPorSeveridadYMandaLosCerradosAlFinal()
    {
        var id = await ConDuenioYEntregablesAsync();
        await RiesgoAsync(id, NivelCualitativo.Baja, NivelCualitativo.Baja);              // 1
        var grave = await RiesgoAsync(id, NivelCualitativo.Alta, NivelCualitativo.Alta);  // 9
        var medio = await RiesgoAsync(id, NivelCualitativo.Media, NivelCualitativo.Media);// 4

        await new CambiarEstadoRiesgoCommandHandler(_ctx, _usuario)
            .Handle(new CambiarEstadoRiesgoCommand(grave, EstadoRiesgo.Cerrado), CancellationToken.None);

        var lista = await new GetRiesgosProyectoQueryHandler(_ctx)
            .Handle(new GetRiesgosProyectoQuery(id), CancellationToken.None);

        lista.Select(x => x.Severidad).Should().Equal(4, 1, 9);   // abiertos por severidad; el cerrado al final
        lista[0].Id.Should().Be(medio);
        lista[2].Estado.Should().Be(EstadoRiesgo.Cerrado);
    }

    [Fact]
    public async Task Riesgo_CerrarloFijaLaFechaDeCierreYReabrirloLaBorra()
    {
        var id = await ConDuenioYEntregablesAsync();
        var rid = await RiesgoAsync(id, NivelCualitativo.Media, NivelCualitativo.Alta);
        var handler = new CambiarEstadoRiesgoCommandHandler(_ctx, _usuario);

        await handler.Handle(new CambiarEstadoRiesgoCommand(rid, EstadoRiesgo.Cerrado), CancellationToken.None);
        (await _ctx.ProyectoRiesgos.FindAsync(rid))!.FechaCierre.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));

        await handler.Handle(new CambiarEstadoRiesgoCommand(rid, EstadoRiesgo.Abierto), CancellationToken.None);
        (await _ctx.ProyectoRiesgos.FindAsync(rid))!.FechaCierre.Should().BeNull();
    }

    [Fact]
    public async Task Riesgo_UnCerradoNoSeModificaSinReabrirlo()
    {
        var id = await ConDuenioYEntregablesAsync();
        var rid = await RiesgoAsync(id, NivelCualitativo.Media, NivelCualitativo.Media);
        await new CambiarEstadoRiesgoCommandHandler(_ctx, _usuario)
            .Handle(new CambiarEstadoRiesgoCommand(rid, EstadoRiesgo.Cerrado), CancellationToken.None);

        var act = () => new ActualizarRiesgoCommandHandler(_ctx, _usuario).Handle(new ActualizarRiesgoCommand(
            rid, "Otro texto", CategoriaRiesgo.Tecnico, NivelCualitativo.Alta, NivelCualitativo.Alta,
            EstrategiaRiesgo.Mitigar, null, null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*cerrado*");
    }

    [Fact]
    public async Task Riesgo_MarcaLaRevisionVencidaYLaFaltaDePlan()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var rid = await RiesgoAsync(id, NivelCualitativo.Alta, NivelCualitativo.Alta);

        // La fecha de revisión no admite pasado al crear, así que se atrasa después.
        var r = await _ctx.ProyectoRiesgos.FindAsync(rid);
        typeof(RiesgoProyecto).GetProperty("FechaRevision")!
            .SetValue(r, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3));
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var dto = (await new GetRiesgosProyectoQueryHandler(_ctx)
            .Handle(new GetRiesgosProyectoQuery(id), CancellationToken.None)).Single();

        dto.RevisionVencida.Should().BeTrue();
        dto.SinPlan.Should().BeTrue("mitigar sin mitigación escrita no es un plan");
    }

    [Fact]
    public async Task Riesgo_UnRiesgoAceptadoNoCuentaComoSinPlan()
    {
        var id = await ConDuenioYEntregablesAsync();
        await RiesgoAsync(id, NivelCualitativo.Baja, NivelCualitativo.Baja, EstrategiaRiesgo.Aceptar);

        var dto = (await new GetRiesgosProyectoQueryHandler(_ctx)
            .Handle(new GetRiesgosProyectoQuery(id), CancellationToken.None)).Single();

        dto.SinPlan.Should().BeFalse();
    }

    [Fact]
    public async Task Riesgo_RechazaUnaFechaDeRevisionEnElPasado()
    {
        var id = await ConDuenioYEntregablesAsync();

        var act = () => RiesgoAsync(id, NivelCualitativo.Media, NivelCualitativo.Media,
            revision: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*revisión*");
    }

    [Fact]
    public async Task Riesgo_QuedaRegistradoEnLaAuditoria()
    {
        var id = await ConDuenioYEntregablesAsync();
        await LimpiarAuditoriaAsync();

        var rid = await RiesgoAsync(id, NivelCualitativo.Baja, NivelCualitativo.Baja);
        await new ActualizarRiesgoCommandHandler(_ctx, _usuario).Handle(new ActualizarRiesgoCommand(
            rid, "El equipo institucional no designa administrador", CategoriaRiesgo.Institucional,
            NivelCualitativo.Alta, NivelCualitativo.Alta, EstrategiaRiesgo.Mitigar,
            "Escalar a la jefatura", null, null, null), CancellationToken.None);

        var detalles = await _ctx.BitacorasProyecto
            .Where(b => b.Tipo == TipoEventoProyecto.Riesgo).OrderBy(b => b.Id).Select(b => b.Detalle).ToListAsync();

        detalles.Should().HaveCount(2);
        detalles[0].Should().Contain("Riesgo registrado");
        detalles[1].Should().Contain("1 → 9", "solo se audita el cambio si la severidad se movió");
    }

    // ── Interesados ───────────────────────────────────────────────
    // Desde el 2026-08-24 un interesado es siempre un usuario del portal, porque el registro le
    // da acceso al proyecto. Por eso las pruebas ahora tienen que sembrar usuarios de verdad.
    private async Task<Guid> UsuarioAsync(string nombre, string? correo = null, bool activo = true)
    {
        var u = Usuario.Crear(nombre, correo ?? $"{Guid.NewGuid():N}@diger.gob.hn", "hash");
        if (!activo) u.Desactivar();
        _ctx.Usuarios.Add(u);
        await _ctx.SaveChangesAsync();
        return u.Id;
    }

    private Task<int> InteresadoAsync(int proyectoId, Guid usuarioId, RolInteresado rol,
                                      NivelCualitativo influencia = NivelCualitativo.Media) =>
        new AgregarInteresadoCommandHandler(_ctx, _usuario).Handle(new AgregarInteresadoCommand(
            proyectoId, usuarioId, rol, influencia, "DIGER"), CancellationToken.None);

    [Fact]
    public async Task Interesado_MarcaComoClaveAlDeAltaInfluenciaQueDecide()
    {
        var id = await ConDuenioYEntregablesAsync();
        await InteresadoAsync(id, await UsuarioAsync("Patrocinador fuerte"), RolInteresado.Patrocinador, NivelCualitativo.Alta);
        await InteresadoAsync(id, await UsuarioAsync("Beneficiario amplio"), RolInteresado.Beneficiario, NivelCualitativo.Alta);
        await InteresadoAsync(id, await UsuarioAsync("Contraparte media"),   RolInteresado.ContraparteTecnica, NivelCualitativo.Media);

        var lista = await new GetInteresadosProyectoQueryHandler(_ctx, _sync)
            .Handle(new GetInteresadosProyectoQuery(id), CancellationToken.None);

        lista.Single(i => i.Nombre == "Patrocinador fuerte").EsClave.Should().BeTrue();
        lista.Single(i => i.Nombre == "Beneficiario amplio").EsClave
            .Should().BeFalse("un beneficiario no decide, por influyente que sea");
        lista.Single(i => i.Nombre == "Contraparte media").EsClave.Should().BeFalse();
    }

    [Fact]
    public async Task Interesado_SeOrdenaPorInfluenciaDescendente()
    {
        var id = await ConDuenioYEntregablesAsync();
        await InteresadoAsync(id, await UsuarioAsync("Baja"),  RolInteresado.Beneficiario, NivelCualitativo.Baja);
        await InteresadoAsync(id, await UsuarioAsync("Alta"),  RolInteresado.Patrocinador, NivelCualitativo.Alta);
        await InteresadoAsync(id, await UsuarioAsync("Media"), RolInteresado.Ejecutor,     NivelCualitativo.Media);

        var lista = await new GetInteresadosProyectoQueryHandler(_ctx, _sync)
            .Handle(new GetInteresadosProyectoQuery(id), CancellationToken.None);

        lista.Select(i => i.Nombre).Should().Equal("Alta", "Media", "Baja");
    }

    [Fact]
    public async Task Interesado_TomaNombreYCorreoDelUsuarioNoDeQuienLoRegistra()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var uid = await UsuarioAsync("Homero Funez", "  HFunez@INPREMA.GOB.HN  ".Trim());

        await new AgregarInteresadoCommandHandler(_ctx, _usuario).Handle(new AgregarInteresadoCommand(
            id, uid, RolInteresado.ContraparteTecnica, NivelCualitativo.Alta,
            "INPREMA", "Gerente de TI"), CancellationToken.None);

        var i = await _ctx.ProyectoInteresados.SingleAsync();
        i.Nombre.Should().Be("Homero Funez");
        i.Correo.Should().Be("hfunez@inprema.gob.hn");
        i.UsuarioId.Should().Be(uid);
        i.Institucion.Should().Be("INPREMA", "la institución por la que participa sí se declara a mano");
    }

    [Fact]
    public async Task Interesado_RechazaAlQueNoEsUsuarioDelPortal()
    {
        var id = await ConDuenioYEntregablesAsync();

        var act = () => InteresadoAsync(id, Guid.NewGuid(), RolInteresado.Ejecutor);

        await act.Should().ThrowAsync<NotFoundException>(
            "un interesado sin cuenta no podría ver el proyecto, que es para lo que se lo registra");
    }

    [Fact]
    public async Task Interesado_RechazaAlUsuarioInactivo()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var uid = await UsuarioAsync("Baja del portal", activo: false);

        var act = () => InteresadoAsync(id, uid, RolInteresado.Ejecutor);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Interesado_NoSePuedeRepetirLaMismaPersonaEnUnProyecto()
    {
        var id  = await ConDuenioYEntregablesAsync();
        var uid = await UsuarioAsync("Repetido");
        await InteresadoAsync(id, uid, RolInteresado.Ejecutor);

        var act = () => InteresadoAsync(id, uid, RolInteresado.Patrocinador);

        await act.Should().ThrowAsync<DomainException>(
            "cada fila otorga acceso: repetirla obligaría a revocar dos veces para sacar a alguien");
        (await _ctx.ProyectoInteresados.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Interesado_AgregarYQuitarQuedanEnLaAuditoria()
    {
        var id = await ConDuenioYEntregablesAsync();
        var uid = await UsuarioAsync("Christhian Quintanilla");
        await LimpiarAuditoriaAsync();

        var iid = await InteresadoAsync(id, uid, RolInteresado.ContraparteTecnica);
        await new QuitarInteresadoCommandHandler(_ctx, _usuario, _sync)
            .Handle(new QuitarInteresadoCommand(iid), CancellationToken.None);

        var detalles = await _ctx.BitacorasProyecto
            .Where(b => b.Tipo == TipoEventoProyecto.Interesado).OrderBy(b => b.Id).Select(b => b.Detalle).ToListAsync();

        detalles.Should().HaveCount(2);
        detalles[0].Should().Contain("agregado").And.Contain("Christhian Quintanilla");
        detalles[1].Should().Contain("quitado");
        (await _ctx.ProyectoInteresados.CountAsync()).Should().Be(0);
    }

    public void Dispose() => _ctx.Dispose();
}
