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

    // ── Divergencia entre lo declarado y el cronograma ────────────
    private async Task ConHitosAsync(int proyectoId, int total, int completados)
    {
        for (var i = 0; i < total; i++)
            _ctx.ProyectoHitos.Add(new HitoProyecto
            {
                ProyectoId = proyectoId,
                Orden      = i + 1,
                Nombre     = $"Hito {i + 1}",
                Estado     = i < completados ? EstadoHito.Completado : EstadoHito.Pendiente
            });
        await _ctx.SaveChangesAsync();
    }

    private async Task ReportarAsync(int proyectoId, int porcentaje)
    {
        var p = await _ctx.Proyectos.SingleAsync(x => x.Id == proyectoId);
        p.AplicarAvance(porcentaje);
        await _ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task DeclararMuchoMasDeLoQueSeCierraMarcaDivergencia()
    {
        var id = await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, Hoy.AddDays(60));
        await ConHitosAsync(id, total: 12, completados: 5);   // 42 % físico
        await ReportarAsync(id, 90);                          // 90 % declarado

        var d = await TableroAsync();
        var fila = d.Semaforo.Single();

        fila.AvanceFisico.Should().Be(42);
        fila.Brecha.Should().Be(48);
        fila.Divergente.Should().BeTrue();
        d.ConDivergencia.Should().Be(1);
    }

    [Fact]
    public async Task CerrarHitosSinReportarlosTambienMarcaDivergencia()
    {
        // La otra dirección: el trabajo avanza y el reporte se queda atrás. Es subregistro, no
        // sobre-reporte, y se corrige de otra manera — por eso la brecha guarda el signo.
        var id = await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, Hoy.AddDays(60));
        await ConHitosAsync(id, total: 5, completados: 4);    // 80 % físico
        await ReportarAsync(id, 40);                          // 40 % declarado

        var fila = (await TableroAsync()).Semaforo.Single();

        fila.Brecha.Should().Be(-40);
        fila.Divergente.Should().BeTrue();
    }

    [Fact]
    public async Task UnDesfaseNormalNoSeMarca()
    {
        var id = await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, Hoy.AddDays(60));
        await ConHitosAsync(id, total: 4, completados: 2);    // 50 % físico
        await ReportarAsync(id, 60);                          // 10 pts de diferencia

        var fila = (await TableroAsync()).Semaforo.Single();

        fila.Divergente.Should().BeFalse("un hito grande a medias explica diferencias chicas");
        (await TableroAsync()).ConDivergencia.Should().Be(0);
    }

    [Fact]
    public async Task SinCronogramaNoHayContraQueCompararYNoSeMarca()
    {
        var id = await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, Hoy.AddDays(60));
        await ReportarAsync(id, 90);   // 90 % declarado y ningún hito cargado

        var fila = (await TableroAsync()).Semaforo.Single();

        fila.Divergente.Should().BeFalse("sin hitos el 0 % físico no significa que no se avanzó");
        fila.AvanceFisico.Should().Be(0);
    }

    [Fact]
    public async Task UnProyectoCerradoDivergenteNoEntraEnElContador()
    {
        var id = await SembrarAsync("PRY-2026-01", EstadoProyecto.EnEjecucion, Hoy.AddDays(60));
        await ConHitosAsync(id, total: 10, completados: 1);
        await ReportarAsync(id, 95);

        var p = await _ctx.Proyectos.SingleAsync(x => x.Id == id);
        p.CambiarEstado(EstadoProyecto.Cerrado, "cierre");
        await _ctx.SaveChangesAsync();

        var d = await TableroAsync();

        d.ConDivergencia.Should().Be(0, "en un proyecto cerrado la diferencia ya no acciona nada");
    }

    [Fact]
    public async Task SinProyectosNoDivideEntreCero()
    {
        var d = await TableroAsync();

        d.Total.Should().Be(0);
        d.AvancePromedio.Should().Be(0, "el promedio de una lista vacía no puede reventar la pantalla");
        d.SinLineaBase.Should().Be(0);
        d.ConDivergencia.Should().Be(0);
    }

    public void Dispose() => _ctx.Dispose();
}
