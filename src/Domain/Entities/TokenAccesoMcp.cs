using Diger.TramitesEstado.Domain.Common;

namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Credencial para el servidor MCP: le permite a un agente (Claude) actuar en el portal
/// como el usuario dueño del token, con su misma institución, su mismo alcance y sus
/// mismos permisos.
///
/// Tres decisiones que vale la pena entender antes de tocar esto:
///
/// 1. **Se guarda el hash, nunca el token.** El valor completo existe una sola vez, en la
///    respuesta que lo genera. <see cref="Prefijo"/> es la parte pública: sirve para
///    encontrar la fila sin escanear la tabla y para que el dueño reconozca cuál es cuál
///    en la pantalla.
///
/// 2. **Queda atado a una <c>AsignacionUsuario</c> concreta.** Un usuario puede tener
///    varias —institución, área, unidad y rol distintos— y la web resuelve eso con el
///    selector de contexto. Un token no tiene dónde elegir, así que el contexto se fija
///    al emitirlo y no cambia. La asignación se vuelve a leer de la base en cada petición,
///    de modo que un cambio de rol o una baja apliquen de inmediato.
///
/// 3. **No implementa <c>ISoftDeletable</c>.** Acá la revocación ES el historial: un token
///    que se filtró y se revocó es justo lo que hay que poder mirar después. Borrar la
///    fila destruiría esa evidencia.
/// </summary>
public sealed class TokenAccesoMcp : BaseAuditableEntity<Guid>
{
    public Guid      UsuarioId    { get; private set; }
    public Guid      AsignacionId { get; private set; }
    public string    Nombre       { get; private set; } = default!;
    public string    Prefijo      { get; private set; } = default!;
    public string    HashSha256   { get; private set; } = default!;
    public DateTime? ExpiraUtc    { get; private set; }
    public DateTime? RevocadoUtc  { get; private set; }
    public DateTime? UltimoUsoUtc { get; private set; }

    private TokenAccesoMcp() { }

    public static TokenAccesoMcp Crear(
        Guid      usuarioId,
        Guid      asignacionId,
        string    nombre,
        string    prefijo,
        string    hashSha256,
        DateTime? expiraUtc)
    {
        if (usuarioId    == Guid.Empty) throw new ArgumentException("El token necesita un usuario.", nameof(usuarioId));
        if (asignacionId == Guid.Empty) throw new ArgumentException("El token necesita una asignación.", nameof(asignacionId));

        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefijo);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashSha256);

        // Un vencimiento ya pasado nace muerto: mejor rechazarlo acá que entregarle al
        // usuario un token que su primera petición va a rechazar.
        if (expiraUtc is { } exp && exp <= DateTime.UtcNow)
            throw new ArgumentException("La fecha de vencimiento ya pasó.", nameof(expiraUtc));

        return new TokenAccesoMcp
        {
            Id           = Guid.NewGuid(),
            UsuarioId    = usuarioId,
            AsignacionId = asignacionId,
            Nombre       = nombre.Trim(),
            Prefijo      = prefijo.Trim(),
            HashSha256   = hashSha256,
            ExpiraUtc    = expiraUtc
        };
    }

    /// <summary>Revocar es idempotente: la primera fecha es la que cuenta. Volver a
    /// revocar no debe correr el sello hacia adelante y perder cuándo pasó de verdad.</summary>
    public void Revocar()
    {
        RevocadoUtc ??= DateTime.UtcNow;
    }

    public void RegistrarUso(DateTime utcAhora) => UltimoUsoUtc = utcAhora;

    public bool EstaRevocado => RevocadoUtc is not null;

    public bool EsUtilizable(DateTime utcAhora) =>
        RevocadoUtc is null && (ExpiraUtc is null || ExpiraUtc > utcAhora);
}
