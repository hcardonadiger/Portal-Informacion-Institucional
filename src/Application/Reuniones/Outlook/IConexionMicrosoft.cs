namespace Diger.TramitesEstado.Application.Reuniones.Outlook;

/// <summary>Lo que la página guarda entre ir a Microsoft y volver: el estado (anti-CSRF) y el
/// verificador PKCE. Nunca va a la base ni a un log.</summary>
public sealed record InicioConexionDto(string Url, string Estado, string Verificador);

/// <summary>
/// Conexión de un usuario con su cuenta Microsoft (OAuth delegado). La implementación es dueña
/// de la fila de <c>ConexionesMicrosoft</c>: MSAL renueva tokens en cada uso y la caché nueva
/// tiene que persistirse ahí mismo.
/// </summary>
public interface IConexionMicrosoft
{
    /// <summary>false si el servidor no tiene TenantId/ClientId/ClientSecret: la UI se apaga.</summary>
    bool EstaConfigurada { get; }

    Task<InicioConexionDto> IniciarAsync(string urlRetorno, CancellationToken ct);

    /// <summary>Canjea el código de Microsoft y crea o renueva la conexión del usuario.</summary>
    Task CompletarAsync(Guid usuarioId, string codigo, string verificador, string urlRetorno, CancellationToken ct);

    /// <summary>Token de acceso a Graph vigente. Lanza <see cref="OutlookConexionVencidaException"/>
    /// (y marca la fila) si hay que reconectar.</summary>
    Task<string> ObtenerTokenAsync(Guid usuarioId, CancellationToken ct);
}
