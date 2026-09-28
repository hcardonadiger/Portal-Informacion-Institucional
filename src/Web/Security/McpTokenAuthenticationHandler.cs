using System.Security.Claims;
using System.Text.Encodings.Web;
using Diger.TramitesEstado.Application.Usuarios.Queries.AutenticarUsuario;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Web.Security;

/// <summary>
/// Autentica las peticiones del servidor MCP con <c>Authorization: Bearer pdt_...</c>.
///
/// La identidad que emite es la MISMA que emite el login web —la arma
/// <see cref="SesionClaimsFactory"/>— y eso es todo el diseño: como
/// <c>CurrentUserService</c> lee solo de los claims, el aislamiento institucional del
/// <c>AppDbContext</c> y la matriz de permisos aplican a estas peticiones sin una línea
/// de código adicional. Acá no se reimplementa seguridad; se reusa la que ya hay.
/// </summary>
public sealed class McpTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceScopeFactory scopeFactory,
    ITokenMcpHasher hasher,
    TimeProvider reloj)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "McpBearer";

    /// <summary>Cada cuánto se refresca el sello de último uso. Sin esto habría un UPDATE
    /// por cada petición MCP solo para mover un timestamp que se mira una vez al mes.</summary>
    private static readonly TimeSpan RefrescoDeUso = TimeSpan.FromMinutes(5);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var cabecera))
            return AuthenticateResult.NoResult();

        var valor = cabecera.ToString();
        if (!valor.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token   = valor["Bearer ".Length..].Trim();
        var prefijo = hasher.LeerPrefijo(token);
        if (prefijo is null)
            return AuthenticateResult.Fail("El token no tiene el formato esperado.");

        // ── Ámbito propio, y esto NO es opcional ──────────────────────────────────────
        //
        // AppDbContext fija su alcance en campos readonly al construirse
        // (`_alcanceGlobal = currentUser.EsGlobal`), y CurrentUserService.EsGlobal devuelve
        // true mientras no haya autenticación — que es justo el momento en que corre este
        // handler. Si acá se tocara el DbContext de la petición, esa instancia quedaría
        // clavada en alcance GLOBAL, y como es scoped la reusarían después las herramientas
        // MCP: verían el portal entero y el aislamiento institucional desaparecería en
        // silencio, sin error y sin prueba que fallara.
        //
        // Con un ámbito aparte que se desecha acá, el DbContext de la petición nace recién
        // cuando una herramienta lo pide, ya con la identidad puesta y el alcance correcto.
        using var scope = scopeFactory.CreateScope();
        var ctx    = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var fila = await ctx.TokensAccesoMcp.FirstOrDefaultAsync(t => t.Prefijo == prefijo);
        if (fila is null)
            return AuthenticateResult.Fail("Token desconocido.");

        if (!hasher.Verificar(fila.HashSha256, token))
            return AuthenticateResult.Fail("Token inválido.");

        var ahora = reloj.GetUtcNow().UtcDateTime;

        if (fila.EstaRevocado)
            return AuthenticateResult.Fail("El token fue revocado.");

        if (!fila.EsUtilizable(ahora))
            return AuthenticateResult.Fail("El token está vencido.");

        // Identidad fresca de la base: el token dice a quién representa, no qué puede hacer.
        var identidad = await sender.Send(new ResolverIdentidadPorTokenQuery(fila.UsuarioId, fila.AsignacionId));
        if (identidad is null)
            return AuthenticateResult.Fail("La cuenta del token ya no está habilitada.");

        if (fila.UltimoUsoUtc is null || ahora - fila.UltimoUsoUtc.Value > RefrescoDeUso)
        {
            fila.RegistrarUso(ahora);
            // Suelto y sin transacción: el DbContext se registra con EnableRetryOnFailure,
            // que es incompatible con transacciones explícitas. Y si este SaveChanges falla,
            // la petición debe seguir: perder un sello de uso no justifica negar el acceso.
            try
            {
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "No se pudo registrar el uso del token {Prefijo}.", prefijo);
            }
        }

        var identity  = SesionClaimsFactory.Crear(identidad.Usuario, Scheme, identidad.Activa);
        var principal = new ClaimsPrincipal(identity);

        // Solo el prefijo en el log. El token completo no debe quedar en ningún archivo.
        Logger.LogDebug("Petición MCP autenticada con el token {Prefijo} ({Usuario}).", prefijo, identidad.Usuario.Correo);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme));
    }

    /// <summary>
    /// Un 401 acá debe ser un 401. Sin este override manda el esquema por defecto —la
    /// cookie— que responde con un 302 al HTML de <c>/Cuenta/Login</c>; el cliente MCP lo
    /// recibe como una respuesta corrupta en vez de "credencial inválida".
    /// </summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        Response.ContentType = "application/json";
        return Response.WriteAsync("""{"error":"Falta un token válido en la cabecera Authorization: Bearer."}""");
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/json";
        return Response.WriteAsync("""{"error":"El token es válido pero la cuenta no tiene acceso a este recurso."}""");
    }
}
