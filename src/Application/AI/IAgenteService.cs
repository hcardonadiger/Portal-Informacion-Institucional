using Diger.TramitesEstado.Application.Chat;

namespace Diger.TramitesEstado.Application.AI;

public sealed class AgenteOptions
{
    public string ApiKey    { get; init; } = "";
    public string Model     { get; init; } = "claude-haiku-4-5-20251001";
    public int    MaxTokens { get; init; } = 400;

    public bool Habilitado => !string.IsNullOrWhiteSpace(ApiKey);
}

public interface IAgenteService
{
    /// <summary>
    /// Genera una respuesta automática para una sesión en cola sin técnico asignado.
    /// Devuelve null si el agente está deshabilitado, si no corresponde responder,
    /// o si ocurre un error en la API.
    /// </summary>
    Task<string?> ResponderAsync(ChatSesionDetalleDto sesion, CancellationToken ct = default);
}
