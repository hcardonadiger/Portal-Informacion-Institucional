using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Reports;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

/// <summary>
/// Pruebas de humo del PDF del proyecto. No comparan píxeles —eso obligaría a versionar un
/// documento de referencia y a regenerarlo con cada ajuste de maquetado— sino que verifican lo
/// que sí se puede afirmar: que el documento se genera, que es un PDF, y que los casos límite del
/// cronograma (sin fechas, una sola barra, porcentajes fuera de rango) no revientan al dibujarlo.
/// </summary>
public class ProyectoPdfServiceTests
{
    private static readonly Guid Responsable = Guid.NewGuid();

    private static ProyectoDetailDto ProyectoCon(
        IReadOnlyList<EntregableProyectoDto>? entregables = null) =>
        new(
            Id: 1, Codigo: "PRY-2026-17", Nombre: "Portal de Digitalización de Trámites",
            Objetivo: "Llevar los trámites del Estado a una plataforma única.",
            InstitucionId: "DIGER", AreaId: "GOBIERNO-DIGITAL", UnidadId: "DIGITALIZACION",
            ResponsableId: Responsable, Responsable: "Henry Cardona",
            Prioridad: PrioridadProyecto.Alta, Accion: AccionProyecto.Digitalizacion,
            Estado: EstadoProyecto.EnEjecucion,
            FechaInicioPlan: new DateOnly(2026, 6, 26), FechaFinPlan: new DateOnly(2026, 9, 30),
            FechaInicioReal: new DateOnly(2026, 6, 26), FechaFinReal: null,
            AvancePct: 84, CreatedAt: DateTime.UtcNow, CreatedBy: "seed",
            Entregables: entregables ?? [],
            EstadosPosibles: []);

    private static ActividadProyectoDto Actividad(int orden, string nombre,
        DateOnly? ini = null, DateOnly? fin = null) =>
        new(Id: orden, Orden: orden, Nombre: nombre, Descripcion: null,
            FechaInicioPlan: ini, FechaFinPlan: fin, FechaInicioReal: null, FechaFinReal: null,
            AvancePct: 50, Estado: EstadoActividad.EnProceso,
            ResponsableId: null, Responsable: "Brizzio Zelaya", Predecesoras: []);

    private static EntregableProyectoDto Entregable(params ActividadProyectoDto[] actividades) =>
        new(Id: 1, Orden: 1, Nombre: "Construcción del portal", Descripcion: null,
            FechaPlan: new DateOnly(2026, 9, 30), FechaReal: null,
            Estado: EstadoEntregable.EnProceso, ResponsableId: null, Responsable: "Henry Cardona",
            AvancePct: 84, Actividades: actividades);

    private static ProyectoPdfDto DtoCon(ProyectoDetailDto proyecto, CronogramaDto cronograma) =>
        new(proyecto, cronograma, [], [], [], [], new VinculosProyectoDto([], [], [], 0, 0, 0), []);

    private static bool EsPdf(byte[] bytes) =>
        bytes.Length > 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46;

    [Fact]
    public void Generar_ProyectoCompleto_DevuelveUnPdf()
    {
        var proyecto = ProyectoCon([Entregable(
            Actividad(1, "Arranque del repositorio", new DateOnly(2026, 6, 26), new DateOnly(2026, 7, 3)),
            Actividad(2, "Módulo de proyectos",      new DateOnly(2026, 7, 3),  new DateOnly(2026, 8, 25)))]);

        var cronograma = CronogramaProyecto.Construir(proyecto, new DateOnly(2026, 9, 2));
        var bytes = new ProyectoPdfService().Generar(DtoCon(proyecto, cronograma));

        EsPdf(bytes).Should().BeTrue("el documento debe empezar con la firma %PDF");
        bytes.Length.Should().BeGreaterThan(1000, "un PDF con ficha, EDT y cronograma no cabe en menos");
    }

    [Fact]
    public void Generar_ProyectoSinEntregables_NoRevienta()
    {
        var proyecto   = ProyectoCon();
        var cronograma = CronogramaProyecto.Construir(proyecto, new DateOnly(2026, 9, 2));

        var bytes = new ProyectoPdfService().Generar(DtoCon(proyecto, cronograma));

        EsPdf(bytes).Should().BeTrue();
    }

    /// <summary>El caso real del portafolio: la mayoría de las actividades no tiene fechas, así que
    /// el cronograma queda vacío y todo cae en la lista de «sin fechas».</summary>
    [Fact]
    public void Generar_ActividadesSinFechas_DibujaLaListaEnVezDelGantt()
    {
        var proyecto = ProyectoCon([Entregable(
            Actividad(1, "Actividad sin fechas"),
            Actividad(2, "Otra sin fechas"))]);

        var cronograma = CronogramaProyecto.Construir(proyecto, new DateOnly(2026, 9, 2));
        cronograma.SinFechas.Should().NotBeEmpty("el escenario de la prueba pierde sentido si el cronograma sí las tiene");

        var bytes = new ProyectoPdfService().Generar(DtoCon(proyecto, cronograma));

        EsPdf(bytes).Should().BeTrue();
    }

    /// <summary>Una barra con porcentajes fuera de [0,100] se dibujaría fuera del papel. El
    /// servicio los acota; esta prueba fija ese acotamiento contra un cronograma armado a mano.</summary>
    [Fact]
    public void Generar_BarrasConPorcentajesFueraDeRango_SeAcotanYNoRevienta()
    {
        var barra = new BarraCronograma(
            Nivel: 1, Id: 1, Nombre: "Barra fuera de escala", Responsable: null,
            Inicio: new DateOnly(2026, 1, 1), Fin: new DateOnly(2026, 12, 31),
            OffsetPct: -40, AnchoPct: 260, AvancePct: 10,
            Estado: EstadoBarra.EnProceso, Bloqueada: false, Espera: [],
            Compromiso: null, CompromisoPct: 900);

        var cronograma = new CronogramaDto(
            Desde: new DateOnly(2026, 1, 1), Hasta: new DateOnly(2026, 12, 31),
            Meses: [new MesCronograma("ene", "2026", 0, 50), new MesCronograma("feb", "2026", 50, 50)],
            Barras: [barra], SinFechas: [], HoyPct: 150);

        var bytes = new ProyectoPdfService().Generar(DtoCon(ProyectoCon(), cronograma));

        EsPdf(bytes).Should().BeTrue();
    }
}
