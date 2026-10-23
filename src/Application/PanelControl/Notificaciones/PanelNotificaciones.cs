using Diger.TramitesEstado.Application.Notificaciones;
using FluentValidation;

namespace Diger.TramitesEstado.Application.PanelControl.Notificaciones;

// ── Lectura ───────────────────────────────────────────────────────────────
public sealed record ModuloPanelDto(
    ModuloNotificacion Modulo, string Nombre, bool Campana, bool Correo, bool Agrupar,
    bool AdmiteCorreo, IReadOnlyList<string> Avisos);

/// <summary>El correo saliente tal como lo ve la pantalla. <b>Sin la contraseña</b>: solo si hay una.</summary>
public sealed record CorreoPanelDto(
    bool DesdePanel, string Host, int Puerto, bool Ssl, string Usuario,
    string Remitente, string NombreRemitente, bool HayPassword, bool PasswordIlegible);

public sealed record PanelNotificacionesDto(
    int IntervaloMinutos, DateTime? UltimaRevisionUtc, DateTime? ProximaRevisionUtc,
    IReadOnlyList<ModuloPanelDto> Modulos, CorreoPanelDto Correo, int CorreosEnEspera, int CorreosFallidos);

public sealed record GetPanelNotificacionesQuery : IRequest<PanelNotificacionesDto>;

public sealed class GetPanelNotificacionesQueryHandler(IAjustesNotificaciones ajustes, IApplicationDbContext ctx)
    : IRequestHandler<GetPanelNotificacionesQuery, PanelNotificacionesDto>
{
    public async Task<PanelNotificacionesDto> Handle(GetPanelNotificacionesQuery _, CancellationToken ct)
    {
        // El panel muestra lo que hay en la base, no lo que quedó en caché hace un minuto.
        ajustes.Invalidar();
        var a = await ajustes.ObtenerAsync(ct);
        var s = await ajustes.SmtpAsync(ct);

        var modulos = Enum.GetValues<ModuloNotificacion>().Select(m =>
        {
            var i = a.De(m);
            return new ModuloPanelDto(m, CatalogoNotificaciones.Nombre(m), i.Campana, i.Correo, i.Agrupar,
                CatalogoNotificaciones.ModuloAdmiteCorreo(m),
                CatalogoNotificaciones.DelModulo(m).Select(d => d.Etiqueta).ToList());
        }).ToList();

        var espera   = await ctx.CorreosPendientes.AsNoTracking().CountAsync(c => c.Estado == EstadoCorreoPendiente.Pendiente, ct);
        var fallidos = await ctx.CorreosPendientes.AsNoTracking().CountAsync(c => c.Estado == EstadoCorreoPendiente.Fallido, ct);

        return new PanelNotificacionesDto(
            a.IntervaloMinutos, a.UltimaRevisionUtc, a.UltimaRevisionUtc?.AddMinutes(a.IntervaloMinutos), modulos,
            new CorreoPanelDto(s.DesdePanel, s.Host, s.Puerto, s.Ssl, s.Usuario, s.Remitente, s.NombreRemitente,
                s.HayPassword, s.PasswordIlegible),
            espera, fallidos);
    }
}

// ── Ayudantes ─────────────────────────────────────────────────────────────
internal static class PanelNotificaciones
{
    public const string SeccionRevision = "Notificaciones · Revisión";
    public const string SeccionModulos  = "Notificaciones · Módulos";
    public const string SeccionCorreo   = "Notificaciones · Correo";

    /// <summary>La fila única, creada si falta con el intervalo que rige hoy.</summary>
    public static async Task<AjustesNotificaciones> FilaAsync(IApplicationDbContext ctx, IAjustesNotificaciones ajustes, CancellationToken ct)
    {
        var fila = await ctx.AjustesNotificaciones.FirstOrDefaultAsync(a => a.Id == AjustesNotificaciones.IdUnico, ct);
        if (fila is not null) return fila;

        fila = AjustesNotificaciones.CrearPorDefecto((await ajustes.ObtenerAsync(ct)).IntervaloMinutos);
        ctx.AjustesNotificaciones.Add(fila);
        return fila;
    }

