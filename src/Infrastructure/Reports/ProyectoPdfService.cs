using Diger.TramitesEstado.Application.Dashboards.Queries;
using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Diger.TramitesEstado.Infrastructure.Reports;

/// <summary>
/// Genera el PDF con la estructura completa de un proyecto. Mismo criterio que
/// <see cref="ActaPdfService"/>: no es un print de la pantalla, es un documento maquetado.
///
/// <para>El documento va en A4 vertical salvo el cronograma, que se emite en su propia página
/// horizontal: un GANTT de doce meses en 17 cm de ancho útil deja las barras cortas ilegibles.</para>
///
/// <para><b>El GANTT no recalcula fechas.</b> <c>CronogramaProyecto.Construir</c> ya devuelve cada
/// barra posicionada en porcentaje del ancho total —esa aritmética tiene pruebas en la capa de
/// aplicación— y acá solo se multiplica por el ancho del papel. Duplicar el cálculo sería abrir
/// la puerta a que el PDF y la pantalla muestren cronogramas distintos.</para>
/// </summary>
public sealed class ProyectoPdfService : IProyectoPdfService
{
    private const string Azul      = "#1a3a5c";
    private const string AzulMed   = "#2e6da4";
    private const string Gris      = "#666666";
    private const string GrisTenue = "#888888";

    // Paleta del semáforo, la misma que usa el cronograma en pantalla.
    private const string Verde  = "#1d9e75";
    private const string Ambar  = "#ef9f27";
    private const string Rojo   = "#a32d2d";
    private const string Neutro = "#8a949e";

    /// <summary>Ancho útil de la banda del GANTT, en puntos. La página horizontal A4 mide 842 pt;
    /// descontando márgenes y la columna de nombres quedan estos para la escala de tiempo.</summary>
    private const float AnchoBanda = 500f;

    public byte[] Generar(ProyectoPdfDto dto)
    {
        Settings.License = LicenseType.Community;

        var p = dto.Proyecto;

        var doc = Document.Create(container =>
        {
            // ── Cuerpo: todo menos el cronograma ─────────────────────────────
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.8f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Arial").FontColor("#1c2333").LineHeight(1.3f));

                Encabezado(page, p);
                PieDePagina(page, p);

                page.Content().PaddingTop(10).Column(col =>
                {
                    Ficha(col, dto);
                    Edt(col, p);
                    Avances(col, dto.Avances);
                    Equipo(col, dto.Interesados, dto.Riesgos);
                    Documentos(col, dto.Documentos);
                    Vinculos(col, dto.Vinculos);
                    Auditoria(col, dto.Auditoria);
                });
            });

            // ── Cronograma: página horizontal aparte ─────────────────────────
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(8.5f).FontFamily("Arial").FontColor("#1c2333"));

                Encabezado(page, p);
                PieDePagina(page, p);

