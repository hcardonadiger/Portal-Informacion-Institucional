namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Asignación de un usuario a una institución (alcance de acceso).
/// Un usuario puede tener varias; define qué registros puede ver/gestionar
/// (Coordinador/Técnico). El Administrador no usa esta tabla (acceso global).</summary>
public sealed class UsuarioInstitucion : BaseEntity
{
    public int UsuarioId    { get; set; }
    public int InstitucionId { get; set; }

    private UsuarioInstitucion() { }

    public static UsuarioInstitucion Crear(int usuarioId, int institucionId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(usuarioId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(institucionId);
        return new UsuarioInstitucion { UsuarioId = usuarioId, InstitucionId = institucionId };
    }
}
