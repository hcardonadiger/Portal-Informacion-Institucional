namespace Diger.TramitesEstado.Application.Common.Interfaces;

/// <summary>Un archivo que viaja adjunto al correo.</summary>
public sealed record AdjuntoCorreo(string Nombre, string TipoContenido, byte[] Contenido);

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string bodyHtml, CancellationToken ct = default);

    /// <summary>
    /// Envía con adjuntos. Es una sobrecarga y no un parámetro opcional a propósito: los siete
    /// llamados que ya existían pasan el <c>CancellationToken</c> como cuarto argumento posicional,
    /// y agregarle un parámetro antes los habría roto a todos.
    /// </summary>
    Task SendEmailAsync(
        string to, string subject, string bodyHtml,
        IReadOnlyList<AdjuntoCorreo> adjuntos, CancellationToken ct = default);
}
