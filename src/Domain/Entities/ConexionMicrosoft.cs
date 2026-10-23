namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// La cuenta Microsoft que un usuario conectó desde su perfil para que el portal escriba en su
/// calendario. Una por usuario: el <see cref="BaseEntity{TId}.Id"/> ES el id del usuario.
///
/// <para><see cref="CacheTokens"/> es la caché de MSAL ya cifrada con Data Protection; el dominio
/// la trata como bytes opacos. Si las llaves de Data Protection se pierden, la caché deja de poder
/// leerse y la conexión se trata como vencida: el usuario reconecta.</para>
///
/// <para>No es <c>ISoftDeletable</c>: desconectar borra la fila, porque lo único que guarda es
/// una credencial y una credencial que ya no se usa no debe quedar en la base.</para>
/// </summary>
public sealed class ConexionMicrosoft : BaseAuditableEntity<Guid>
{
    public string   Upn                { get; private set; } = default!;
    public string   HomeAccountId      { get; private set; } = default!;
    public byte[]   CacheTokens        { get; private set; } = [];
    public DateTime ConectadaEl        { get; private set; }
    public bool     RequiereReconexion { get; private set; }
    public string?  UltimoError        { get; private set; }

    private ConexionMicrosoft() { }

    public static ConexionMicrosoft Crear(Guid usuarioId, string upn, string homeAccountId, byte[] cacheCifrada, DateTime utcAhora)
    {
        if (usuarioId == Guid.Empty) throw new ArgumentException("La conexión necesita un usuario.", nameof(usuarioId));

        var c = new ConexionMicrosoft { Id = usuarioId };
        c.Reconectar(upn, homeAccountId, cacheCifrada, utcAhora);
        return c;
    }

    public void Reconectar(string upn, string homeAccountId, byte[] cacheCifrada, DateTime utcAhora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(upn);
        ArgumentException.ThrowIfNullOrWhiteSpace(homeAccountId);
        ArgumentNullException.ThrowIfNull(cacheCifrada);

        Upn                = upn.Trim().ToLowerInvariant();
        HomeAccountId      = homeAccountId;
        CacheTokens        = cacheCifrada;
        ConectadaEl        = utcAhora;
        RequiereReconexion = false;
        UltimoError        = null;
    }

    public void GuardarCache(byte[] cacheCifrada)
    {
        ArgumentNullException.ThrowIfNull(cacheCifrada);
        CacheTokens = cacheCifrada;
    }

    public void MarcarVencida(string motivo)
    {
        RequiereReconexion = true;
        UltimoError        = motivo.Length > 500 ? motivo[..500] : motivo;
    }
}
