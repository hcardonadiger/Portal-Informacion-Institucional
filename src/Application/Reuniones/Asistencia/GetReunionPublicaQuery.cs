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
        //
        // El catálogo se pide saltándose el alcance a propósito. Esta consulta la sirve una página
        // anónima —quien escanea el QR no tiene sesión—, así que el filtro global de Institucion
        // resolvía `i.Id == null` y devolvía CERO instituciones por las dos ramas: el formulario de
        // auto-registro se quedaba sin una sola opción que elegir. El recorte que sí importa acá lo
        // hace idsConvocados, que es la lista que el organizador definió en la reunión.
        var idsConvocados = r.InstitucionesParticipantes.Select(x => x.InstitucionId).ToList();
        var catalogo = await institucionRepo.GetActivasParaConvocarAsync(ct);
        var institucionesActivas = idsConvocados.Count > 0
            ? catalogo.Where(i => idsConvocados.Contains(i.Id)).ToList()
            : catalogo;
        var insts = institucionesActivas.Select(i => i.Nombre).OrderBy(n => n).ToList();

        return new ReunionPublicaDto(
            r.RegistroToken, r.Titulo, r.Fecha, r.Hora, r.Modalidad, r.Lugar,
            r.Institucion, r.RegistroAbierto, r.Asistentes.Count, insts);
    }
}
