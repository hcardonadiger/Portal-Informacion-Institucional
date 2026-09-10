using Diger.TramitesEstado.Application.MiDia.Common;

namespace Diger.TramitesEstado.Application.MiDia.Queries.GetMiDia;

/// <summary>
/// Todo lo que la persona conectada tiene que cerrar, de los cuatro módulos que le asignan trabajo,
/// en una sola lista ordenada por urgencia.
///
/// <para><b>Por qué existe teniendo «Mi Tablero»</b>: aquél agrupa por módulo y responde «¿cómo voy
/// en cada cosa?». Esta responde «¿qué hago hoy?», que es una pregunta distinta y hoy obliga a
/// recorrer cinco pantallas —compromisos, actividades, metas, tickets y agenda— para contestarla.
/// Ninguna de las dos reemplaza a la otra.</para>
///
/// <para><b>El usuario no viaja en la petición</b>, igual que en <c>GetMisActividadesQuery</c>: lo
/// resuelve <c>ICurrentUserService</c>, para que nadie pida la bandeja de otro cambiando un valor
/// en el navegador. Encima actúan los filtros globales de <c>AppDbContext</c>, así que un
/// pendiente de una institución fuera del alcance no aparece aunque esté a su nombre.</para>
/// </summary>
/// <param name="IncluirMasAdelante">Por omisión la bandeja llega hasta siete días. Lo que vence
/// después, y lo que no tiene fecha, se pide aparte: si aparece todos los días deja de leerse.</param>
public sealed record GetMiDiaQuery(bool IncluirMasAdelante = false) : IRequest<MiDiaDto>;

public sealed class GetMiDiaQueryHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMiDiaQuery, MiDiaDto>
{
    public async Task<MiDiaDto> Handle(GetMiDiaQuery q, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated)
            return MiDiaDto.Vacio;

        var userId = currentUser.UserId;
        var correo = currentUser.Correo?.Trim().ToLowerInvariant();
        var nombre = currentUser.Nombre?.Trim().ToLowerInvariant();
        var hoy    = DateOnly.FromDateTime(DateTime.Now);

        var pendientes = new List<PendienteDto>();

        // ── 1. Compromisos de reunión ─────────────────────────────────────────────
        // El criterio de «mío» es el mismo de Mi Tablero, a propósito: si las dos pantallas
        // respondieran distinto a «¿cuáles son mis compromisos?», la diferencia se leería como un
        // error del portal. Incluye el calce por nombre porque los compromisos anteriores al
        // directorio guardan al responsable como texto libre; es laxo —un nombre corto puede calzar
        // de más— y se corregirá cuando todos apunten a un contacto.
        List<int> contactoIds = [];
        if (!string.IsNullOrEmpty(correo))
        {
            contactoIds = await ctx.Contactos.AsNoTracking()
                .Where(c => c.Correo != null && c.Correo.ToLower() == correo)
                .Select(c => c.Id)
                .ToListAsync(ct);
        }

        var compromisos = await (
            from a in ctx.Acuerdos.AsNoTracking()
            join r in ctx.Reuniones.AsNoTracking() on a.ReunionId equals r.Id
            where a.Estado != EstadoCompromiso.Cumplido && a.Estado != EstadoCompromiso.Cancelado
               && ((a.ResponsableContactoId != null && contactoIds.Contains(a.ResponsableContactoId.Value))
                   || (correo != null && a.Responsable != null && a.Responsable.ToLower().Contains(correo))
                   || (nombre != null && a.Responsable != null && a.Responsable.ToLower().Contains(nombre)))
            select new { a.Id, a.Compromiso, a.Plazo, a.Estado, Reunion = r.Titulo, r.Institucion })
            .Take(TopePorOrigen)
            .ToListAsync(ct);

        foreach (var c in compromisos)
        {
            pendientes.Add(new PendienteDto(
                OrigenPendiente.Compromiso,
                c.Id,
                c.Compromiso,
                c.Reunion,
                "/Reuniones/CompromisoDetalle",
                c.Plazo,
                EtiquetaCompromiso(c.Estado),
                c.Institucion,
                // Único origen que avanza desde acá: el módulo ya tiene el comando liviano para
                // decir «lo terminé» sin abrir el detalle.
                PermiteCierreRapido: true));
        }

