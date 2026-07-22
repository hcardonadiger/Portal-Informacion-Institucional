using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diger.TramitesEstado.Application.AI;
using Diger.TramitesEstado.Application.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Infrastructure.AI;

public sealed class AgenteService(
    HttpClient http,
    IOptions<AgenteOptions> options,
    ILogger<AgenteService> logger) : IAgenteService
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private const string PromptBase =
        """
        Eres el Asistente Virtual de Soporte del sistema SOL de DIGER (Dirección General
        de Recursos Humanos del Gobierno de Honduras). Atiendes consultas de funcionarios
        públicos mientras un técnico especializado se conecta a la sesión.

        Puedes orientar sobre:
        - Uso general del sistema SOL: expedientes, trámites, tickets y reuniones
        - Estado o seguimiento de trámites y expedientes
        - Pasos para registrar o consultar información en el sistema

        Restricciones estrictas:
        - No puedes realizar acciones directas en el sistema ni modificar datos
        - Si el usuario pide cambios de datos o acciones administrativas, indícale que
          el técnico lo asistirá en breve
        - No compartas credenciales ni información de seguridad
        - Si no sabes algo con certeza, sé honesto y dilo
        - Respuestas concisas: máximo 3 párrafos cortos
        - Siempre en español, tono profesional y amable
        """;

    private sealed record ApiMsg(string role, string content);

    public async Task<string?> ResponderAsync(ChatSesionDetalleDto sesion, CancellationToken ct = default)
    {
        var opts = options.Value;
        if (!opts.Habilitado) return null;

        var mensajes = ConstruirMensajes(sesion.Mensajes);
        if (mensajes is null) return null;

        var systemPrompt = PromptBase;
        if (sesion.Sesion.TemaNombre is { } tema)
            systemPrompt += $"\n\nContexto de la consulta: el usuario pregunta sobre el tema «{tema}».";

        var cuerpo = new
        {
            model      = opts.Model,
            max_tokens = opts.MaxTokens,
            system     = systemPrompt,
            messages   = mensajes,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/messages");
        req.Headers.Add("x-api-key", opts.ApiKey);
        req.Content = JsonContent.Create(cuerpo, options: _json);

        try
        {
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Agente IA: respuesta {Status} — {Error}", res.StatusCode, err);
                return null;
            }

            using var doc = await JsonDocument.ParseAsync(
                await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            return doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Agente IA: error al llamar la API de Anthropic");
            return null;
        }
    }

    // La API de Anthropic exige alternancia estricta user/assistant.
    // Filtramos mensajes de sistema y fusionamos los consecutivos del mismo rol.
    private static List<ApiMsg>? ConstruirMensajes(IReadOnlyList<ChatMensajeDto> mensajes)
    {
        var pares = mensajes
            .Where(m => !m.EsSistema)
            .Select(m => (role: m.EsDelTecnico ? "assistant" : "user", m.Texto))
            .ToList();

        if (pares.Count == 0 || pares[0].role != "user") return null;

        var resultado = new List<ApiMsg>();
        foreach (var (role, texto) in pares)
        {
            if (resultado.Count > 0 && resultado[^1].role == role)
                resultado[^1] = resultado[^1] with { content = resultado[^1].content + "\n" + texto };
            else
                resultado.Add(new ApiMsg(role, texto));
        }

        // Debe terminar con mensaje del usuario (el agente responde a él)
        return resultado[^1].role == "user" ? resultado : null;
    }
}