                page.Content().PaddingTop(10).Column(col => Cronograma(col, dto.Cronograma));
            });
        });

        return doc.GeneratePdf();
    }

    // ── Encabezado y pie, comunes a ambas orientaciones ────────────────────────
    private static void Encabezado(PageDescriptor page, ProyectoDetailDto p) =>
        page.Header().Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(p.Nombre).FontSize(15).Bold().FontColor(Azul);
                row.ConstantItem(120).AlignRight().Text(p.Codigo).FontSize(10).Bold().FontColor(AzulMed);
            });
            col.Item().PaddingTop(4).LineHorizontal(1.5f).LineColor(AzulMed);
        });

    private static void PieDePagina(PageDescriptor page, ProyectoDetailDto p) =>
        page.Footer().PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text($"{p.Codigo} — generado el {DateTime.Now:dd/MM/yyyy HH:mm}")
                .FontSize(7.5f).FontColor(GrisTenue);
            row.ConstantItem(90).AlignRight().Text(txt =>
            {
                txt.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(GrisTenue));
                txt.Span("Página ");
                txt.CurrentPageNumber();
                txt.Span(" de ");
                txt.TotalPages();
            });
        });

    // ── 1. Ficha ───────────────────────────────────────────────────────────────
    private static void Ficha(ColumnDescriptor col, ProyectoPdfDto dto)
    {
        var p = dto.Proyecto;

        Seccion(col, "1", "Ficha del proyecto", body =>
        {
            if (Hay(p.Objetivo))
                body.Item().PaddingBottom(6).Text(p.Objetivo).FontSize(9.5f);

            body.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    Dato(c, "Estado", Etiquetas.Estado(p.Estado));
                    Dato(c, "Prioridad", p.Prioridad.ToString());
                    Dato(c, "Acción", Etiquetas.Accion(p.Accion));
                    Dato(c, "Responsable", p.Responsable);
                });
                row.RelativeItem().Column(c =>
                {
                    Dato(c, "Institución", PrimeraNoVacia(dto.InstitucionNombre, p.InstitucionId));
                    Dato(c, "Área", PrimeraNoVacia(dto.AreaNombre, p.AreaId));
                    Dato(c, "Unidad", PrimeraNoVacia(dto.UnidadNombre, p.UnidadId));
                    Dato(c, "Avance", $"{p.AvancePct} %");
                });
                row.RelativeItem().Column(c =>
                {
                    Dato(c, "Inicio planificado", Fecha(p.FechaInicioPlan));
                    Dato(c, "Cierre planificado", Fecha(p.FechaFinPlan));
                    Dato(c, "Inicio real", Fecha(p.FechaInicioReal));
                    Dato(c, "Cierre real", Fecha(p.FechaFinReal));
                });
            });
        });
    }

    // ── 2. EDT ─────────────────────────────────────────────────────────────────
    private static void Edt(ColumnDescriptor col, ProyectoDetailDto p)
    {
        Seccion(col, "2", $"Estructura de desglose ({p.Entregables.Count} entregable(s))", body =>
        {
            if (p.Entregables.Count == 0)
            {
                body.Item().Text("El proyecto todavía no tiene entregables cargados.").FontColor(Gris).Italic();
                return;
            }

            foreach (var e in p.Entregables.OrderBy(x => x.Orden))
            {
                // El entregable y sus actividades no se separan entre páginas si caben juntos:
                // una tabla de actividades huérfana de su título obliga a volver atrás para saber
                // de qué entregable es.
                body.Item().PaddingTop(8).Column(bloque =>
                {
                    bloque.Item().Background("#eef4fb").Padding(5).Row(row =>
                    {
                        row.RelativeItem().Text($"{e.Orden}. {e.Nombre}").Bold().FontColor(Azul).FontSize(10);
                        row.ConstantItem(190).AlignRight().Text(
                            $"{Etiquetas.Entregable(e.Estado)} · {e.AvancePct} % · entrega {Fecha(e.FechaPlan)}")
                            .FontSize(8.5f).FontColor(Gris);
                    });

                    if (Hay(e.Responsable))
                        bloque.Item().PaddingTop(2).Text($"Responsable: {e.Responsable}").FontSize(8.5f).FontColor(Gris);

                    if (e.Actividades.Count == 0)
                    {
                        bloque.Item().PaddingTop(3).Text("Sin actividades desglosadas.").FontSize(8.5f).FontColor(Gris).Italic();
                        return;
                    }

                    bloque.Item().PaddingTop(4).Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(22);   // #
                            c.RelativeColumn(4);    // Actividad
                            c.RelativeColumn(2);    // Responsable
                            c.ConstantColumn(58);   // Inicio plan
                            c.ConstantColumn(58);   // Fin plan
                            c.ConstantColumn(58);   // Fin real
                            c.ConstantColumn(38);   // Avance
                            c.ConstantColumn(58);   // Estado
                        });

                        tbl.Header(h =>
                        {
                            foreach (var lbl in new[] { "#", "Actividad", "Responsable", "Inicio", "Fin plan.", "Fin real", "%", "Estado" })
                                h.Cell().Element(EncabezadoCelda).Text(lbl).FontColor(Colors.White).FontSize(7.5f).Bold();
                        });

                        var i = 0;
                        foreach (var a in e.Actividades.OrderBy(x => x.Orden))
                        {
                            var bg = i++ % 2 == 0 ? "#ffffff" : "#f6f9fd";
                            Celda(tbl, bg).Text(a.Orden.ToString()).FontSize(7.5f);
                            Celda(tbl, bg).Text(a.Nombre).FontSize(7.5f);
                            Celda(tbl, bg).Text(Ni(a.Responsable)).FontSize(7.5f);
                            Celda(tbl, bg).Text(Fecha(a.FechaInicioPlan)).FontSize(7.5f);
                            Celda(tbl, bg).Text(Fecha(a.FechaFinPlan)).FontSize(7.5f);
                            Celda(tbl, bg).Text(Fecha(a.FechaFinReal)).FontSize(7.5f);
                            Celda(tbl, bg).Text($"{a.AvancePct}").FontSize(7.5f);
                            Celda(tbl, bg).Text(Etiquetas.Actividad(a.Estado)).FontSize(7.5f);
                        }
                    });
                });
            }
        });
    }

    // ── 3. Cronograma (página horizontal) ──────────────────────────────────────
    private static void Cronograma(ColumnDescriptor col, CronogramaDto c)
    {
        col.Item().Text("Cronograma").FontSize(13).Bold().FontColor(Azul);

        if (c.Barras.Count == 0)
        {
            col.Item().PaddingTop(6)
                .Text("Ninguna actividad o entregable tiene fechas cargadas: no hay nada que dibujar.")
                .FontColor(Gris).Italic();
            SinFechas(col, c.SinFechas);
            return;
        }

        col.Item().PaddingTop(2).Text($"Del {Fecha(c.Desde)} al {Fecha(c.Hasta)}").FontSize(8.5f).FontColor(Gris);

        // Escala de meses.
        col.Item().PaddingTop(6).Row(row =>
        {
            row.ConstantItem(210).Text("").FontSize(7.5f);
            row.ConstantItem(AnchoBanda).Height(14).Layers(layers =>
            {
                layers.PrimaryLayer().Background("#f2f5f9");
                foreach (var m in c.Meses)
                {
                    layers.Layer().PaddingLeft(Pt(m.OffsetPct)).Width(Pt(m.AnchoPct))
                        .BorderLeft(0.5f).BorderColor("#c9d6e4")
                        .PaddingLeft(2).AlignMiddle()
                        .Text($"{m.Etiqueta} {m.Anio}").FontSize(6.5f).FontColor(Gris);
                }
            });
        });

        // Una fila por barra. Los entregables van en negrita para que la jerarquía se lea.
        foreach (var b in c.Barras)
        {
            col.Item().PaddingTop(2).Row(row =>
            {
                row.ConstantItem(210).PaddingRight(6).Text(txt =>
                {
                    txt.DefaultTextStyle(s => s.FontSize(7.5f));
                    var nombre = b.EsEntregable ? b.Nombre : $"   {b.Nombre}";
                    if (b.EsEntregable) txt.Span(nombre).Bold().FontColor(Azul);
                    else                txt.Span(nombre);
                });

                row.ConstantItem(AnchoBanda).Height(11).Layers(layers =>
                {
                    layers.PrimaryLayer().Background("#f7f9fc");

                    // Línea de hoy, si cae dentro de la ventana dibujada.
                    if (c.HoyPct is { } hoy)
                        layers.Layer().PaddingLeft(Pt(hoy)).Width(1).Background(Rojo);

                    layers.Layer().PaddingLeft(Pt(b.OffsetPct))
                        .Width(Math.Max(1.5f, Pt(b.AnchoPct)))
                        .Background(ColorBarra(b.Estado));

                    // Hito de compromiso del entregable: un tick, no una barra.
                    if (b.CompromisoPct is { } cp)
                        layers.Layer().PaddingLeft(Pt(cp)).Width(2).Background(Azul);
                });

                row.ConstantItem(34).AlignRight().Text($"{b.AvancePct}%").FontSize(7f).FontColor(Gris);
            });
        }

        // Leyenda: sin ella, los colores son adivinanza.
        col.Item().PaddingTop(10).Row(row =>
        {
            row.AutoItem().Text("Leyenda:").FontSize(7.5f).Bold().FontColor(Gris);
            Muestra(row, Verde,  "Completada");
            Muestra(row, AzulMed, "En proceso");
            Muestra(row, Ambar,  "Pendiente");
            Muestra(row, Rojo,   "Vencida / hoy");
            Muestra(row, Neutro, "Cancelada");
            Muestra(row, Azul,   "Compromiso del entregable");
        });

        SinFechas(col, c.SinFechas);
    }

    /// <summary>Lo que no se pudo dibujar se lista, no se omite: esconderlo daría a entender que
    /// el cronograma está completo cuando puede faltarle la mayor parte.</summary>
    private static void SinFechas(ColumnDescriptor col, IReadOnlyList<BarraCronograma> sinFechas)
    {
        if (sinFechas.Count == 0) return;

        col.Item().PaddingTop(12).Text($"Sin fechas cargadas ({sinFechas.Count})")
            .FontSize(10).Bold().FontColor(Azul);
        col.Item().PaddingTop(2)
            .Text("No se pueden dibujar en el cronograma. Se listan para que no pasen por inexistentes.")
            .FontSize(7.5f).FontColor(Gris);

        foreach (var b in sinFechas)
            col.Item().PaddingTop(1).Text($"• {(b.EsEntregable ? b.Nombre : "   " + b.Nombre)}  ({b.AvancePct} %)")
                .FontSize(7.5f);
    }

    private static void Muestra(RowDescriptor row, string color, string etiqueta)
    {
        row.AutoItem().PaddingLeft(10).AlignMiddle().Width(10).Height(6).Background(color);
        row.AutoItem().PaddingLeft(3).AlignMiddle().Text(etiqueta).FontSize(7f).FontColor(Gris);
    }

    private static string ColorBarra(EstadoBarra e) => e switch
    {
        EstadoBarra.Completada => Verde,
        EstadoBarra.EnProceso  => AzulMed,
        EstadoBarra.Vencida    => Rojo,
        EstadoBarra.Cancelada  => Neutro,
        _                      => Ambar
    };

    /// <summary>Porcentaje de la escala a puntos del papel. Se acota a [0, 100] porque una barra
    /// fuera de rango se dibujaría fuera de la página en vez de fallar visiblemente.</summary>
    private static float Pt(double pct) => (float)(Math.Clamp(pct, 0, 100) / 100d * AnchoBanda);

    // ── 4. Bitácora de avances ─────────────────────────────────────────────────
    private static void Avances(ColumnDescriptor col, IReadOnlyList<AvanceProyectoDto> avances)
    {
        Seccion(col, "3", $"Bitácora de avances ({avances.Count})", body =>
        {
            if (avances.Count == 0)
            {
                body.Item().Text("Nadie ha reportado avance todavía.").FontColor(Gris).Italic();
                return;
            }

            foreach (var a in avances.OrderByDescending(x => x.Fecha))
            {
                body.Item().PaddingTop(5).Column(item =>
                {
                    item.Item().Row(row =>
                    {
                        row.RelativeItem().Text(txt =>
                        {
                            txt.DefaultTextStyle(s => s.FontSize(8.5f));
                            txt.Span($"{a.Fecha.ToLocalTime():dd/MM/yyyy} · ").FontColor(Gris);
                            txt.Span(a.Autor).Bold();
                            if (Hay(a.ActividadNombre))      txt.Span($"  —  {a.ActividadNombre}").FontColor(Gris);
                            else if (Hay(a.EntregableNombre)) txt.Span($"  —  {a.EntregableNombre}").FontColor(Gris);
                        });
                        if (a.PorcentajeReportado is { } pct)
                            row.ConstantItem(40).AlignRight().Text($"{pct} %").FontSize(8.5f).Bold().FontColor(AzulMed);
                    });

                    item.Item().Text(a.Descripcion).FontSize(8.5f);

                    if (Hay(a.Bloqueo))
                        item.Item().PaddingTop(1).Text($"Bloqueo: {a.Bloqueo}").FontSize(8f).FontColor(Rojo);
                    if (a.TieneEvidencia)
                        item.Item().PaddingTop(1).Text($"Evidencia: {a.ArchivoNombre}").FontSize(7.5f).FontColor(Gris);
                    if (a.FueEditada)
                        item.Item().PaddingTop(1)
                            .Text($"Corregida el {a.EditadoEn!.Value.ToLocalTime():dd/MM/yyyy} por {Ni(a.EditadoPor)}")
                            .FontSize(7.5f).FontColor(GrisTenue).Italic();
                });
            }
        });
    }

    // ── 5. Equipo: interesados y riesgos ───────────────────────────────────────
    private static void Equipo(ColumnDescriptor col,
        IReadOnlyList<InteresadoProyectoDto> interesados, IReadOnlyList<RiesgoProyectoDto> riesgos)
    {
        Seccion(col, "4", $"Equipo y riesgos ({interesados.Count} interesado(s), {riesgos.Count} riesgo(s))", body =>
        {
            if (interesados.Count > 0)
            {
                body.Item().Text("Interesados").Bold().FontColor(Azul).FontSize(9.5f);
                body.Item().PaddingTop(3).Table(tbl =>
                {
                    tbl.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3); c.RelativeColumn(3); c.RelativeColumn(2);
                        c.ConstantColumn(90); c.ConstantColumn(55);
                    });
                    tbl.Header(h =>
                    {
                        foreach (var lbl in new[] { "Nombre", "Cargo / institución", "Correo", "Rol", "Influencia" })
                            h.Cell().Element(EncabezadoCelda).Text(lbl).FontColor(Colors.White).FontSize(7.5f).Bold();
                    });

                    var i = 0;
                    foreach (var x in interesados)
                    {
                        var bg = i++ % 2 == 0 ? "#ffffff" : "#f6f9fd";
                        Celda(tbl, bg).Text(x.Nombre).FontSize(7.5f);
                        Celda(tbl, bg).Text(Ni(Combinar(x.Cargo, x.Institucion, " · "))).FontSize(7.5f);
                        Celda(tbl, bg).Text(Ni(x.Correo)).FontSize(7.5f);
                        Celda(tbl, bg).Text(x.Rol.ToString()).FontSize(7.5f);
                        Celda(tbl, bg).Text(x.Influencia.ToString()).FontSize(7.5f);
                    }
                });
            }

            if (riesgos.Count == 0) return;

            body.Item().PaddingTop(8).Text("Riesgos").Bold().FontColor(Azul).FontSize(9.5f);
            body.Item().PaddingTop(3).Table(tbl =>
            {
                tbl.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(4); c.ConstantColumn(70); c.ConstantColumn(46);
                    c.ConstantColumn(62); c.RelativeColumn(2); c.ConstantColumn(62);
                });
                tbl.Header(h =>
                {
                    foreach (var lbl in new[] { "Riesgo", "Categoría", "Sev.", "Estrategia", "Responsable", "Estado" })
                        h.Cell().Element(EncabezadoCelda).Text(lbl).FontColor(Colors.White).FontSize(7.5f).Bold();
                });

                var i = 0;
                foreach (var r in riesgos.OrderByDescending(x => x.Severidad))
                {
                    var bg = i++ % 2 == 0 ? "#ffffff" : "#f6f9fd";
                    Celda(tbl, bg).Text(r.Descripcion).FontSize(7.5f);
                    Celda(tbl, bg).Text(r.Categoria.ToString()).FontSize(7.5f);
                    Celda(tbl, bg).Text($"{r.Severidad} ({r.NivelSeveridad})").FontSize(7.5f);
                    Celda(tbl, bg).Text(r.Estrategia.ToString()).FontSize(7.5f);
                    Celda(tbl, bg).Text(Ni(r.Responsable)).FontSize(7.5f);
                    Celda(tbl, bg).Text(r.Estado.ToString()).FontSize(7.5f);
                }
            });
        });
    }

    // ── 6. Documentación ───────────────────────────────────────────────────────
    private static void Documentos(ColumnDescriptor col, IReadOnlyList<DocumentoProyectoDto> docs)
    {
        Seccion(col, "5", $"Documentación ({docs.Count})", body =>
        {
            if (docs.Count == 0)
            {
                body.Item().Text("El repositorio del proyecto está vacío.").FontColor(Gris).Italic();
                return;
            }

            // El PDF lista la documentación; no la incrusta. Un expediente con veinte adjuntos
            // pesaría cientos de megas y dejaría de poder enviarse por correo, que es justamente
            // para lo que se descarga.
            foreach (var grupo in docs.GroupBy(d => d.Categoria).OrderBy(g => g.Key))
            {
                body.Item().PaddingTop(5).Text(grupo.Key).Bold().FontColor(Azul).FontSize(9f);
                foreach (var d in grupo.OrderBy(x => x.Titulo))
                {
                    var v = d.Vigente;
                    body.Item().PaddingTop(1).Text(txt =>
                    {
                        txt.DefaultTextStyle(s => s.FontSize(8f));
                        txt.Span($"• {d.Titulo}");
                        if (v is not null)
                            txt.Span($"  —  v{v.Numero}, {v.ArchivoNombre}, subido el {v.SubidoEn.ToLocalTime():dd/MM/yyyy} por {v.SubidoPor}")
                               .FontColor(Gris);
                        if (d.FueCorregido)
                            txt.Span($"  ({d.TotalVersiones} versiones)").FontColor(GrisTenue);
                    });
                }
            }
        });
    }

    // ── 7. Vínculos ────────────────────────────────────────────────────────────
    private static void Vinculos(ColumnDescriptor col, VinculosProyectoDto v)
    {
        Seccion(col, "6", "Vínculos", body =>
        {
            if (!v.HayAlguno)
            {
                body.Item().Text("El proyecto no tiene reuniones, expedientes ni tickets vinculados.")
                    .FontColor(Gris).Italic();
                return;
            }

            foreach (var r in v.Reuniones)
                body.Item().Text($"• Reunión: {r.Titulo}  —  {Fecha(r.Fecha)}  ·  {Ni(r.Institucion)}").FontSize(8f);
            foreach (var e in v.Expedientes)
                body.Item().Text($"• Expediente {e.Codigo}  —  {e.Institucion}  ·  {e.Estado}").FontSize(8f);
            foreach (var t in v.Tickets)
                body.Item().Text($"• Ticket {t.Numero}: {t.Titulo}  —  {t.Estado}  ·  {t.Prioridad}").FontSize(8f);

            // Los que quedan fuera del alcance de quien descarga se cuentan, no se detallan: el
            // PDF no puede mostrar más de lo que la pantalla le muestra a esa persona.
            var fuera = v.ReunionesFueraDeAlcance + v.ExpedientesFueraDeAlcance + v.TicketsFueraDeAlcance;
            if (fuera > 0)
                body.Item().PaddingTop(3)
                    .Text($"Hay {fuera} vínculo(s) más fuera de su alcance, no detallados aquí.")
                    .FontSize(7.5f).FontColor(GrisTenue).Italic();
        });
    }

    // ── 8. Auditoría ───────────────────────────────────────────────────────────
    private static void Auditoria(ColumnDescriptor col, IReadOnlyList<BitacoraProyectoDto> auditoria)
    {
        Seccion(col, "7", $"Auditoría de la ficha ({auditoria.Count})", body =>
        {
            if (auditoria.Count == 0)
            {
                body.Item().Text("Sin movimientos registrados.").FontColor(Gris).Italic();
                return;
            }

            foreach (var a in auditoria.OrderByDescending(x => x.Fecha))
                body.Item().PaddingTop(2).Text(txt =>
                {
                    txt.DefaultTextStyle(s => s.FontSize(8f));
                    txt.Span($"{a.Fecha.ToLocalTime():dd/MM/yyyy HH:mm} · ").FontColor(Gris);
                    txt.Span($"[{a.Etiqueta}] ").Bold().FontColor(AzulMed);
                    txt.Span(a.Detalle);
                    txt.Span($"  — {a.Actor}").FontColor(Gris);
                });
        });
    }

    // ── Helpers de maquetado ───────────────────────────────────────────────────
    private static void Seccion(ColumnDescriptor col, string num, string titulo, Action<ColumnDescriptor> contenido)
    {
        col.Item().PaddingTop(12).Text($"{num}. {titulo}").FontSize(11.5f).Bold().FontColor(Azul);
        col.Item().PaddingTop(2).PaddingBottom(4).LineHorizontal(0.8f).LineColor("#d7e0ea");
        col.Item().Column(contenido);
    }

    private static void Dato(ColumnDescriptor col, string etiqueta, string? valor)
    {
        if (!Hay(valor)) return;
        col.Item().PaddingBottom(2).Text(txt =>
        {
            txt.DefaultTextStyle(s => s.FontSize(9f));
            txt.Span($"{etiqueta}: ").Bold().FontColor(Azul);
            txt.Span(valor);
        });
    }

    private static IContainer EncabezadoCelda(IContainer c) => c.Background(AzulMed).Padding(3);

    private static IContainer Celda(TableDescriptor tbl, string bg) =>
        tbl.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(3);

    // ── Helpers de datos ───────────────────────────────────────────────────────
    private static bool Hay(string? s) => !string.IsNullOrWhiteSpace(s);
    private static string Ni(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();
    private static string Fecha(DateOnly? f) => f?.ToString("dd/MM/yyyy") ?? "—";

    private static string? PrimeraNoVacia(params string?[] vals) =>
        vals.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string? Combinar(string? a, string? b, string sep)
    {
        var parts = new[] { a, b }.Where(Hay).ToArray();
        return parts.Length == 0 ? null : string.Join(sep, parts);
    }
}
