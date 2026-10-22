using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using static Diger.TramitesEstado.Application.Notificaciones.PlantillasCorreo;

namespace Diger.TramitesEstado.Application.Notificaciones.Commands.EnviarRecordatorioManual;

/// <summary>
/// Lo que pasó con un recordatorio manual, para que la pantalla diga la verdad: el aviso de la
/// campana se guarda siempre, pero el correo puede fallar o no estar configurado (antes la
/// pantalla decía «enviado exitosamente» en todos los casos; corregido el 2026-10-22).
/// </summary>
/// <param name="AvisosCampana">Personas con usuario en el portal que recibieron el aviso.</param>
/// <param name="CorreosIntentados">Correos que se intentó mandar.</param>
/// <param name="CorreosEnviados">Correos que el servidor aceptó.</param>
/// <param name="ServidorSinConfigurar">La instalación no tiene servidor de correo: no se intentó.</param>
public sealed record ResultadoRecordatorio(
    int AvisosCampana, int CorreosIntentados, int CorreosEnviados, bool ServidorSinConfigurar)
{
    public int CorreosFallidos => ServidorSinConfigurar ? 0 : CorreosIntentados - CorreosEnviados;
}

/// <summary>Cuenta los envíos de correo de un recordatorio.</summary>
internal sealed class ConteoCorreos
{
    public int  Intentados    { get; private set; }
    public int  Enviados      { get; private set; }
    public bool SinConfigurar { get; private set; }

    public void Anotar(ResultadoCorreo r)
    {
        Intentados++;
        if (r == ResultadoCorreo.Enviado)       Enviados++;
        if (r == ResultadoCorreo.SinConfigurar) SinConfigurar = true;
    }

    public ResultadoRecordatorio Resultado(int avisosCampana) =>
        new(avisosCampana, Intentados, Enviados, SinConfigurar);
}

// ── 1. TICKET ─────────────────────────────────────────────────────────────
public sealed record EnviarRecordatorioTicketCommand(int TicketId, string? Mensaje = null) : IRequest<ResultadoRecordatorio>;

public sealed class EnviarRecordatorioTicketCommandHandler(
    IApplicationDbContext ctx,
    INotificacionService notifSvc,
    IEmailService emailSvc,
    IOptions<CorreoOptions> correo,
    IOptions<InstitucionOptions> institucion)
    : IRequestHandler<EnviarRecordatorioTicketCommand, ResultadoRecordatorio>
{
    public async Task<ResultadoRecordatorio> Handle(EnviarRecordatorioTicketCommand request, CancellationToken ct)
    {
        var ticket = await ctx.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, ct)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        int notificados = 0;
        var correos = new ConteoCorreos();
        HashSet<Guid> usuariosNotificados = [];

        // Notificar al agente asignado
        if (ticket.AsignadoAId is Guid agenteId)
        {
            var agente = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Id == agenteId, ct);
            if (agente != null && usuariosNotificados.Add(agente.Id))
            {
                var tituloNotif = $"Recordatorio Ticket {ticket.Numero}: {ticket.Titulo}";
                var url = $"/Tickets/Detalle/{ticket.Id}";
                await notifSvc.EncolarAsync(agente.Id, TipoNotificacion.RecordatorioManualTicket, tituloNotif, url, ct: ct);
                notificados++;

                if (!string.IsNullOrWhiteSpace(agente.Correo))
                {
                    var msgExtra = string.IsNullOrWhiteSpace(request.Mensaje) ? "" : $"<p><strong>Nota:</strong> {E(request.Mensaje)}</p>";
                    var body = $"<p>Estimado(a) <strong>{E(agente.Nombre)}</strong>,</p>" +
                               $"<p>Se le ha enviado un recordatorio respecto al ticket de soporte <strong>{E(ticket.Numero)}</strong>: {E(ticket.Titulo)}.</p>" +
                               $"{msgExtra}" +
                               $"<p>Puede dar seguimiento haciendo clic en el siguiente enlace:</p>" +
                               $"<p><a href='{E(correo.Value.Enlace(url))}'>Ver Detalle del Ticket</a></p>";
                    correos.Anotar(await emailSvc.SendEmailAsync(agente.Correo, $"Recordatorio Ticket {ticket.Numero} — {institucion.Value.NombreCorto}", body, TipoCorreo.RecordatorioTicket, ct));
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
                await notifSvc.EncolarAsync(reportante.Id, TipoNotificacion.RecordatorioManualTicket, tituloNotif, url, ct: ct);
                notificados++;
            }

            var msgExtraRep = string.IsNullOrWhiteSpace(request.Mensaje) ? "" : $"<p><strong>Nota:</strong> {E(request.Mensaje)}</p>";
            var bodyRep = $"<p>Estimado(a) <strong>{E(ticket.ReportanteNombre ?? "Usuario")}</strong>,</p>" +
                          $"<p>Le enviamos un recordatorio sobre su ticket de soporte <strong>{E(ticket.Numero)}</strong>: {E(ticket.Titulo)}.</p>" +
                          $"{msgExtraRep}" +
                          $"<p>Puede revisar el estado de su solicitud en el portal.</p>";
            correos.Anotar(await emailSvc.SendEmailAsync(ticket.ReportanteCorreo, $"Recordatorio Ticket {ticket.Numero} — {institucion.Value.NombreCorto}", bodyRep, TipoCorreo.RecordatorioTicket, ct));
        }

        await ctx.SaveChangesAsync(ct);
        return correos.Resultado(notificados);
    }
}

