using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetMiTablero;

public sealed record MiCompromisoItemDto(
    int              Id,
    int              ReunionId,
    string           ReunionTitulo,
    string           Compromiso,
    DateOnly?        Plazo,
    EstadoCompromiso Estado,
    bool             Vencido,
    string?          Responsable,
    string?          Institucion);

public sealed record MiReunionItemDto(
    int       Id,
    Guid      Token,
    string    Titulo,
    DateOnly? Fecha,
    string?   Hora,
    string?   Lugar,
    string?   Institucion,
    int       NumAsistentes,
    bool      EsOrganizador,
    bool      Confirmada);

public sealed record MiTicketItemDto(
    int             Id,
    string          Numero,
    string          Titulo,
    EstadoTicket    Estado,
    PrioridadTicket Prioridad,
    string          TemaNombre,
    DateTime        FechaCreacion,
    DateTime?       FechaActualizacion,
    bool            EsMioReportado,
    bool            EsMioAsignado);

public sealed record MiExpedienteItemDto(
    int      Id,
    string   Codigo,
    string   TramiteNombre,
    string   Institucion,
    string   Estado,
    string?  ResponsableNombre,
    int      PorcentajeAvance,
    DateTime FechaCreacion);

public sealed record MiTableroDto(
    int CompromisosPendientes,
    int CompromisosVencidos,
    int ReunionesProximas,
    int TicketsAbiertos,
    int ExpedientesAsignados,
    IReadOnlyList<MiCompromisoItemDto> Compromisos,
    IReadOnlyList<MiReunionItemDto> Reuniones,
    IReadOnlyList<MiTicketItemDto> Tickets,
    IReadOnlyList<MiExpedienteItemDto> Expedientes);

public sealed record GetMiTableroQuery() : IRequest<MiTableroDto>;

