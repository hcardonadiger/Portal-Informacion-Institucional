using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Application.Usuarios.Common;

public sealed record UsuarioListItemDto(
    Guid Id, string Nombre, string Correo, string Rol, bool Activo, DateTime FechaCreacion);

public sealed record UsuarioDetailDto(
    Guid Id, string Nombre, string Correo, string Rol, bool Activo, string? CertificadoThumbprint,
    IReadOnlyList<AsignacionDto> Asignaciones, IReadOnlyList<int> TemaIds, string? Telefono = null);
