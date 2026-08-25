namespace Diger.TramitesEstado.Api.Consultas;

/// <summary>
/// Las dos rutas con que un consumidor mantiene su copia al día. Van juntas porque se
/// complementan: una trae lo que cambió, la otra lo que sigue vivo — y hacen falta las dos.
/// </summary>
public sealed class ConsultaSincronizacion(ApiDbContext db)
{
    /// <summary>Códigos a refrescar desde una fecha — GET /api/v1/cambios?desde=.
    /// No distingue alta de cambio a propósito: el consumidor hace upsert, que es idempotente.
    /// Las bajas se resuelven contra <see cref="CodigosPublicadosAsync"/>, no acá.</summary>
    public async Task<CambiosPublicosDto> CambiosAsync(DateTime desde, CancellationToken ct)
    {
        // La hora se toma ANTES de consultar. Al revés, un cambio ocurrido durante la consulta
        // quedaría por debajo del sello que el consumidor guarda como «desde» y no lo pediría
        // nunca más.
        var generadoEl = DateTime.UtcNow;

        var codigos = await db.Fichas
            .Where(t => t.Publicado && (t.UpdatedAt ?? t.CreatedAt) >= desde)
            .Select(t => t.Codigo)
            .ToListAsync(ct);

        return new CambiosPublicosDto(codigos, generadoEl);
    }

    /// <summary>Todos los códigos vivos — GET /api/v1/codigos-publicados. El consumidor retira
    /// de su catálogo local cualquier código que ya no aparezca acá: cubre por igual la ficha
    /// despublicada y la borrada, sin necesitar una columna de bitácora de bajas.</summary>
    public async Task<IReadOnlyList<string>> CodigosPublicadosAsync(CancellationToken ct) =>
        await db.Fichas
            .Where(t => t.Publicado)
            .Select(t => t.Codigo)
            .ToListAsync(ct);
}
