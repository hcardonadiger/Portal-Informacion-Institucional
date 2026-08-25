using Diger.TramitesEstado.Application.Siger.Historial;

namespace Diger.TramitesEstado.Application.Siger.Promocion.Commands.PasarASiger;

/// <summary>Escribe un trámite de expediente hacia su ficha SIGER, creándola si no existe.</summary>
public sealed record PasarASigerCommand(int ExpedienteId, int TramiteIndex) : IRequest<ResultadoPase>;

/// <param name="TramiteSigerId">La ficha escrita.</param>
/// <param name="Codigo">Su código.</param>
/// <param name="FueCreada">Cierto si el pase la creó; falso si actualizó una que ya existía.</param>
/// <param name="VersionArchivada">Número de versión de la foto que se guardó antes de
/// sobrescribir. Nulo cuando la ficha se acaba de crear: no había nada que archivar.</param>
public sealed record ResultadoPase(int TramiteSigerId, string Codigo, bool FueCreada, int? VersionArchivada);

/// <summary>
/// Promover y actualizar son la misma operación —escribir del expediente hacia la ficha— una
/// creando y otra sobrescribiendo. Por eso van en un solo comando: separarlas dejaría dos
/// caminos que escriben lo mismo y que acabarían discrepando.
/// </summary>
/// <remarks>
/// <para>
/// <b>Antes de sobrescribir se archiva</b> (D-07, D-15). La foto guarda el estado que se está
/// reemplazando, no el nuevo: es lo que permite responder «qué decía esta ficha antes del pase
/// del martes». La versión 0 está reservada para el inventario original de SIGER, así que las de
/// los pases empiezan en 1.
/// </para>
/// <para>
/// <b>No archiva al crear</b>, y no es un olvido: no había ficha que retratar. Una ficha promovida
/// que nadie ha vuelto a pasar no tiene historial, y eso es lo cierto.
/// </para>
/// <para>
/// <b>Las tres colecciones se reemplazan en bloque</b>, igual que en el expediente. Intentar
/// casar fila por fila requeriría una identidad estable por requisito que no existe —el orden
/// cambia cuando alguien quita uno del medio— y produciría duplicados o pérdidas silenciosas.
/// </para>
/// </remarks>
public sealed class PasarASigerCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<PasarASigerCommand, ResultadoPase>
{
    public async Task<ResultadoPase> Handle(PasarASigerCommand cmd, CancellationToken ct)
    {
        var expediente = await ctx.Expedientes
            .FirstOrDefaultAsync(x => x.Id == cmd.ExpedienteId, ct)
            ?? throw new NotFoundException(nameof(Expediente), cmd.ExpedienteId.ToString());

        var tramite = await ctx.Tramites
            .FirstOrDefaultAsync(x => x.ExpedienteId == cmd.ExpedienteId && x.TramiteIndex == cmd.TramiteIndex, ct)
            ?? throw new NotFoundException(nameof(ExpedienteTramite), $"{cmd.ExpedienteId}/{cmd.TramiteIndex}");

        if (string.IsNullOrWhiteSpace(tramite.NombreTramite))
            throw new DomainException("El trámite no tiene nombre; no se puede pasar a SIGER sin uno.");

        var (requisitos, entregables, lugares) = await HijosAsync(cmd.ExpedienteId, cmd.TramiteIndex, ct);

        return tramite.TramiteSigerId is { } fichaId
            ? await ActualizarAsync(fichaId, tramite, expediente, requisitos, entregables, lugares, ct)
            : await CrearAsync(tramite, expediente, requisitos, entregables, lugares, ct);
    }

    // ── Crear ─────────────────────────────────────────────────────────────────

    private async Task<ResultadoPase> CrearAsync(
        ExpedienteTramite t, Expediente e,
        List<TramiteRequisito> requisitos,
        List<ExpedienteTramiteEntregable> entregables,
        List<ExpedienteTramiteLugar> lugares,
        CancellationToken ct)
    {
        var codigo = await SiguienteCodigoAsync(e.InstitucionId, ct);
        var ficha = PromocionMapeo.CrearFicha(t, e, codigo);

        ficha.Requisitos      = PromocionMapeo.Requisitos(requisitos);
        ficha.Entregables     = PromocionMapeo.Entregables(entregables);
        ficha.LugaresAtencion = PromocionMapeo.Lugares(lugares);

        ctx.TramitesSiger.Add(ficha);
        await ctx.SaveChangesAsync(ct);

        // El enlace se escribe después de guardar porque hasta entonces la ficha no tiene Id.
        // Con él queda además bloqueada del lado de la ficha (D-17).
        t.TramiteSigerId = ficha.Id;
        await ctx.SaveChangesAsync(ct);

        return new ResultadoPase(ficha.Id, ficha.Codigo, FueCreada: true, VersionArchivada: null);
    }

