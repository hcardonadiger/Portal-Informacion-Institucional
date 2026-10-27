using System.Linq.Expressions;
using Diger.TramitesEstado.Application.Chat;

namespace Diger.TramitesEstado.Infrastructure.Chat;

public sealed class ChatService(IApplicationDbContext ctx, IUnitOfWork uow) : IChatService
{
    // ── Comandos ─────────────────────────────────────────────────────────

    public async Task<ChatSesion> IniciarSesionAsync(Guid usuarioId, string usuarioNombre, int? temaId, CancellationToken ct = default)
    {
        var sesion = ChatSesion.Iniciar(usuarioId, usuarioNombre);

        if (temaId.HasValue)
        {
            var tema = await ctx.TemasTicket.FindAsync([temaId.Value], ct);
            if (tema != null)
                sesion.AsignarTema(tema.Id, tema.Nombre);
        }

        ctx.ChatSesiones.Add(sesion);
        await uow.SaveChangesAsync(ct);
        return sesion;
    }

    public async Task<string?> TomarSesionAsync(int sesionId, Guid tecnicoId, string tecnicoNombre, bool puedeReasignar, CancellationToken ct = default)
    {
        var sesion = await ctx.ChatSesiones.FindAsync([sesionId], ct)
            ?? throw new InvalidOperationException($"Sesión {sesionId} no encontrada.");

        var anterior = sesion.Tomar(tecnicoId, tecnicoNombre, puedeReasignar);
        await uow.SaveChangesAsync(ct);
        return anterior;
    }

    public async Task<string> AsignarDesdeColaAsync(int sesionId, Guid tecnicoId, CancellationToken ct = default)
    {
        var sesion = await ctx.ChatSesiones.FindAsync([sesionId], ct)
            ?? throw new InvalidOperationException($"Sesión {sesionId} no encontrada.");
        if (sesion.Estado != ChatEstado.EnCola)
            throw new DomainException("Ese chat ya no está en la cola.");

        var tecnico = (await GetTecnicosAsignablesAsync(null, ct)).FirstOrDefault(t => t.Id == tecnicoId)
            ?? throw new DomainException("El usuario elegido no puede atender el chat de soporte.");

        sesion.Tomar(tecnico.Id, tecnico.Nombre, puedeReasignar: false);
        await uow.SaveChangesAsync(ct);
        return tecnico.Nombre;
    }

    public Task<bool> AtiendeSesionAsync(int sesionId, Guid tecnicoId, CancellationToken ct = default)
        => ctx.ChatSesiones.AsNoTracking().AnyAsync(s => s.Id == sesionId && s.TecnicoId == tecnicoId &&
            (s.Estado == ChatEstado.EnCola || s.Estado == ChatEstado.Activo), ct);

    public async Task<ChatMensajeDto> EnviarMensajeAsync(int sesionId, string texto, bool esDelTecnico, bool esSistema, string autorNombre, CancellationToken ct = default)
    {
        var sesion = await ctx.ChatSesiones.FindAsync([sesionId], ct)
            ?? throw new InvalidOperationException($"Sesión {sesionId} no encontrada.");

        var msg = sesion.AgregarMensaje(texto, esDelTecnico, esSistema, autorNombre);
        await uow.SaveChangesAsync(ct);

        return ToDto(msg);
    }

    public async Task MarcarLeidosAsync(int sesionId, bool lectoPorTecnico, CancellationToken ct = default)
    {
        var mensajes = await ctx.ChatMensajes
            .Where(m => m.SesionId == sesionId && m.EsDelTecnico != lectoPorTecnico && !m.Leido)
            .ToListAsync(ct);

        foreach (var m in mensajes) m.MarcarLeido();
        if (mensajes.Count > 0) await uow.SaveChangesAsync(ct);
    }

    public async Task CerrarSesionAsync(int sesionId, ChatEstado estado = ChatEstado.Resuelto, CancellationToken ct = default)
    {
        var sesion = await ctx.ChatSesiones.FindAsync([sesionId], ct)
            ?? throw new InvalidOperationException($"Sesión {sesionId} no encontrada.");

        sesion.Cerrar(estado);
        await uow.SaveChangesAsync(ct);
    }

    public async Task CalificarAsync(int sesionId, byte puntuacion, CancellationToken ct = default)
    {
        var sesion = await ctx.ChatSesiones.FindAsync([sesionId], ct)
            ?? throw new InvalidOperationException($"Sesión {sesionId} no encontrada.");

        sesion.Calificar(puntuacion);
        await uow.SaveChangesAsync(ct);
    }

    public async Task VincularTicketAsync(int sesionId, int ticketId, CancellationToken ct = default)
    {
        var sesion = await ctx.ChatSesiones.FindAsync([sesionId], ct)
            ?? throw new InvalidOperationException($"Sesión {sesionId} no encontrada.");

        sesion.VincularTicket(ticketId);
        await uow.SaveChangesAsync(ct);
    }

    // ── Queries ──────────────────────────────────────────────────────────

    public async Task<ChatSesionDetalleDto?> GetDetalleAsync(int sesionId, CancellationToken ct = default)
    {
        var sesion = await ctx.ChatSesiones
            .AsNoTracking()
            .Where(s => s.Id == sesionId)
            .Select(s => new
            {
                Sesion   = s,
                Mensajes = s.Mensajes.OrderBy(m => m.Enviado).ToList(),
            })
            .FirstOrDefaultAsync(ct);

        if (sesion is null) return null;

        var noLeidos = sesion.Mensajes.Count(m => !m.Leido);
        return new ChatSesionDetalleDto(
            ToSesionDto(sesion.Sesion, noLeidos),
            sesion.Mensajes.Select(ToDto).ToList());
    }

