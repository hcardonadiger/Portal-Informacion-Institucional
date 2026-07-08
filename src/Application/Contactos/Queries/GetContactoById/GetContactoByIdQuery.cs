using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Contactos.Queries.GetContactos;

namespace Diger.TramitesEstado.Application.Contactos.Queries.GetContactoById;

public sealed record GetContactoByIdQuery(int Id) : IRequest<ContactoDto>;

public sealed class GetContactoByIdQueryHandler(IContactoRepository repo)
    : IRequestHandler<GetContactoByIdQuery, ContactoDto>
{
    public async Task<ContactoDto> Handle(GetContactoByIdQuery q, CancellationToken ct)
    {
        var c = await repo.GetByIdAsync(q.Id, ct)
            ?? throw new NotFoundException(nameof(Contacto), q.Id);

        return new ContactoDto(c.Id, c.Nombre, c.InstitucionId, c.Institucion, c.Cargo, c.Correo, c.Telefono, c.Notas, c.Origen, c.Activo);
    }
}
