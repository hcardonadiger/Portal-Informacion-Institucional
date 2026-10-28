using System.Collections.Concurrent;
using System.Security.Cryptography;
using Diger.TramitesEstado.Application.Reuniones.Memoria;
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

    // Fase 2, parte B: localizar la reunión en línea por su enlace y leer sus transcripciones. El
    // segundo exige consentimiento de administrador; por eso solo se piden con «Transcripciones».
    internal static readonly string[] AlcancesTranscripciones =
    [
        "https://graph.microsoft.com/OnlineMeetings.Read",
        "https://graph.microsoft.com/OnlineMeetingTranscript.Read.All"
    ];

    /// <summary>Lo que se pide al conectar. El token de calendario sigue pidiendo solo
    /// <see cref="Alcances"/>: así una conexión de antes del permiso no se cae para el calendario.</summary>
    private string[] AlcancesAlConectar => _o.Transcripciones ? [.. Alcances, .. AlcancesTranscripciones] : Alcances;

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
        var app = Cliente(urlRetorno, null, null);
        try
        {
            var url = await app
                .GetAuthorizationRequestUrl(AlcancesAlConectar)
                .WithPkce(out var verificador)
                .WithPrompt(Prompt.SelectAccount)
                // MSAL 4.91 marcó obsoleta la sobrecarga Dictionary<string,string>; esta es la vigente.
                // El estado solo viaja en la URL: no tiene por qué entrar en la llave de la caché.
                .WithExtraQueryParameters(new Dictionary<string, (string, bool)> { ["state"] = (estado, false) })
                .ExecuteAsync(ct);
            // AbsoluteUri y no ToString(): ToString() des-escapa los %20 que separan los alcances y
            // la cabecera Location salía con espacios literales — una URL inválida que el navegador
            // suele reparar pero un proxy o una pasarela pueden romper. Medido el 2026-10-23.
            return new InicioConexionDto(url.AbsoluteUri, estado, verificador);
        }
        catch (MsalException ex)
        {
            // Como en CompletarAsync: el código de error, nunca ex.Message.
            log.LogWarning("No se pudo armar la URL de conexión Outlook: {Codigo}", ex.ErrorCode);
            throw new OutlookNoDisponibleException($"Microsoft rechazó la conexión ({ex.ErrorCode}).");
        }
    }

    public async Task CompletarAsync(Guid usuarioId, string codigo, string verificador, string urlRetorno, CancellationToken ct)
    {
        byte[]? cacheNueva = null;
        var app = Cliente(urlRetorno, null, c => cacheNueva = c);

        AuthenticationResult res;
        try
        {
            res = await app.AcquireTokenByAuthorizationCode(AlcancesAlConectar, codigo)
                .WithPkceCodeVerifier(verificador)
                .ExecuteAsync(ct);
        }
        catch (MsalException ex)
        {
            log.LogWarning("Conexión Outlook rechazada para {UsuarioId}: {Codigo}", usuarioId, ex.ErrorCode);
            throw new OutlookNoDisponibleException($"Microsoft rechazó la conexión ({ex.ErrorCode}).");
        }

        if (cacheNueva is null)
            throw new OutlookNoDisponibleException("Microsoft no devolvió una caché de tokens.");

        var cifrada = protector.Cifrar(cacheNueva);
        var fila = await ctx.ConexionesMicrosoft.FirstOrDefaultAsync(c => c.Id == usuarioId, ct);
        if (fila is null)
            ctx.ConexionesMicrosoft.Add(ConexionMicrosoft.Crear(usuarioId, res.Account.Username, res.Account.HomeAccountId.Identifier, cifrada, DateTime.UtcNow));
        else
            fila.Reconectar(res.Account.Username, res.Account.HomeAccountId.Identifier, cifrada, DateTime.UtcNow);

        await ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Un candado por usuario alrededor de leer caché → renovar en silencio → guardar caché. MSAL
    /// rota el refresh token al renovar: con dos escritores en paralelo, el último podía guardar una
    /// caché ya vieja y dejar la conexión inservible. El portal corre en un solo proceso, así que un
    /// candado en memoria por usuario basta; una granja de servidores necesitaría un rowversion.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Candados = new();

    public Task<string> ObtenerTokenAsync(Guid usuarioId, CancellationToken ct) =>
        TokenAsync(usuarioId, Alcances, paraTranscripciones: false, ct);

    public Task<string> ObtenerTokenTranscripcionesAsync(Guid usuarioId, CancellationToken ct)
    {
        if (!_o.Transcripciones)
            throw new TranscripcionNoDisponibleException(
                "La descarga de transcripciones de Teams no está activada en este servidor. Pídale a la persona que pegue la transcripción.");
        return TokenAsync(usuarioId, AlcancesTranscripciones, paraTranscripciones: true, ct);
    }

    private async Task<string> TokenAsync(Guid usuarioId, string[] alcances, bool paraTranscripciones, CancellationToken ct)
    {
        var candado = Candados.GetOrAdd(usuarioId, _ => new SemaphoreSlim(1, 1));
        await candado.WaitAsync(ct);
        try
        {
            // Se lee dentro del candado: así se ve la caché que dejó quien renovó antes.
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

                var res = await app.AcquireTokenSilent(alcances, cuenta).ExecuteAsync(ct);
                if (cacheNueva is not null)
                {
                    fila.GuardarCache(protector.Cifrar(cacheNueva));
                    await ctx.SaveChangesAsync(ct);
                }
                return res.AccessToken;
            }
            catch (MsalUiRequiredException ex) when (paraTranscripciones)
            {
                // Lo normal en una conexión hecha antes de encender las transcripciones: el refresh
                // token no cubre el permiso nuevo. Para el calendario sigue sirviendo, así que la
                // fila no se marca vencida; solo se pide reconectar una vez.
                log.LogInformation("Conexión de {UsuarioId} sin permiso de transcripciones: {Codigo}", usuarioId, ex.ErrorCode);
                throw new TranscripcionNoDisponibleException(
                    "La conexión de Outlook de quien organizó la reunión es anterior al permiso de transcripciones: " +
                    "tiene que reconectarse una vez desde su perfil.");
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
            catch (MsalClientException ex)
            {
                // Falla de MSAL del lado del cliente sobre la caché guardada (p. ej. una caché que se
                // descifra pero está malformada): la conexión guardada no sirve, hay que reconectar.
                // Nunca ex.Message: podría llevar fragmentos de tokens.
                log.LogWarning("MSAL no pudo usar la caché guardada de {UsuarioId}: {Codigo}", usuarioId, ex.ErrorCode);
                return await VencerAsync(fila, ex.ErrorCode, ct);
            }
        }
        finally
        {
            candado.Release();
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
