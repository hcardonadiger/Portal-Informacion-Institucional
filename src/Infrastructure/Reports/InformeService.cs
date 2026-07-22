using ClosedXML.Excel;
using Diger.TramitesEstado.Application.Informes;
using Diger.TramitesEstado.Application.Informes.Common;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Diger.TramitesEstado.Infrastructure.Reports;

public sealed class InformeService : IInformeService
{
    private static readonly string[] EstadoLabels =
    [
        "", "En Exploración", "En Levantamiento", "En Modelado", "En Validación", "Cerrado"
    ];

    private static string EstadoLabel(EstadoExpediente e) =>
        (int)e >= 1 && (int)e <= 5 ? EstadoLabels[(int)e] : e.ToString();

    // ── PDF ───────────────────────────────────────────────────────────────────
    public byte[] GenerarPdf(InformeInstitucionDto dto)
    {
        Settings.License = LicenseType.Community;

        var periodo = dto.Desde is not null || dto.Hasta is not null
            ? $"{dto.Desde?.ToString("dd/MM/yyyy") ?? "–"} al {dto.Hasta?.ToString("dd/MM/yyyy") ?? "–"}"
            : "Sin filtro de período";

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                // ── Encabezado ─────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("GOBIERNO DE LA REPÚBLICA DE HONDURAS")
                                .FontSize(7).FontColor("#666666");
                            c.Item().Text("DIGER – Digitalización de Trámites del Estado")
                                .FontSize(12).Bold().FontColor("#1a3a5c");
                            c.Item().Text("Informe de Estado por Institución")
                                .FontSize(10).FontColor("#2e6da4");
                        });
                        row.ConstantItem(160).AlignRight().Column(c =>
                        {
                            c.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                                .FontSize(7).FontColor("#888888");
                            c.Item().Text($"Período: {periodo}")
                                .FontSize(7).FontColor("#555555");
                        });
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#2e6da4");
                });

                // ── Contenido ──────────────────────────────────────────────
                page.Content().PaddingTop(8).Column(col =>
                {
                    // Institución
                    col.Item().Text(dto.InstitucionNombre)
                        .FontSize(11).Bold().FontColor("#1a3a5c");

                    col.Item().PaddingTop(6).Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(60);  // Indicador
                            c.ConstantColumn(55);  // Valor
                            c.ConstantColumn(60);
                            c.ConstantColumn(55);
                            c.ConstantColumn(60);
                            c.ConstantColumn(55);
                            c.ConstantColumn(60);
                            c.ConstantColumn(55);
                        });

                        void MetricaCell(string label, string valor, string bg = "#e8f0fb")
                        {
                            tbl.Cell().Background(bg).Padding(4).Column(c =>
                            {
                                c.Item().Text(label).FontSize(7).FontColor("#444444");
                                c.Item().Text(valor).FontSize(13).Bold().FontColor("#1a3a5c");
                            });
                        }

                        MetricaCell("Total expedientes", dto.Expedientes.Count.ToString());
                        MetricaCell("Total trámites",    dto.TotalTramites.ToString(), "#f0f8f0");
                        MetricaCell("Avance promedio",   $"{dto.AvancePromedio}%", "#fff8e8");
                        MetricaCell("Cerrados",          dto.Cerrados.ToString(), "#e8fbe8");
                        MetricaCell("En Validación",     dto.EnValidacion.ToString());
                        MetricaCell("En Modelado",       dto.EnModelado.ToString(), "#f0f8f0");
                        MetricaCell("En Levantamiento",  dto.EnLevantamiento.ToString(), "#fff8e8");
                        MetricaCell("En Exploración",    dto.EnExploracion.ToString(), "#fce8e8");
                    });

                    col.Item().PaddingTop(10).Text("Detalle de Expedientes")
                        .FontSize(9).Bold().FontColor("#1a3a5c");

                    col.Item().PaddingTop(4).Table(tbl =>
                    {
                        tbl.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(70);   // Código
                            c.RelativeColumn(3);    // Trámite / nombre
                            c.ConstantColumn(80);   // Estado
                            c.ConstantColumn(90);   // Analista
                            c.ConstantColumn(65);   // F. Apertura
                            c.ConstantColumn(45);   // Avance
                        });

                        // Cabecera
                        static IContainer HeaderCell(IContainer c) =>
                            c.Background("#2e6da4").Padding(4);

                        tbl.Header(h =>
                        {
                            foreach (var lbl in new[] { "Código", "Nombre del trámite", "Estado", "Analista", "F. Apertura", "Avance" })
                                h.Cell().Element(HeaderCell).Text(lbl).FontColor(Colors.White).FontSize(8).Bold();
                        });

                        var rowBg1 = "#ffffff";
                        var rowBg2 = "#f5f8fd";
                        var rowBgSub = "#fafcff";
                        int rowIdx = 0;

                        foreach (var exp in dto.Expedientes)
                        {
                            var bg = rowIdx % 2 == 0 ? rowBg1 : rowBg2;
                            rowIdx++;

                            tbl.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#dddddd")
                                .Padding(3).Text(exp.Codigo).FontSize(8).Bold();

                            var nombreTramites = exp.Tramites.Count == 1
                                ? exp.Tramites[0].NombreTramite
                                : $"{exp.Tramites.Count} trámites";

                            tbl.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#dddddd")
                                .Padding(3).Text(nombreTramites).FontSize(8);

                            tbl.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#dddddd")
                                .Padding(3).Text(EstadoLabel(exp.Estado)).FontSize(8);

                            tbl.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#dddddd")
                                .Padding(3).Text(exp.Analista).FontSize(8);

                            tbl.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#dddddd")
                                .Padding(3).Text(exp.FechaApertura?.ToString("dd/MM/yyyy") ?? "—").FontSize(8);

                            tbl.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#dddddd")
                                .Padding(3).Text($"{exp.AvancePct}%").FontSize(8).Bold();

                            // Sub-filas por trámite (cuando hay >1)
                            if (exp.Tramites.Count > 1)
                            {
                                foreach (var t in exp.Tramites)
                                {
                                    tbl.Cell().Background(rowBgSub)
                                        .PaddingLeft(10).PaddingVertical(2)
                                        .Text("↳").FontSize(7).FontColor("#888888");

                                    tbl.Cell().ColumnSpan(4).Background(rowBgSub)
                                        .PaddingLeft(4).PaddingVertical(2)
                                        .Text(t.NombreTramite).FontSize(7).FontColor("#444444");

                                    tbl.Cell().Background(rowBgSub)
                                        .PaddingVertical(2).PaddingRight(3)
                                        .Text($"{t.AvancePct}%").FontSize(7).FontColor("#2e6da4");
                                }
                            }
                        }
                    });
                });

                // ── Pie de página ──────────────────────────────────────────
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("DIGER – Informe generado automáticamente")
                        .FontSize(7).FontColor("#aaaaaa");
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

        return doc.GeneratePdf();
    }

    // ── Excel ─────────────────────────────────────────────────────────────────
    public byte[] GenerarExcel(InformeInstitucionDto dto)
    {
        using var wb = new XLWorkbook();
        wb.Style.Font.FontName = "Calibri";
        wb.Style.Font.FontSize = 10;

        var periodo = dto.Desde is not null || dto.Hasta is not null
            ? $"{dto.Desde?.ToString("dd/MM/yyyy") ?? "–"} al {dto.Hasta?.ToString("dd/MM/yyyy") ?? "–"}"
            : "Sin filtro";

        // ── Hoja 1: Resumen ────────────────────────────────────────────────
        var wsRes = wb.Worksheets.Add("Resumen");
        wsRes.Cell(1, 1).Value = "DIGER – Digitalización de Trámites";
        wsRes.Cell(1, 1).Style.Font.Bold = true;
        wsRes.Cell(1, 1).Style.Font.FontSize = 14;
        wsRes.Cell(2, 1).Value = $"Informe: {dto.InstitucionNombre}";
        wsRes.Cell(2, 1).Style.Font.FontSize = 11;
        wsRes.Cell(3, 1).Value = $"Período: {periodo}";
        wsRes.Cell(4, 1).Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
        wsRes.Cell(4, 1).Style.Font.FontColor = XLColor.Gray;

        void ResumenFila(IXLWorksheet ws, int fila, string label, string valor)
        {
            ws.Cell(fila, 1).Value = label;
            ws.Cell(fila, 2).Value = valor;
            ws.Cell(fila, 1).Style.Font.Bold = true;
        }

        ResumenFila(wsRes, 6,  "Total expedientes",   dto.Expedientes.Count.ToString());
        ResumenFila(wsRes, 7,  "Total trámites",      dto.TotalTramites.ToString());
        ResumenFila(wsRes, 8,  "Avance promedio",     $"{dto.AvancePromedio}%");
        ResumenFila(wsRes, 9,  "Cerrados",            dto.Cerrados.ToString());
        ResumenFila(wsRes, 10, "En Validación",       dto.EnValidacion.ToString());
        ResumenFila(wsRes, 11, "En Modelado",         dto.EnModelado.ToString());
        ResumenFila(wsRes, 12, "En Levantamiento",    dto.EnLevantamiento.ToString());
        ResumenFila(wsRes, 13, "En Exploración",      dto.EnExploracion.ToString());

        wsRes.Columns(1, 2).AdjustToContents();

        // ── Hoja 2: Expedientes ────────────────────────────────────────────
        var wsExp = wb.Worksheets.Add("Expedientes");
        var hdrs = new[] { "Código", "Estado", "Analista", "F. Apertura", "Trámites", "Avance %", "Registro" };
        for (int i = 0; i < hdrs.Length; i++)
        {
            var cell = wsExp.Cell(1, i + 1);
            cell.Value = hdrs[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2e6da4");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int rowExp = 2;
        foreach (var exp in dto.Expedientes)
        {
            wsExp.Cell(rowExp, 1).Value = exp.Codigo;
            wsExp.Cell(rowExp, 2).Value = EstadoLabel(exp.Estado);
            wsExp.Cell(rowExp, 3).Value = exp.Analista;
            wsExp.Cell(rowExp, 4).Value = exp.FechaApertura?.ToString("dd/MM/yyyy") ?? "";
            wsExp.Cell(rowExp, 5).Value = exp.Tramites.Count;
            wsExp.Cell(rowExp, 6).Value = $"{exp.AvancePct}%";
            wsExp.Cell(rowExp, 7).Value = exp.CreatedAt.ToString("dd/MM/yyyy");

            if (rowExp % 2 == 0)
                wsExp.Row(rowExp).Style.Fill.BackgroundColor = XLColor.FromHtml("#f5f8fd");

            rowExp++;
        }

        wsExp.Columns().AdjustToContents();
        wsExp.SheetView.FreezeRows(1);

        // ── Hoja 3: Trámites ───────────────────────────────────────────────
        var wsTr = wb.Worksheets.Add("Tramites");
        var hdrsTr = new[] { "Código Exp.", "Trámite", "Estado Exp.", "Analista", "Pasos totales", "Pasos completados", "Avance %" };
        for (int i = 0; i < hdrsTr.Length; i++)
        {
            var cell = wsTr.Cell(1, i + 1);
            cell.Value = hdrsTr[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2e6da4");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int rowTr = 2;
        foreach (var exp in dto.Expedientes)
        {
            foreach (var t in exp.Tramites)
            {
                wsTr.Cell(rowTr, 1).Value = exp.Codigo;
                wsTr.Cell(rowTr, 2).Value = t.NombreTramite;
                wsTr.Cell(rowTr, 3).Value = EstadoLabel(exp.Estado);
                wsTr.Cell(rowTr, 4).Value = exp.Analista;
                wsTr.Cell(rowTr, 5).Value = t.TotalPasos;
                wsTr.Cell(rowTr, 6).Value = t.PasosCompletados;
                wsTr.Cell(rowTr, 7).Value = $"{t.AvancePct}%";

                if (rowTr % 2 == 0)
                    wsTr.Row(rowTr).Style.Fill.BackgroundColor = XLColor.FromHtml("#f5f8fd");

                rowTr++;
            }
        }

        wsTr.Columns().AdjustToContents();
        wsTr.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
