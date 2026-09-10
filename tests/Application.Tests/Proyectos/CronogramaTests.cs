using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

/// <summary>
/// La escala del cronograma.
///
/// <para>Se prueba la aritmética y no el dibujo: un desplazamiento equivocado por un día corre la
/// barra un pelo y no lo nota nadie mirando la pantalla, pero deja de coincidir con el eje. Acá
/// los números se comparan contra valores calculados a mano.</para>
/// </summary>
public class CronogramaTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 26);

    private static ActividadProyectoDto Act(
        string nombre, DateOnly? ini, DateOnly? fin, int pct = 0,
        EstadoActividad estado = EstadoActividad.Pendiente,
        params PredecesoraDto[] predecesoras) =>
        new(1, 1, nombre, null, ini, fin, null, null, pct, estado, null, null, predecesoras);

    private static EntregableProyectoDto Ent(
        string nombre, DateOnly? plan, EstadoEntregable estado, params ActividadProyectoDto[] acts) =>
        new(1, 1, nombre, null, plan, null, estado, null, null,
            acts.Length == 0 ? 0 : (int)Math.Round(acts.Average(a => (double)a.AvancePct)), acts);

    private static ProyectoDetailDto Proyecto(params EntregableProyectoDto[] entregables) =>
        new(1, "PRY-2026-01", "Demo", null, "DIGER", null, null, null, null,
            PrioridadProyecto.Media, null, EstadoProyecto.EnEjecucion,
            null, null, null, null, 0, DateTime.UtcNow, null, entregables, []);

    // ── Escala ────────────────────────────────────────────────────
    [Fact]
    public void El_eje_se_redondea_a_meses_completos()
    {
        // Una actividad del 10 de mayo al 20 de junio tiene que producir un eje del 1 de mayo al
        // 30 de junio: si el eje empezara el día 10, la primera barra arrancaría pegada al borde.
        var c = CronogramaProyecto.Construir(
            Proyecto(Ent("E", null, EstadoEntregable.EnProceso,
                Act("A", new DateOnly(2026, 5, 10), new DateOnly(2026, 6, 20)))), Hoy);

        c.Desde.Should().Be(new DateOnly(2026, 5, 1));
        c.Hasta.Should().Be(new DateOnly(2026, 6, 30));
        c.TotalDias.Should().Be(61);            // 31 de mayo + 30 de junio
        c.Meses.Select(m => m.Etiqueta).Should().Equal("may", "jun");
    }

    [Fact]
    public void Los_meses_no_se_dibujan_todos_del_mismo_ancho()
    {
        // Febrero tiene 28 días y marzo 31: pintarlos iguales corre las barras respecto del eje.
        var c = CronogramaProyecto.Construir(
            Proyecto(Ent("E", null, EstadoEntregable.EnProceso,
                Act("A", new DateOnly(2026, 2, 5), new DateOnly(2026, 3, 25)))), Hoy);

        var feb = c.Meses[0];
        var mar = c.Meses[1];

        feb.AnchoPct.Should().BeApproximately(28d / 59d * 100d, 0.01);
        mar.AnchoPct.Should().BeApproximately(31d / 59d * 100d, 0.01);
        feb.OffsetPct.Should().Be(0);
        mar.OffsetPct.Should().BeApproximately(feb.AnchoPct, 0.01);
    }

    [Fact]
    public void La_posicion_de_una_barra_coincide_con_su_fecha()
    {
        // Mayo entero: 31 días. Del 11 al 20 son 10 días que arrancan en el día 10 (base cero).
        var c = CronogramaProyecto.Construir(
            Proyecto(Ent("E", null, EstadoEntregable.EnProceso,
                Act("A", new DateOnly(2026, 5, 11), new DateOnly(2026, 5, 20)))), Hoy);

        var barra = c.Barras.Single(b => b.Nivel == 1);

        barra.OffsetPct.Should().BeApproximately(10d / 31d * 100d, 0.01);
        barra.AnchoPct.Should().BeApproximately(10d / 31d * 100d, 0.01);
    }

    [Fact]
    public void Una_actividad_de_un_solo_dia_sigue_siendo_visible()
    {
        // Con inicio y fin el mismo día, hasta-desde es cero: sin el mínimo de un día la barra
        // tendría ancho 0 y desaparecería de la pantalla.
        var c = CronogramaProyecto.Construir(
            Proyecto(Ent("E", null, EstadoEntregable.EnProceso,
                Act("A", new DateOnly(2026, 5, 15), new DateOnly(2026, 5, 15)))), Hoy);

        c.Barras.Single(b => b.Nivel == 1).AnchoPct.Should().BeApproximately(100d / 31d, 0.01);
    }

    [Fact]
    public void El_eje_se_estira_para_incluir_lo_que_se_pasa_del_plan()
    {
        // Dibujar solo hasta la fecha comprometida recortaría justamente lo que hay que ver.
        var c = CronogramaProyecto.Construir(
            Proyecto(Ent("E", new DateOnly(2026, 6, 30), EstadoEntregable.EnProceso,
                Act("A", new DateOnly(2026, 6, 1), new DateOnly(2026, 9, 15)))), Hoy);

        c.Hasta.Should().Be(new DateOnly(2026, 9, 30));
    }

    // ── Hoy ───────────────────────────────────────────────────────
    [Fact]
    public void La_linea_de_hoy_solo_aparece_si_cae_dentro_del_rango()
    {
        var dentro = CronogramaProyecto.Construir(
            Proyecto(Ent("E", null, EstadoEntregable.EnProceso,
                Act("A", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)))), Hoy);

        var fuera = CronogramaProyecto.Construir(
            Proyecto(Ent("E", null, EstadoEntregable.EnProceso,
                Act("A", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)))), Hoy);

        dentro.HoyPct.Should().BeApproximately(25d / 31d * 100d, 0.01);   // 26 de agosto = día 25
        fuera.HoyPct.Should().BeNull("dibujar la línea fuera del eje la pondría sobre una barra ajena");
    }

    // ── Lo que no se puede dibujar ────────────────────────────────
    [Fact]
    public void Las_actividades_sin_fecha_van_aparte_y_no_se_omiten()
    {
        // En el portafolio real son 178 de 191: esconderlas daría a entender que el cronograma
        // está completo cuando casi no existe.
        var c = CronogramaProyecto.Construir(
            Proyecto(Ent("E", null, EstadoEntregable.EnProceso,
                Act("Con fechas", new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 10)),
                Act("Sin fin",    new DateOnly(2026, 5, 1), null),
                Act("Sin nada",   null, null))), Hoy);

        c.Barras.Where(b => b.Nivel == 1).Select(b => b.Nombre).Should().Equal("Con fechas");
        c.SinFechas.Select(b => b.Nombre).Should().Equal("Sin fin", "Sin nada");
    }

    [Fact]
    public void Un_proyecto_sin_una_sola_fecha_no_dibuja_nada_pero_lista_todo()
    {
        var c = CronogramaProyecto.Construir(
            Proyecto(Ent("E", null, EstadoEntregable.Pendiente,
                Act("A", null, null), Act("B", null, null))), Hoy);

        c.TieneQueDibujar.Should().BeFalse();
        c.Desde.Should().BeNull();
        c.SinFechas.Should().HaveCount(2);
    }

    // ── Estado visual ─────────────────────────────────────────────
    [Fact]
    public void Una_actividad_abierta_con_la_ventana_vencida_se_pinta_como_vencida()
    {
        var c = CronogramaProyecto.Construir(
            Proyecto(Ent("E", null, EstadoEntregable.EnProceso,
                Act("Tarde",     new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 1), 40, EstadoActividad.EnProceso),
                Act("A tiempo",  new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 1), 40, EstadoActividad.EnProceso),
                Act("Terminada", new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 1), 100, EstadoActividad.Completada))), Hoy);

        var porNombre = c.Barras.Where(b => b.Nivel == 1).ToDictionary(b => b.Nombre, b => b.Estado);

        porNombre["Tarde"].Should().Be(EstadoBarra.Vencida);
        porNombre["A tiempo"].Should().Be(EstadoBarra.EnProceso);
        porNombre["Terminada"].Should().Be(EstadoBarra.Completada,
            "una actividad terminada no es tardía aunque su ventana haya pasado");
    }

    [Fact]
    public void El_entregable_resume_la_ventana_de_sus_actividades_y_marca_su_compromiso_aparte()
    {
        var c = CronogramaProyecto.Construir(
            Proyecto(Ent("E", new DateOnly(2026, 6, 15), EstadoEntregable.EnProceso,
                Act("A", new DateOnly(2026, 5, 5),  new DateOnly(2026, 5, 20)),
                Act("B", new DateOnly(2026, 5, 25), new DateOnly(2026, 6, 10)))), Hoy);

        var e = c.Barras.Single(b => b.EsEntregable);

        e.Inicio.Should().Be(new DateOnly(2026, 5, 5),  "la barra resumen arranca con la primera actividad");
        e.Fin.Should().Be(new DateOnly(2026, 6, 10),    "y termina con la última");
        e.Compromiso.Should().Be(new DateOnly(2026, 6, 15));
        e.CompromisoPct.Should().NotBeNull("el compromiso se dibuja como hito, aparte de la barra");
    }

    [Fact]
    public void La_actividad_bloqueada_trae_a_quien_espera()
    {
        var predecesora = new PredecesoraDto(9, "Capacitación", "Entregable", EstadoActividad.EnProceso, null);

        var c = CronogramaProyecto.Construir(
            Proyecto(Ent("E", null, EstadoEntregable.EnProceso,
                Act("Producción", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10),
                    0, EstadoActividad.Pendiente, predecesora))), Hoy);

        var barra = c.Barras.Single(b => b.Nivel == 1);
        barra.Bloqueada.Should().BeTrue();
        barra.Espera.Should().Equal("Capacitación");
    }
}
