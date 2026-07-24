using Diger.TramitesEstado.Application.Contactos.Queries.GetContactos;

namespace Diger.TramitesEstado.Application.Contactos.Queries.GetContactoPorCorreo;

/// <summary>Busca un contacto activo por correo en el directorio institucional, ignorando el
/// alcance del usuario (uso desde el auto-registro público de asistencia, sin sesión). Devuelve
/// <c>null</c> si no existe: no encontrarlo es un resultado esperado, no un error.</summary>
public sealed record GetContactoPorCorreoQuery(string Correo) : IRequest<ContactoDto?>;

public sealed class GetContactoPorCorreoQueryHandler(IContactoRepository repo)
    : IRequestHandler<GetContactoPorCorreoQuery, ContactoDto?>
{
    public async Task<ContactoDto?> Handle(GetContactoPorCorreoQuery q, CancellationToken ct)
    {
        var c = await repo.GetByCorreoAsync(q.Correo, ct);
        return c is null ? null : new ContactoDto(c.Id, c.Nombre, c.InstitucionId, c.Institucion, c.AreaId, c.Area, c.Cargo, c.Correo, c.Telefono, c.Notas, c.Origen, c.Activo);
    }
}
