using Diger.TramitesEstado.Application.Reuniones.Common;

namespace Diger.TramitesEstado.Application.Reuniones.Import;

public sealed record ImportarReunionesResult(
    int Total, int Creadas, int Omitidas, IReadOnlyList<string> Errores);

/// <summary>Importa reuniones (+ asistencias) desde la fuente externa hacia SQL Server.
/// Idempotente por <c>OrigenExternoId</c>: las ya importadas se omiten.</summary>
public sealed record ImportarReunionesCommand(bool OmitirExistentes = true)
    : IRequest<ImportarReunionesResult>;

public sealed class ImportarReunionesCommandHandler(
    IReunionImportSource source,
    IReunionRepository repo,
    IInstitucionRepository institucionRepo,
    IContactoRepository contactoRepo,
    IApplicationDbContext ctx,
    IUnitOfWork uow)
    : IRequestHandler<ImportarReunionesCommand, ImportarReunionesResult>
{
    public async Task<ImportarReunionesResult> Handle(ImportarReunionesCommand cmd, CancellationToken ct)
    {
        var rows = await source.ObtenerReunionesAsync(ct);
        var existentes = await repo.GetOrigenExternoIdsAsync(ct);

        var errores = new List<string>();
        int creadas = 0, omitidas = 0;

        foreach (var row in rows)
        {
            if (cmd.OmitirExistentes && existentes.Contains(row.Id))
            {
                omitidas++;
                continue;
            }

            try
            {
                var (datos, asistentes, acuerdos, instNombre) = ReunionImportMapper.ToForm(row);

                Institucion? inst = string.IsNullOrWhiteSpace(instNombre)
                    ? null
                    : await institucionRepo.GetByNombreAsync(instNombre, ct);
                datos.InstitucionId = inst?.Id;

                var r = Reunion.Crear(datos.Titulo);
                r.OrigenExternoId = row.Id;
                ReunionMapper.Aplicar(r, datos, asistentes, acuerdos);

                // Snapshot del nombre aunque la institución no esté catalogada.
                r.InstitucionId = inst?.Id;
                r.Institucion   = inst?.Nombre ?? instNombre;

                await repo.AddAsync(r, ct);
                await ContactoFeeder.FeedAsync(r, contactoRepo, institucionRepo, ctx, uow, ct);
                await uow.SaveChangesAsync(ct);

                existentes.Add(row.Id);
                creadas++;
            }
            catch (Exception ex)
            {
                errores.Add($"[{row.Id}] {row.Titulo}: {ex.Message}");
            }
        }

        return new ImportarReunionesResult(rows.Count, creadas, omitidas, errores);
    }
}