// ── 2. EXPEDIENTE ─────────────────────────────────────────────────────────
public sealed record EnviarRecordatorioExpedienteCommand(int ExpedienteId, string? Mensaje = null) : IRequest<ResultadoRecordatorio>;

public sealed class EnviarRecordatorioExpedienteCommandHandler(
    IApplicationDbContext ctx,
    INotificacionService notifSvc,
    IEmailService emailSvc,
    IOptions<CorreoOptions> correo,
    IOptions<InstitucionOptions> institucion)
    : IRequestHandler<EnviarRecordatorioExpedienteCommand, ResultadoRecordatorio>
{
    public async Task<ResultadoRecordatorio> Handle(EnviarRecordatorioExpedienteCommand request, CancellationToken ct)
    {
        var exp = await ctx.Expedientes.FirstOrDefaultAsync(e => e.Id == request.ExpedienteId, ct)
            ?? throw new NotFoundException(nameof(Expediente), request.ExpedienteId);

        int notificados = 0;
        var correos = new ConteoCorreos();
        HashSet<Guid> usuariosNotificados = [];

        // Notificar al analista asignado
        if (exp.AnalistaId is Guid analistaId)
        {
            var analista = await ctx.Usuarios.FirstOrDefaultAsync(u => u.Id == analistaId, ct);
            if (analista != null && usuariosNotificados.Add(analista.Id))
            {
                var tituloNotif = $"Recordatorio Expediente {exp.Codigo}: {exp.Institucion}";
                var url = $"/Expedientes/Editor/{exp.Id}";
                await notifSvc.EncolarAsync(analista.Id, TipoNotificacion.RecordatorioManualExpediente, tituloNotif, url, ct: ct);
                notificados++;

                if (!string.IsNullOrWhiteSpace(analista.Correo))
                {
                    var msgExtra = string.IsNullOrWhiteSpace(request.Mensaje) ? "" : $"<p><strong>Nota:</strong> {E(request.Mensaje)}</p>";
                    var body = $"<p>Estimado(a) <strong>{E(analista.Nombre)}</strong>,</p>" +
                               $"<p>Se le ha enviado un recordatorio relativo al expediente <strong>{E(exp.Codigo)}</strong> de la institución <strong>{E(exp.Institucion)}</strong>.</p>" +
                               $"{msgExtra}" +
                               $"<p><a href='{E(correo.Value.Enlace(url))}'>Abrir Expediente</a></p>";
                    correos.Anotar(await emailSvc.SendEmailAsync(analista.Correo, $"Recordatorio Expediente {exp.Codigo} — {institucion.Value.NombreCorto}", body, TipoCorreo.RecordatorioExpediente, ct));
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
                await notifSvc.EncolarAsync(contactoUser.Id, TipoNotificacion.RecordatorioManualExpediente, tituloNotif, url, ct: ct);
                notificados++;
            }

            var msgExtraContact = string.IsNullOrWhiteSpace(request.Mensaje) ? "" : $"<p><strong>Nota:</strong> {E(request.Mensaje)}</p>";
            var bodyContact = $"<p>Estimado(a) <strong>{E(exp.ContactoNombre ?? "Contacto Institucional")}</strong>,</p>" +
                              $"<p>Le enviamos un recordatorio sobre el expediente de digitalización <strong>{E(exp.Codigo)}</strong> correspondiente a {E(exp.Institucion)}.</p>" +
                              $"{msgExtraContact}";
            correos.Anotar(await emailSvc.SendEmailAsync(exp.ContactoCorreo, $"Recordatorio Expediente {exp.Codigo} — {institucion.Value.NombreCorto}", bodyContact, TipoCorreo.RecordatorioExpediente, ct));
        }

        await ctx.SaveChangesAsync(ct);
        return correos.Resultado(notificados);
    }
}

// ── 3. REUNIÓN ────────────────────────────────────────────────────────────
public sealed record EnviarRecordatorioReunionCommand(int ReunionId, string? Mensaje = null) : IRequest<ResultadoRecordatorio>;

public sealed class EnviarRecordatorioReunionCommandHandler(
    IApplicationDbContext ctx,
    INotificacionService notifSvc,
    IEmailService emailSvc,
    IOptions<CorreoOptions> correo,
    IOptions<InstitucionOptions> institucion)
    : IRequestHandler<EnviarRecordatorioReunionCommand, ResultadoRecordatorio>
{
    public async Task<ResultadoRecordatorio> Handle(EnviarRecordatorioReunionCommand request, CancellationToken ct)
    {
        var reunion = await ctx.Reuniones
            .Include(r => r.Asistentes)
            .FirstOrDefaultAsync(r => r.Id == request.ReunionId, ct)
            ?? throw new NotFoundException(nameof(Reunion), request.ReunionId);

        int notificados = 0;
        var correos = new ConteoCorreos();
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
                await notifSvc.EncolarAsync(user.Id, TipoNotificacion.RecordatorioManualReunion, $"Recordatorio de Reunión: {reunion.Titulo}", url, ct: ct);
                notificados++;
            }

            var msgExtra = string.IsNullOrWhiteSpace(request.Mensaje) ? "" : $"<p><strong>Nota adicional:</strong> {E(request.Mensaje)}</p>";
            var fechaStr = reunion.Fecha?.ToString("dd/MM/yyyy") ?? "Próximamente";
            var body = $"<p>Estimado(a) <strong>{E(asist.Nombre)}</strong>,</p>" +
                       $"<p>Le enviamos un recordatorio sobre la reunión: <strong>{E(reunion.Titulo)}</strong> programada para el <strong>{fechaStr}</strong>.</p>" +
                       $"{msgExtra}" +
                       $"<p><a href='{E(correo.Value.Enlace(url))}'>Ver Acta / Detalles de la Reunión</a></p>";

            correos.Anotar(await emailSvc.SendEmailAsync(asist.Correo, $"Recordatorio de Reunión: {reunion.Titulo} — {institucion.Value.NombreCorto}", body, TipoCorreo.RecordatorioReunion, ct));
        }

        await ctx.SaveChangesAsync(ct);
        return correos.Resultado(notificados);
    }
}

