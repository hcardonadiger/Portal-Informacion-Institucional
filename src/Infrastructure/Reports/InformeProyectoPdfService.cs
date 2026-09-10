using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Domain.Enums;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Diger.TramitesEstado.Infrastructure.Reports;

/// <summary>
/// Genera el <b>Informe de Estado del Proyecto</b> en PDF con QuestPDF, siguiendo la estructura de un
/// <i>status report</i> del PMI: resumen ejecutivo, acta/ficha, desempeño contra la línea base,
/// estructura de desglose (EDT), cronograma, riesgos, interesados, equipo, puntos de atención y
/// avances recientes. Mismo estilo institucional que <see cref="InformeService"/>.
/// </summary>
public sealed class InformeProyectoPdfService : IInformeProyectoPdfService
{
    // Paleta (coherente con el tablero y la capacitación PMI)
    private const string Navy = "#1a3a5c", Blue = "#2e6da4", Ink = "#222222", Muted = "#666666";
    private const string GreenBg = "#e8fbe8", AmberBg = "#fff8e8", RedBg = "#fce8e8", BlueBg = "#e8f0fb";
    private const string Green = "#2f7d4f", Amber = "#a9741a", Red = "#b23a30";

    public byte[] Generar(InformeProyectoDto dto) => Construir(dto).GeneratePdf();

