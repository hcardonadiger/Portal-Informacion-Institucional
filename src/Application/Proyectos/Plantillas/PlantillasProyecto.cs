using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Application.Proyectos.Plantillas;

// ══════════════════════════════════════════════════════════════════════
// Plantillas de ESTRUCTURA de proyecto (2026-10-19).
//
// Para los proyectos cíclicos: los que se repiten cada año con los mismos
// entregables y las mismas actividades. La plantilla guarda el esqueleto y
// nada más —ni fechas, ni estados, ni avances—, porque eso cambia en cada
// edición y traerlo caducado sería peor que no traerlo.
//
// Comandos y consultas viven juntos en un archivo porque son pocos y se leen
// como una sola función del portal; el día que crezcan, se parten igual que
// en Expedientes/Plantillas.
// ══════════════════════════════════════════════════════════════════════

// ── DTOs ──────────────────────────────────────────────────────────────

public sealed record ActividadPlantillaDto(int Orden, string Nombre, string? Descripcion);

public sealed record EntregablePlantillaDto(
    int Orden, string Nombre, string? Descripcion,
    IReadOnlyList<ActividadPlantillaDto> Actividades);

/// <summary>Una plantilla con su árbol completo. Es lo que viaja al editor.</summary>
public sealed record PlantillaProyectoDto(
    int Id, string Nombre, string? Descripcion, bool Activa,
    NivelAlcance Nivel, string InstitucionId, string? AreaId, string? UnidadId,
    IReadOnlyList<EntregablePlantillaDto> Entregables,
    int? CategoriaId = null, string? Categoria = null, AccionProyecto? Accion = null)
{
    /// <summary>Cuántas actividades hay en total, para no recorrer el árbol en la vista.</summary>
    public int TotalActividades => Entregables.Sum(e => e.Actividades.Count);
}

/// <summary>Una plantilla sin su árbol, para listarlas.</summary>
public sealed record PlantillaProyectoResumenDto(
    int Id, string Nombre, string? Descripcion, bool Activa,
    NivelAlcance Nivel, string InstitucionId, string? AreaId, string? UnidadId,
    int NumEntregables, int NumActividades,
    int? CategoriaId = null, string? Categoria = null, AccionProyecto? Accion = null);

// ── Consultas ─────────────────────────────────────────────────────────

/// <summary>
/// Las plantillas que alcanzan a quien está parado en este ámbito.
/// </summary>
/// <param name="SoloActivas">
/// La pantalla de administración las quiere todas, para poder reactivar una;
/// el selector del editor solo las activas, porque ofrecer una desactivada es
/// ofrecer algo que alguien retiró a propósito.
/// </param>
public sealed record GetPlantillasProyectoQuery(bool SoloActivas = true)
    : IRequest<IReadOnlyList<PlantillaProyectoResumenDto>>;

public sealed class GetPlantillasProyectoQueryHandler(
    IApplicationDbContext ctx, ICurrentUserService usuario)
    : IRequestHandler<GetPlantillasProyectoQuery, IReadOnlyList<PlantillaProyectoResumenDto>>
{
    public async Task<IReadOnlyList<PlantillaProyectoResumenDto>> Handle(
        GetPlantillasProyectoQuery q, CancellationToken ct)
    {
        var area   = usuario.ActiveAreaId;
        var unidad = usuario.ActiveUnidadId;

        // El filtro global del DbContext ya cortó por institución y por
        // soft-delete. Acá va el reparto fino, que es el que depende de dónde
        // está parado quien mira: mismo criterio que PlantillaProyecto.Alcanza,
        // traducido a SQL.
        var baseq = ctx.PlantillasProyecto.AsNoTracking().Where(p =>
            p.Nivel == NivelAlcance.Institucion
            || (p.Nivel == NivelAlcance.Area   && p.AreaId   == area)
            || (p.Nivel == NivelAlcance.Unidad && p.UnidadId == unidad));

        if (q.SoloActivas)
            baseq = baseq.Where(p => p.Activa);

        return await baseq
            .OrderBy(p => p.Nombre)
            .Select(p => new PlantillaProyectoResumenDto(
                p.Id, p.Nombre, p.Descripcion, p.Activa,
                p.Nivel, p.InstitucionId, p.AreaId, p.UnidadId,
                p.Entregables.Count,
                p.Entregables.Sum(e => e.Actividades.Count),
                p.CategoriaId, p.CategoriaRef != null ? p.CategoriaRef.Nombre : null, p.Accion))
            .ToListAsync(ct);
    }
}

