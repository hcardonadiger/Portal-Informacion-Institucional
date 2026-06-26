using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Reuniones.Common;

namespace Diger.TramitesEstado.Application.Reuniones.Queries.GetReunionById;

public sealed record ReunionDetailDto(
    int Id, ReunionFormDto Datos, List<AsistenteInput> Asistentes, List<AcuerdoInput> Acuerdos,
    string? InstitucionNombre);

public sealed record GetReunionByIdQuery(int Id) : IRequest<ReunionDetailDto>;

public sealed class GetReunionByIdQueryHandler(IReunionRepository repo)
    : IRequestHandler<GetReunionByIdQuery, ReunionDetailDto>
{
    public async Task<ReunionDetailDto> Handle(GetReunionByIdQuery q, CancellationToken ct)
    {
        var r = await repo.GetByIdWithDetailsAsync(q.Id, ct)
            ?? throw new NotFoundException(nameof(Reunion), q.Id);

        var (datos, asistentes, acuerdos) = ReunionMapper.ToForm(r);
        return new ReunionDetailDto(r.Id, datos, asistentes, acuerdos, r.Institucion);
    }
}
