using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Application.Usuarios.Common;

public sealed record UsuarioListItemDto(
    Guid Id, string Nombre, string Correo, string Rol, bool Activo, DateTime FechaCreacion);

public sealed record UsuarioDetailDto(
    Guid Id, string Nombre, string Correo, string Rol, bool Activo,
    IReadOnlyList<string> Instituciones, IReadOnlyList<int> TemaIds);