/// <summary>Una plantilla con su árbol. Es lo que el editor copia al proyecto.</summary>
public sealed record GetPlantillaProyectoQuery(int Id) : IRequest<PlantillaProyectoDto?>;

public sealed class GetPlantillaProyectoQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetPlantillaProyectoQuery, PlantillaProyectoDto?>
{
    public async Task<PlantillaProyectoDto?> Handle(GetPlantillaProyectoQuery q, CancellationToken ct)
        => await ctx.PlantillasProyecto.AsNoTracking()
            .Where(p => p.Id == q.Id)
            .Select(p => new PlantillaProyectoDto(
                p.Id, p.Nombre, p.Descripcion, p.Activa,
                p.Nivel, p.InstitucionId, p.AreaId, p.UnidadId,
                p.Entregables.OrderBy(e => e.Orden)
                    .Select(e => new EntregablePlantillaDto(
                        e.Orden, e.Nombre, e.Descripcion,
                        e.Actividades.OrderBy(a => a.Orden)
                            .Select(a => new ActividadPlantillaDto(a.Orden, a.Nombre, a.Descripcion))
                            .ToList()))
                    .ToList(),
                p.CategoriaId, p.CategoriaRef != null ? p.CategoriaRef.Nombre : null, p.Accion))
            .FirstOrDefaultAsync(ct);
}

// ── Comandos ──────────────────────────────────────────────────────────

/// <summary>El árbol que llega del formulario, sin ids: se reemplaza en bloque.</summary>
public sealed record EntregablePlantillaInput(string Nombre, string? Descripcion, IReadOnlyList<ActividadPlantillaInput> Actividades);
public sealed record ActividadPlantillaInput(string Nombre, string? Descripcion);

public sealed record GuardarPlantillaProyectoCommand(
    int? Id, string Nombre, string? Descripcion, bool Activa,
    NivelAlcance Nivel, string? AreaId, string? UnidadId,
    IReadOnlyList<EntregablePlantillaInput> Entregables,
    int? CategoriaId = null, AccionProyecto? Accion = null) : IRequest<int>;