    public Task<IReadOnlyList<ChatSesionDto>> GetSesionesTecnicoAsync(Guid tecnicoId, CancellationToken ct = default)
        => GetSesionesWhere(s => s.TecnicoId == tecnicoId &&
            (s.Estado == ChatEstado.Activo || s.Estado == ChatEstado.EnCola), ct);

    public Task<IReadOnlyList<ChatSesionDto>> GetColaAsync(int? temaId = null, CancellationToken ct = default)
        => GetSesionesWhere(s => s.Estado == ChatEstado.EnCola &&
            (!temaId.HasValue || s.TemaId == temaId), ct);

    public Task<IReadOnlyList<ChatSesionDto>> GetActivasDeOtrosAsync(Guid tecnicoId, CancellationToken ct = default)
        => GetSesionesWhere(s => s.Estado == ChatEstado.Activo &&
            s.TecnicoId.HasValue && s.TecnicoId != tecnicoId, ct);

    // Las cerradas crecen sin tope: se ordenan por cierre (lo último resuelto primero) y se cortan.
    public async Task<IReadOnlyList<ChatSesionDto>> GetCerradasRecientesAsync(int maximo = 50, CancellationToken ct = default)
    {
        var sesiones = await ctx.ChatSesiones
            .AsNoTracking()
            .Where(s => s.Estado != ChatEstado.EnCola && s.Estado != ChatEstado.Activo)
            .OrderByDescending(s => s.Cierre ?? s.Inicio)
            .Take(maximo)
            .Select(s => new
            {
                Sesion   = s,
                NoLeidos = s.Mensajes.Count(m => !m.Leido),
            })
            .ToListAsync(ct);

        return sesiones.Select(x => ToSesionDto(x.Sesion, x.NoLeidos)).ToList();
    }

    public Task<int> ContarColaAsync(CancellationToken ct = default)
        => ctx.ChatSesiones.AsNoTracking().CountAsync(s => s.Estado == ChatEstado.EnCola, ct);

    public async Task<IReadOnlyList<TecnicoAsignableDto>> GetTecnicosAsignablesAsync(int? temaId, CancellationToken ct = default)
    {
        // Atiende soporte quien tiene alguna asignación con un rol activo que lo permite —la
        // misma capacidad que abre la bandeja—, no solo quien tiene temas: un chat sin tema, o de
        // un tema que nadie tiene, también tiene que poder repartirse.
        var rolesTecnicos = ctx.Roles.Where(r => r.Activo && r.EsTecnicoSoporte).Select(r => r.Id);
        var tecnicos = await ctx.Usuarios
            .AsNoTracking()
            .Where(u => u.Activo &&
                ctx.AsignacionesUsuario.Any(a => a.UsuarioId == u.Id && rolesTecnicos.Contains(a.Rol)))
            .Select(u => new
            {
                u.Id,
                u.Nombre,
                DelTema = temaId != null && ctx.UsuarioTemas.Any(ut => ut.UsuarioId == u.Id && ut.TemaId == temaId),
            })
            .ToListAsync(ct);

        return tecnicos
            .OrderByDescending(t => t.DelTema).ThenBy(t => t.Nombre)
            .Select(t => new TecnicoAsignableDto(t.Id, t.Nombre, t.DelTema))
            .ToList();
    }

    public async Task<ChatSesionDto?> GetSesionActivaUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var sesion = await ctx.ChatSesiones
            .AsNoTracking()
            .Where(s => s.UsuarioId == usuarioId &&
                (s.Estado == ChatEstado.EnCola || s.Estado == ChatEstado.Activo))
            .OrderByDescending(s => s.Inicio)
            .FirstOrDefaultAsync(ct);

        if (sesion is null) return null;
        var noLeidos = await ctx.ChatMensajes
            .CountAsync(m => m.SesionId == sesion.Id && m.EsDelTecnico && !m.Leido, ct);
        return ToSesionDto(sesion, noLeidos);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<ChatSesionDto>> GetSesionesWhere(
        Expression<Func<ChatSesion, bool>> predicate, CancellationToken ct)
    {
        var sesiones = await ctx.ChatSesiones
            .AsNoTracking()
            .Where(predicate)
            .OrderByDescending(s => s.Inicio)
            .Select(s => new
            {
                Sesion    = s,
                NoLeidos  = s.Mensajes.Count(m => !m.Leido),
            })
            .ToListAsync(ct);

        return sesiones.Select(x => ToSesionDto(x.Sesion, x.NoLeidos)).ToList();
    }

    private static ChatSesionDto ToSesionDto(ChatSesion s, int noLeidos) => new(
        s.Id, s.UsuarioId, s.UsuarioNombre,
        s.TecnicoId, s.TecnicoNombre,
        s.TemaId, s.TemaNombre,
        s.TicketId, s.Estado,
        s.Inicio, s.Cierre, noLeidos);

    private static ChatMensajeDto ToDto(ChatMensaje m) => new(
        m.Id, m.SesionId, m.Texto, m.EsDelTecnico, m.EsSistema,
        m.AutorNombre, m.Enviado, m.Leido);
}
