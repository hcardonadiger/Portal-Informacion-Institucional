using System.Text.RegularExpressions;
using Diger.TramitesEstado.Application.Reuniones.Common;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Diger.TramitesEstado.Infrastructure.Reports;

/// <summary>
/// Genera el "Registro de Reunión" en PDF con QuestPDF, replicando el formato institucional:
/// bloque de título, secciones numeradas (Datos generales, Participantes, Objetivo, Desarrollo,
/// Acuerdos y compromisos, Documentos y recursos) y pie de página con paginación.
/// </summary>
public sealed class ActaPdfService : IActaPdfService
{
    private const string Azul     = "#1a3a5c";
    private const string AzulMed  = "#2e6da4";
    private const string Gris     = "#666666";
    private const string GrisTenue = "#888888";
    private const string Linea    = "#d7e0ea";

    public byte[] Generar(ActaPdfDto dto)
    {
        Settings.License = LicenseType.Community;

        var d = dto.Datos;
        var institucion = PrimeraNoVacia(dto.InstitucionNombre, d.InstitucionId);
        var fechaTxt = d.Fecha?.ToString("yyyy-MM-dd");
        var subtitulo = string.Join("  •  ",
            new[] { institucion, Combinar(fechaTxt, d.Hora, " ") }.Where(Hay));

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial").FontColor("#1c2333").LineHeight(1.35f));

                page.Content().Column(col =>
                {
                    // ── Bloque de título ─────────────────────────────────────
                    col.Item().Text(Hay(d.Titulo) ? d.Titulo : "Registro de Reunión")
                        .FontSize(18).Bold().FontColor(Azul);
                    if (Hay(subtitulo))
                        col.Item().PaddingTop(2).Text(subtitulo).FontSize(10).FontColor(Gris);
                    col.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(AzulMed);
                    col.Item().PaddingBottom(6);

                    // ── 1. Datos generales ───────────────────────────────────
                    Seccion(col, "1", "Datos Generales", body =>
                    {
                        Dato(body, "Fecha", Combinar(fechaTxt, d.Hora, "  •  "));
                        Dato(body, "Modalidad", Combinar(d.Modalidad, Hay(d.Duracion) ? $"Duración: {d.Duracion}" : null, "  •  "));
                        Dato(body, "Lugar", d.Lugar);
                        Dato(body, "Institución", institucion);
                        Dato(body, "Tipo de evento", d.Tipo);
                    });

                    // ── 2. Participantes ─────────────────────────────────────
                    if (dto.Asistentes.Count > 0)
                    {
                        Seccion(col, "2", "Participantes", body =>
                        {
                            body.Item().Table(tbl =>
                            {
                                tbl.ColumnsDefinition(c =>
                                {
                                    c.ConstantColumn(24);   // N°
                                    c.RelativeColumn(3);    // Nombre
                                    c.RelativeColumn(3);    // Cargo
                                    c.RelativeColumn(2);    // Institución
                                    c.RelativeColumn(3);    // Correo
                                });

                                tbl.Header(h =>
                                {
                                    foreach (var lbl in new[] { "N°", "Nombre", "Cargo", "Institución", "Correo" })
                                        h.Cell().Element(EncabezadoCelda).Text(lbl).FontColor(Colors.White).FontSize(8.5f).Bold();
                                });

                                var i = 0;
                                foreach (var a in dto.Asistentes)
                                {
                                    var bg = i % 2 == 0 ? "#ffffff" : "#f5f8fd";
                                    i++;
                                    Celda(tbl, bg).Text((i).ToString()).FontSize(8.5f);
                                    Celda(tbl, bg).Text(Ni(a.Nombre)).FontSize(8.5f);
                                    Celda(tbl, bg).Text(Ni(a.Cargo)).FontSize(8.5f);
                                    Celda(tbl, bg).Text(Ni(a.Institucion)).FontSize(8.5f);
                                    Celda(tbl, bg).Text(Ni(a.Correo)).FontSize(8.5f);
                                }
                            });
                        });
                    }

                    // ── 3. Objetivo de la reunión ────────────────────────────
                    var objetivo = PrimeraNoVacia(d.ObjetivoAgenda, d.ObjetivoCap);
                    if (Hay(objetivo))
                        Seccion(col, "3", "Objetivo de la Reunión", body =>
                            body.Item().Text(objetivo).Justify());

                    // ── 4. Desarrollo de la reunión ──────────────────────────
                    var parrafos = HtmlAParrafos(d.Desarrollo);
                    if (parrafos.Count > 0)
                        Seccion(col, "4", "Desarrollo de la Reunión", body =>
                        {
                            foreach (var p in parrafos)
                                body.Item().PaddingBottom(6).Text(p).Justify();
                        });

                    // ── 5. Acuerdos y compromisos ────────────────────────────
                    if (dto.Acuerdos.Count > 0)
                    {
                        Seccion(col, "5", "Acuerdos y Compromisos", body =>
                        {
                            body.Item().Table(tbl =>
                            {
                                tbl.ColumnsDefinition(c =>
                                {
                                    c.ConstantColumn(24);   // No.
                                    c.RelativeColumn(6);    // Acuerdo / Compromiso
                                    c.RelativeColumn(2);    // Responsable
                                    c.ConstantColumn(70);   // Plazo
                                });

                                tbl.Header(h =>
                                {
                                    foreach (var lbl in new[] { "No.", "Acuerdo / Compromiso", "Responsable", "Plazo" })
                                        h.Cell().Element(EncabezadoCelda).Text(lbl).FontColor(Colors.White).FontSize(8.5f).Bold();
                                });

                                var i = 0;
                                foreach (var ac in dto.Acuerdos)
                                {
                                    var bg = i % 2 == 0 ? "#ffffff" : "#f5f8fd";
                                    i++;
                                    Celda(tbl, bg).Text(i.ToString()).FontSize(8.5f);
                                    Celda(tbl, bg).Text(Ni(ac.Compromiso)).FontSize(8.5f);
                                    Celda(tbl, bg).Text(Ni(ac.Responsable)).FontSize(8.5f);
                                    Celda(tbl, bg).Text(ac.Plazo?.ToString("yyyy-MM-dd") ?? "—").FontSize(8.5f);
                                }
                            });
                        });
                    }

                    // ── 6. Documentos y recursos ─────────────────────────────
                    var docs = Lineas(d.DocsRecursos);
                    if (docs.Count > 0)
                        Seccion(col, "6", "Documentos y Recursos", body =>
                        {
                            if (docs.Count == 1)
                                body.Item().Text(docs[0]).Justify();
                            else
                                foreach (var l in docs)
                                    body.Item().Row(r =>
                                    {
                                        r.ConstantItem(12).Text("•").FontColor(AzulMed);
                                        r.RelativeItem().Text(l);
                                    });
                        });

                    // ── Cierre ───────────────────────────────────────────────
                    col.Item().PaddingTop(18).AlignCenter()
                        .Text("Generado por el Sistema DIGER - Plataforma SOL")
                        .FontSize(8).Italic().FontColor(GrisTenue);
                });

                // ── Pie de página ────────────────────────────────────────────
                page.Footer().PaddingTop(6).BorderTop(0.5f).BorderColor(Linea).Row(row =>
                {
                    row.RelativeItem().PaddingTop(4).Text("DIGER - Registro de Reunión")
                        .FontSize(7.5f).FontColor(GrisTenue);
                    row.ConstantItem(70).PaddingTop(4).AlignRight().Text(txt =>
                    {
                        txt.Span("Pág ").FontSize(7.5f).FontColor(GrisTenue);
                        txt.CurrentPageNumber().FontSize(7.5f).FontColor(GrisTenue);
                        txt.Span(" / ").FontSize(7.5f).FontColor(GrisTenue);
                        txt.TotalPages().FontSize(7.5f).FontColor(GrisTenue);
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }

    // ── Helpers de maquetación ─────────────────────────────────────────────────
    private static void Seccion(ColumnDescriptor col, string num, string titulo, Action<ColumnDescriptor> cuerpo)
    {
        col.Item().PaddingTop(10).Text($"{num}. {titulo}").FontSize(12).Bold().FontColor(Azul);
        col.Item().PaddingTop(2).LineHorizontal(1).LineColor(Linea);
        col.Item().PaddingTop(6).Column(cuerpo);
    }

    private static void Dato(ColumnDescriptor col, string etiqueta, string? valor)
    {
        if (!Hay(valor)) return;
        col.Item().PaddingBottom(2).Text(txt =>
        {
            txt.Span($"{etiqueta}: ").Bold().FontColor(Azul);
            txt.Span(valor);
        });
    }

    private static IContainer EncabezadoCelda(IContainer c) => c.Background(AzulMed).Padding(4);

    private static IContainer Celda(TableDescriptor tbl, string bg) =>
        tbl.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#e2e8f0").Padding(4);

    // ── Helpers de datos ───────────────────────────────────────────────────────
    private static bool Hay(string? s) => !string.IsNullOrWhiteSpace(s);
    private static string Ni(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();

    private static string? PrimeraNoVacia(params string?[] vals) =>
        vals.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string? Combinar(string? a, string? b, string sep)
    {
        var parts = new[] { a, b }.Where(Hay).ToArray();
        return parts.Length == 0 ? null : string.Join(sep, parts);
    }

    /// <summary>Convierte HTML del editor enriquecido a párrafos de texto plano.</summary>
    private static IReadOnlyList<string> HtmlAParrafos(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return Array.Empty<string>();
        var s = html;
        s = Regex.Replace(s, @"<\s*br\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"</\s*(p|div|li|h[1-6]|tr)\s*>", "\n\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*li[^>]*>", "• ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", "");
        s = System.Net.WebUtility.HtmlDecode(s);
        return Regex.Split(s, @"\n\s*\n")
            .Select(p => Regex.Replace(p.Replace("\r", " ").Replace("\n", " ").Trim(), @"\s+", " "))
            .Where(p => p.Length > 0)
            .ToList();
    }

    /// <summary>Divide un texto multilínea en líneas no vacías (para listas simples).</summary>
    private static IReadOnlyList<string> Lineas(string? s) =>
        string.IsNullOrWhiteSpace(s)
            ? Array.Empty<string>()
            : s.Replace("\r\n", "\n").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
}
