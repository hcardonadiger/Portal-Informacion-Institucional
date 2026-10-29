using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;

public sealed record ResultadoSincronizacionCalendario(int Creadas, int Actualizadas, int Canceladas, int Saltadas);

/// <summary>
/// Trae el calendario de un usuario al portal (spec 2026-10-29). Lo despacha el servicio en segundo
/// plano, sin sesión: por eso todas las lecturas van con IgnoreQueryFilters y el dueño viene en el
/// comando, nunca de ICurrentUserService.
/// </summary>
public sealed record SincronizarCalendarioOutlookCommand(Guid UsuarioId) : IRequest<ResultadoSincronizacionCalendario>;

public sealed class SincronizarCalendarioOutlookCommandHandler(
    IApplicationDbContext ctx,
    ICalendarioOutlookLectura calendario,
    IUnitOfWork uow,
    TimeProvider reloj,
    IOptions<CorreoOptions> correo)
    : IRequestHandler<SincronizarCalendarioOutlookCommand, ResultadoSincronizacionCalendario>
{
    public const int VentanaDias = 90;

    public async Task<ResultadoSincronizacionCalendario> Handle(SincronizarCalendarioOutlookCommand cmd, CancellationToken ct)
    {
        var usuarioId = cmd.UsuarioId;
        var hoy       = DateOnly.FromDateTime(HoraHonduras.Ahora(reloj));
        var hasta     = hoy.AddDays(VentanaDias);
        var utcAhora  = reloj.GetUtcNow().UtcDateTime;

        var citas = await calendario.ListarAsync(usuarioId, hoy, hasta, ct);

        // Las reuniones que nacieron en el portal y ya están en Outlook: su cita (y la copia en el
        // calendario de cada invitado, mismo iCalUId) no se importa otra vez. De la copia del
        // organizador sí se toma el horario, si lo movieron en Outlook (decisión del 2026-10-29).
        var delPortal = await ctx.Reuniones.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.OutlookImportadaPorId == null && (r.OutlookICalUId != null || r.OutlookEventId != null))
            .Select(r => new { r.Id, r.OutlookICalUId, r.OutlookEventId, r.OutlookOrganizadorId })
            .ToListAsync(ct);
        var icalsDelPortal   = delPortal.Where(x => x.OutlookICalUId != null).Select(x => x.OutlookICalUId!).ToHashSet();
        var eventosDelPortal = delPortal.Where(x => x.OutlookEventId != null)
            .ToDictionary(x => x.OutlookEventId!, x => (x.Id, x.OutlookOrganizadorId));

        // Con las borradas: una reunión que el usuario borró no debe volver a nacer. Con sus
        // asistentes: la sincronización de invitados agrega y quita sobre la colección cargada.
        var mias = await ctx.Reuniones.IgnoreQueryFilters()
            .Include(r => r.Asistentes)
            .Where(r => r.OutlookImportadaPorId == usuarioId)
            .ToListAsync(ct);
        var porClave = mias.ToDictionary(r => r.OrigenExternoId!);

        // El dueño del calendario aparece entre los invitados de las citas a las que lo invitaron.
        var correoDueno = await ctx.ConexionesMicrosoft.AsNoTracking()
            .Where(x => x.Id == usuarioId).Select(x => x.Upn).FirstOrDefaultAsync(ct);

        var asignacion = await ctx.AsignacionesUsuario.AsNoTracking()
            .Where(a => a.UsuarioId == usuarioId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new { a.InstitucionId, a.AreaId, a.UnidadId })
            .FirstOrDefaultAsync(ct);

        int creadas = 0, actualizadas = 0, canceladas = 0, saltadas = 0;
        var vistas = new HashSet<string>();

        foreach (var c in citas)
        {
            if (eventosDelPortal.TryGetValue(c.Id, out var delPortalAqui))
            {
                // La cita es la del organizador solo en su propio calendario: el id es por buzón.
                var movida = delPortalAqui.OutlookOrganizadorId == usuarioId
                             && await AplicarHorarioDelPortalAsync(delPortalAqui.Id, c, correoDueno, utcAhora, ct);
                if (movida) actualizadas++; else saltadas++;
                continue;
            }
            if (c.ICalUId is not null && icalsDelPortal.Contains(c.ICalUId))
            {
                saltadas++;
                continue;
            }

            var clave = Reunion.ClaveOrigenOutlook(c.Id);
            vistas.Add(clave);

            if (porClave.TryGetValue(clave, out var r))
            {
                if (r.IsDeleted) { saltadas++; continue; }
                if (c.Cancelada)
                {
                    if (r.MarcarCanceladaEnOutlook(utcAhora)) canceladas++;
                    continue;
                }
                var quitoCancelacion = r.QuitarCancelacionOutlook();
                var cambio = r.ActualizarDeOutlook(CitaOutlookTraductor.Traducir(c));
                var cambioInvitados = r.SincronizarInvitadosOutlook(CitaOutlookTraductor.Invitados(c, correoDueno));
                if (cambio || cambioInvitados || quitoCancelacion) actualizadas++;
            }
            else
            {
                if (c.Cancelada) { saltadas++; continue; }
                var nueva = Reunion.ImportarDeOutlook(c.Id, CitaOutlookTraductor.Traducir(c), usuarioId,
                    asignacion?.InstitucionId, asignacion?.AreaId, asignacion?.UnidadId);
                nueva.SincronizarInvitadosOutlook(CitaOutlookTraductor.Invitados(c, correoDueno));
                ctx.Reuniones.Add(nueva);
                porClave[clave] = nueva;
                creadas++;
            }
        }

        // Las que estaban en la ventana y ya no vinieron: borradas en Outlook, o movidas fuera
        // de la ventana. Se pregunta por cada una; las de fecha pasada no se tocan (congeladas).
        foreach (var r in mias.Where(r => !r.IsDeleted && r.CanceladaEnOutlookEl is null
                                          && r.Fecha >= hoy && r.Fecha <= hasta
                                          && !vistas.Contains(r.OrigenExternoId!)))
        {
            var c = await calendario.ObtenerAsync(usuarioId, r.OutlookImportadoEventId!, ct);
            if (c is null || c.Cancelada)
            {
                if (r.MarcarCanceladaEnOutlook(utcAhora)) canceladas++;
            }
            else
            {
                var cambio = r.ActualizarDeOutlook(CitaOutlookTraductor.Traducir(c));
                var cambioInvitados = r.SincronizarInvitadosOutlook(CitaOutlookTraductor.Invitados(c, correoDueno));
                if (cambio || cambioInvitados) actualizadas++;
            }
        }

        if (creadas + actualizadas + canceladas > 0)
            await uow.SaveChangesAsync(ct);

        return new ResultadoSincronizacionCalendario(creadas, actualizadas, canceladas, saltadas);
    }

    /// <summary>
    /// Una reunión del portal que su organizador movió en Outlook toma de la cita la fecha, la hora y
    /// la duración. Se compara el horario resuelto, no el texto («1 hora» y «1:00» son lo mismo). Con
    /// el horario nuevo se recalcula la huella, igual que al enviar: sin eso, el próximo guardado en
    /// el portal vería una huella distinta y le devolvería a Outlook la hora vieja. La ficha va en el
    /// cuerpo de la cita, así que la huella necesita Correo:UrlBase; sin ella no se recalcula y ese
    /// guardado manda un PATCH de más, con la hora ya buena.
    /// </summary>
    private async Task<bool> AplicarHorarioDelPortalAsync(int reunionId, CitaOutlook c, string? correoOrganizador,
        DateTime utcAhora, CancellationToken ct)
    {
        if (c.TodoElDia || c.Cancelada) return false;

        var r = await ctx.Reuniones.IgnoreQueryFilters().Include(x => x.Asistentes)
            .FirstOrDefaultAsync(x => x.Id == reunionId, ct);
        if (r is null || r.IsDeleted || !r.EstaEnOutlook) return false;

        HorarioResuelto? actual = null;
        try { actual = HorarioReunion.Resolver(r.Fecha, r.Hora, r.Duracion); }
        catch (DomainException) { /* ilegible en el portal: Outlook trae uno bueno */ }
        if (actual is not null && actual.Inicio == c.Inicio && actual.Fin == c.Fin) return false;

        var g = CitaOutlookTraductor.Traducir(c);
        if (g.Hora is null || g.Duracion is null) return false;
        if (!r.AplicarHorarioDeOutlook(g.Fecha, g.Hora, g.Duracion)) return false;

        if (correo.Value.UrlBaseValida() is { } urlBase)
        {
            // La misma dirección que arma el editor: la ficha es /Reuniones/Acta/{id}.
            var huella = EventoOutlookBuilder.Construir(r, correoOrganizador, $"{urlBase}/Reuniones/Acta/{r.Id}").Huella();
            r.RegistrarSincronizacionOutlook(null, huella, utcAhora);
        }
        return true;
    }
}
