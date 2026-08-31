namespace Diger.TramitesEstado.Application.Proyectos.Queries;

/// <summary>Una reunión vinculada, tal como la ve la ficha del proyecto.</summary>
public sealed record ReunionVinculadaDto(
    int       VinculoId,
    int       ReunionId,
    string    Titulo,
    DateOnly? Fecha,
    string?   Institucion,
    string?   Tipo,
    int       Asistentes,
    string?   Nota,
    string    VinculadoPor,
    DateTime  VinculadoEn);

/// <summary>Un expediente vinculado.</summary>
public sealed record ExpedienteVinculadoDto(
    int       VinculoId,
    int       ExpedienteId,
    string    Codigo,
    string    Institucion,
    string    Estado,
    DateOnly? FechaApertura,
    string?   Nota,
    string    VinculadoPor,
    DateTime  VinculadoEn);

/// <summary>
/// Lo que la ficha muestra en su pestaña de vínculos.
/// </summary>
/// <param name="FueraDeAlcance">Vínculos que existen pero cuyo destino esta persona no puede abrir.
/// <b>Se cuentan, no se esconden.</b> Ver la nota de la consulta.</param>
public sealed record VinculosProyectoDto(
    IReadOnlyList<ReunionVinculadaDto>    Reuniones,
    IReadOnlyList<ExpedienteVinculadoDto> Expedientes,
    int ReunionesFueraDeAlcance,
    int ExpedientesFueraDeAlcance)
{
    public bool HayAlguno => Reuniones.Count > 0 || Expedientes.Count > 0
                          || ReunionesFueraDeAlcance > 0 || ExpedientesFueraDeAlcance > 0;
}

/// <summary>
/// Las reuniones y expedientes vinculados a un proyecto.
///
/// <para><b>El desajuste de alcance, y qué se hace con él.</b> El vínculo se ancla en el proyecto,
/// pero la reunión y el expediente llevan su propio filtro —y no es el mismo—. El de
/// <see cref="Proyecto"/> deja ver el proyecto al responsable y a los interesados aunque caigan
/// fuera de su institución; el de <see cref="Reunion"/> no tiene esa salida y además esconde las
/// privadas de otros. Sumado a que la institución del proyecto es la EJECUTORA y la de la reunión
/// la beneficiaria, el resultado es que alguien puede abrir el proyecto y no poder abrir la mitad
/// de lo que tiene vinculado.</para>
///
/// <para>Había tres salidas y las tres tienen coste: respetar los filtros y que la pestaña mienta
/// por omisión; saltarlos con <c>IgnoreQueryFilters</c> y fugar reuniones privadas; o mostrar lo
/// que se alcanza <b>y decir cuántos quedan fuera</b>. Se eligió la tercera: un conteo no revela
/// nada del contenido —ni título, ni institución, ni fecha— y evita que una ficha con seis
/// reuniones vinculadas se lea como si tuviera dos.</para>
/// </summary>
public sealed record GetVinculosProyectoQuery(int ProyectoId) : IRequest<VinculosProyectoDto>;

public sealed class GetVinculosProyectoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetVinculosProyectoQuery, VinculosProyectoDto>
{
    public async Task<VinculosProyectoDto> Handle(GetVinculosProyectoQuery q, CancellationToken ct)
    {
        // Los vínculos van filtrados por el proyecto: pedir los de un proyecto ajeno da vacío.
        var vReuniones = await ctx.ProyectoReuniones.AsNoTracking()
            .Where(x => x.ProyectoId == q.ProyectoId)
            .Select(x => new { x.Id, x.ReunionId, x.Nota, x.VinculadoPor, x.VinculadoEn })
            .ToListAsync(ct);

        var vExpedientes = await ctx.ProyectoExpedientes.AsNoTracking()
            .Where(x => x.ProyectoId == q.ProyectoId)
            .Select(x => new { x.Id, x.ExpedienteId, x.Nota, x.VinculadoPor, x.VinculadoEn })
            .ToListAsync(ct);

        // Y los destinos van por SU propio filtro, sin excepción: lo que no se alcance no aparece.
        var reunionIds = vReuniones.Select(x => x.ReunionId).ToList();
        var reuniones = await ctx.Reuniones.AsNoTracking()
            .Where(r => reunionIds.Contains(r.Id))
            .Select(r => new
            {
                r.Id, r.Titulo, r.Fecha, r.Institucion, r.Tipo,
                Asistentes = ctx.Asistentes.Count(a => a.ReunionId == r.Id)
            })
            .ToDictionaryAsync(r => r.Id, ct);

        var expedienteIds = vExpedientes.Select(x => x.ExpedienteId).ToList();
        var expedientes = await ctx.Expedientes.AsNoTracking()
            .Where(e => expedienteIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Codigo, e.Institucion, e.EstadoExpediente, e.FechaApertura })
            .ToDictionaryAsync(e => e.Id, ct);

