using Diger.TramitesEstado.Domain.Common;

namespace Diger.TramitesEstado.Domain.Entities;

public sealed class AsignacionUsuario : BaseAuditableEntity<Guid>
{
    public Guid    UsuarioId     { get; private set; }
    public string  InstitucionId { get; private set; } = default!;
    public string? AreaId        { get; private set; }
    public string? UnidadId      { get; private set; }
    public string  Rol           { get; private set; } = default!;

    private AsignacionUsuario() { }

    public static AsignacionUsuario Crear(Guid usuarioId, string institucionId, string? areaId, string? unidadId, string rol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rol);

        return new AsignacionUsuario
        {
            Id            = Guid.NewGuid(),
            UsuarioId     = usuarioId,
            InstitucionId = institucionId.Trim().ToUpper(),
            AreaId        = areaId?.Trim().ToUpper(),
            UnidadId      = unidadId?.Trim().ToUpper(),
            Rol           = rol.Trim()
        };
    }

    public void CambiarRol(string nuevoRol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nuevoRol);
        Rol = nuevoRol.Trim();
    }

    public void CambiarAsignacion(string institucionId, string? areaId, string? unidadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionId);
        InstitucionId = institucionId.Trim().ToUpper();
        AreaId        = areaId?.Trim().ToUpper();
        UnidadId      = unidadId?.Trim().ToUpper();
    }
}
