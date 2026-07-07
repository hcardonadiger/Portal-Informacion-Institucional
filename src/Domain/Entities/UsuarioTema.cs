namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Tema de ticket que un usuario (especialista) atiende.
/// Un usuario puede atender varios; define su alcance ("Sus temas") para ver y tomar tickets.</summary>
public sealed class UsuarioTema : BaseEntity
{
    public Guid UsuarioId { get; set; }
    public int TemaId    { get; set; }

    private UsuarioTema() { }

    public static UsuarioTema Crear(Guid usuarioId, int temaId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(temaId);
        return new UsuarioTema { UsuarioId = usuarioId, TemaId = temaId };
    }
}