        var filasReuniones = vReuniones
            .Where(v => reuniones.ContainsKey(v.ReunionId))
            .Select(v =>
            {
                var r = reuniones[v.ReunionId];
                return new ReunionVinculadaDto(
                    v.Id, r.Id, r.Titulo, r.Fecha, r.Institucion, r.Tipo, r.Asistentes,
                    v.Nota, v.VinculadoPor, v.VinculadoEn);
            })
            .OrderByDescending(r => r.Fecha ?? DateOnly.MinValue)
            .ThenBy(r => r.Titulo)
            .ToList();

        var filasExpedientes = vExpedientes
            .Where(v => expedientes.ContainsKey(v.ExpedienteId))
            .Select(v =>
            {
                var e = expedientes[v.ExpedienteId];
                return new ExpedienteVinculadoDto(
                    v.Id, e.Id, e.Codigo, e.Institucion, e.EstadoExpediente.ToString(),
                    e.FechaApertura, v.Nota, v.VinculadoPor, v.VinculadoEn);
            })
            .OrderBy(e => e.Codigo)
            .ToList();

        return new VinculosProyectoDto(
            filasReuniones, filasExpedientes,
            vReuniones.Count   - filasReuniones.Count,
            vExpedientes.Count - filasExpedientes.Count);
    }
}

/// <summary>Una opción del selector para vincular. Solo lo que la persona puede ver.</summary>
public sealed record OpcionVinculoDto(int Id, string Etiqueta);

/// <summary>
/// Lo que se puede vincular a este proyecto: reuniones y expedientes dentro del alcance de quien
/// pregunta, <b>menos los ya vinculados</b>. Ofrecer algo que el comando va a rechazar por
/// duplicado sería prometer una acción que no se puede hacer.
/// </summary>
public sealed record GetVinculablesQuery(int ProyectoId) : IRequest<(IReadOnlyList<OpcionVinculoDto> Reuniones,
                                                                    IReadOnlyList<OpcionVinculoDto> Expedientes)>;

public sealed class GetVinculablesQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetVinculablesQuery, (IReadOnlyList<OpcionVinculoDto>, IReadOnlyList<OpcionVinculoDto>)>
{
    public async Task<(IReadOnlyList<OpcionVinculoDto>, IReadOnlyList<OpcionVinculoDto>)> Handle(
        GetVinculablesQuery q, CancellationToken ct)
    {
        var yaReuniones = await ctx.ProyectoReuniones.AsNoTracking()
            .Where(x => x.ProyectoId == q.ProyectoId).Select(x => x.ReunionId).ToListAsync(ct);

        var yaExpedientes = await ctx.ProyectoExpedientes.AsNoTracking()
            .Where(x => x.ProyectoId == q.ProyectoId).Select(x => x.ExpedienteId).ToListAsync(ct);

        var reuniones = await ctx.Reuniones.AsNoTracking()
            .Where(r => !yaReuniones.Contains(r.Id))
            .OrderByDescending(r => r.Fecha)
            .Take(300)
            .Select(r => new OpcionVinculoDto(
                r.Id,
                (r.Fecha != null ? r.Fecha.Value.ToString("yyyy-MM-dd") + " · " : "") + r.Titulo))
            .ToListAsync(ct);

        var expedientes = await ctx.Expedientes.AsNoTracking()
            .Where(e => !yaExpedientes.Contains(e.Id))
            .OrderBy(e => e.Codigo)
            .Take(300)
            .Select(e => new OpcionVinculoDto(e.Id, e.Codigo + " · " + e.Institucion))
            .ToListAsync(ct);

        return (reuniones, expedientes);
    }
}
