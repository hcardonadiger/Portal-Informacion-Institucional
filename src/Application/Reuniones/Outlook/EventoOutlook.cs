using System.Security.Cryptography;
using System.Text;

namespace Diger.TramitesEstado.Application.Reuniones.Outlook;

public sealed record InvitadoOutlook(string Correo, string? Nombre);

/// <summary>La cita tal como la verá el invitado. Solo datos: ni ids de Graph ni tokens.</summary>
public sealed record EventoOutlook(
    string Asunto,
    DateTime Inicio,
    DateTime Fin,
    string? Ubicacion,
    string CuerpoHtml,
    IReadOnlyList<InvitadoOutlook> Invitados,
    bool EsTeams)
{
    /// <summary>Nombre de Windows de la zona de America/Tegucigalpa, que es el que pide Graph.</summary>
    public const string ZonaHoraria = "Central America Standard Time";

    /// <summary>
    /// SHA-256 en hex de la forma canónica. Invitados por correo en minúsculas y ordenados: el
    /// orden en que se capturaron no es un cambio que justifique avisar a todos.
    /// </summary>
    public string Huella()
    {
        var sb = new StringBuilder()
            .Append(Asunto).Append('\n')
            .Append(Inicio.ToString("s")).Append('\n')
            .Append(Fin.ToString("s")).Append('\n')
            .Append(Ubicacion).Append('\n')
            .Append(CuerpoHtml).Append('\n')
            .Append(EsTeams ? '1' : '0').Append('\n');

        foreach (var correo in Invitados.Select(i => i.Correo.Trim().ToLowerInvariant()).Order(StringComparer.Ordinal))
            sb.Append(correo).Append(';');

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }
}
