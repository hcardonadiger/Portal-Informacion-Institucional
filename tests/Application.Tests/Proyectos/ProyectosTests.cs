using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.EventHandlers;
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

public class ProyectosTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();

    public ProyectosTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
        _usuario.Nombre.Returns("Henry Cardona");
    }

    private Task<int> CrearAsync(string nombre = "Proyecto de prueba") =>
        new CrearProyectoCommandHandler(_ctx, _usuario)
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

        var act = () => new CrearProyectoCommandHandler(_ctx, _usuario).Handle(cmd, CancellationToken.None);

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

    // ── Bitácora de ejecución ─────────────────────────────────────
    [Fact]
    public async Task RegistrarAvance_MueveElPorcentajeYDejaLaEntradaEnLaBitacora()
    {
        var id = await CrearAsync();

        await new RegistrarAvanceCommandHandler(_ctx, _usuario).Handle(
            new RegistrarAvanceCommand(id, "Se cerró el levantamiento", 40, Bloqueo: "Falta firma"),
            CancellationToken.None);

        (await _ctx.Proyectos.FindAsync(id))!.AvancePct.Should().Be(40);

        var avance = await _ctx.ProyectoAvances.SingleAsync();
        avance.PorcentajeReportado.Should().Be(40);
        avance.Autor.Should().Be("Henry Cardona");
        avance.Bloqueo.Should().Be("Falta firma");
    }

    // ── Cerrar el hito desde el reporte de avance ─────────────────
    [Fact]
    public async Task RegistrarAvance_PuedeDarPorCumplidoElHitoAlQueSeImputa()
    {
        var id   = await ConDuenioYHitosAsync();
        var hito = await _ctx.ProyectoHitos.OrderBy(h => h.Orden).FirstAsync(h => h.ProyectoId == id);

        await new RegistrarAvanceCommandHandler(_ctx, _usuario).Handle(
            new RegistrarAvanceCommand(id, "Entregado y validado", 30, HitoId: hito.Id, CompletarHito: true),
            CancellationToken.None);

        var actualizado = await _ctx.ProyectoHitos.SingleAsync(h => h.Id == hito.Id);
        actualizado.Estado.Should().Be(EstadoHito.Completado);
        actualizado.FechaReal.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow),
            "cerrar el hito sin fecha real dejaría el cronograma a medias");
    }

    [Fact]
    public async Task RegistrarAvance_SinMarcarNoTocaElHito()
    {
        var id   = await ConDuenioYHitosAsync();
        var hito = await _ctx.ProyectoHitos.OrderBy(h => h.Orden).FirstAsync(h => h.ProyectoId == id);

        await new RegistrarAvanceCommandHandler(_ctx, _usuario).Handle(
            new RegistrarAvanceCommand(id, "Sigue en curso", 30, HitoId: hito.Id),
            CancellationToken.None);

        (await _ctx.ProyectoHitos.SingleAsync(h => h.Id == hito.Id)).Estado
            .Should().Be(EstadoHito.Pendiente);
    }

    [Fact]
    public async Task RegistrarAvance_NoSePuedeCerrarUnHitoSinImputarleElReporte()
    {
        var id = await ConDuenioYHitosAsync();

        var act = () => new RegistrarAvanceCommandHandler(_ctx, _usuario).Handle(
            new RegistrarAvanceCommand(id, "Avance general", 30, CompletarHito: true),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task RegistrarAvance_CerrarElHitoQuedaEnLaAuditoria()
    {
        var id   = await ConDuenioYHitosAsync();
        var hito = await _ctx.ProyectoHitos.OrderBy(h => h.Orden).FirstAsync(h => h.ProyectoId == id);
        await LimpiarAuditoriaAsync();

        await new RegistrarAvanceCommandHandler(_ctx, _usuario).Handle(
            new RegistrarAvanceCommand(id, "Entregado", 30, HitoId: hito.Id, CompletarHito: true),
            CancellationToken.None);

        var detalle = await _ctx.BitacorasProyecto
            .Where(b => b.Tipo == TipoEventoProyecto.ModificacionHitos)
            .Select(b => b.Detalle).SingleAsync();

        detalle.Should().Contain("Primero").And.Contain("cumplido");
    }

    [Fact]
    public async Task RegistrarAvance_VolverACerrarUnHitoYaCumplidoNoDuplicaLaAuditoria()
    {
        var id   = await ConDuenioYHitosAsync();
        var hito = await _ctx.ProyectoHitos.OrderBy(h => h.Orden).FirstAsync(h => h.ProyectoId == id);

        var handler = new RegistrarAvanceCommandHandler(_ctx, _usuario);
        await handler.Handle(new RegistrarAvanceCommand(id, "Entregado", 30, HitoId: hito.Id, CompletarHito: true), CancellationToken.None);
        await LimpiarAuditoriaAsync();
        await handler.Handle(new RegistrarAvanceCommand(id, "Ajuste posterior", 35, HitoId: hito.Id, CompletarHito: true), CancellationToken.None);

        (await _ctx.BitacorasProyecto.CountAsync(b => b.Tipo == TipoEventoProyecto.ModificacionHitos))
            .Should().Be(0, "el hito ya estaba cumplido: no hubo cambio que registrar");
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
        var id  = await ConDuenioYHitosAsync();
        var rid = await RiesgoAsync(id);

        await new RegistrarAvanceCommandHandler(_ctx, _usuario).Handle(
            new RegistrarAvanceCommand(id, "Avance parcial", 30,
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
        var id  = await ConDuenioYHitosAsync();
        var rid = await RiesgoAsync(id);

        var act = () => new RegistrarAvanceCommandHandler(_ctx, _usuario).Handle(
            new RegistrarAvanceCommand(id, "Avance", 30, RiesgoId: rid), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>(
            "lo que materializa el riesgo es el bloqueo, no la referencia suelta");
    }

    [Fact]
    public async Task RegistrarAvance_NoSePuedeVincularUnRiesgoDeOtroProyecto()
    {
        var id    = await ConDuenioYHitosAsync();
        var otro  = await CrearAsync("Otro proyecto");
        var ajeno = await RiesgoAsync(otro);

        var act = () => new RegistrarAvanceCommandHandler(_ctx, _usuario).Handle(
            new RegistrarAvanceCommand(id, "Avance", 30, Bloqueo: "Trabado", RiesgoId: ajeno),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task RegistrarAvance_UnRiesgoYaCerradoNoSeReabreSolo()
    {
        var id  = await ConDuenioYHitosAsync();
        var rid = await RiesgoAsync(id);
        await new CambiarEstadoRiesgoCommandHandler(_ctx, _usuario).Handle(
            new CambiarEstadoRiesgoCommand(rid, EstadoRiesgo.Cerrado), CancellationToken.None);

        await new RegistrarAvanceCommandHandler(_ctx, _usuario).Handle(
            new RegistrarAvanceCommand(id, "Avance", 30, Bloqueo: "Volvió a trabarse", RiesgoId: rid),
            CancellationToken.None);

        (await _ctx.ProyectoRiesgos.SingleAsync(r => r.Id == rid)).Estado
            .Should().Be(EstadoRiesgo.Cerrado, "reabrir un riesgo cerrado es una decisión, no un efecto secundario");
    }

    [Fact]
    public async Task CorregirElAvance_BorrarElBloqueoSueltaElVinculoConElRiesgo()
    {
        var id  = await ConDuenioYHitosAsync();
        var rid = await RiesgoAsync(id);
        var aid = await new RegistrarAvanceCommandHandler(_ctx, _usuario).Handle(
            new RegistrarAvanceCommand(id, "Avance", 30, Bloqueo: "Trabado", RiesgoId: rid),
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
        var id = await CrearAsync();
        var handler = new RegistrarAvanceCommandHandler(_ctx, _usuario);

        await handler.Handle(new RegistrarAvanceCommand(id, "Primer corte", 60), CancellationToken.None);
        // Un replanteo puede bajar el porcentaje: el snapshot sigue al último reporte,
        // pero los reportes anteriores no se tocan.
        await handler.Handle(new RegistrarAvanceCommand(id, "Se replanteó el alcance", 25), CancellationToken.None);

        (await _ctx.Proyectos.FindAsync(id))!.AvancePct.Should().Be(25);
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
            .Handle(new RegistrarAvanceCommand(id, "Tarde", 10), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task RegistrarAvance_RechazaUnHitoDeOtroProyecto()
    {
        var idA = await CrearAsync("A");
        var idB = await CrearAsync("B");

        await new ActualizarProyectoCommandHandler(_ctx, _usuario).Handle(new ActualizarProyectoCommand(
            idB, "B", null, null, null, null, null, PrioridadProyecto.Media, null, null,
            [new HitoInput(0, "Hito de B", null, null, null, EstadoHito.Pendiente, null, null)]),
            CancellationToken.None);

        var hitoDeB = await _ctx.ProyectoHitos.SingleAsync();

        var act = () => new RegistrarAvanceCommandHandler(_ctx, _usuario)
            .Handle(new RegistrarAvanceCommand(idA, "Imputación cruzada", 10, HitoId: hitoDeB.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task RegistrarAvance_RechazaUnPorcentajeFueraDeRango()
    {
        var id = await CrearAsync();

        var act = () => new RegistrarAvanceCommandHandler(_ctx, _usuario)
            .Handle(new RegistrarAvanceCommand(id, "Imposible", 140), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    // ── Hitos ─────────────────────────────────────────────────────
    [Fact]
    public async Task Actualizar_NumeraLosHitosNuevosYDescartaLosVacios()
    {
        var id = await CrearAsync();

        await new ActualizarProyectoCommandHandler(_ctx, _usuario).Handle(new ActualizarProyectoCommand(
            id, "Proyecto de prueba", null, null, null, null, null, PrioridadProyecto.Alta, null, null,
            [
                new HitoInput(0, "Segundo",  null, null, null, EstadoHito.Pendiente,  null, null),
                new HitoInput(0, "   ",      null, null, null, EstadoHito.Pendiente,  null, null), // fila vacía del editor
                new HitoInput(0, "Tercero",  null, null, null, EstadoHito.Completado, null, null)
            ]), CancellationToken.None);

        var hitos = await _ctx.ProyectoHitos.OrderBy(h => h.Orden).ToListAsync();
        hitos.Select(h => h.Nombre).Should().Equal("Segundo", "Tercero");
        hitos.Select(h => h.Orden).Should().Equal(1, 2);
    }

    // ── Reconciliación de hitos: la imputación de la bitácora no se pierde ──
    /// <summary>
    /// La que habría atrapado el bug: antes el guardado hacía LimpiarHitos() + Agregar(), los
    /// hitos renacían con Id nuevo y la FK en SetNull dejaba el avance sin imputar.
    /// </summary>
    [Fact]
    public async Task Actualizar_ConservaLaImputacionDelAvanceAlGuardarLaFicha()
    {
        var id = await ConDuenioYHitosAsync();
        var hitoId = (await IdsPorOrdenAsync(id))[1];               // "Segundo"

        var avanceId = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(id, "Avance imputado", 20, hitoId), CancellationToken.None);

        await GuardarFichaAsync(id, await HitosActualesAsync(id));

        var avance = await _ctx.ProyectoAvances.FindAsync(avanceId);
        avance!.HitoId.Should().Be(hitoId, "el hito conserva su identidad al guardar la ficha");
        (await _ctx.ProyectoHitos.FindAsync(hitoId)).Should().NotBeNull();
    }

    /// <summary>
    /// La tabla de hitos del editor no muestra la descripción, así que si el formulario no la
    /// devuelve llega null y el comando la interpreta como «vaciar». Cada guardado de la ficha
    /// borraba las descripciones de todos los hitos, en silencio. La vista ahora las manda en un
    /// campo oculto; esta prueba fija que lo que viaja de vuelta se conserva.
    /// </summary>
    [Fact]
    public async Task Actualizar_ConservaLaDescripcionQueElFormularioDevuelve()
    {
        var id  = await ConDuenioYHitosAsync();
        var ids = await IdsPorOrdenAsync(id);

        // Un script cargó descripciones ricas que el editor no muestra.
        foreach (var h in await _ctx.ProyectoHitos.Where(x => x.ProyectoId == id).ToListAsync())
            h.Descripcion = $"Descripción larga de {h.Nombre}";
        await _ctx.SaveChangesAsync(CancellationToken.None);

        await GuardarFichaAsync(id, await HitosActualesAsync(id));

        var descripciones = await _ctx.ProyectoHitos.Where(x => x.ProyectoId == id)
            .OrderBy(x => x.Orden).Select(x => x.Descripcion).ToArrayAsync();
        descripciones.Should().AllSatisfy(d => d.Should().StartWith("Descripción larga de"));
        ids.Should().HaveCount(3);
    }

    [Fact]
    public async Task Actualizar_EditaElHitoEnSuLugarSinCambiarleElId()
    {
        var id  = await ConDuenioYHitosAsync();
        var ids = await IdsPorOrdenAsync(id);

        var entrada = (await HitosActualesAsync(id))
            .Select(h => h.Id == ids[0] ? h with { Nombre = "Primero renombrado" } : h).ToList();
        await GuardarFichaAsync(id, entrada);

        (await IdsPorOrdenAsync(id)).Should().Equal(ids, "no se recrea ninguno");
        (await _ctx.ProyectoHitos.FindAsync(ids[0]))!.Nombre.Should().Be("Primero renombrado");
    }

    [Fact]
    public async Task Actualizar_QuitaSoloElHitoAusenteYDesimputaSuAvance()
    {
        var id  = await ConDuenioYHitosAsync();
        var ids = await IdsPorOrdenAsync(id);

        var avanceId = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(id, "Imputado al que se va", 20, ids[2]), CancellationToken.None);

        var entrada = (await HitosActualesAsync(id)).Where(h => h.Id != ids[2]).ToList();
        await GuardarFichaAsync(id, entrada);

        (await IdsPorOrdenAsync(id)).Should().Equal(ids[0], ids[1]);
        (await _ctx.ProyectoAvances.FindAsync(avanceId))!.HitoId.Should().BeNull(
            "quitar un hito sí desimputa: la entrada queda, deja de estar imputada");
    }

    [Fact]
    public async Task Actualizar_NoAlteraElOrdenExistenteYMandaLosNuevosAlFinal()
    {
        var id  = await ConDuenioYHitosAsync();
        var ids = await IdsPorOrdenAsync(id);

        // El responsable reordena; después alguien guarda la ficha.
        await new ReordenarHitosCommandHandler(_ctx, Como(Duenio))
            .Handle(new ReordenarHitosCommand(id, [ids[2], ids[0], ids[1]]), CancellationToken.None);

        var entrada = (await HitosActualesAsync(id)).ToList();
        entrada.Add(new HitoInput(0, "Cuarto", null, null, null, EstadoHito.Pendiente, null, null));
        await GuardarFichaAsync(id, entrada);

        var final = await _ctx.ProyectoHitos.Where(h => h.ProyectoId == id)
            .OrderBy(h => h.Orden).Select(h => h.Nombre).ToArrayAsync();
        // Guardar la ficha respeta el orden que fijó el responsable y agrega al final.
        final.Should().Equal("Tercero", "Primero", "Segundo", "Cuarto");
    }

    /// <summary>Los hitos vigentes como los mandaría el editor: con su Id.</summary>
    private async Task<List<HitoInput>> HitosActualesAsync(int proyectoId) =>
        await _ctx.ProyectoHitos.Where(h => h.ProyectoId == proyectoId).OrderBy(h => h.Orden)
            .Select(h => new HitoInput(h.Id, h.Nombre, h.Descripcion, h.FechaPlan, h.FechaReal,
                                       h.Estado, h.ResponsableId, h.Responsable))
            .ToListAsync();

    private Task GuardarFichaAsync(int id, IReadOnlyList<HitoInput> hitos) =>
        new ActualizarProyectoCommandHandler(_ctx, _usuario).Handle(new ActualizarProyectoCommand(
            id, "Proyecto de prueba", null, null, null, Duenio, "Dueño del proyecto",
            PrioridadProyecto.Media, null, null, hitos), CancellationToken.None);

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

        await new RegistrarAvanceCommandHandler(_ctx, _usuario)
            .Handle(new RegistrarAvanceCommand(id, "Reporte de hoy", 20), CancellationToken.None);

        var conReporteReciente = (await new GetProyectosQueryHandler(_ctx)
            .Handle(new GetProyectosQuery(), CancellationToken.None)).Single();

        conReporteReciente.SinReportar.Should().BeFalse();
        conReporteReciente.AvancePct.Should().Be(20);
    }

    // ── Reordenar hitos y corregir bitácora (solo el propietario) ──
    private static readonly Guid Duenio = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Ajeno  = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Proyecto con responsable y tres hitos, que es el escenario de estas acciones.</summary>
    private Task<int> ConDuenioYHitosAsync() => ConHitosAsync(Duenio);

    /// <summary>Igual, pero permite dejar el proyecto sin responsable — el caso en que la guarda
    /// de propiedad no tiene contra quién comparar.</summary>
    private async Task<int> ConHitosAsync(Guid? responsable)
    {
        var id = await CrearAsync();
        await new ActualizarProyectoCommandHandler(_ctx, _usuario).Handle(new ActualizarProyectoCommand(
            id, "Proyecto de prueba", null, null, null,
            responsable, responsable is null ? null : "Dueño del proyecto",
            PrioridadProyecto.Media, null, null,
            [
                new HitoInput(0, "Primero",  null, null, null, EstadoHito.Pendiente, null, null),
                new HitoInput(0, "Segundo",  null, null, null, EstadoHito.Pendiente, null, null),
                new HitoInput(0, "Tercero",  null, null, null, EstadoHito.Pendiente, null, null)
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

    private Task<int[]> IdsPorOrdenAsync(int proyectoId) =>
        _ctx.ProyectoHitos.Where(h => h.ProyectoId == proyectoId)
            .OrderBy(h => h.Orden).Select(h => h.Id).ToArrayAsync();

    [Fact]
    public async Task Reordenar_ElPropietarioMueveElUltimoHitoAlPrincipio()
    {
        var id  = await ConDuenioYHitosAsync();
        var ids = await IdsPorOrdenAsync(id);
        int[] nuevo = [ids[2], ids[0], ids[1]];

        await new ReordenarHitosCommandHandler(_ctx, Como(Duenio))
            .Handle(new ReordenarHitosCommand(id, nuevo), CancellationToken.None);

        (await IdsPorOrdenAsync(id)).Should().Equal(nuevo);
        _ctx.ProyectoHitos.Where(h => h.ProyectoId == id).OrderBy(h => h.Orden)
            .Select(h => h.Orden).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Reordenar_LoRechazaAQuienNoEsElResponsable()
    {
        var id  = await ConDuenioYHitosAsync();
        var ids = await IdsPorOrdenAsync(id);

        var act = () => new ReordenarHitosCommandHandler(_ctx, Como(Ajeno))
            .Handle(new ReordenarHitosCommand(id, [ids[2], ids[0], ids[1]]), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*responsable del proyecto*");
        (await IdsPorOrdenAsync(id)).Should().Equal(ids);   // no se movió nada
    }

    [Fact]
    public async Task Reordenar_UnProyectoSinResponsableNoAdmiteLaAccion()
    {
        // Sin bypass de administrador: si nadie es dueño, nadie reordena.
        var id  = await ConHitosAsync(responsable: null);
        var ids = await IdsPorOrdenAsync(id);

        var act = () => new ReordenarHitosCommandHandler(_ctx, Como(Duenio))
            .Handle(new ReordenarHitosCommand(id, [ids[2], ids[0], ids[1]]), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*no tiene responsable*");
    }

    [Fact]
    public async Task Reordenar_ExigeLaListaCompletaDeHitos()
    {
        var id  = await ConDuenioYHitosAsync();
        var ids = await IdsPorOrdenAsync(id);

        var act = () => new ReordenarHitosCommandHandler(_ctx, Como(Duenio))
            .Handle(new ReordenarHitosCommand(id, [ids[1], ids[0]]), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*no corresponde*");
        (await IdsPorOrdenAsync(id)).Should().Equal(ids);
    }

    [Fact]
    public async Task CorregirAvance_GuardaElCambioYSellaQuienLoEdito()
    {
        var id = await ConDuenioYHitosAsync();
        var avanceId = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(id, "Texto con herror", 30, Bloqueo: "algo traba"), CancellationToken.None);

        await new ActualizarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new ActualizarAvanceCommand(avanceId, "Texto corregido", null), CancellationToken.None);

        var a = await _ctx.ProyectoAvances.FindAsync(avanceId);
        a!.Descripcion.Should().Be("Texto corregido");
        a.Bloqueo.Should().BeNull();
        a.EditadoPor.Should().Be("Henry Cardona");
        a.EditadoEn.Should().NotBeNull();
        a.Autor.Should().Be("Henry Cardona");           // el autor original no se toca
        a.PorcentajeReportado.Should().Be(30);          // el porcentaje tampoco
    }

    [Fact]
    public async Task CorregirAvance_LoRechazaAQuienNoEsElResponsable()
    {
        var id = await ConDuenioYHitosAsync();
        var avanceId = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(id, "Original", 30), CancellationToken.None);

        var act = () => new ActualizarAvanceCommandHandler(_ctx, Como(Ajeno))
            .Handle(new ActualizarAvanceCommand(avanceId, "Intento ajeno", null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*responsable del proyecto*");
        (await _ctx.ProyectoAvances.FindAsync(avanceId))!.Descripcion.Should().Be("Original");
    }

    [Fact]
    public async Task CorregirAvance_NoAceptaUnHitoDeOtroProyecto()
    {
        var propio = await ConDuenioYHitosAsync();
        var otro   = await ConDuenioYHitosAsync();
        var hitoAjeno = (await IdsPorOrdenAsync(otro))[0];

        var avanceId = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(propio, "Original", 30), CancellationToken.None);

        var act = () => new ActualizarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new ActualizarAvanceCommand(avanceId, "Reimputado", null, hitoAjeno), CancellationToken.None);

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
        var id = await ConDuenioYHitosAsync();
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
        var id = await ConDuenioYHitosAsync();
        await LimpiarAuditoriaAsync();
        var e  = new ProyectoEstadoCambiadoEvent(id, "PRY-2026-01", "Planificado", "EnEjecucion", "Dueño del proyecto");

        await new ProyectoEstadoCambiadoEventHandler(_ctx).Handle(e, CancellationToken.None);

        (await _ctx.BitacorasProyecto.CountAsync()).Should().Be(1, "la auditoría se escribe igual");
        (await _ctx.Notificaciones.CountAsync()).Should().Be(0, "notificarle su propia acción es ruido");
    }

    [Fact]
    public async Task Auditoria_ResumeQueCambioEnLaFichaYEnLosHitos()
    {
        var id  = await ConDuenioYHitosAsync();
        var ids = await IdsPorOrdenAsync(id);
        await LimpiarAuditoriaAsync();

        var entrada = (await HitosActualesAsync(id))
            .Where(h => h.Id != ids[2])                                     // quita "Tercero"
            .Select(h => h.Id == ids[0] ? h with { Nombre = "Renombrado" } : h)
            .Append(new HitoInput(0, "Nuevo", null, null, null, EstadoHito.Pendiente, null, null))
            .ToList();

        await new ActualizarProyectoCommandHandler(_ctx, _usuario).Handle(new ActualizarProyectoCommand(
            id, "Proyecto con otro nombre", null, null, null, Duenio, "Dueño del proyecto",
            PrioridadProyecto.Alta, null, null, entrada), CancellationToken.None);

        var auditoria = await _ctx.BitacorasProyecto.OrderBy(b => b.Id).ToListAsync();

        var ficha = auditoria.Single(b => b.Tipo == TipoEventoProyecto.ModificacionFicha);
        ficha.Detalle.Should().Contain("nombre").And.Contain("prioridad");
        ficha.Actor.Should().Be("Henry Cardona");

        var hitos = auditoria.Single(b => b.Tipo == TipoEventoProyecto.ModificacionHitos);
        hitos.Detalle.Should().Contain("«Nuevo»").And.Contain("«Tercero»").And.Contain("1 hito modificado");
    }

    [Fact]
    public async Task Auditoria_UnGuardadoSinCambiosNoEnsuciaElHistorial()
    {
        var id = await ConDuenioYHitosAsync();
        await LimpiarAuditoriaAsync();

        await GuardarFichaAsync(id, await HitosActualesAsync(id));

        (await _ctx.BitacorasProyecto.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Auditoria_DejaRastroDelReordenamientoYDeLaCorreccion()
    {
        var id  = await ConDuenioYHitosAsync();
        var ids = await IdsPorOrdenAsync(id);
        var avanceId = await new RegistrarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new RegistrarAvanceCommand(id, "Original", 20), CancellationToken.None);
        await LimpiarAuditoriaAsync();

        await new ReordenarHitosCommandHandler(_ctx, Como(Duenio))
            .Handle(new ReordenarHitosCommand(id, [ids[2], ids[0], ids[1]]), CancellationToken.None);
        await new ActualizarAvanceCommandHandler(_ctx, Como(Duenio))
            .Handle(new ActualizarAvanceCommand(avanceId, "Corregido", null), CancellationToken.None);

        var tipos = await _ctx.BitacorasProyecto.OrderBy(b => b.Id).Select(b => b.Tipo).ToListAsync();
        tipos.Should().Equal(TipoEventoProyecto.ModificacionHitos, TipoEventoProyecto.CorreccionBitacora);
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
        var id = await ConDuenioYHitosAsync();

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
        var id = await ConDuenioYHitosAsync();
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
        var id = await ConDuenioYHitosAsync();
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
        var id = await ConDuenioYHitosAsync();
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
        var id  = await ConDuenioYHitosAsync();
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
        var id = await ConDuenioYHitosAsync();
        await RiesgoAsync(id, NivelCualitativo.Baja, NivelCualitativo.Baja, EstrategiaRiesgo.Aceptar);

        var dto = (await new GetRiesgosProyectoQueryHandler(_ctx)
            .Handle(new GetRiesgosProyectoQuery(id), CancellationToken.None)).Single();

        dto.SinPlan.Should().BeFalse();
    }

    [Fact]
    public async Task Riesgo_RechazaUnaFechaDeRevisionEnElPasado()
    {
        var id = await ConDuenioYHitosAsync();

        var act = () => RiesgoAsync(id, NivelCualitativo.Media, NivelCualitativo.Media,
            revision: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*revisión*");
    }

    [Fact]
    public async Task Riesgo_QuedaRegistradoEnLaAuditoria()
    {
        var id = await ConDuenioYHitosAsync();
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
        var id = await ConDuenioYHitosAsync();
        await InteresadoAsync(id, await UsuarioAsync("Patrocinador fuerte"), RolInteresado.Patrocinador, NivelCualitativo.Alta);
        await InteresadoAsync(id, await UsuarioAsync("Beneficiario amplio"), RolInteresado.Beneficiario, NivelCualitativo.Alta);
        await InteresadoAsync(id, await UsuarioAsync("Contraparte media"),   RolInteresado.ContraparteTecnica, NivelCualitativo.Media);

        var lista = await new GetInteresadosProyectoQueryHandler(_ctx)
            .Handle(new GetInteresadosProyectoQuery(id), CancellationToken.None);

        lista.Single(i => i.Nombre == "Patrocinador fuerte").EsClave.Should().BeTrue();
        lista.Single(i => i.Nombre == "Beneficiario amplio").EsClave
            .Should().BeFalse("un beneficiario no decide, por influyente que sea");
        lista.Single(i => i.Nombre == "Contraparte media").EsClave.Should().BeFalse();
    }

    [Fact]
    public async Task Interesado_SeOrdenaPorInfluenciaDescendente()
    {
        var id = await ConDuenioYHitosAsync();
        await InteresadoAsync(id, await UsuarioAsync("Baja"),  RolInteresado.Beneficiario, NivelCualitativo.Baja);
        await InteresadoAsync(id, await UsuarioAsync("Alta"),  RolInteresado.Patrocinador, NivelCualitativo.Alta);
        await InteresadoAsync(id, await UsuarioAsync("Media"), RolInteresado.Ejecutor,     NivelCualitativo.Media);

        var lista = await new GetInteresadosProyectoQueryHandler(_ctx)
            .Handle(new GetInteresadosProyectoQuery(id), CancellationToken.None);

        lista.Select(i => i.Nombre).Should().Equal("Alta", "Media", "Baja");
    }

    [Fact]
    public async Task Interesado_TomaNombreYCorreoDelUsuarioNoDeQuienLoRegistra()
    {
        var id  = await ConDuenioYHitosAsync();
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
        var id = await ConDuenioYHitosAsync();

        var act = () => InteresadoAsync(id, Guid.NewGuid(), RolInteresado.Ejecutor);

        await act.Should().ThrowAsync<NotFoundException>(
            "un interesado sin cuenta no podría ver el proyecto, que es para lo que se lo registra");
    }

    [Fact]
    public async Task Interesado_RechazaAlUsuarioInactivo()
    {
        var id  = await ConDuenioYHitosAsync();
        var uid = await UsuarioAsync("Baja del portal", activo: false);

        var act = () => InteresadoAsync(id, uid, RolInteresado.Ejecutor);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Interesado_NoSePuedeRepetirLaMismaPersonaEnUnProyecto()
    {
        var id  = await ConDuenioYHitosAsync();
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
        var id = await ConDuenioYHitosAsync();
        var uid = await UsuarioAsync("Christhian Quintanilla");
        await LimpiarAuditoriaAsync();

        var iid = await InteresadoAsync(id, uid, RolInteresado.ContraparteTecnica);
        await new QuitarInteresadoCommandHandler(_ctx, _usuario)
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
