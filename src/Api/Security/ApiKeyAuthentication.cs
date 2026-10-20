using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Api.Security;

/// <summary>Clave estática por cliente (decisión P-02). Adecuado para un solo
/// consumidor conocido; si algún día hay más de uno, esto sube a algo más robusto (OAuth/JWT).</summary>
public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
}

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var provided) ||
            string.IsNullOrWhiteSpace(provided))
            return Task.FromResult(AuthenticateResult.Fail($"Falta la cabecera {ApiKeyAuthenticationOptions.HeaderName}."));

        // P-03 (todavía abierta): en Development vive en appsettings/user-secrets;
        // dónde vive en producción está pendiente de decidir.
        var esperada = configuration["PortalDigitalApi:ApiKey"];
        if (string.IsNullOrEmpty(esperada))
            return Task.FromResult(AuthenticateResult.Fail("La API no tiene una clave configurada del lado del servidor."));

        if (!string.Equals(provided.ToString(), esperada, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.Fail("Clave inválida."));

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "cliente-api-portaldigital")], ApiKeyAuthenticationOptions.Scheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeyAuthenticationOptions.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
