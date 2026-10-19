using Diger.TramitesEstado.Application.Dashboards.Common;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Tableros;

/// <summary>
/// Las reglas del tablero ejecutivo (especificación §2.3 y §3): el avance esperado por
/// interpolación y el semáforo de salud, paso por paso y con la precedencia entre pasos.
/// </summary>
public class ProyectoEstadoReglasSaludTests
{
    private static readonly DateOnly Hoy   = new(2026, 10, 19);
    private static readonly DateTime Ahora = new(2026, 10, 19, 15, 0, 0, DateTimeKind.Utc);

    // ── Avance esperado ────────────────────────────────────────────────────

    [Fact]
    public void Esperado_antes_del_inicio_es_cero() =>
        ProyectoEstadoReglas.AvanceEsperado([(Hoy.AddDays(1), Hoy.AddDays(10))], Hoy).Should().Be(0);

    [Fact]
    public void Esperado_pasado_el_fin_es_cien() =>
        ProyectoEstadoReglas.AvanceEsperado([(Hoy.AddDays(-10), Hoy.AddDays(-1))], Hoy).Should().Be(100);

    [Fact]
    public void Esperado_a_mitad_de_ventana_es_cincuenta() =>
        ProyectoEstadoReglas.AvanceEsperado([(Hoy.AddDays(-5), Hoy.AddDays(5))], Hoy).Should().Be(50);

    [Fact]
    public void Esperado_con_ventana_de_un_dia_que_ya_empezo_es_cien() =>
        // Inicio y fin el mismo día pasado: no hay tramo que interpolar.
        ProyectoEstadoReglas.AvanceEsperado([(Hoy.AddDays(-1), Hoy.AddDays(-1))], Hoy).Should().Be(100);

    [Fact]
    public void Esperado_sin_fechas_es_null_porque_no_hay_linea_base() =>
        ProyectoEstadoReglas.AvanceEsperado([], Hoy).Should().BeNull();

    [Fact]
    public void Esperado_promedia_las_ventanas() =>
        // 0 + 100 + 50 = 150 / 3 = 50.
        ProyectoEstadoReglas.AvanceEsperado(
            [(Hoy.AddDays(3), Hoy.AddDays(9)), (Hoy.AddDays(-9), Hoy.AddDays(-3)), (Hoy.AddDays(-2), Hoy.AddDays(2))],
            Hoy).Should().Be(50);

    // ── Salud: un caso por paso ────────────────────────────────────────────

    private static SaludEvaluada Salud(
        EstadoProyecto estado = EstadoProyecto.EnEjecucion,
        DateOnly? fin = null, DateTime? ultimo = null, bool bloqueo = false,
        int entregables = 3, int? desviacion = 0, DateOnly? inicio = null, DateOnly? finReal = null,
        bool conFinPorDefecto = true) =>
        ProyectoEstadoReglas.Salud(estado,
            fin ?? (conFinPorDefecto ? Hoy.AddDays(60) : null),
            ultimo ?? Ahora.AddDays(-2), bloqueo, entregables, desviacion, Hoy, Ahora, inicio, finReal);

    [Fact]
    public void Paso1_cerrado_es_neutro_finalizado_con_su_fecha()
    {
        var s = Salud(EstadoProyecto.Cerrado, finReal: new DateOnly(2026, 9, 2), bloqueo: true);
        s.Should().Be(new SaludEvaluada(SaludProyecto.Neutro, "Finalizado", "cerrado el 2 de septiembre"));
    }

    [Fact]
    public void Paso2_cancelado_es_neutro() =>
        Salud(EstadoProyecto.Cancelado).Should().Be(new SaludEvaluada(SaludProyecto.Neutro, "Cancelado", ""));

    [Fact]
    public void Paso3_sin_entregables_es_sin_dato() =>
        Salud(entregables: 0, bloqueo: true).Should()
            .Be(new SaludEvaluada(SaludProyecto.Neutro, "Sin dato", "sin entregables cargados"));

    [Fact]
    public void Paso4_cierre_planificado_vencido_es_rojo_vencido()
    {
        var s = Salud(fin: new DateOnly(2026, 10, 2));
        s.Nivel.Should().Be(SaludProyecto.Rojo);
        s.Etiqueta.Should().Be("Vencido");
        s.Motivo.Should().Be("cierre planificado el 2 de octubre, hace 17 días");
    }

