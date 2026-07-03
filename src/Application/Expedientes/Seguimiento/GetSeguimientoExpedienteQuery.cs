using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Expedientes.Seguimiento;

public sealed record TramiteItem(int Index, string Nombre);

public sealed record SeguimientoExpedienteDto(
    int ExpedienteId, string Codigo, string Institucion,
    IReadOnlyList<TramiteItem> Tramites, int TramiteActual,
    IReadOnlyDictionary<string, int>  Estados,
    IReadOnlyDictionary<string, bool> Aplica);

/// <summary>Seguimiento de un trámite (por su índice) dentro del expediente.</summary>
public sealed record GetSeguimientoExpedienteQuery(int ExpedienteId, int? TramiteIndex = null)
    : IRequest<SeguimientoExpedienteDto>;

public sealed class GetSeguimientoExpedienteQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetSeguimientoExpedienteQuery, SeguimientoExpedienteDto>
{
    public async Task<SeguimientoExpedienteDto> Handle(GetSeguimientoExpedienteQuery q, CancellationToken ct)
    {
        // ctx.Expedientes está filtrado por alcance → 404 si no es visible.
        var exp = await ctx.Expedientes.AsNoTracking()
            .Where(e => e.Id == q.ExpedienteId)
            .Select(e => new { e.Id, e.Codigo, e.Institucion })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Expediente), q.ExpedienteId);

        var tramites = await ctx.Tramites.AsNoTracking()
            .Where(t => t.ExpedienteId == q.ExpedienteId)
            .OrderBy(t => t.TramiteIndex)
            .Select(t => new TramiteItem(t.TramiteIndex, t.NombreTramite))
            .ToListAsync(ct);

        // Trámite seleccionado: el solicitado (si existe), si no el primero.
        var actual = q.TramiteIndex is int ti && tramites.Any(x => x.Index == ti)
            ? ti
            : tramites.Count > 0 ? tramites[0].Index : 0;

        var filas = await ctx.ExpedienteEtapaAvances.AsNoTracking()
            .Where(a => a.ExpedienteId == q.ExpedienteId && a.TramiteIndex == actual)
            .ToListAsync(ct);

        var estados = new Dictionary<string, int>();
        var aplica  = new Dictionary<string, bool>();
        foreach (var f in filas)
        {
            if (f.SubId.StartsWith("APLICA:"))
                aplica[f.SubId["APLICA:".Length..]] = f.Estado == 1;
            else
                estados[f.SubId] = f.Estado;
        }

        return new SeguimientoExpedienteDto(exp.Id, exp.Codigo, exp.Institucion, tramites, actual, estados, aplica);
    }
}