    public static string Texto(int minutos) =>
        minutos < 60 ? $"{minutos} min"
        : minutos % 60 == 0 ? $"{minutos / 60} h"
        : $"{minutos / 60} h {minutos % 60} min";

    public static string Actor(ICurrentUserService u) => u.Nombre ?? u.Correo ?? "—";
}

// ── Revisión automática ───────────────────────────────────────────────────
public sealed record GuardarRevisionCommand(int Horas, int Minutos) : IRequest<Unit>;

public sealed class GuardarRevisionValidator : AbstractValidator<GuardarRevisionCommand>
{
    public GuardarRevisionValidator()
    {
        RuleFor(x => x.Horas).InclusiveBetween(0, 24).WithMessage("Las horas van de 0 a 24.");
        RuleFor(x => x.Minutos).InclusiveBetween(0, 59).WithMessage("Los minutos van de 0 a 59.");
        RuleFor(x => x.Horas * 60 + x.Minutos)
            .InclusiveBetween(AjustesNotificaciones.MinimoMinutos, AjustesNotificaciones.MaximoMinutos)
            // Una expresión que no es una propiedad: sin esto FluentValidation no sabe cómo llamarla y lanza.
            .OverridePropertyName("Revision")
            .WithMessage("La revisión automática tiene que ser de entre 5 minutos y 24 horas.");
    }
}

public sealed class GuardarRevisionCommandHandler(
    IApplicationDbContext ctx, IAjustesNotificaciones ajustes, SenalRevision senal,
    ICurrentUserService usuario, TimeProvider reloj) : IRequestHandler<GuardarRevisionCommand, Unit>
{
    public async Task<Unit> Handle(GuardarRevisionCommand cmd, CancellationToken ct)
    {
        var minutos = cmd.Horas * 60 + cmd.Minutos;
        var fila    = await PanelNotificaciones.FilaAsync(ctx, ajustes, ct);
        var antes   = fila.IntervaloMinutos;

        fila.CambiarIntervalo(minutos);
        if (antes != minutos)
            ctx.PanelControlBitacora.Add(PanelControlBitacora.Crear(PanelNotificaciones.SeccionRevision,
                $"intervalo: {PanelNotificaciones.Texto(antes)} → {PanelNotificaciones.Texto(minutos)}",
                PanelNotificaciones.Actor(usuario), reloj.GetUtcNow().UtcDateTime));

        await ctx.SaveChangesAsync(ct);
        ajustes.Invalidar();
        senal.Disparar();
        return Unit.Value;
    }
}

// ── Módulos ───────────────────────────────────────────────────────────────
public sealed record InterruptoresInput(ModuloNotificacion Modulo, bool Campana, bool Correo, bool Agrupar);

public sealed record GuardarModulosCommand(IReadOnlyList<InterruptoresInput> Modulos) : IRequest<Unit>;

public sealed class GuardarModulosValidator : AbstractValidator<GuardarModulosCommand>
{
    public GuardarModulosValidator()
    {
        RuleFor(x => x.Modulos).NotNull().WithMessage("Falta la lista de módulos.");
        // Un valor manipulado en el formulario crearía una fila de un módulo que no existe.
        RuleForEach(x => x.Modulos).ChildRules(m =>
            m.RuleFor(i => i.Modulo).IsInEnum().WithMessage("Módulo de notificación desconocido."));
    }
}

