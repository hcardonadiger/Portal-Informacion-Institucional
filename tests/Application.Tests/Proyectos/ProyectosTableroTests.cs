using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Dashboards.Queries;
using Diger.TramitesEstado.Application.Tests.Expedientes;   // FakeCurrentUser (alcance global)
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

/// <summary>
/// El tablero gerencial del portafolio. Es la pantalla que mira la gerencia y hasta el 2026-08-25
/// no tenía ninguna prueba, pese a ser la que más lógica de interpretación concentra.
///
/// <para>El foco está en que <b>no dé falso verde</b>: un portafolio sin fechas comprometidas
/// produce «0 atrasados», y leído solo eso parece que todo va bien.</para>
/// </summary>
public class ProyectosTableroTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public ProyectosTableroTests()
    {
        _ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
    }

    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<int> SembrarAsync(string codigo, EstadoProyecto estado, DateOnly? finPlan)
    {
        var p = Proyecto.Crear(codigo, $"Proyecto {codigo}");
        p.InstitucionId = "DIGER";
        p.FechaFinPlan  = finPlan;
        if (estado != EstadoProyecto.Planificado) p.CambiarEstado(estado, "siembra");

        _ctx.Proyectos.Add(p);
        await _ctx.SaveChangesAsync();
        return p.Id;
    }

    private Task<Dashboards.Common.ProyectosDashboardDto> TableroAsync() =>
        new GetProyectosDashboardQueryHandler(_ctx).Handle(new GetProyectosDashboardQuery(), CancellationToken.None);

    [Fact]
    public async Task SinFechaDeCierre_NoCuentaComoAtrasadoPeroSiComoSinLineaBase()
    {
        await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, null);
        await SembrarAsync("PRY-2026-02", EstadoProyecto.EnEjecucion, null);

        var d = await TableroAsync();

        d.Atrasados.Should().Be(0);
        d.SinLineaBase.Should().Be(2,
            "sin este contador el tablero muestra «0 atrasados» y se lee como que el portafolio está al día");
    }

    [Fact]
    public async Task ConFechaVencida_CuentaComoAtrasadoYNoComoSinLineaBase()
    {
        await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, Hoy.AddDays(-10));

        var d = await TableroAsync();

        d.Atrasados.Should().Be(1);
        d.SinLineaBase.Should().Be(0);
    }

    [Fact]
    public async Task ConFechaFutura_NoCuentaEnNingunoDeLosDos()
    {
        await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, Hoy.AddDays(30));

        var d = await TableroAsync();

        d.Atrasados.Should().Be(0);
        d.SinLineaBase.Should().Be(0);
    }

    [Fact]
    public async Task UnProyectoCerradoSinFechaNoEsSinLineaBase()
    {
        // Cerrar ya no admite compromiso de fecha: pedirle línea base a lo que terminó sería ruido.
        var id = await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, null);
        var p  = await _ctx.Proyectos.SingleAsync(x => x.Id == id);
        p.CambiarEstado(EstadoProyecto.Cerrado, "cierre");
        await _ctx.SaveChangesAsync();

        var d = await TableroAsync();

        d.SinLineaBase.Should().Be(0);
        d.Cerrados.Should().Be(1);
    }

    [Fact]
    public async Task ElSemaforoMarcaCadaFilaSegunSuSituacion()
    {
        await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, null);              // sin comprometer
        await SembrarAsync("PRY-2026-02", EstadoProyecto.EnEjecucion, Hoy.AddDays(-5));   // vencido
        await SembrarAsync("PRY-2026-03", EstadoProyecto.EnEjecucion, Hoy.AddDays(60));   // a tiempo

        var d = await TableroAsync();
        var porCodigo = d.Semaforo.ToDictionary(s => s.Codigo);

        porCodigo["PRY-2026-01"].SinLineaBase.Should().BeTrue();
        porCodigo["PRY-2026-01"].Atrasado.Should().BeFalse();

        porCodigo["PRY-2026-02"].Atrasado.Should().BeTrue();
        porCodigo["PRY-2026-02"].SinLineaBase.Should().BeFalse();

        porCodigo["PRY-2026-03"].Atrasado.Should().BeFalse();
        porCodigo["PRY-2026-03"].SinLineaBase.Should().BeFalse("tiene fecha y todavía no llega");
    }

    [Fact]
    public async Task ElSemaforoOrdenaPrimeroLoQueExigeAtencion()
    {
        await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, Hoy.AddDays(60));   // tranquilo
        await SembrarAsync("PRY-2026-02", EstadoProyecto.EnEjecucion, Hoy.AddDays(-5));   // atrasado

        var d = await TableroAsync();

        d.Semaforo.First().Codigo.Should().Be("PRY-2026-02", "el atraso manda al tope de la lista");
    }

    // ── Divergencia entre el trabajo reportado y lo que se cierra ─
    /// <summary>
    /// Arma la estructura del proyecto y recalcula su avance, que es como queda en producción:
    /// el porcentaje del proyecto ya no se declara, sale del árbol.
    /// </summary>
    /// <param name="pctActividad">Porcentaje de la única actividad de cada entregable. Null deja los
    /// entregables sin desglosar, y entonces valen por su estado (regla 0/50/100).</param>
    private async Task ConEstructuraAsync(int proyectoId, int total, int completados, int? pctActividad = null)
    {
        var p = await _ctx.Proyectos
            .Include(x => x.Entregables).ThenInclude(e => e.Actividades)
            .SingleAsync(x => x.Id == proyectoId);

        for (var i = 0; i < total; i++)
        {
            var e = EntregableProyecto.Crear($"Entregable {i + 1}", i + 1);
            if (i < completados) e.CambiarEstado(EstadoEntregable.Completado);

            if (pctActividad is { } pct)
            {
                var a = ActividadProyecto.Crear("Actividad", 1);
                a.Reportar(pct, Hoy);
                e.Agregar(a);
            }

            p.Agregar(e);
        }

        p.RecalcularAvance(p.Entregables);
        await _ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task ReportarMuchoMasDeLoQueSeCierraMarcaDivergencia()
    {
        var id = await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, Hoy.AddDays(60));
        // 12 entregables, 5 cerrados (42 % físico) y todas las actividades al 90 %.
        await ConEstructuraAsync(id, total: 12, completados: 5, pctActividad: 90);

        var d = await TableroAsync();
        var fila = d.Semaforo.Single();

        fila.AvancePct.Should().Be(90);
        fila.AvanceFisico.Should().Be(42);
        fila.Brecha.Should().Be(48);
        fila.Divergente.Should().BeTrue();
        d.ConDivergencia.Should().Be(1);
    }

    [Fact]
    public async Task CerrarEntregablesSinReportarTrabajoTambienMarcaDivergencia()
    {
        // La otra dirección: los entregables se cierran y las actividades se quedan atrás. Es
        // subregistro, no sobre-reporte, y se corrige de otra manera — por eso la brecha guarda
        // el signo.
        var id = await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, Hoy.AddDays(60));
        await ConEstructuraAsync(id, total: 5, completados: 4, pctActividad: 40);   // 80 % físico

        var fila = (await TableroAsync()).Semaforo.Single();

        fila.Brecha.Should().Be(-40);
        fila.Divergente.Should().BeTrue();
    }

    [Fact]
    public async Task UnDesfaseNormalNoSeMarca()
    {
        var id = await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, Hoy.AddDays(60));
        await ConEstructuraAsync(id, total: 4, completados: 2, pctActividad: 60);   // 50 % físico

        var fila = (await TableroAsync()).Semaforo.Single();

        fila.Divergente.Should().BeFalse("un entregable grande a medias explica diferencias chicas");
        (await TableroAsync()).ConDivergencia.Should().Be(0);
    }

    [Fact]
    public async Task SinEstructuraNoHayContraQueCompararYNoSeMarca()
    {
        var id = await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, Hoy.AddDays(60));

        var fila = (await TableroAsync()).Semaforo.Single();

        fila.AvancePct.Should().Be(0, "sin entregables no hay de dónde calcular");
        fila.AvanceFisico.Should().Be(0);
        fila.Divergente.Should().BeFalse();
        fila.SinDesglose.Should().BeTrue();
        _ = id;
    }

    /// <summary>
    /// Un entregable sin actividades vale por su estado, no cero. Es lo que sostiene al portafolio
    /// que viene de la carga inicial: si valiera cero, estrenar el desglose habría puesto en cero
    /// el avance de los 24 proyectos cargados.
    /// </summary>
    [Fact]
    public async Task SinDesglosarElAvanceSaleDelEstadoDeLosEntregables()
    {
        var id = await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, Hoy.AddDays(60));
        await ConEstructuraAsync(id, total: 4, completados: 2);   // sin actividades

        var fila = (await TableroAsync()).Semaforo.Single();

        fila.AvancePct.Should().Be(50, "dos de cuatro cumplidos, los otros dos en cero");
        fila.SinDesglose.Should().BeTrue();
    }

    [Fact]
    public async Task UnProyectoCerradoDivergenteNoEntraEnElContador()
    {
        var id = await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, Hoy.AddDays(60));
        await ConEstructuraAsync(id, total: 10, completados: 1, pctActividad: 95);

        var p = await _ctx.Proyectos.SingleAsync(x => x.Id == id);
        p.CambiarEstado(EstadoProyecto.Cerrado, "cierre");
        await _ctx.SaveChangesAsync();

        var d = await TableroAsync();

        d.ConDivergencia.Should().Be(0, "en un proyecto cerrado la diferencia ya no acciona nada");
    }

    // ── Actividades vencidas ──────────────────────────────────────
    /// <summary>
    /// La señal que el modelo anterior no podía dar: una actividad vencida dentro de un entregable
    /// cuya fecha comprometida todavía no llega.
    /// </summary>
    [Fact]
    public async Task UnaActividadVencidaSeVeAunqueSuEntregableNoHayaVencido()
    {
        var id = await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, Hoy.AddDays(60));

        var p = await _ctx.Proyectos.Include(x => x.Entregables).SingleAsync(x => x.Id == id);
        var e = EntregableProyecto.Crear("Entregable a tres meses", 1);
        e.Definir("Entregable a tres meses", null, Hoy.AddDays(90), null, null);

        var atrasada = ActividadProyecto.Crear("Debía cerrarse la semana pasada", 1);
        atrasada.Definir("Debía cerrarse la semana pasada", null, Hoy.AddDays(-20), Hoy.AddDays(-5), null, null);
        atrasada.Reportar(30, Hoy);
        e.Agregar(atrasada);

        var proxima = ActividadProyecto.Crear("Vence en dos semanas", 2);
        proxima.Definir("Vence en dos semanas", null, Hoy, Hoy.AddDays(14), null, null);
        e.Agregar(proxima);

        p.Agregar(e);
        p.RecalcularAvance(p.Entregables);
        await _ctx.SaveChangesAsync();

        var d = await TableroAsync();

        d.ActividadesVencidas.Should().Be(1);
        d.ActividadesProximas.Should().Be(1);
        d.EntregablesVencidos.Should().Be(0, "el entregable todavía no vence: esa es la gracia");
        d.Actividades.Should().HaveCount(2);
        d.Actividades.First().Actividad.Should().Be("Debía cerrarse la semana pasada");
        d.Semaforo.Single().ActividadesVencidas.Should().Be(1);
    }

    [Fact]
    public async Task SinProyectosNoDivideEntreCero()
    {
        var d = await TableroAsync();

        d.Total.Should().Be(0);
        d.AvancePromedio.Should().Be(0, "el promedio de una lista vacía no puede reventar la pantalla");
        d.SinLineaBase.Should().Be(0);
        d.ConDivergencia.Should().Be(0);
        d.ActividadesVencidas.Should().Be(0);
    }

    public void Dispose() => _ctx.Dispose();
}