    [Fact]
    public void Paso5_bloqueo_vigente_es_rojo_bloqueado() =>
        Salud(bloqueo: true).Should()
            .Be(new SaludEvaluada(SaludProyecto.Rojo, "Bloqueado", "bloqueo en el último reporte"));

    [Fact]
    public void Paso6_veinte_puntos_por_detras_es_rojo_atrasado() =>
        Salud(desviacion: -20).Should()
            .Be(new SaludEvaluada(SaludProyecto.Rojo, "Atrasado", "20 pts por detrás del plan"));

    /// <summary>R4: un planificado que ya debía haber arrancado sigue en rojo «Atrasado», y el motivo
    /// dice que ni siquiera pasó a ejecución.</summary>
    [Fact]
    public void Paso6_un_planificado_atrasado_dice_que_sigue_en_planificado() =>
        Salud(EstadoProyecto.Planificado, desviacion: -100).Should()
            .Be(new SaludEvaluada(SaludProyecto.Rojo, "Atrasado", "100 pts por detrás del plan · sigue en planificado"));

    [Fact]
    public void Paso7_cinco_puntos_por_detras_es_ambar_en_riesgo() =>
        Salud(desviacion: -5).Should()
            .Be(new SaludEvaluada(SaludProyecto.Ambar, "En riesgo", "5 pts por detrás del plan"));

    [Fact]
    public void Paso8_en_ejecucion_sin_reportes_recientes_es_ambar_sin_reportar()
    {
        Salud(ultimo: Ahora.AddDays(-45)).Should()
            .Be(new SaludEvaluada(SaludProyecto.Ambar, "Sin reportar", "hace 45 días"));

        // Nunca reportó: la regla es la misma, el motivo lo dice.
        ProyectoEstadoReglas.Salud(EstadoProyecto.EnEjecucion, Hoy.AddDays(60), null, false, 3, 0, Hoy, Ahora)
            .Should().Be(new SaludEvaluada(SaludProyecto.Ambar, "Sin reportar", "nunca"));
    }

    [Fact]
    public void Paso9_suspendido_es_ambar() =>
        Salud(EstadoProyecto.Suspendido).Should().Be(new SaludEvaluada(SaludProyecto.Ambar, "Suspendido", ""));

    [Fact]
    public void Paso10_en_ejecucion_sin_contra_que_comparar_es_ambar_sin_linea_base()
    {
        Salud(conFinPorDefecto: false).Should()
            .Be(new SaludEvaluada(SaludProyecto.Ambar, "Sin línea base", "sin fecha de cierre comprometida"));
        Salud(desviacion: null).Should()
            .Be(new SaludEvaluada(SaludProyecto.Ambar, "Sin línea base", "sin fechas en las actividades"));
    }

    [Fact]
    public void Paso11_planificado_es_neutro_no_iniciado()
    {
        Salud(EstadoProyecto.Planificado, desviacion: null, inicio: new DateOnly(2026, 11, 3)).Should()
            .Be(new SaludEvaluada(SaludProyecto.Neutro, "No iniciado", "inicio planificado el 3 de noviembre"));
        Salud(EstadoProyecto.Planificado, desviacion: null).Motivo.Should().Be("sin fecha de inicio");
    }

    [Fact]
    public void Paso12_lo_demas_es_verde_en_plan()
    {
        Salud(desviacion: 4).Should().Be(new SaludEvaluada(SaludProyecto.Verde, "En plan", "+4 pts"));
        Salud(desviacion: 0).Motivo.Should().Be("en el plan");
        Salud(desviacion: -4).Nivel.Should().Be(SaludProyecto.Verde, "−4 está dentro del margen de −5");
    }

    // ── Precedencia ────────────────────────────────────────────────────────

    [Fact]
    public void Vencido_gana_a_bloqueado() =>
        Salud(fin: Hoy.AddDays(-1), bloqueo: true).Etiqueta.Should().Be("Vencido");

    [Fact]
    public void Bloqueado_gana_a_atrasado() =>
        Salud(bloqueo: true, desviacion: -30).Etiqueta.Should().Be("Bloqueado");

    [Fact]
    public void Los_niveles_se_ordenan_de_rojo_a_neutro() =>
        new[] { SaludProyecto.Neutro, SaludProyecto.Verde, SaludProyecto.Rojo, SaludProyecto.Ambar }
            .OrderBy(ProyectoEstadoReglas.Orden)
            .Should().Equal(SaludProyecto.Rojo, SaludProyecto.Ambar, SaludProyecto.Verde, SaludProyecto.Neutro);
}
