using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Application.Usuarios.Common;

public sealed record UsuarioListItemDto(
    int Id, string Nombre, string Correo, RolUsuario Rol, bool Activo, DateTime FechaCreacion);

public sealed record UsuarioDetailDto(
    int Id, string Nombre, string Correo, RolUsuario Rol, bool Activo, IReadOnlyList<int> Instituciones);
