using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Instituciones.Queries.GetInstitucionById;

public sealed record InstitucionDetailDto(int Id, string Nombre, bool Activo, IReadOnlyList<string> Tramites);

public sealed record GetInstitucionByIdQuery(int Id) : IRequest<InstitucionDetailDto>;

public sealed class GetInstitucionByIdQueryHandler(IInstitucionRepository repo)
    : IRequestHandler<GetInstitucionByIdQuery, InstitucionDetailDto>
{
    public async Task<InstitucionDetailDto> Handle(GetInstitucionByIdQuery q, CancellationToken ct)
    {
        var inst = await repo.GetByIdWithTramitesAsync(q.Id, ct)
            ?? throw new NotFoundException(nameof(Institucion), q.Id);

        return new InstitucionDetailDto(
            inst.Id, inst.Nombre, inst.Activo,
            inst.Tramites.OrderBy(t => t.Orden).Select(t => t.Nombre).ToList());
    }
}