    /// <summary>
    /// El código sale del prefijo que ya usan las fichas de esa institución, con la marca «-P»
    /// que delata que no viene del inventario. Si la institución no tiene ninguna ficha todavía,
    /// se cae al prefijo por defecto.
    /// </summary>
    private async Task<string> SiguienteCodigoAsync(string institucionId, CancellationToken ct)
    {
        var codigos = await ctx.TramitesSiger.AsNoTracking()
            .Where(f => f.InstitucionId == institucionId)
            .Select(f => f.Codigo)
            .ToListAsync(ct);

        // El prefijo se toma de un código que NO sea ya uno promovido: si se tomara de
        // «400-P01» el prefijo saldría «400» igual, pero de «DGR-P01» saldría «DGR» y una
        // institución con solo fichas promovidas se quedaría anclada al valor por defecto.
        var prefijo = codigos
            .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c) && !c.Contains("-P", StringComparison.OrdinalIgnoreCase));

        return CodigoPromovido.Siguiente(
            prefijo is null ? CodigoPromovido.PrefijoPorDefecto : CodigoPromovido.PrefijoDe(prefijo),
            codigos);
    }

    // ── Actualizar ────────────────────────────────────────────────────────────

    private async Task<ResultadoPase> ActualizarAsync(
        int fichaId, ExpedienteTramite t, Expediente e,
        List<TramiteRequisito> requisitos,
        List<ExpedienteTramiteEntregable> entregables,
        List<ExpedienteTramiteLugar> lugares,
        CancellationToken ct)
    {
        var ficha = await ctx.TramitesSiger
            .Include(f => f.Pasos)
            .Include(f => f.Requisitos)
            .Include(f => f.Entregables)
            .Include(f => f.LugaresAtencion)
            .Include(f => f.Enlaces)
            .Include(f => f.TareasDigitalizacion)
            .FirstOrDefaultAsync(f => f.Id == fichaId, ct);

        if (ficha is null)
        {
            // La ficha enlazada ya no existe: alguien la borró. Se desenlaza y se crea una
            // nueva en vez de fallar, porque el trámite del expediente sigue siendo real y
            // dejarlo apuntando a la nada solo repetiría el error en cada intento.
            t.TramiteSigerId = null;
            return await CrearAsync(t, e, requisitos, entregables, lugares, ct);
        }

        var version = await ArchivarAsync(ficha, ct);

        PromocionMapeo.CamposDelExpediente(ficha, t, e);

        ReemplazarRequisitos(ficha, requisitos);
        ReemplazarEntregables(ficha, entregables);
        ReemplazarLugares(ficha, lugares);

        await ctx.SaveChangesAsync(ct);

        return new ResultadoPase(ficha.Id, ficha.Codigo, FueCreada: false, VersionArchivada: version);
    }

    /// <summary>Guarda el estado actual de la ficha antes de que el pase lo reemplace.</summary>
    private async Task<int> ArchivarAsync(TramiteSiger ficha, CancellationToken ct)
    {
        var ultima = await ctx.FotosTramiteSiger.AsNoTracking()
            .Where(f => f.TramiteSigerId == ficha.Id)
            .Select(f => (int?)f.Version)
            .MaxAsync(ct) ?? OrigenFoto.VersionOriginal;

        var version = ultima + 1;

        ctx.FotosTramiteSiger.Add(new FotoTramiteSiger
        {
            TramiteSigerId = ficha.Id,
            Version        = version,
            Origen         = OrigenFoto.PaseDesdeExpediente,
            Codigo         = ficha.Codigo,
            IdSiger        = ficha.IdSiger,
            CapturadaEl    = DateTime.UtcNow,
            Contenido      = FotoSigerSerializador.Serializar(FotoSigerSerializador.Retratar(ficha))
        });

        return version;
    }

    private void ReemplazarRequisitos(TramiteSiger ficha, List<TramiteRequisito> origen)
    {
        ctx.RequisitosSiger.RemoveRange(ficha.Requisitos);
        ficha.Requisitos = PromocionMapeo.Requisitos(origen);
    }

    private void ReemplazarEntregables(TramiteSiger ficha, List<ExpedienteTramiteEntregable> origen)
    {
        ctx.EntregablesSiger.RemoveRange(ficha.Entregables);
        ficha.Entregables = PromocionMapeo.Entregables(origen);
    }

    private void ReemplazarLugares(TramiteSiger ficha, List<ExpedienteTramiteLugar> origen)
    {
        ctx.LugaresAtencionSiger.RemoveRange(ficha.LugaresAtencion);
        ficha.LugaresAtencion = PromocionMapeo.Lugares(origen);
    }

    // ── Apoyo ─────────────────────────────────────────────────────────────────

    private async Task<(List<TramiteRequisito>, List<ExpedienteTramiteEntregable>, List<ExpedienteTramiteLugar>)>
        HijosAsync(int expedienteId, int tramiteIndex, CancellationToken ct)
    {
        var requisitos = await ctx.Requisitos.AsNoTracking()
            .Where(r => r.ExpedienteId == expedienteId && r.TramiteIndex == tramiteIndex)
            .OrderBy(r => r.Orden).ToListAsync(ct);

        var entregables = await ctx.EntregablesTramite.AsNoTracking()
            .Where(g => g.ExpedienteId == expedienteId && g.TramiteIndex == tramiteIndex)
            .OrderBy(g => g.Orden).ToListAsync(ct);

        var lugares = await ctx.LugaresTramite.AsNoTracking()
            .Where(l => l.ExpedienteId == expedienteId && l.TramiteIndex == tramiteIndex)
            .OrderBy(l => l.Orden).ToListAsync(ct);

        return (requisitos, entregables, lugares);
    }
}