public sealed class GuardarModulosCommandHandler(
    IApplicationDbContext ctx, IAjustesNotificaciones ajustes, ICurrentUserService usuario, TimeProvider reloj)
    : IRequestHandler<GuardarModulosCommand, Unit>
{
    public async Task<Unit> Handle(GuardarModulosCommand cmd, CancellationToken ct)
    {
        var filas   = await ctx.InterruptoresNotificacion.ToListAsync(ct);
        var cambios = new List<string>();

        foreach (var input in cmd.Modulos)
        {
            var correo = input.Correo && CatalogoNotificaciones.ModuloAdmiteCorreo(input.Modulo);
            var fila   = filas.FirstOrDefault(f => f.Modulo == input.Modulo);
            if (fila is null)
            {
                fila = InterruptorNotificacion.Crear(input.Modulo);
                ctx.InterruptoresNotificacion.Add(fila);
                filas.Add(fila);
            }

            var (campana0, correo0, agrupar0) = (fila.CampanaActiva, fila.CorreoActivo, fila.Agrupar);
            if (!fila.Cambiar(input.Campana, correo, input.Agrupar)) continue;

            var nombre = CatalogoNotificaciones.Nombre(input.Modulo);
            if (campana0 != input.Campana) cambios.Add($"{nombre}: campana {(input.Campana ? "encendida" : "apagada")}");
            if (correo0  != correo)        cambios.Add($"{nombre}: correo {(correo ? "encendido" : "apagado")}");
            if (agrupar0 != input.Agrupar) cambios.Add($"{nombre}: agrupar {(input.Agrupar ? "encendido" : "apagado")}");
        }

        if (cambios.Count > 0)
            ctx.PanelControlBitacora.Add(PanelControlBitacora.Crear(PanelNotificaciones.SeccionModulos,
                string.Join("; ", cambios), PanelNotificaciones.Actor(usuario), reloj.GetUtcNow().UtcDateTime));

        await ctx.SaveChangesAsync(ct);
        ajustes.Invalidar();
        return Unit.Value;
    }
}

// ── Correo saliente ───────────────────────────────────────────────────────
public sealed record GuardarCorreoCommand(
    string Host, int Puerto, bool Ssl, string Usuario, string? Password, bool QuitarPassword,
    string Remitente, string? NombreRemitente) : IRequest<Unit>
{
    /// <summary>El record imprimiría la contraseña en un registro de MediatR o de una excepción.</summary>
    public override string ToString() => $"GuardarCorreoCommand {{ Host = {Host}, Usuario = {Usuario}, Password = *** }}";
}

public sealed class GuardarCorreoValidator : AbstractValidator<GuardarCorreoCommand>
{
    public GuardarCorreoValidator()
    {
        // WithMessage se ata solo a la regla que lo precede: cada regla lleva el suyo, o el mensaje
        // por defecto de FluentValidation (en el idioma del servidor) llega a la pantalla.
        // Stop: con el campo vacío basta «Falta…»; no se suman «no es válida» ni el largo.
        RuleFor(x => x.Host).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Falta el servidor de correo.")
            .MaximumLength(200).WithMessage("El servidor no puede pasar de 200 caracteres.");
        RuleFor(x => x.Puerto).InclusiveBetween(1, 65535).WithMessage("El puerto tiene que estar entre 1 y 65535.");
        RuleFor(x => x.Usuario).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Falta el usuario del correo.")
            .MaximumLength(200).WithMessage("El usuario no puede pasar de 200 caracteres.");
        RuleFor(x => x.Remitente).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Falta el correo remitente.")
            .EmailAddress().WithMessage("El correo remitente no es una dirección válida.")
            .MaximumLength(200).WithMessage("El correo remitente no puede pasar de 200 caracteres.");
        RuleFor(x => x.NombreRemitente).MaximumLength(200).WithMessage("El nombre del remitente no puede pasar de 200 caracteres.");
        RuleFor(x => x.Password).MaximumLength(200).WithMessage("La contraseña no puede pasar de 200 caracteres.");
    }
}

