using Diger.TramitesEstado.Application.Common.Tiempo;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Application.Calendario.Ics;

/// <summary>El contenido de un archivo .ics y el nombre con el que se descarga.</summary>
public sealed record ArchivoIcs(string Contenido, string NombreArchivo)
{
    public const string TipoContenido = "text/calendar";
}

// ── Una reunión ───────────────────────────────────────────────────────────────

/// <summary>
/// El archivo .ics de una reunión, para el botón «Agregar a mi calendario».
/// </summary>
/// <param name="UrlPortal">Enlace a la ficha, que viaja en el campo URL de la cita. Lo arma la
/// página porque es la única que conoce el host de la petición.</param>
public sealed record GetIcsReunionQuery(int ReunionId, string? UrlPortal = null) : IRequest<ArchivoIcs>;

public sealed class GetIcsReunionQueryHandler(
    IApplicationDbContext ctx,
    RelojInstitucional reloj,
    IOptions<InstitucionOptions> institucion)
    : IRequestHandler<GetIcsReunionQuery, ArchivoIcs>
{
    public async Task<ArchivoIcs> Handle(GetIcsReunionQuery q, CancellationToken ct)
    {
        // El filtro global de Reuniones ya aplica el alcance: una reunión fuera del alcance del
        // usuario no aparece, y la descarga responde 404 como cualquier otra pantalla.
        var r = await ctx.Reuniones
            .Include(x => x.Asistentes)
            .FirstOrDefaultAsync(x => x.Id == q.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), q.ReunionId);

        var inst = institucion.Value;
        var evento = ReunionIcs.Mapear(r, reloj, IcsDominio.De(inst), q.UrlPortal);

        var contenido = IcsWriter.Escribir([evento], IcsDominio.ProdId(inst));
        return new ArchivoIcs(contenido, $"reunion-{r.Id}.ics");
    }
}

// ── Agenda personal (suscripción) ─────────────────────────────────────────────

/// <summary>
/// El calendario suscribible de una persona, resuelto por su token.
///
/// <para>Es anónimo por diseño: un calendario suscrito lo pide el cliente (Outlook, Google) sin
/// sesión ni cookies, así que el token <b>es</b> la credencial. De ahí que se pueda regenerar y que
/// se entregue solo lo mínimo: las reuniones donde la persona organiza o asiste.</para>
/// </summary>
/// <param name="DiasAtras">Cuánto pasado se incluye. Un calendario suscrito no es un archivo
/// histórico; con un mes alcanza para revisar lo reciente sin que la descarga crezca sin fin.</param>
public sealed record GetIcsAgendaQuery(Guid Token, int DiasAtras = 30, int DiasAdelante = 365)
    : IRequest<ArchivoIcs?>;

public sealed class GetIcsAgendaQueryHandler(
    IApplicationDbContext ctx,
    RelojInstitucional reloj,
    IOptions<InstitucionOptions> institucion)
    : IRequestHandler<GetIcsAgendaQuery, ArchivoIcs?>
{
    public async Task<ArchivoIcs?> Handle(GetIcsAgendaQuery q, CancellationToken ct)
    {
        if (q.Token == Guid.Empty) return null;

        // Usuarios no lleva filtro de alcance (la administración de usuarios es global), así que
        // basta con exigir que esté activo: dar de baja a alguien también apaga su feed.
        var usuario = await ctx.Usuarios.AsNoTracking()
            .Where(u => u.CalendarioToken == q.Token && u.Activo)
            .Select(u => new { u.Id, u.Nombre, u.Correo })
            .FirstOrDefaultAsync(ct);

        if (usuario is null) return null;

        var hoy    = reloj.Hoy();
        var desde  = hoy.AddDays(-Math.Abs(q.DiasAtras));
        var hasta  = hoy.AddDays(Math.Abs(q.DiasAdelante));
        var correo = usuario.Correo.Trim().ToLowerInvariant();

        // Mismo criterio de «mi agenda» que usa Mi día: organizo o asisto.
        //
        // IgnoreQueryFilters es deliberado y no un atajo: la petición llega sin sesión, así que
        // ICurrentUserService no resuelve a nadie y el filtro de alcance dejaría el feed vacío
        // siempre. El alcance lo da el WHERE de abajo, que es más estricto que el institucional:
        // solo las reuniones de esta persona.
        var reuniones = await ctx.Reuniones
            .IgnoreQueryFilters()
            .Include(r => r.Asistentes)
            .Where(r => !r.IsDeleted
                     && r.Fecha != null && r.Fecha >= desde && r.Fecha <= hasta
                     && (r.CreadoPorId == usuario.Id
                         || r.Asistentes.Any(a => a.Correo != null && a.Correo.ToLower() == correo)))
            .OrderBy(r => r.Fecha)
            .ToListAsync(ct);

        var inst    = institucion.Value;
        var dominio = IcsDominio.De(inst);

        // Sin lista de asistentes: en la agenda propia no aporta y publicaría los correos de todos
        // los participantes en un archivo que se sirve sin sesión.
        var eventos = reuniones.Select(r => ReunionIcs.Mapear(r, reloj, dominio, incluirAsistentes: false));

        var contenido = IcsWriter.Escribir(
            eventos,
            IcsDominio.ProdId(inst),
            nombreCalendario: $"{inst.NombreCorto} — Agenda de {usuario.Nombre}");

        return new ArchivoIcs(contenido, "agenda.ics");
    }
}

// ── Identidad del calendario ──────────────────────────────────────────────────

/// <summary>Cómo se identifica el portal ante los clientes de calendario.</summary>
internal static class IcsDominio
{
    /// <summary>Dominio para los UID. Sale del sitio institucional configurado; si no se puede
    /// interpretar como URL, se usa tal cual —el UID solo necesita ser estable y único, no
    /// resolver en DNS—.</summary>
    public static string De(InstitucionOptions inst) =>
        Uri.TryCreate(inst.SitioWeb, UriKind.Absolute, out var u) ? u.Host : "portal.local";

    public static string ProdId(InstitucionOptions inst) =>
        $"{inst.NombreCorto}//Portal de Trámites";
}
