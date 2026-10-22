using System.Security.Cryptography;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

namespace Diger.TramitesEstado.Infrastructure.Outlook;

/// <summary>
/// OAuth delegado con MSAL (authorization code + PKCE). Un ConfidentialClientApplication por
/// operación, con la caché enganchada a la fila del usuario: MSAL renueva el token en silencio y
/// la caché nueva se cifra y se guarda en la misma operación.
///
/// <para>Nunca registra tokens ni la caché. Los logs llevan el id del usuario y el tipo de error.</para>
/// </summary>
public sealed class ConexionMicrosoftService(
    IOptions<MicrosoftGraphOptions> opciones,
    AppDbContext ctx,
    CacheTokensProtector protector,
    ILogger<ConexionMicrosoftService> log) : IConexionMicrosoft
{
    // offline_access, openid y profile los agrega MSAL: pasarlos aquí es un error de MSAL.
    internal static readonly string[] Alcances =
    [
        "https://graph.microsoft.com/Calendars.ReadWrite",
        "https://graph.microsoft.com/User.Read"
    ];

    private readonly MicrosoftGraphOptions _o = opciones.Value;

    public bool EstaConfigurada => _o.EstaCompleta;

    private IConfidentialClientApplication Cliente(string? urlRetorno, byte[]? cacheClara, Action<byte[]>? alCambiar)
    {
        if (!_o.EstaCompleta)
            throw new OutlookNoDisponibleException("La integración con Outlook no está configurada en este servidor.");

        var b = ConfidentialClientApplicationBuilder.Create(_o.ClientId)
            .WithTenantId(_o.TenantId)
            .WithClientSecret(_o.ClientSecret);
        if (urlRetorno is not null) b = b.WithRedirectUri(urlRetorno);
        var app = b.Build();

        app.UserTokenCache.SetBeforeAccess(args =>
        {
            if (cacheClara is not null) args.TokenCache.DeserializeMsalV3(cacheClara);
        });
        app.UserTokenCache.SetAfterAccess(args =>
        {
            if (args.HasStateChanged) alCambiar?.Invoke(args.TokenCache.SerializeMsalV3());
        });
        return app;
    }

    public async Task<InicioConexionDto> IniciarAsync(string urlRetorno, CancellationToken ct)
    {
        var estado = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var url = await Cliente(urlRetorno, null, null)
            .GetAuthorizationRequestUrl(Alcances)
            .WithPkce(out var verificador)
            .WithPrompt(Prompt.SelectAccount)
            // MSAL 4.91 marcó obsoleta la sobrecarga Dictionary<string,string>; esta es la vigente.
            // El estado solo viaja en la URL: no tiene por qué entrar en la llave de la caché.
            .WithExtraQueryParameters(new Dictionary<string, (string, bool)> { ["state"] = (estado, false) })
            .ExecuteAsync(ct);
        return new InicioConexionDto(url.ToString(), estado, verificador);
    }

    public async Task CompletarAsync(Guid usuarioId, string codigo, string verificador, string urlRetorno, CancellationToken ct)
    {
        byte[]? cacheNueva = null;
        var app = Cliente(urlRetorno, null, c => cacheNueva = c);

        AuthenticationResult res;
        try
        {
            res = await app.AcquireTokenByAuthorizationCode(Alcances, codigo)
                .WithPkceCodeVerifier(verificador)
                .ExecuteAsync(ct);
        }
        catch (MsalException ex)
        {
            log.LogWarning("Conexión Outlook rechazada para {UsuarioId}: {Codigo}", usuarioId, ex.ErrorCode);
            throw new OutlookNoDisponibleException($"Microsoft rechazó la conexión ({ex.ErrorCode}).");
        }

        var cifrada = protector.Cifrar(cacheNueva ?? []);
        var fila = await ctx.ConexionesMicrosoft.FirstOrDefaultAsync(c => c.Id == usuarioId, ct);
        if (fila is null)
            ctx.ConexionesMicrosoft.Add(ConexionMicrosoft.Crear(usuarioId, res.Account.Username, res.Account.HomeAccountId.Identifier, cifrada, DateTime.UtcNow));
        else
            fila.Reconectar(res.Account.Username, res.Account.HomeAccountId.Identifier, cifrada, DateTime.UtcNow);

        await ctx.SaveChangesAsync(ct);
    }

    public async Task<string> ObtenerTokenAsync(Guid usuarioId, CancellationToken ct)
    {
        var fila = await ctx.ConexionesMicrosoft.FirstOrDefaultAsync(c => c.Id == usuarioId, ct)
            ?? throw new OutlookConexionVencidaException("El usuario no tiene Outlook conectado.");
        if (fila.RequiereReconexion)
            throw new OutlookConexionVencidaException(fila.UltimoError ?? "Requiere reconexión.");

        var clara = protector.Descifrar(fila.CacheTokens);
        if (clara is null)
            return await VencerAsync(fila, "La caché no se pudo descifrar (¿cambiaron las llaves del servidor?).", ct);

        byte[]? cacheNueva = null;
        var app = Cliente(null, clara, c => cacheNueva = c);

        try
        {
            var cuenta = await app.GetAccountAsync(fila.HomeAccountId);
            if (cuenta is null)
                return await VencerAsync(fila, "La cuenta no está en la caché.", ct);

            var res = await app.AcquireTokenSilent(Alcances, cuenta).ExecuteAsync(ct);
            if (cacheNueva is not null)
            {
                fila.GuardarCache(protector.Cifrar(cacheNueva));
                await ctx.SaveChangesAsync(ct);
            }
            return res.AccessToken;
        }
        catch (MsalUiRequiredException ex)
        {
            return await VencerAsync(fila, ex.ErrorCode, ct);
        }
        catch (MsalServiceException ex)
        {
            log.LogWarning("Microsoft no entregó token para {UsuarioId}: {Codigo}", usuarioId, ex.ErrorCode);
            throw new OutlookNoDisponibleException($"Microsoft no respondió ({ex.ErrorCode}).");
        }
    }

    private async Task<string> VencerAsync(ConexionMicrosoft fila, string motivo, CancellationToken ct)
    {
        fila.MarcarVencida(motivo);
        await ctx.SaveChangesAsync(ct);
        log.LogInformation("Conexión Outlook vencida para {UsuarioId}: {Motivo}", fila.Id, motivo);
        throw new OutlookConexionVencidaException(motivo);
    }
}
