using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Application.Notificaciones.Commands.EnviarRecordatorioManual;

// ── 1. TICKET ─────────────────────────────────────────────────────────────
public sealed record EnviarRecordatorioTicketCommand(int TicketId, string? Mensaje = null) : IRequest<int>;

public sealed class EnviarRecordatorioTicketCommandHandler(
    IApplicationDbContext ctx,
    INotificacionService notifSvc,
    IEmailService emailSvc)
    : IRequestHandler<EnviarRecordatorioTicketCommand, int>
{
    public async Task<int> Handle(EnviarRecordatorioTicketCommand request, CancellationToken ct)
    {
        var ticket = await ctx.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, ct)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        int notificados = 0;
        HashSet<Guid> usuariosNotificados = [];

        // Notificar al agente asignado
        if (ticket.AsignadoAId is Guid agenteId)
        {
            var agente = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Id == agenteId, ct);
            if (agente != null && usuariosNotificados.Add(agente.Id))
            {
                var tituloNotif = $"Recordatorio Ticket {ticket.Numero}: {ticket.Titulo}";
                var url = $"/Tickets/Detalle/{ticket.Id}";
                notifSvc.Encolar(agente.Id, TipoNotificacion.RecordatorioManualTicket, tituloNotif, url);
                notificados++;

                if (!string.IsNullOrWhiteSpace(agente.Correo))
                {
                    var msgExtra = string.IsNullOrWhiteSpace(request.Mensaje) ? "" : $"<p><strong>Nota:</strong> {request.Mensaje}</p>";
                    var body = $"<p>Estimado(a) <strong>{agente.Nombre}</strong>,</p>" +
                               $"<p>Se le ha enviado un recordatorio respecto al ticket de soporte <strong>{ticket.Numero}</strong>: {ticket.Titulo}.</p>" +
                               $"{msgExtra}" +
                               $"<p>Puede dar seguimiento haciendo clic en el siguiente enlace:</p>" +
                               $"<p><a href='{url}'>Ver Detalle del Ticket</a></p>";
                    await emailSvc.SendEmailAsync(agente.Correo, $"Recordatorio Ticket {ticket.Numero} — DIGER", body, ct);
                }
            }
        }

        // Notificar al reportante si es un usuario registrado
        if (!string.IsNullOrWhiteSpace(ticket.ReportanteCorreo))
        {
            var reportante = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Correo.ToLower() == ticket.ReportanteCorreo.ToLower(), ct);
            if (reportante != null && usuariosNotificados.Add(reportante.Id))
            {
                var tituloNotif = $"Recordatorio Ticket {ticket.Numero}: {ticket.Titulo}";
                var url = $"/Tickets/Detalle/{ticket.Id}";
                notifSvc.Encolar(reportante.Id, TipoNotificacion.RecordatorioManualTicket, tituloNotif, url);
                notificados++;
            }

            var msgExtraRep = string.IsNullOrWhiteSpace(request.Mensaje) ? "" : $"<p><strong>Nota:</strong> {request.Mensaje}</p>";
            var bodyRep = $"<p>Estimado(a) <strong>{ticket.ReportanteNombre ?? "Usuario"}</strong>,</p>" +
                          $"<p>Le enviamos un recordatorio sobre su ticket de soporte <strong>{ticket.Numero}</strong>: {ticket.Titulo}.</p>" +
                          $"{msgExtraRep}" +
                          $"<p>Puede revisar el estado de su solicitud en el portal.</p>";
            await emailSvc.SendEmailAsync(ticket.ReportanteCorreo, $"Recordatorio Ticket {ticket.Numero} — DIGER", bodyRep, ct);
        }

        await ctx.SaveChangesAsync(ct);
        return Math.Max(1, notificados);
    }
}

// ── 2. EXPEDIENTE ─────────────────────────────────────────────────────────
public sealed record EnviarRecordatorioExpedienteCommand(int ExpedienteId, string? Mensaje = null) : IRequest<int>;

