namespace Diger.TramitesEstado.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string bodyHtml, CancellationToken ct = default);
}
