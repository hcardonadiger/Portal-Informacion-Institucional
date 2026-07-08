using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Reuniones.Asistencia;

/// <summary>Datos públicos de la reunión para mostrar el formulario de auto-registro (acceso anónimo).</summary>
public sealed record GetReunionPublicaQuery(Guid Token) : IRequest<ReunionPublicaDto>;

public sealed class GetReunionPublicaQueryHandler(
    IReunionRepository repo, IInstitucionRepository institucionRepo)
    : IRequestHandler<GetReunionPublicaQuery, ReunionPublicaDto>
{
    public async Task<ReunionPublicaDto> Handle(GetReunionPublicaQuery q, CancellationToken ct)
    {
        var r = await repo.GetByTokenWithAsistentesAsync(q.Token, ct)
            ?? throw new NotFoundException(nameof(Reunion), q.Token);

        // Si la reunión tiene instituciones convocadas, la asistencia solo permite elegir entre
        // esas; si no se seleccionó ninguna (reuniones anteriores a esta funcionalidad), se ofrecen
        // todas las activas para no dejar el formulario sin opciones.
        var idsConvocados = r.InstitucionesParticipantes.Select(x => x.InstitucionId).ToList();
        var institucionesActivas = idsConvocados.Count > 0
            ? await institucionRepo.GetByIdsAsync(idsConvocados, ct)
            : await institucionRepo.GetAllActivasAsync(ct);
        var insts = institucionesActivas.Select(i => i.Nombre).OrderBy(n => n).ToList();

        return new ReunionPublicaDto(
            r.RegistroToken, r.Titulo, r.Fecha, r.Hora, r.Modalidad, r.Lugar,
            r.Institucion, r.RegistroAbierto, r.Asistentes.Count, insts);
    }
}