public sealed class EnviarRecordatorioExpedienteCommandHandler(
    IApplicationDbContext ctx,
    INotificacionService notifSvc,
    IEmailService emailSvc)
    : IRequestHandler<EnviarRecordatorioExpedienteCommand, int>
{
    public async Task<int> Handle(EnviarRecordatorioExpedienteCommand request, CancellationToken ct)
    {
        var exp = await ctx.Expedientes.FirstOrDefaultAsync(e => e.Id == request.ExpedienteId, ct)
            ?? throw new NotFoundException(nameof(Expediente), request.ExpedienteId);

        int notificados = 0;
        HashSet<Guid> usuariosNotificados = [];

        // Notificar al analista asignado
        if (exp.AnalistaId is Guid analistaId)
        {
            var analista = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Id == analistaId, ct);
            if (analista != null && usuariosNotificados.Add(analista.Id))
            {
                var tituloNotif = $"Recordatorio Expediente {exp.Codigo}: {exp.Institucion}";
                var url = $"/Expedientes/Editor/{exp.Id}";
                notifSvc.Encolar(analista.Id, TipoNotificacion.RecordatorioManualExpediente, tituloNotif, url);
                notificados++;

                if (!string.IsNullOrWhiteSpace(analista.Correo))
                {
                    var msgExtra = string.IsNullOrWhiteSpace(request.Mensaje) ? "" : $"<p><strong>Nota:</strong> {request.Mensaje}</p>";
                    var body = $"<p>Estimado(a) <strong>{analista.Nombre}</strong>,</p>" +
                               $"<p>Se le ha enviado un recordatorio relativo al expediente <strong>{exp.Codigo}</strong> de la institución <strong>{exp.Institucion}</strong>.</p>" +
                               $"{msgExtra}" +
                               $"<p><a href='{url}'>Abrir Expediente</a></p>";
                    await emailSvc.SendEmailAsync(analista.Correo, $"Recordatorio Expediente {exp.Codigo} — DIGER", body, ct);
                }
            }
        }

        // Notificar al contacto institucional
        if (!string.IsNullOrWhiteSpace(exp.ContactoCorreo))
        {
            var contactoUser = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Correo.ToLower() == exp.ContactoCorreo.ToLower(), ct);
            if (contactoUser != null && usuariosNotificados.Add(contactoUser.Id))
            {
                var tituloNotif = $"Recordatorio Expediente {exp.Codigo}: {exp.Institucion}";
                var url = $"/Expedientes/Editor/{exp.Id}";
                notifSvc.Encolar(contactoUser.Id, TipoNotificacion.RecordatorioManualExpediente, tituloNotif, url);
                notificados++;
            }

            var msgExtraContact = string.IsNullOrWhiteSpace(request.Mensaje) ? "" : $"<p><strong>Nota:</strong> {request.Mensaje}</p>";
            var bodyContact = $"<p>Estimado(a) <strong>{exp.ContactoNombre ?? "Contacto Institucional"}</strong>,</p>" +
                              $"<p>Le enviamos un recordatorio sobre el expediente de digitalización <strong>{exp.Codigo}</strong> correspondiente a {exp.Institucion}.</p>" +
                              $"{msgExtraContact}";
            await emailSvc.SendEmailAsync(exp.ContactoCorreo, $"Recordatorio Expediente {exp.Codigo} — DIGER", bodyContact, ct);
        }

        await ctx.SaveChangesAsync(ct);
        return Math.Max(1, notificados);
    }
}

// ── 3. REUNIÓN ────────────────────────────────────────────────────────────
public sealed record EnviarRecordatorioReunionCommand(int ReunionId, string? Mensaje = null) : IRequest<int>;

public sealed class EnviarRecordatorioReunionCommandHandler(
    IApplicationDbContext ctx,
    INotificacionService notifSvc,
    IEmailService emailSvc)
    : IRequestHandler<EnviarRecordatorioReunionCommand, int>
{
    public async Task<int> Handle(EnviarRecordatorioReunionCommand request, CancellationToken ct)
    {
        var reunion = await ctx.Reuniones
            .Include(r => r.Asistentes)
            .FirstOrDefaultAsync(r => r.Id == request.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), request.ReunionId);

        int notificados = 0;
        HashSet<string> correosProcesados = [];
        var url = $"/Reuniones/Acta/{reunion.Id}";

        foreach (var asist in reunion.Asistentes)
        {
            if (string.IsNullOrWhiteSpace(asist.Correo)) continue;
            var correoNorm = asist.Correo.Trim().ToLowerInvariant();

            if (!correosProcesados.Add(correoNorm)) continue;

            var user = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Correo.ToLower() == correoNorm, ct);
            if (user != null)
            {
                notifSvc.Encolar(user.Id, TipoNotificacion.RecordatorioManualReunion, $"Recordatorio de Reunión: {reunion.Titulo}", url);
                notificados++;
            }

            var msgExtra = string.IsNullOrWhiteSpace(request.Mensaje) ? "" : $"<p><strong>Nota adicional:</strong> {request.Mensaje}</p>";
            var fechaStr = reunion.Fecha?.ToString("dd/MM/yyyy") ?? "Próximamente";
            var body = $"<p>Estimado(a) <strong>{asist.Nombre}</strong>,</p>" +
                       $"<p>Le enviamos un recordatorio sobre la reunión: <strong>{reunion.Titulo}</strong> programada para el <strong>{fechaStr}</strong>.</p>" +
                       $"{msgExtra}" +
                       $"<p><a href='{url}'>Ver Acta / Detalles de la Reunión</a></p>";

            await emailSvc.SendEmailAsync(asist.Correo, $"Recordatorio de Reunión: {reunion.Titulo} — DIGER", body, ct);
        }

        await ctx.SaveChangesAsync(ct);
        return Math.Max(1, notificados);
    }
}