public sealed class GuardarCorreoCommandHandler(
    IApplicationDbContext ctx, IAjustesNotificaciones ajustes, IProtectorSecretos protector,
    ICurrentUserService usuario, TimeProvider reloj) : IRequestHandler<GuardarCorreoCommand, Unit>
{
    public async Task<Unit> Handle(GuardarCorreoCommand cmd, CancellationToken ct)
    {
        var fila = await PanelNotificaciones.FilaAsync(ctx, ajustes, ct);

        if (!fila.SmtpConfigurado && string.IsNullOrEmpty(cmd.Password) && !cmd.QuitarPassword)
            throw new DomainException(
                "Es la primera vez que se guarda el correo desde el panel: escriba la contraseña. " +
                "Si el servidor no la pide, marque «Quitar la contraseña».");

        var host   = cmd.Host.Trim();
        var user   = cmd.Usuario.Trim();
        var rem    = cmd.Remitente.Trim();
        var nombre = string.IsNullOrWhiteSpace(cmd.NombreRemitente) ? null : cmd.NombreRemitente.Trim();

        var cambios = new List<string>();
        if (fila.SmtpHost != host)              cambios.Add($"servidor: {fila.SmtpHost ?? "—"} → {host}");
        if (fila.SmtpPuerto != cmd.Puerto)      cambios.Add($"puerto: {fila.SmtpPuerto} → {cmd.Puerto}");
        if (fila.SmtpSsl != cmd.Ssl)            cambios.Add($"SSL: {(cmd.Ssl ? "activado" : "desactivado")}");
        if (fila.SmtpUsuario != user)           cambios.Add($"usuario: {fila.SmtpUsuario ?? "—"} → {user}");
        if (fila.SmtpRemitente != rem)          cambios.Add($"remitente: {fila.SmtpRemitente ?? "—"} → {rem}");
        if (fila.SmtpNombreRemitente != nombre) cambios.Add($"nombre del remitente: {fila.SmtpNombreRemitente ?? "—"} → {nombre ?? "—"}");

        fila.ConfigurarSmtp(host, cmd.Puerto, cmd.Ssl, user, rem, nombre);

        if (cmd.QuitarPassword)
        {
            if (fila.SmtpPasswordCifrada is not null) cambios.Add("contraseña quitada");
            fila.FijarPasswordCifrada(null);
        }
        else if (!string.IsNullOrEmpty(cmd.Password))
        {
            fila.FijarPasswordCifrada(protector.Cifrar(cmd.Password));
            cambios.Add("contraseña cambiada");
        }

        if (cambios.Count > 0)
            ctx.PanelControlBitacora.Add(PanelControlBitacora.Crear(PanelNotificaciones.SeccionCorreo,
                string.Join("; ", cambios), PanelNotificaciones.Actor(usuario), reloj.GetUtcNow().UtcDateTime));

        await ctx.SaveChangesAsync(ct);
        ajustes.Invalidar();
        return Unit.Value;
    }
}

// ── Correo de prueba ──────────────────────────────────────────────────────
/// <summary>Manda un correo a quien pulsa el botón, con lo que está guardado.</summary>
public sealed record EnviarCorreoPruebaCommand : IRequest<ResultadoPruebaCorreo>;

public sealed class EnviarCorreoPruebaCommandHandler(
    IApplicationDbContext ctx, ICurrentUserService usuario, IProbadorCorreo probador)
    : IRequestHandler<EnviarCorreoPruebaCommand, ResultadoPruebaCorreo>
{
    public async Task<ResultadoPruebaCorreo> Handle(EnviarCorreoPruebaCommand _, CancellationToken ct)
    {
        var uid = usuario.UserId ?? throw new DomainException("No se pudo identificar su usuario.");
        var correo = await ctx.Usuarios.AsNoTracking()
            .Where(u => u.Id == uid).Select(u => u.Correo).FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(correo))
            throw new DomainException("Su usuario no tiene un correo registrado al que mandar la prueba.");

        return await probador.ProbarAsync(correo, ct);
    }
}
