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

        var insts = (await institucionRepo.GetAllActivasAsync(ct)).Select(i => i.Nombre).ToList();

        return new ReunionPublicaDto(
            r.RegistroToken, r.Titulo, r.Fecha, r.Hora, r.Modalidad, r.Lugar,
            r.Institucion, r.RegistroAbierto, r.Asistentes.Count, insts);
    }
}
