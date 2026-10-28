using System.Text;
using System.Text.RegularExpressions;

namespace Diger.TramitesEstado.Application.Reuniones.Memoria;

/// <summary>La transcripción más reciente de una reunión de Teams, ya en texto llano.</summary>
public sealed record TranscripcionTeams(string Texto, DateTime CreadaElUtc);

/// <summary>
/// De dónde sale la transcripción de Teams (fase 2, parte B). Siempre actúa con la conexión de
/// <paramref name="organizadorId"/>: Graph solo deja leer las transcripciones a quien organizó la
/// reunión en línea, y esa reunión nació de la cita que el portal creó en su calendario.
/// Los fallos llegan como <see cref="TranscripcionNoDisponibleException"/>,
/// <see cref="Outlook.OutlookConexionVencidaException"/> o <see cref="Outlook.OutlookNoDisponibleException"/>.
/// </summary>
public interface ITranscripcionesTeams
{
    Task<TranscripcionTeams> ObtenerUltimaAsync(Guid organizadorId, string enlaceTeams, CancellationToken ct);
}

/// <summary>No hay transcripción que entregar, por una razón que la persona puede entender y
/// resolver: el mensaje se le muestra tal cual (por el MCP, a la IA que se lo dice).</summary>
public sealed class TranscripcionNoDisponibleException(string message) : Exception(message);

/// <summary>
/// Teams entrega la transcripción en WebVTT: un bloque por frase, con marcas de tiempo y el
/// hablante en <c>&lt;v Nombre&gt;</c>. Para la ayuda memoria sobran los tiempos y sobra repetir
/// el nombre en cada frase, así que se deja «Nombre: texto» y se juntan las frases seguidas de la
/// misma persona. Es también lo que más reduce el tamaño de lo que se le pasa a la IA.
/// </summary>
public static partial class TranscripcionVtt
{
    [GeneratedRegex(@"^<v(?:\.[^\s>]*)?\s+([^>]+)>(.*?)(?:</v>)?$")]
    private static partial Regex Hablante();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Etiqueta();

    public static string ATexto(string vtt)
    {
        var lineas = (vtt ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lineas.Length == 0 || !lineas[0].TrimStart('﻿').StartsWith("WEBVTT", StringComparison.Ordinal))
            return (vtt ?? string.Empty).Trim();

        var frases = new List<(string? Quien, string Texto)>();
        var enTexto = false;   // pasada la línea «-->» del bloque actual
        foreach (var cruda in lineas.Skip(1))
        {
            var linea = cruda.Trim();
            if (linea.Length == 0) { enTexto = false; continue; }
            if (linea.Contains("-->", StringComparison.Ordinal)) { enTexto = true; continue; }
            if (!enTexto) continue;   // id del bloque, NOTE, STYLE…

            string? quien = null;
            var m = Hablante().Match(linea);
            if (m.Success) { quien = m.Groups[1].Value.Trim(); linea = m.Groups[2].Value; }
            var texto = System.Net.WebUtility.HtmlDecode(Etiqueta().Replace(linea, string.Empty)).Trim();
            if (texto.Length == 0) continue;

            // Una línea sin <v> dentro del mismo bloque sigue siendo de quien venía hablando.
            if (frases.Count > 0 && (quien is null || quien == frases[^1].Quien))
                frases[^1] = (frases[^1].Quien, frases[^1].Texto + " " + texto);
            else
                frases.Add((quien, texto));
        }

        var sb = new StringBuilder();
        foreach (var (quien, texto) in frases)
            sb.AppendLine(quien is null ? texto : $"{quien}: {texto}");
        return sb.ToString().TrimEnd();
    }
}
