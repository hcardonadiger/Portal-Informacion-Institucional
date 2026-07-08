using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Reuniones.Asistencia;

/// <summary>Lista de asistencia de una reunión para el organizador (respeta el alcance institucional).</summary>
public sealed record GetAsistenciaQuery(int ReunionId) : IRequest<AsistenciaAdminDto>;

public sealed class GetAsistenciaQueryHandler(IReunionRepository repo, IInstitucionRepository institucionRepo)
    : IRequestHandler<GetAsistenciaQuery, AsistenciaAdminDto>
{
    public async Task<AsistenciaAdminDto> Handle(GetAsistenciaQuery q, CancellationToken ct)
    {
        var r = await repo.GetByIdWithDetailsAsync(q.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), q.ReunionId);

        var asistentes = r.Asistentes
            .OrderByDescending(a => a.RegistradoEl ?? DateTime.MinValue)
            .ThenBy(a => a.Nombre)
            .Select(a => new AsistenteVm(
                a.Id, a.Nombre, a.Cargo, a.Institucion, a.Departamento,
                a.Correo, a.Telefono, a.AutoRegistro, a.RegistradoEl))
            .ToList();

        var institucionIds = r.InstitucionesParticipantes.Select(x => x.InstitucionId).ToList();
        var institucionesNombres = institucionIds.Count == 0
            ? []
            : (await institucionRepo.GetByIdsAsync(institucionIds, ct)).Select(i => i.Nombre).OrderBy(n => n).ToList();

        return new AsistenciaAdminDto(r.Id, r.Titulo, r.RegistroToken, r.RegistroAbierto,
            r.Convocados, r.InstitucionId, r.Institucion, institucionesNombres, asistentes);
    }
}