// ── 4. COMPROMISO ─────────────────────────────────────────────────────────
public sealed record EnviarRecordatorioCompromisoCommand(int CompromisoId, string? Mensaje = null) : IRequest<int>;

public sealed class EnviarRecordatorioCompromisoCommandHandler(
    IApplicationDbContext ctx,
    INotificacionService notifSvc,
    IEmailService emailSvc)
    : IRequestHandler<EnviarRecordatorioCompromisoCommand, int>
{
    public async Task<int> Handle(EnviarRecordatorioCompromisoCommand request, CancellationToken ct)
    {
        var item = await (
            from a in ctx.Acuerdos
            join r in ctx.Reuniones on a.ReunionId equals r.Id
            where a.Id == request.CompromisoId
            select new { a, r }
        ).FirstOrDefaultAsync(ct) ?? throw new NotFoundException(nameof(AcuerdoReunion), request.CompromisoId);

        var acuerdo = item.a;
        var reunion = item.r;
        int notificados = 0;
        string? correoTarget = null;
        Guid? usuarioTargetId = null;

        if (acuerdo.ResponsableContactoId is int contactoId)
        {
            var contacto = await ctx.Contactos.FirstOrDefaultAsync(c => c.Id == contactoId, ct);
            if (contacto != null && !string.IsNullOrWhiteSpace(contacto.Correo))
            {
                correoTarget = contacto.Correo;
                var user = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Correo.ToLower() == correoTarget.ToLower(), ct);
                if (user != null) usuarioTargetId = user.Id;
            }
        }

        if (correoTarget is null && !string.IsNullOrWhiteSpace(acuerdo.Responsable) && acuerdo.Responsable.Contains('@'))
        {
            correoTarget = acuerdo.Responsable.Trim();
            var user = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Correo.ToLower() == correoTarget.ToLower(), ct);
            if (user != null) usuarioTargetId = user.Id;
        }

        var url = $"/Reuniones/CompromisoDetalle/{acuerdo.Id}";

        if (usuarioTargetId is Guid uid)
        {
            notifSvc.Encolar(uid, TipoNotificacion.RecordatorioManualCompromiso, $"Recordatorio de Compromiso: {acuerdo.Compromiso}", url);
            notificados++;
        }

        if (!string.IsNullOrWhiteSpace(correoTarget))
        {
            var plazoStr = acuerdo.Plazo?.ToString("dd/MM/yyyy") ?? "Sin plazo fijo";
            var msgExtra = string.IsNullOrWhiteSpace(request.Mensaje) ? "" : $"<p><strong>Nota:</strong> {request.Mensaje}</p>";
            var body = $"<p>Estimado(a) <strong>{acuerdo.Responsable ?? "Responsable"}</strong>,</p>" +
                       $"<p>Le enviamos un recordatorio sobre el compromiso pendiente derivado de la reunión <strong>{reunion.Titulo}</strong>:</p>" +
                       $"<blockquote style='border-left:4px solid #1455a4;padding-left:10px;color:#334155'><strong>Compromiso:</strong> {acuerdo.Compromiso}<br><strong>Plazo:</strong> {plazoStr}</blockquote>" +
                       $"{msgExtra}" +
                       $"<p><a href='{url}'>Ver Avance y Detalle del Compromiso</a></p>";
            await emailSvc.SendEmailAsync(correoTarget, $"Recordatorio de Compromiso — DIGER", body, ct);
        }

        await ctx.SaveChangesAsync(ct);
        return Math.Max(1, notificados);
    }
}
