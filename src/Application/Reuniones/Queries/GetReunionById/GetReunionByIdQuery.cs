using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Reuniones.Common;

namespace Diger.TramitesEstado.Application.Reuniones.Queries.GetReunionById;

public sealed record ReunionDetailDto(
    int Id, ReunionFormDto Datos, List<AsistenteInput> Asistentes, List<AcuerdoInput> Acuerdos,
    string? InstitucionNombre, IReadOnlyList<InstitucionRefDto> InstitucionesParticipantes);

public sealed record GetReunionByIdQuery(int Id) : IRequest<ReunionDetailDto>;

public sealed class GetReunionByIdQueryHandler(IReunionRepository repo, IInstitucionRepository institucionRepo)
    : IRequestHandler<GetReunionByIdQuery, ReunionDetailDto>
{
    public async Task<ReunionDetailDto> Handle(GetReunionByIdQuery q, CancellationToken ct)
    {
        var r = await repo.GetByIdWithDetailsAsync(q.Id, ct)
            ?? throw new NotFoundException(nameof(Reunion), q.Id);

        var (datos, asistentes, acuerdos) = ReunionMapper.ToForm(r);

        var ids = r.InstitucionesParticipantes.OrderBy(x => x.Orden).Select(x => x.InstitucionId).ToList();
        var nombresPorId = ids.Count == 0
            ? []
            : (await institucionRepo.GetByIdsAsync(ids, ct)).ToDictionary(i => i.Id, i => i.Nombre);
        var participantes = ids
            .Select(id => new InstitucionRefDto(id, nombresPorId.TryGetValue(id, out var n) ? n : "(institución eliminada)"))
            .ToList();

        return new ReunionDetailDto(r.Id, datos, asistentes, acuerdos, r.Institucion, participantes);
    }
}