public sealed class GetMiTableroQueryHandler(
    IApplicationDbContext ctx,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMiTableroQuery, MiTableroDto>
{
    public async Task<MiTableroDto> Handle(GetMiTableroQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var correo = currentUser.Correo?.Trim().ToLowerInvariant();
        var hoyDateOnly = DateOnly.FromDateTime(DateTime.Today);

        // 1. COMPROMISOS / ACUERDOS VINCULADOS
        List<int> contactoIds = [];
        if (!string.IsNullOrEmpty(correo))
        {
            contactoIds = await ctx.Contactos.AsNoTracking()
                .Where(c => c.Correo != null && c.Correo.ToLower() == correo)
                .Select(c => c.Id)
                .ToListAsync(ct);
        }

        var compromisosQuery = from a in ctx.Acuerdos.AsNoTracking()
                               join r in ctx.Reuniones on a.ReunionId equals r.Id
                               where (contactoIds.Count > 0 && a.ResponsableContactoId.HasValue && contactoIds.Contains(a.ResponsableContactoId.Value)) ||
                                     (!string.IsNullOrEmpty(correo) && a.Responsable != null && a.Responsable.ToLower().Contains(correo)) ||
                                     (!string.IsNullOrEmpty(currentUser.Nombre) && a.Responsable != null && a.Responsable.ToLower().Contains(currentUser.Nombre.ToLower()))
                               select new { a, r };

        var rawCompromisos = await compromisosQuery
            .OrderBy(x => x.a.Estado)
            .ThenBy(x => x.a.Plazo)
            .Take(50)
            .ToListAsync(ct);

        var compromisosList = rawCompromisos.Select(x =>
        {
            var vencido = x.a.Plazo.HasValue && x.a.Plazo.Value < hoyDateOnly &&
                          (x.a.Estado == EstadoCompromiso.Pendiente || x.a.Estado == EstadoCompromiso.EnProgreso || x.a.Estado == EstadoCompromiso.Reprogramado || x.a.Estado == EstadoCompromiso.EnRevision);
            return new MiCompromisoItemDto(
                x.a.Id,
                x.a.ReunionId,
                x.r.Titulo,
                x.a.Compromiso,
                x.a.Plazo,
                x.a.Estado,
                vencido,
                x.a.Responsable,
                x.r.Institucion
            );
        }).ToList();

        int compPendientes = compromisosList.Count(c => c.Estado != EstadoCompromiso.Cumplido && c.Estado != EstadoCompromiso.Cancelado);
        int compVencidos = compromisosList.Count(c => c.Vencido);

        // 2. REUNIONES VINCULADAS (Asistente o Creador)
        var reunionesQuery = ctx.Reuniones.AsNoTracking()
            .Where(r => (userId.HasValue && r.CreadoPorId == userId.Value) ||
                        (!string.IsNullOrEmpty(correo) && r.Asistentes.Any(a => a.Correo != null && a.Correo.ToLower() == correo)));

        var reunionesList = await reunionesQuery
            .OrderByDescending(r => r.Fecha)
            .Take(40)
            .Select(r => new MiReunionItemDto(
                r.Id,
                r.RegistroToken,
                r.Titulo,
                r.Fecha,
                r.Hora,
                r.Lugar,
                r.Institucion,
                r.Asistentes.Count(a => !a.EsPreregistro || (a.Confirmado == true)),
                userId.HasValue && r.CreadoPorId == userId.Value,
                !string.IsNullOrEmpty(correo) && r.Asistentes.Any(a => a.Correo != null && a.Correo.ToLower() == correo && (a.Confirmado == true))
            ))
            .ToListAsync(ct);

        int reunProximas = reunionesList.Count(r => r.Fecha.HasValue && r.Fecha.Value >= hoyDateOnly);

        // 3. TICKETS DE SOPORTE VINCULADOS (Reportante o Agente)
        var ticketsQuery = ctx.Tickets.AsNoTracking()
            .Where(t => (userId.HasValue && (t.AsignadoAId == userId.Value || t.CreadoPorId == userId.Value)) ||
                        (!string.IsNullOrEmpty(correo) && t.ReportanteCorreo != null && t.ReportanteCorreo.ToLower() == correo));

        var ticketsList = await ticketsQuery
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .Take(40)
            .Select(t => new MiTicketItemDto(
                t.Id,
                t.Numero,
                t.Titulo,
                t.Estado,
                t.Prioridad,
                ctx.TemasTicket.Where(m => m.Id == t.TemaId).Select(m => m.Nombre).FirstOrDefault() ?? t.TemaOtro ?? "General",
                t.CreatedAt,
                t.UpdatedAt,
                !string.IsNullOrEmpty(correo) && t.ReportanteCorreo != null && t.ReportanteCorreo.ToLower() == correo,
                userId.HasValue && t.AsignadoAId == userId.Value
            ))
            .ToListAsync(ct);

        int tickAbiertos = ticketsList.Count(t => t.Estado == EstadoTicket.Abierto || t.Estado == EstadoTicket.EnProgreso);

        // 4. EXPEDIENTES VINCULADOS (Analista o Contacto)
        var expedientesQuery = ctx.Expedientes.AsNoTracking()
            .Where(e => (userId.HasValue && e.AnalistaId == userId.Value) ||
                        (!string.IsNullOrEmpty(correo) && e.ContactoCorreo != null && e.ContactoCorreo.ToLower() == correo));

        var rawExpedientes = await expedientesQuery
            .OrderByDescending(e => e.CreatedAt)
            .Take(40)
            .Select(e => new
            {
                e.Id,
                e.Codigo,
                TramiteNombre = e.Tramites.Select(t => t.NombreTramite).FirstOrDefault() ?? "Digitalización de Trámites",
                e.Institucion,
                Estado = e.EstadoExpediente,
                e.Analista,
                e.CreatedAt
            })
            .ToListAsync(ct);

        var expedientesList = rawExpedientes.Select(e => new MiExpedienteItemDto(
            e.Id,
            e.Codigo,
            e.TramiteNombre,
            e.Institucion,
            EstadoLabel(e.Estado),
            e.Analista,
            e.Estado == EstadoExpediente.Cerrado ? 100 :
            e.Estado == EstadoExpediente.EnValidacion ? 80 :
            e.Estado == EstadoExpediente.EnModelado ? 60 :
            e.Estado == EstadoExpediente.EnLevantamiento ? 40 : 20,
            e.CreatedAt
        )).ToList();

        int expAsignados = expedientesList.Count(e => e.Estado != "Cerrado");

        return new MiTableroDto(
            compPendientes,
            compVencidos,
            reunProximas,
            tickAbiertos,
            expAsignados,
            compromisosList,
            reunionesList,
            ticketsList,
            expedientesList
        );
    }

    private static string EstadoLabel(EstadoExpediente e) => e switch
    {
        EstadoExpediente.EnExploracion   => "En exploración",
        EstadoExpediente.EnLevantamiento => "En levantamiento",
        EstadoExpediente.EnModelado      => "En modelado",
        EstadoExpediente.EnValidacion    => "En validación",
        EstadoExpediente.Cerrado         => "Cerrado",
        _ => e.ToString()
    };
}
