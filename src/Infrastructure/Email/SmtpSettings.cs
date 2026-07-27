namespace Diger.TramitesEstado.Infrastructure.Email;

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "smtp.office365.com";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "noreply@diger.gob.hn";
    public string FromName { get; set; } = "Portal DIGER Trámites";
}