// ── 4. COMPROMISO ─────────────────────────────────────────────────────────
public sealed record EnviarRecordatorioCompromisoCommand(int CompromisoId, string? Mensaje = null) : IRequest<ResultadoRecordatorio>;

public sealed class EnviarRecordatorioCompromisoCommandHandler(
    IApplicationDbContext ctx,
    INotificacionService notifSvc,
    IEmailService emailSvc,
    IOptions<CorreoOptions> correo,
    IOptions<InstitucionOptions> institucion)
    : IRequestHandler<EnviarRecordatorioCompromisoCommand, ResultadoRecordatorio>
{
    public async Task<ResultadoRecordatorio> Handle(EnviarRecordatorioCompromisoCommand request, CancellationToken ct)
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
        var correos = new ConteoCorreos();
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
            await notifSvc.EncolarAsync(uid, TipoNotificacion.RecordatorioManualCompromiso, $"Recordatorio de Compromiso: {acuerdo.Compromiso}", url, ct: ct);
            notificados++;
        }

        if (!string.IsNullOrWhiteSpace(correoTarget))
        {
            var plazoStr = acuerdo.Plazo?.ToString("dd/MM/yyyy") ?? "Sin plazo fijo";
            var msgExtra = string.IsNullOrWhiteSpace(request.Mensaje) ? "" : $"<p><strong>Nota:</strong> {E(request.Mensaje)}</p>";
            var body = $"<p>Estimado(a) <strong>{E(acuerdo.Responsable ?? "Responsable")}</strong>,</p>" +
                       $"<p>Le enviamos un recordatorio sobre el compromiso pendiente derivado de la reunión <strong>{E(reunion.Titulo)}</strong>:</p>" +
                       $"<blockquote style='border-left:4px solid #1455a4;padding-left:10px;color:#334155'><strong>Compromiso:</strong> {E(acuerdo.Compromiso)}<br><strong>Plazo:</strong> {plazoStr}</blockquote>" +
                       $"{msgExtra}" +
                       $"<p><a href='{E(correo.Value.Enlace(url))}'>Ver Avance y Detalle del Compromiso</a></p>";
            correos.Anotar(await emailSvc.SendEmailAsync(correoTarget, $"Recordatorio de Compromiso — {institucion.Value.NombreCorto}", body, TipoCorreo.RecordatorioCompromiso, ct));
        }

        await ctx.SaveChangesAsync(ct);
        return correos.Resultado(notificados);
    }
}