public sealed class GuardarPlantillaProyectoCommandHandler(
    IApplicationDbContext ctx, ICurrentUserService usuario)
    : IRequestHandler<GuardarPlantillaProyectoCommand, int>
{
    public async Task<int> Handle(GuardarPlantillaProyectoCommand c, CancellationToken ct)
    {
        if (c.Entregables.Count == 0)
            throw new DomainException("Una plantilla sin entregables no tiene nada que traer.");

        var institucion = usuario.ActiveInstitucionId;
        if (string.IsNullOrWhiteSpace(institucion))
            throw new DomainException("No hay institución activa para guardar la plantilla.");

        // La categoría tiene que existir: la llave foránea lo rechazaría igual,
        // pero con un error de base en vez de un mensaje que se pueda leer.
        if (c.CategoriaId is > 0 and var categoriaId
            && !await ctx.CategoriasProyecto.AnyAsync(x => x.Id == categoriaId, ct))
            throw new DomainException("La categoría elegida ya no existe.");

        PlantillaProyecto plantilla;

        if (c.Id is { } id)
        {
            plantilla = await ctx.PlantillasProyecto
                .Include(p => p.Entregables).ThenInclude(e => e.Actividades)
                .FirstOrDefaultAsync(p => p.Id == id, ct)
                ?? throw new DomainException("La plantilla ya no existe.");

            plantilla.Renombrar(c.Nombre, c.Descripcion);
            plantilla.FijarAlcance(c.Nivel, c.AreaId, c.UnidadId);
            plantilla.Activar(c.Activa);
            plantilla.Clasificar(c.CategoriaId, c.Accion);

            // Se reemplaza en bloque, igual que los hijos de Expediente: casar
            // por id qué entregable cambió es de donde salen los huérfanos, y
            // acá no hay nada colgando de ellos que valga la pena conservar.
            plantilla.LimpiarEntregables();
        }
        else
        {
            plantilla = PlantillaProyecto.Crear(
                c.Nombre, institucion!, c.Nivel, c.AreaId, c.UnidadId, c.Descripcion);
            plantilla.Activar(c.Activa);
            plantilla.Clasificar(c.CategoriaId, c.Accion);
            ctx.PlantillasProyecto.Add(plantilla);
        }

        // El orden lo pone el servidor por la posición en la lista, no el
        // formulario: así reordenar en la pantalla es mover filas, y no hay
        // forma de guardar dos entregables con el mismo número.
        var orden = 1;
        foreach (var e in c.Entregables.Where(e => !string.IsNullOrWhiteSpace(e.Nombre)))
        {
            var entregable = new PlantillaEntregable
            {
                Orden       = orden++,
                Nombre      = e.Nombre.Trim(),
                Descripcion = string.IsNullOrWhiteSpace(e.Descripcion) ? null : e.Descripcion.Trim()
            };

            var ordenAct = 1;
            foreach (var a in e.Actividades.Where(a => !string.IsNullOrWhiteSpace(a.Nombre)))
                entregable.Agregar(new PlantillaActividad
                {
                    Orden       = ordenAct++,
                    Nombre      = a.Nombre.Trim(),
                    Descripcion = string.IsNullOrWhiteSpace(a.Descripcion) ? null : a.Descripcion.Trim()
                });

            plantilla.Agregar(entregable);
        }

        if (plantilla.Entregables.Count == 0)
            throw new DomainException("Una plantilla sin entregables no tiene nada que traer.");

        await ctx.SaveChangesAsync(ct);
        return plantilla.Id;
    }
}

/// <summary>
/// Borrado suave: la plantilla desaparece de las listas pero no se lleva por
/// delante nada. Un proyecto que ya la usó no la referencia —se copió la
/// estructura, no se enlazó— así que borrarla nunca rompe un proyecto.
/// </summary>
public sealed record EliminarPlantillaProyectoCommand(int Id) : IRequest<Unit>;

public sealed class EliminarPlantillaProyectoCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<EliminarPlantillaProyectoCommand, Unit>
{
    public async Task<Unit> Handle(EliminarPlantillaProyectoCommand c, CancellationToken ct)
    {
        var plantilla = await ctx.PlantillasProyecto.FirstOrDefaultAsync(p => p.Id == c.Id, ct)
            ?? throw new DomainException("La plantilla ya no existe.");

        plantilla.IsDeleted = true;
        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── Traer la plantilla al proyecto ────────────────────────────────────

/// <summary>
/// Copia la estructura de una plantilla dentro de un proyecto que todavía no
/// tiene ninguna.
/// </summary>
/// <remarks>
/// <para><b>Copia, no enlace.</b> El proyecto no guarda de qué plantilla salió:
/// a partir de acá su estructura es suya y se edita como cualquier otra. Es lo
/// que permite que editar o retirar la plantilla no toque a los proyectos que
/// ya la usaron —un ciclo cerrado no debería cambiar porque el molde del
/// siguiente se corrigió—.</para>
///
/// <para><b>Solo sobre estructura vacía.</b> Traerla sobre un proyecto que ya
/// tiene entregables obligaría a decidir qué hacer con los avances, las fechas
/// reales y las dependencias ya imputadas; cualquier respuesta a eso pierde
/// dato en silencio. La pantalla ni ofrece el control cuando hay estructura,
/// pero el comando lo exige igual: la regla no puede vivir solo en la vista.</para>
///
/// <para><b>Lo que NO se copia:</b> fechas, estados, responsables, avances y
/// dependencias. La plantilla es el esqueleto —qué se entrega y qué hay que
/// hacer—; el resto nace del ciclo que arranca.</para>
/// </remarks>
public sealed record TraerPlantillaAProyectoCommand(int ProyectoId, int PlantillaId) : IRequest<int>;

public sealed class TraerPlantillaAProyectoCommandHandler(
    IApplicationDbContext ctx, ICurrentUserService usuario)
    : IRequestHandler<TraerPlantillaAProyectoCommand, int>
{
    public async Task<int> Handle(TraerPlantillaAProyectoCommand c, CancellationToken ct)
    {
        var proyecto = await ctx.Proyectos
            .Include(p => p.Entregables)
            .FirstOrDefaultAsync(p => p.Id == c.ProyectoId, ct)
            ?? throw new DomainException("El proyecto ya no existe.");

        if (proyecto.Entregables.Count > 0)
            throw new DomainException(
                "El proyecto ya tiene estructura. Una plantilla solo se puede traer sobre un " +
                "proyecto sin entregables, para no pisar avances ni fechas ya registradas.");

        var plantilla = await ctx.PlantillasProyecto.AsNoTracking()
            .Include(p => p.Entregables).ThenInclude(e => e.Actividades)
            .FirstOrDefaultAsync(p => p.Id == c.PlantillaId, ct)
            ?? throw new DomainException("La plantilla ya no existe.");

        if (!plantilla.Activa)
            throw new DomainException("Esa plantilla está desactivada.");

        // El filtro global del DbContext ya cortó por institución —y deja pasar
        // entero al usuario global—; acá va solo el reparto fino, exactamente el
        // mismo que decide qué se lista. Sin esto alguien podría traer por id una
        // plantilla de otra área que la pantalla nunca le ofreció.
        //
        // Deliberadamente NO se llama a PlantillaProyecto.Alcanza: esa compara
        // además la institución, y volver a exigirla acá dejaría fuera al usuario
        // global, que no tiene una activa y a quien el filtro ya autorizó.
        var enAmbito = plantilla.Nivel switch
        {
            NivelAlcance.Institucion => true,
            NivelAlcance.Area        => plantilla.AreaId   == usuario.ActiveAreaId,
            NivelAlcance.Unidad      => plantilla.UnidadId == usuario.ActiveUnidadId,
            _                        => false
        };
        if (!enAmbito)
            throw new DomainException("Esa plantilla no está disponible en su ámbito.");

        foreach (var e in plantilla.Entregables.OrderBy(e => e.Orden))
        {
            var entregable = EntregableProyecto.Crear(e.Nombre, e.Orden);
            entregable.Definir(e.Nombre, e.Descripcion, null, null, null);

            foreach (var a in e.Actividades.OrderBy(a => a.Orden))
            {
                var actividad = ActividadProyecto.Crear(a.Nombre, a.Orden);
                actividad.Definir(a.Nombre, a.Descripcion, null, null, null, null);
                entregable.Agregar(actividad);
            }

            proyecto.Agregar(entregable);
        }

        // Nace en cero por definición —ninguna actividad reporta nada— pero se
        // recalcula igual en vez de asumirlo: el promedio es del árbol, y es el
        // árbol el que acaba de cambiar.
        proyecto.RecalcularAvance(proyecto.Entregables);

        var actividades = plantilla.Entregables.Sum(e => e.Actividades.Count);
        ctx.BitacorasProyecto.Add(BitacoraProyecto.Crear(
            proyecto.Id, TipoEventoProyecto.ModificacionEstructura,
            $"Estructura traída de la plantilla «{plantilla.Nombre}»: " +
            $"{plantilla.Entregables.Count} entregable{(plantilla.Entregables.Count == 1 ? "" : "s")}" +
            $" y {actividades} actividad{(actividades == 1 ? "" : "es")}.",
            usuario.Nombre ?? "—"));

        await ctx.SaveChangesAsync(ct);
        return proyecto.Entregables.Count;
    }
}
