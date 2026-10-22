namespace Diger.TramitesEstado.Application.Siger.Importacion.Commands.ImportarFicha;

/// <summary>Trae una ficha SIGER a un expediente y la deja enlazada —y con eso, bloqueada.</summary>
/// <param name="ExpedienteId">Destino elegido. Null usa el bucket de la institución (D-06).</param>
public sealed record ImportarFichaCommand(int TramiteSigerId, int? ExpedienteId = null)
    : IRequest<ResultadoImportacion>;

/// <param name="ExpedienteId">Dónde aterrizó.</param>
/// <param name="TramiteIndex">Su posición dentro de ese expediente.</param>
/// <param name="EnBucket">Cierto si fue al bucket de importación y no a un levantamiento.</param>
/// <param name="BucketCreado">Cierto si el bucket se creó en esta llamada.</param>
public sealed record ResultadoImportacion(
    int ExpedienteId, string ExpedienteCodigo, int TramiteIndex, bool EnBucket, bool BucketCreado);

/// <remarks>
/// <para>
/// <b>Una ficha se importa una sola vez.</b> Si ya hay un trámite de expediente apuntándola, el
/// comando falla en vez de crear un segundo: dos trámites enlazados a la misma ficha significan
/// que dos expedientes creen mandar sobre ella, y el último que pase gana sin que nadie lo sepa.
/// La guarda se apoya en el mismo enlace que produce el bloqueo, así que no puede desincronizarse
/// de él.
/// </para>
/// <para>
/// <b>El bucket se crea al vuelo</b> la primera vez que una institución importa algo, y se
/// reencuentra por su <c>OrigenExternoId</c>. Nace y se queda en <c>EnExploracion</c>: un bucket
/// no es un levantamiento y nunca avanza de etapa.
/// </para>
/// </remarks>
public sealed class ImportarFichaCommandHandler(IApplicationDbContext ctx, ICurrentUserService usuario)
    : IRequestHandler<ImportarFichaCommand, ResultadoImportacion>
{
    public async Task<ResultadoImportacion> Handle(ImportarFichaCommand cmd, CancellationToken ct)
    {
        var ficha = await ctx.TramitesSiger
            .Include(f => f.Requisitos)
            .Include(f => f.Entregables)
            .Include(f => f.LugaresAtencion)
            .FirstOrDefaultAsync(f => f.Id == cmd.TramiteSigerId, ct)
            ?? throw new NotFoundException(nameof(TramiteSiger), cmd.TramiteSigerId.ToString());

        // La guarda contra doble importación. Va antes que nada para no dejar a medias un bucket
        // recién creado si la importación se va a rechazar igual.
        var yaEnlazada = await ctx.Tramites.AsNoTracking()
            .Where(t => t.TramiteSigerId == cmd.TramiteSigerId)
            .Select(t => new { t.ExpedienteId, t.NombreTramite })
            .FirstOrDefaultAsync(ct);

        if (yaEnlazada is not null)
        {
            var codigoExistente = await ctx.Expedientes.AsNoTracking()
                .Where(e => e.Id == yaEnlazada.ExpedienteId)
                .Select(e => e.Codigo).FirstOrDefaultAsync(ct) ?? yaEnlazada.ExpedienteId.ToString();

            throw new DomainException(
                $"La ficha {ficha.Codigo} ya está en el expediente {codigoExistente}. " +
                "Una ficha solo puede pertenecer a un expediente; para moverla, desenlácela primero.");
        }

        var (expediente, creado) = cmd.ExpedienteId is { } destinoId
            ? (await ExpedienteElegidoAsync(destinoId, ficha, ct), false)
            : await BucketAsync(ficha, ct);

        // El índice va después del último que haya. Se calcula sobre la base y no sobre la
        // colección cargada porque el expediente puede tener trámites que nadie trajo a memoria.
        var indice = (await ctx.Tramites.AsNoTracking()
            .Where(t => t.ExpedienteId == expediente.Id)
            .Select(t => (int?)t.TramiteIndex)
            .MaxAsync(ct) ?? -1) + 1;

        var tramite = ImportacionMapeo.CrearTramite(ficha, indice);
        tramite.ExpedienteId = expediente.Id;
        ctx.Tramites.Add(tramite);

        ctx.Requisitos.AddRange(ImportacionMapeo.Requisitos(expediente.Id, indice, ficha.Requisitos));
        ctx.EntregablesTramite.AddRange(ImportacionMapeo.Entregables(expediente.Id, indice, ficha.Entregables));
        ctx.LugaresTramite.AddRange(ImportacionMapeo.Lugares(expediente.Id, indice, ficha.LugaresAtencion));

        await ctx.SaveChangesAsync(ct);

        return new ResultadoImportacion(
            expediente.Id, expediente.Codigo, indice,
            EnBucket: BucketImportacion.EsBucket(expediente.OrigenExternoId),
            BucketCreado: creado);
    }

    /// <summary>
    /// El expediente que alguien eligió. Se exige que sea de la misma institución que la ficha:
    /// importar una ficha de ADUANAS a un expediente de SALUD produciría un trámite que dice
    /// pertenecer a dos instituciones a la vez, y la promoción de vuelta le cambiaría la
    /// institución a la ficha sin que nadie lo pidiera.
    /// </summary>
    private async Task<Expediente> ExpedienteElegidoAsync(int id, TramiteSiger ficha, CancellationToken ct)
    {
        var e = await ctx.Expedientes.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(nameof(Expediente), id.ToString());

        if (!string.IsNullOrWhiteSpace(ficha.InstitucionId) &&
            !string.Equals(e.InstitucionId, ficha.InstitucionId, StringComparison.OrdinalIgnoreCase))
            throw new DomainException(
                $"La ficha {ficha.Codigo} es de {ficha.InstitucionId} y el expediente {e.Codigo} " +
                $"es de {e.InstitucionId}. Elija un expediente de la misma institución.");

        return e;
    }

    private async Task<(Expediente, bool)> BucketAsync(TramiteSiger ficha, CancellationToken ct)
    {
        var institucionId = string.IsNullOrWhiteSpace(ficha.InstitucionId)
            ? throw new DomainException(
                $"La ficha {ficha.Codigo} no tiene institución, así que no hay bucket donde ponerla. " +
                "Elija un expediente destino a mano.")
            : ficha.InstitucionId;

        var marca = BucketImportacion.OrigenExternoId(institucionId);

        var existente = await ctx.Expedientes.FirstOrDefaultAsync(e => e.OrigenExternoId == marca, ct);
        if (existente is not null) return (existente, false);

        var bucket = Expediente.Crear(
            await CodigoLibreAsync(institucionId, ct),
            institucionId, null, null,
            ficha.Institucion,
            usuario.Nombre ?? BucketImportacion.NombreAnalista);

        bucket.OrigenExternoId = marca;
        ctx.Expedientes.Add(bucket);
        await ctx.SaveChangesAsync(ct);

        return (bucket, true);
    }

    /// <summary>
    /// El código del bucket se recorta a lo que cabe en la columna, así que dos instituciones de
    /// llave muy larga podrían chocar. Es improbable pero no imposible, y un choque acá abortaría
    /// la importación con un error de base de datos que nadie sabría leer.
    /// </summary>
    private async Task<string> CodigoLibreAsync(string institucionId, CancellationToken ct)
    {
        var baseCodigo = BucketImportacion.CodigoSugerido(institucionId);
        if (!await ctx.Expedientes.AnyAsync(e => e.Codigo == baseCodigo, ct)) return baseCodigo;

        for (var n = 2; n < 100; n++)
        {
            var sufijo = $"-{n}";
            var candidato = baseCodigo.Length + sufijo.Length <= 40
                ? baseCodigo + sufijo
                : baseCodigo[..(40 - sufijo.Length)] + sufijo;

            if (!await ctx.Expedientes.AnyAsync(e => e.Codigo == candidato, ct)) return candidato;
        }

        throw new DomainException("No se pudo generar un código libre para el bucket de importación.");
    }
}
