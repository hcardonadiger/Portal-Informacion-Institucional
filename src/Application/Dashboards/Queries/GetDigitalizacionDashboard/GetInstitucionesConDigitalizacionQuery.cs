using Diger.TramitesEstado.Application.Expedientes.Seguimiento;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetDigitalizacionDashboard;

/// <summary>Opciones para el filtro de institución del Tablero de Digitalización: solo las que
/// tienen algún trámite vigente en un expediente, no el catálogo completo — una institución sin
/// trámites en digitalización no aporta nada que filtrar y solo ensucia la lista.</summary>
public sealed record InstitucionDigitalizacionDto(string Id, string Nombre);

public sealed record GetInstitucionesConDigitalizacionQuery : IRequest<IReadOnlyList<InstitucionDigitalizacionDto>>;

public sealed class GetInstitucionesConDigitalizacionQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetInstitucionesConDigitalizacionQuery, IReadOnlyList<InstitucionDigitalizacionDto>>
{
    public async Task<IReadOnlyList<InstitucionDigitalizacionDto>> Handle(
        GetInstitucionesConDigitalizacionQuery query, CancellationToken ct)
    {
        // Mismo criterio que el propio tablero (GetDigitalizacionDashboardQueryHandler): los
        // legados sin FechaApertura confiable no cuentan, y un expediente sin trámites no aporta
        // ninguna institución que valga la pena ofrecer en el filtro.
        var idsConTramites = ctx.Expedientes.AsNoTracking()
            .Where(e => e.FechaApertura != null && e.FechaApertura >= CorteLegado.Fecha && e.Tramites.Any())
            .Select(e => e.InstitucionId)
            .Distinct();

        return await ctx.Instituciones.AsNoTracking()
            .Where(i => i.Activo && idsConTramites.Contains(i.Id))
            .OrderBy(i => i.Nombre)
            .Select(i => new InstitucionDigitalizacionDto(i.Id, i.Nombre))
            .ToListAsync(ct);
    }
}