        // ── 2. Actividades de proyecto ────────────────────────────────────────────
        // Mismas exclusiones que «Mis actividades»: un proyecto cerrado o cancelado no le deja
        // trabajo pendiente a nadie aunque sus actividades sigan asignadas.
        if (userId is { } uid)
        {
            var actividades = await (
                from a in ctx.ProyectoActividades.AsNoTracking()
                join e in ctx.ProyectoEntregables.AsNoTracking() on a.EntregableId equals e.Id
                join p in ctx.Proyectos.AsNoTracking() on e.ProyectoId equals p.Id
                where a.ResponsableId == uid
                   && a.Estado != EstadoActividad.Completada && a.Estado != EstadoActividad.Cancelada
                   && p.Estado != EstadoProyecto.Cerrado && p.Estado != EstadoProyecto.Cancelado
                select new
                {
                    a.Nombre, a.FechaFinPlan, a.AvancePct, a.Estado,
                    Entregable = e.Nombre,
                    ProyectoId = p.Id, p.Codigo, Proyecto = p.Nombre
                })
                .Take(TopePorOrigen)
                .ToListAsync(ct);

            foreach (var a in actividades)
            {
                pendientes.Add(new PendienteDto(
                    OrigenPendiente.Actividad,
                    a.ProyectoId,   // el enlace abre el proyecto: la actividad se reporta desde su estructura
                    a.Nombre,
                    $"{a.Codigo} · {a.Proyecto}",
                    "/Proyectos/Editor",
                    a.FechaFinPlan,
                    a.Estado == EstadoActividad.EnProceso ? $"En proceso · {a.AvancePct}%" : "Pendiente",
                    a.Entregable));
            }

            // ── 3. Metas del plan de trabajo ──────────────────────────────────────
            // Solo de planes activos: las metas de un borrador todavía no son un compromiso, y las
            // de un plan cerrado ya no se persiguen.
            var metas = await (
                from m in ctx.MetasTrabajo.AsNoTracking()
                join p in ctx.PlanTrabajos.AsNoTracking() on m.PlanTrabajoId equals p.Id
                where m.ResponsableId == uid
                   && m.Estado != EstadoMeta.Cumplida && m.Estado != EstadoMeta.Cancelada
                   && p.Estado == EstadoPlanTrabajo.Activo
                select new { PlanId = p.Id, p.Anio, p.Institucion, m.NombreTramite, m.FechaEstimadaFin, m.Estado })
                .Take(TopePorOrigen)
                .ToListAsync(ct);

            foreach (var m in metas)
            {
                pendientes.Add(new PendienteDto(
                    OrigenPendiente.Meta,
                    m.PlanId,
                    m.NombreTramite,
                    $"Plan de trabajo {m.Anio}",
                    "/PlanTrabajo/Editor",
                    m.FechaEstimadaFin,
                    EtiquetaMeta(m.Estado),
                    m.Institucion));
            }

            // ── 4. Tickets asignados ──────────────────────────────────────────────
            // La fecha del ticket no está guardada: sale del SLA en horas del tema. Un tema sin SLA
            // deja el ticket sin fecha, y por eso la bandeja tiene una franja «sin fecha» en vez de
            // inventarle un vencimiento.
            var tickets = await (
                from t in ctx.Tickets.AsNoTracking()
                where t.AsignadoAId == uid
                   && (t.Estado == EstadoTicket.Abierto || t.Estado == EstadoTicket.EnProgreso)
                select new
                {
                    t.Id, t.Numero, t.Titulo, t.Estado, t.Prioridad, t.CreatedAt,
                    Tema      = ctx.TemasTicket.Where(m => m.Id == t.TemaId).Select(m => m.Nombre).FirstOrDefault(),
                    HorasSla  = ctx.TemasTicket.Where(m => m.Id == t.TemaId).Select(m => (int?)m.HorasResolucion).FirstOrDefault(),
                    t.TemaOtro
                })
                .Take(TopePorOrigen)
                .ToListAsync(ct);

            foreach (var t in tickets)
            {
                pendientes.Add(new PendienteDto(
                    OrigenPendiente.Ticket,
                    t.Id,
                    t.Titulo,
                    $"{t.Numero} · {t.Tema ?? t.TemaOtro ?? "General"}",
                    "/Tickets/Detalle",
                    VencimientoSla(t.CreatedAt, t.HorasSla),
                    t.Estado == EstadoTicket.EnProgreso ? "En progreso" : "Abierto",
                    $"Prioridad {t.Prioridad}"));
            }
        }

