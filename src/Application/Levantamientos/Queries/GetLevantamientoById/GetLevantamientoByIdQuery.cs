using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Levantamientos.Common;

namespace Diger.TramitesEstado.Application.Levantamientos.Queries.GetLevantamientoById;

public sealed record GetLevantamientoByIdQuery(int Id) : IRequest<LevantamientoDetailDto>;

public sealed class GetLevantamientoByIdQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetLevantamientoByIdQuery, LevantamientoDetailDto>
{
    public async Task<LevantamientoDetailDto> Handle(
        GetLevantamientoByIdQuery q, CancellationToken ct)
    {
        var l = await ctx.Levantamientos
            .AsNoTracking()
            .Include(x => x.Tramites.OrderBy(t => t.Orden))
            .Include(x => x.Equipo.OrderBy(m => m.Orden))
            .Include(x => x.Documentos.OrderBy(d => d.FechaRegistro))
            .FirstOrDefaultAsync(x => x.Id == q.Id, ct)
            ?? throw new NotFoundException(nameof(Levantamiento), q.Id);

        return new LevantamientoDetailDto(
            l.Id, l.Institucion, l.Encargado, l.Correo, l.Celular,
            l.Estado, l.ObsEstado,
            l.MigradaSOL, l.Limitante, l.LimitanteObs,
            l.Personal, l.PersonalObs,
            l.RequiereAcompanamiento, l.Habilidad, l.HabilidadObs,
            l.ObsGenerales, l.CreatedAt, l.UpdatedAt,
            l.Tramites.Select(t => new TramiteChecklistDto(
                t.Id, t.NombreTramite, t.Orden,
                t.ActaFirmada, t.RequiereMejoras, t.TieneInstructivo,
                t.Socializado, t.Observaciones)).ToList(),
            l.Equipo.Select(m => new MiembroEquipoDto(
                m.Id, m.Funcion, m.Nombre, m.Contacto, m.Orden)).ToList(),
            l.Documentos.Select(d => new DocumentoAdjuntoDto(
                d.Id, d.Nombre, d.Tipo, d.Url, d.FechaDocumento, d.FechaRegistro)).ToList());
    }
}