    /// <summary>Arma el documento (separado de la exportación para poder previsualizarlo o
    /// rasterizarlo a imágenes en pruebas sin duplicar la maqueta).</summary>
    private static Document Construir(InformeProyectoDto dto)
    {
        Settings.License = LicenseType.Community;

        var f = dto.Ficha;
        var t = dto.Tablero;
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial").FontColor(Ink));

                // ── Encabezado ─────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("GOBIERNO DE LA REPÚBLICA DE HONDURAS").FontSize(7).FontColor(Muted);
                            c.Item().Text("DIGER – Dirección de Gestión por Resultados").FontSize(12).Bold().FontColor(Navy);
                            c.Item().Text("Informe de Estado del Proyecto").FontSize(10).FontColor(Blue);
                        });
                        row.ConstantItem(170).AlignRight().Column(c =>
                        {
                            c.Item().Text($"Fecha de corte: {dto.GeneradoEn.ToLocalTime():dd/MM/yyyy}").FontSize(7).FontColor("#555555");
                            c.Item().Text($"Generado por: {dto.GeneradoPor}").FontSize(7).FontColor(Muted);
                            c.Item().Text("Metodología: PMI (status report)").FontSize(7).FontColor(Muted);
                        });
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Blue);
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Spacing(10);

                    // ── Título del proyecto ────────────────────────────────
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"{f.Codigo} — {f.Nombre}").FontSize(14).Bold().FontColor(Navy);
                            if (!string.IsNullOrWhiteSpace(f.Objetivo))
                                c.Item().Text(f.Objetivo).FontSize(9).FontColor(Muted);
                        });
                        row.ConstantItem(110).AlignRight().AlignMiddle()
                            .Background(EstadoBg(f.Estado)).Padding(6)
                            .Text(EstadoLabel(f.Estado)).FontSize(10).Bold().FontColor(EstadoFg(f.Estado));
                    });

                    // ── 1. Resumen ejecutivo ───────────────────────────────
                    Titulo(col, "1. Resumen ejecutivo");
                    var (semLabel, semBg, semFg) = Semaforo(t);
                    col.Item().Background(semBg).Padding(6).Text(semLabel).FontSize(9.5f).Bold().FontColor(semFg);
                    col.Item().Table(tb =>
                    {
                        Cols4(tb);
                        Metrica(tb, "Avance real",     $"{t.AvanceReal}%", BlueBg);
                        Metrica(tb, "Avance esperado", t.HayPlan ? $"{t.AvanceEsperado}%" : "s/ plan", "#ffffff");
                        Metrica(tb, "Desviación",      t.HayPlan ? $"{(t.Desviacion>=0?"+":"")}{t.Desviacion} pp" : "—", DesvBg(t));
                        Metrica(tb, "Días al cierre",  t.DiasAlCierre?.ToString() ?? "—", t.EstaAtrasado ? RedBg : "#ffffff");
                        Metrica(tb, "Entregables",     $"{t.EntCumplidos}/{t.EntTotal}", BlueBg);
                        Metrica(tb, "Actividades",     $"{t.ActTerminadas}/{t.TotalActividades}", "#ffffff");
                        Metrica(tb, "Riesgos altos",   t.RiesgosAltos.ToString(), t.RiesgosAltos>0 ? RedBg : GreenBg);
                        Metrica(tb, "Bloqueo vigente", string.IsNullOrWhiteSpace(t.BloqueoVigente) ? "No" : "Sí", string.IsNullOrWhiteSpace(t.BloqueoVigente) ? GreenBg : RedBg);
                    });
                    if (t.PlanIncompleto)
                        col.Item().Text($"Nota: solo {t.PctConFechas}% de las actividades tiene fechas planificadas; los indicadores contra línea base son referenciales.")
                            .FontSize(7.5f).Italic().FontColor(Amber);

                    // ── 2. Ficha del proyecto (acta) ───────────────────────
                    Titulo(col, "2. Ficha del proyecto (acta de constitución)");
                    col.Item().Table(tb =>
                    {
                        tb.ColumnsDefinition(c => { c.ConstantColumn(120); c.RelativeColumn(); c.ConstantColumn(120); c.RelativeColumn(); });
                        KV(tb, "Responsable",     f.Responsable ?? "—");
                        KV(tb, "Prioridad",       PrioridadLabel(f.Prioridad));
                        KV(tb, "Institución",     f.InstitucionId ?? "—");
                        KV(tb, "Área / Unidad",   string.Join(" / ", new[]{f.AreaId, f.UnidadId}.Where(x=>!string.IsNullOrWhiteSpace(x))) is {Length:>0} au ? au : "Transversal");
                        KV(tb, "Inicio (plan)",   Fecha(f.FechaInicioPlan));
                        KV(tb, "Inicio (real)",   Fecha(f.FechaInicioReal));
                        KV(tb, "Fin (plan)",      Fecha(f.FechaFinPlan));
                        KV(tb, "Fin (real)",      Fecha(f.FechaFinReal));
                    });

                    // ── 3. Desempeño vs. línea base ────────────────────────
                    Titulo(col, "3. Desempeño frente a la línea base");
                    col.Item().Text(NarrativaDesempeno(t)).FontSize(9);
                    col.Item().Table(tb =>
                    {
                        Cols4(tb);
                        Metrica(tb, "Avance real (ponderado)", $"{t.AvanceReal}%", BlueBg);
                        Metrica(tb, "Avance físico (act.)",    $"{t.AvanceFisico}%", "#ffffff");
                        Metrica(tb, "Avance esperado",         t.HayPlan?$"{t.AvanceEsperado}%":"—", "#ffffff");
                        Metrica(tb, "Desviación",              t.HayPlan?$"{(t.Desviacion>=0?"+":"")}{t.Desviacion} pp":"—", DesvBg(t));
                        Metrica(tb, "Cobertura cronograma",    $"{t.PctConFechas}%", t.PlanIncompleto?AmberBg:GreenBg);
                        Metrica(tb, "Con responsable",         $"{t.PctConResponsable}%", "#ffffff");
                        Metrica(tb, "Entregables vencidos",    t.EntVencidos.ToString(), t.EntVencidos>0?RedBg:GreenBg);
                        Metrica(tb, "Actividades vencidas",    t.ActVencidas.ToString(), t.ActVencidas>0?RedBg:GreenBg);
                    });

                    // ── 4. Estructura de desglose (EDT) / Entregables ──────
                    Titulo(col, "4. Estructura de desglose del trabajo (entregables)");
                    if (f.Entregables.Count == 0)
                        col.Item().Text("El proyecto todavía no tiene su EDT cargada.").FontSize(9).Italic().FontColor(Amber);
                    else
                        col.Item().Table(tb =>
                        {
                            tb.ColumnsDefinition(c => { c.ConstantColumn(18); c.RelativeColumn(); c.ConstantColumn(70); c.ConstantColumn(42); c.ConstantColumn(62); c.ConstantColumn(62); });
                            Header(tb, "#", "Entregable", "Estado", "Avance", "F. plan", "F. real");
                            int i = 0;
                            foreach (var e in f.Entregables.OrderBy(x=>x.Orden))
                            {
                                var bg = (i++%2==0) ? "#ffffff" : "#f5f8fd";
                                Celda(tb, bg, (i).ToString(), 8, false);
                                tb.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#dddddd").Padding(3).Column(cc=>{
                                    cc.Item().Text(e.Nombre).FontSize(8).Bold();
                                    cc.Item().Text($"{e.ActividadesTerminadas}/{e.ActividadesVigentes} actividades" + (e.EstaAtrasado?"  · atrasado":"")).FontSize(6.5f).FontColor(e.EstaAtrasado?Red:Muted);
                                });
                                Celda(tb, bg, EstadoEntLabel(e.Estado), 8, false);
                                Celda(tb, bg, $"{e.AvancePct}%", 8, true);
                                Celda(tb, bg, Fecha(e.FechaPlan), 8, false);
                                Celda(tb, bg, Fecha(e.FechaReal), 8, false);
                            }
                        });

                    // ── 5. Cronograma (recuento) ───────────────────────────
                    Titulo(col, "5. Situación del cronograma");
                    col.Item().Table(tb =>
                    {
                        c6(tb);
                        Metrica(tb, "Terminadas", t.ActTerminadas.ToString(), GreenBg);
                        Metrica(tb, "En proceso", t.ActEnProceso.ToString(), BlueBg);
                        Metrica(tb, "Pendientes", t.ActPendientes.ToString(), "#ffffff");
                        Metrica(tb, "Vencidas",   t.ActVencidas.ToString(), t.ActVencidas>0?RedBg:"#ffffff");
                        Metrica(tb, "Próximas",   t.ActProximas.ToString(), AmberBg);
                        Metrica(tb, "Bloqueadas", t.ActBloqueadas.ToString(), t.ActBloqueadas>0?RedBg:"#ffffff");
                    });

                    // ── 6. Riesgos e incidencias ───────────────────────────
                    Titulo(col, "6. Registro de riesgos");
                    col.Item().Text($"Abiertos: {t.RiesgosAbiertos}   ·   Severidad alta: {t.RiesgosAltos}   ·   Revisión vencida: {t.RiesgosRevisionVencida}")
                        .FontSize(8).FontColor(Muted);
                    if (!string.IsNullOrWhiteSpace(t.BloqueoVigente))
                        col.Item().PaddingTop(2).Background(RedBg).Padding(5)
                            .Text($"Incidencia vigente (riesgo materializado): {t.BloqueoVigente}").FontSize(8).Bold().FontColor(Red);
                    var abiertos = dto.Riesgos.Where(r => r.EstaAbierto).OrderByDescending(r => r.Severidad).ToList();
                    if (abiertos.Count == 0)
                        col.Item().PaddingTop(2).Text("Sin riesgos abiertos registrados.").FontSize(9).Italic().FontColor(Muted);
                    else
                        col.Item().PaddingTop(3).Table(tb =>
                        {
                            tb.ColumnsDefinition(c => { c.RelativeColumn(3); c.ConstantColumn(70); c.ConstantColumn(60); c.ConstantColumn(58); c.ConstantColumn(70); c.ConstantColumn(58); });
                            Header(tb, "Riesgo", "Categoría", "P×I (sev.)", "Estrategia", "Responsable", "Revisión");
                            int i=0;
                            foreach (var r in abiertos.Take(12))
                            {
                                var bg = (i++%2==0) ? "#ffffff" : "#f5f8fd";
                                tb.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#dddddd").Padding(3).Text(r.Descripcion).FontSize(7.5f);
                                Celda(tb, bg, CategoriaLabel(r.Categoria), 7.5f, false);
                                tb.Cell().Background(SevBg(r.NivelSeveridad)).BorderBottom(0.5f).BorderColor("#dddddd").Padding(3)
                                    .Text($"{(int)r.Probabilidad}×{(int)r.Impacto}={r.Severidad}").FontSize(7.5f).Bold().FontColor(SevFg(r.NivelSeveridad));
                                Celda(tb, bg, r.Estrategia.ToString(), 7.5f, false);
                                Celda(tb, bg, r.Responsable ?? "—", 7.5f, false);
                                tb.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#dddddd").Padding(3)
                                    .Text(r.FechaRevision is {} fr ? fr.ToString("dd/MM/yy") : "—").FontSize(7.5f).FontColor(r.RevisionVencida?Red:Ink);
                            }
                        });

                    // ── 7. Interesados ─────────────────────────────────────
                    Titulo(col, "7. Interesados");
                    if (dto.Interesados.Count == 0)
                        col.Item().Text("Sin interesados registrados.").FontSize(9).Italic().FontColor(Muted);
                    else
                        col.Item().Table(tb =>
                        {
                            tb.ColumnsDefinition(c => { c.RelativeColumn(2); c.ConstantColumn(95); c.ConstantColumn(60); c.RelativeColumn(2); c.ConstantColumn(42); });
                            Header(tb, "Nombre", "Rol", "Influencia", "Institución", "Clave");
                            int i=0;
                            foreach (var p in dto.Interesados.OrderByDescending(x=>x.EsClave).ThenByDescending(x=>(int)x.Influencia))
                            {
                                var bg = (i++%2==0) ? "#ffffff" : "#f5f8fd";
                                Celda(tb, bg, p.Nombre, 8, false);
                                Celda(tb, bg, RolLabel(p.Rol), 8, false);
                                Celda(tb, bg, p.Influencia.ToString(), 8, false);
                                Celda(tb, bg, p.Institucion ?? "—", 8, false);
                                tb.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#dddddd").Padding(3)
                                    .Text(p.EsClave ? "★" : "").FontSize(9).Bold().FontColor(Amber);
                            }
                        });

                    // ── 8. Equipo / carga ──────────────────────────────────
                    if (t.Carga.Count > 0)
                    {
                        Titulo(col, "8. Equipo y carga de trabajo");
                        col.Item().Table(tb =>
                        {
                            tb.ColumnsDefinition(c => { c.RelativeColumn(2); c.ConstantColumn(46); c.ConstantColumn(60); c.ConstantColumn(52); c.ConstantColumn(52); c.ConstantColumn(56); c.ConstantColumn(46); });
                            Header(tb, "Responsable", "Total", "Terminadas", "Abiertas", "Vencidas", "Bloqueadas", "%");
                            int i=0;
                            foreach (var cg in t.Carga.OrderByDescending(x=>x.Abiertas))
                            {
                                var bg = (i++%2==0) ? "#ffffff" : "#f5f8fd";
                                Celda(tb, bg, cg.Responsable, 8, false);
                                Celda(tb, bg, cg.Total.ToString(), 8, false);
                                Celda(tb, bg, cg.Terminadas.ToString(), 8, false);
                                Celda(tb, bg, cg.Abiertas.ToString(), 8, false);
                                Celda(tb, bg, cg.Vencidas.ToString(), 8, false);
                                Celda(tb, bg, cg.Bloqueadas.ToString(), 8, false);
                                Celda(tb, bg, $"{cg.PctTerminado}%", 8, true);
                            }
                        });
                        if (t.ActSinResponsable > 0)
                            col.Item().Text($"Actividades sin responsable: {t.ActSinResponsable}.").FontSize(7.5f).Italic().FontColor(Amber);
                    }

                    // ── 9. Puntos de atención ──────────────────────────────
                    if (t.Atencion.Count > 0)
                    {
                        Titulo(col, "9. Actividades que requieren atención");
                        col.Item().Table(tb =>
                        {
                            tb.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); c.ConstantColumn(58); c.ConstantColumn(90); });
                            Header(tb, "Entregable", "Actividad", "Motivo", "F. fin plan", "Responsable");
                            int i=0;
                            foreach (var a in t.Atencion.Take(10))
                            {
                                var bg = (i++%2==0) ? "#ffffff" : "#f5f8fd";
                                Celda(tb, bg, a.Entregable, 7.5f, false);
                                Celda(tb, bg, a.Nombre, 7.5f, false);
                                Celda(tb, bg, a.Motivo, 7.5f, false);
                                Celda(tb, bg, Fecha(a.FechaFinPlan), 7.5f, false);
                                Celda(tb, bg, a.Responsable ?? "—", 7.5f, false);
                            }
                        });
                    }

                    // ── 10. Ritmo y proyección ─────────────────────────────
                    Titulo(col, "10. Ritmo y proyección de cierre");
                    col.Item().Table(tb =>
                    {
                        Cols4(tb);
                        Metrica(tb, "Días sin reportar", t.DiasSinReportar?.ToString() ?? "—", (t.DiasSinReportar ?? 0) > 14 ? AmberBg : "#ffffff");
                        Metrica(tb, "Reportes de avance", t.TotalReportes.ToString(), "#ffffff");
                        Metrica(tb, "Ritmo (pts/mes)", t.PuntosPorMes.ToString("0.0"), BlueBg);
                        Metrica(tb, "Cierre proyectado", Fecha(t.CierreProyectado), CierreBg(t));
                    });

                    // ── 11. Avances recientes ──────────────────────────────
                    var recientes = dto.Avances.OrderByDescending(a => a.Fecha).Take(6).ToList();
                    if (recientes.Count > 0)
                    {
                        Titulo(col, "11. Avances recientes");
                        foreach (var a in recientes)
                        {
                            col.Item().BorderBottom(0.5f).BorderColor("#eeeeee").PaddingVertical(2).Row(row =>
                            {
                                row.ConstantItem(62).Text(a.Fecha.ToLocalTime().ToString("dd/MM/yyyy")).FontSize(7.5f).FontColor(Muted);
                                row.RelativeItem().Column(cc =>
                                {
                                    var titulo = a.ActividadNombre ?? a.EntregableNombre;
                                    cc.Item().Text(txt =>
                                    {
                                        txt.Span($"{a.Autor}").FontSize(8).Bold();
                                        if (a.PorcentajeReportado is int p) txt.Span($"  ({p}%)").FontSize(7.5f).FontColor(Blue);
                                        if (!string.IsNullOrWhiteSpace(titulo)) txt.Span($"  · {titulo}").FontSize(7.5f).FontColor(Muted);
                                    });
                                    cc.Item().Text(a.Descripcion).FontSize(8);
                                    if (!string.IsNullOrWhiteSpace(a.Bloqueo))
                                        cc.Item().Text($"Bloqueo: {a.Bloqueo}").FontSize(7.5f).FontColor(Red);
                                });
                            });
                        }
                    }
                });

                // ── Pie ────────────────────────────────────────────────────
                page.Footer().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Text("DIGER · Informe de estado generado desde el módulo de Proyectos").FontSize(7).FontColor("#aaaaaa");
                    row.ConstantItem(80).AlignRight().Text(txt =>
                    {
                        txt.Span("Página ").FontSize(7).FontColor("#aaaaaa");
                        txt.CurrentPageNumber().FontSize(7).FontColor("#aaaaaa");
                        txt.Span(" / ").FontSize(7).FontColor("#aaaaaa");
                        txt.TotalPages().FontSize(7).FontColor("#aaaaaa");
                    });
                });
            });
        });

        return doc;
    }

    // ── Helpers de layout ──────────────────────────────────────────────────
    private static void Titulo(ColumnDescriptor col, string texto) =>
        col.Item().PaddingTop(4).BorderBottom(1).BorderColor("#dbe4ee").PaddingBottom(2)
            .Text(texto).FontSize(11).Bold().FontColor(Navy);

    private static void Cols4(TableDescriptor tb) => tb.ColumnsDefinition(c =>
    { c.ConstantColumn(72); c.RelativeColumn(); c.ConstantColumn(72); c.RelativeColumn(); });

    private static void c6(TableDescriptor tb) => tb.ColumnsDefinition(c =>
    { for (int i=0;i<6;i++){ c.RelativeColumn(); } });

    private static void Metrica(TableDescriptor tb, string label, string valor, string bg)
    {
        tb.Cell().Background(bg).Border(0.5f).BorderColor("#e3e9f0").Padding(4).Column(c =>
        {
            c.Item().Text(label).FontSize(6.5f).FontColor("#555555");
            c.Item().Text(valor).FontSize(12).Bold().FontColor(Navy);
        });
    }

    private static void KV(TableDescriptor tb, string k, string v)
    {
        tb.Cell().Padding(2).Text(k).FontSize(7.5f).FontColor("#555555");
        tb.Cell().Padding(2).Text(v).FontSize(9).Bold().FontColor(Ink);
    }

    private static void Header(TableDescriptor tb, params string[] labels)
    {
        tb.Header(h =>
        {
            foreach (var l in labels)
                h.Cell().Background(Blue).Padding(3).Text(l).FontColor(Colors.White).FontSize(7.5f).Bold();
        });
    }

    private static void Celda(TableDescriptor tb, string bg, string txt, float fs, bool bold)
    {
        var cell = tb.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#dddddd").Padding(3).Text(txt).FontSize(fs);
        if (bold) cell.Bold();
    }

    // ── Semáforo / narrativa ───────────────────────────────────────────────
    private static (string, string, string) Semaforo(TableroProyectoDto t)
    {
        if (t.Estado is EstadoProyecto.Cerrado)
            return ("Proyecto CERRADO. Desempeño consolidado al cierre.", BlueBg, Navy);
        if (t.Estado is EstadoProyecto.Cancelado)
            return ("Proyecto CANCELADO.", RedBg, Red);
        var rojo = t.EstaAtrasado || (t.HayPlan && t.Desviacion <= -10) || t.RiesgosAltos > 0 || !string.IsNullOrWhiteSpace(t.BloqueoVigente);
        var ambar = t.HayPlan && t.Desviacion < 0;
        if (rojo)  return ("REQUIERE ATENCIÓN — el proyecto va por detrás del plan o tiene riesgos/incidencias abiertas que amenazan el cierre.", RedBg, Red);
        if (ambar) return ("EN OBSERVACIÓN — ligeramente por detrás de lo planificado; vigilar el ritmo.", AmberBg, Amber);
        return ("EN CURSO — el proyecto avanza conforme a lo planificado.", GreenBg, Green);
    }

    private static string NarrativaDesempeno(TableroProyectoDto t)
    {
        if (!t.HayPlan)
            return "El cronograma aún no tiene fechas suficientes para comparar contra una línea base. El avance mostrado es físico (estado de las actividades).";
        var d = t.Desviacion;
        var sentido = d > 2 ? $"adelantado {d} puntos porcentuales respecto de lo esperado"
                    : d < -2 ? $"atrasado {-d} puntos porcentuales respecto de lo esperado"
                    : "alineado con lo esperado";
        return $"A la fecha de corte, el avance real es {t.AvanceReal}% frente a un esperado de {t.AvanceEsperado}%: el proyecto está {sentido}.";
    }

    private static string DesvBg(TableroProyectoDto t) =>
        !t.HayPlan ? "#ffffff" : t.Desviacion <= -10 ? RedBg : t.Desviacion < 0 ? AmberBg : GreenBg;

    private static string CierreBg(TableroProyectoDto t) =>
        t.CierreProyectado is {} cp && t.FechaFinPlan is {} fp && cp > fp ? AmberBg : "#ffffff";

    private static string SevBg(NivelCualitativo n) => n switch
    { NivelCualitativo.Alta => RedBg, NivelCualitativo.Media => AmberBg, _ => GreenBg };
    private static string SevFg(NivelCualitativo n) => n switch
    { NivelCualitativo.Alta => Red, NivelCualitativo.Media => Amber, _ => Green };

    private static string EstadoBg(EstadoProyecto e) => e switch
    {
        EstadoProyecto.EnEjecucion => "#e8f5ee", EstadoProyecto.Planificado => BlueBg,
        EstadoProyecto.Suspendido => AmberBg, EstadoProyecto.Cancelado => RedBg, _ => "#eef1f4"
    };
    private static string EstadoFg(EstadoProyecto e) => e switch
    {
        EstadoProyecto.EnEjecucion => Green, EstadoProyecto.Suspendido => Amber,
        EstadoProyecto.Cancelado => Red, _ => Navy
    };

    // ── Etiquetas ──────────────────────────────────────────────────────────
    private static string Fecha(DateOnly? d) => d?.ToString("dd/MM/yyyy") ?? "—";
    private static string EstadoLabel(EstadoProyecto e) => e switch
    { EstadoProyecto.EnEjecucion => "En ejecución", _ => e.ToString() };
    private static string EstadoEntLabel(EstadoEntregable e) => e switch
    { EstadoEntregable.EnProceso => "En proceso", _ => e.ToString() };
    private static string PrioridadLabel(PrioridadProyecto p) => p.ToString();
    private static string CategoriaLabel(CategoriaRiesgo c) => c switch
    { CategoriaRiesgo.Tecnico => "Técnico", _ => c.ToString() };
    private static string RolLabel(RolInteresado r) => r switch
    { RolInteresado.ContraparteTecnica => "Contraparte técnica", _ => r.ToString() };
}