        // ── 5. Agenda propia ──────────────────────────────────────────────────────
        // Organizador o asistente, de hoy en adelante y dentro del mismo horizonte de la bandeja.
        var hasta = hoy.AddDays(PendienteDto.DiasSemana);
        var agenda = await ctx.Reuniones.AsNoTracking()
            .Where(r => r.Fecha != null && r.Fecha >= hoy && r.Fecha <= hasta
                     && ((userId != null && r.CreadoPorId == userId)
                         || (correo != null && r.Asistentes.Any(a => a.Correo != null && a.Correo.ToLower() == correo))))
            .OrderBy(r => r.Fecha).ThenBy(r => r.Hora)
            .Select(r => new
            {
                r.Id, r.Titulo, Fecha = r.Fecha!.Value, r.Hora, r.Lugar, r.Institucion, r.CreadoPorId
            })
            .Take(TopePorOrigen)
            .ToListAsync(ct);

        var citas = agenda
            .Select(r => new CitaDto(r.Id, r.Titulo, r.Fecha, r.Hora, r.Lugar, r.Institucion,
                                     userId != null && r.CreadoPorId == userId))
            .ToList();

        // ── Contadores y orden ────────────────────────────────────────────────────
        // Los contadores se calculan sobre todo lo abierto, no sobre lo que se muestra: es lo que
        // permite decir «hay 12 más adelante» sin listarlos.
        var visibles = (q.IncluirMasAdelante ? pendientes : pendientes.Where(p => p.EnHorizonte))
            .OrderBy(p => p.Franja)                              // Atrasado primero, «sin fecha» al final
            .ThenBy(p => p.Fecha ?? DateOnly.MaxValue)
            .ThenBy(p => p.Origen)
            .ThenBy(p => p.Titulo)
            .ToList();

        return new MiDiaDto(
            visibles,
            citas,
            Atrasados:     pendientes.Count(p => p.Franja == FranjaDia.Atrasado),
            ParaHoy:       pendientes.Count(p => p.Franja == FranjaDia.Hoy),
            ParaManana:    pendientes.Count(p => p.Franja == FranjaDia.Manana),
            EstaSemana:    pendientes.Count(p => p.Franja == FranjaDia.EstaSemana),
            MasAdelante:   pendientes.Count(p => p.Franja == FranjaDia.MasAdelante),
            SinFecha:      pendientes.Count(p => p.Franja == FranjaDia.SinFecha),
            TotalAbiertos: pendientes.Count,
            Ocultos:       pendientes.Count - visibles.Count);
    }

    /// <summary>Tope defensivo por origen. El trabajo asignado a una persona está acotado por
    /// naturaleza; el límite existe para que un dato sucio no traiga miles de filas a memoria.</summary>
    private const int TopePorOrigen = 200;

    /// <summary>
    /// Vencimiento que resulta del SLA del tema. <c>CreatedAt</c> es UTC y la bandeja razona en día
    /// local, así que se convierte antes de recortar la hora.
    /// </summary>
    private static DateOnly? VencimientoSla(DateTime creadoUtc, int? horasSla)
    {
        if (horasSla is not int h || h <= 0) return null;
        var limite = DateTime.SpecifyKind(creadoUtc, DateTimeKind.Utc).AddHours(h).ToLocalTime();
        return DateOnly.FromDateTime(limite);
    }

    // Las etiquetas repiten el vocabulario de Web/Models/CompromisoUi.cs. Se duplican porque
    // Application no conoce a Web; si allá cambia una palabra, hay que cambiarla acá también.
    private static string EtiquetaCompromiso(EstadoCompromiso e) => e switch
    {
        EstadoCompromiso.Pendiente    => "Pendiente a revisar",
        EstadoCompromiso.EnProgreso   => "En proceso",
        EstadoCompromiso.EnRevision   => "En revisión",
        EstadoCompromiso.Reprogramado => "Reprogramado",
        _                             => e.ToString()
    };

    private static string EtiquetaMeta(EstadoMeta e) => e switch
    {
        EstadoMeta.EnProgreso => "En proceso",
        EstadoMeta.Postergada => "Postergada",
        _                     => "Pendiente"
    };
}
