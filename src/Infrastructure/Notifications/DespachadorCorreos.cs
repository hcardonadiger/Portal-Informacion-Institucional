using System.Text;
using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Notificaciones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Diger.TramitesEstado.Application.Notificaciones.PlantillasCorreo;

namespace Diger.TramitesEstado.Infrastructure.Notifications;

/// <summary>
/// Junta lo pendiente de cada persona en un solo correo, ordenado por tipo. Lo llama el revisor al
/// final de cada vuelta. Un envío que falla se reintenta en las vueltas siguientes hasta
/// <see cref="MaxIntentos"/>; después queda fallido y el panel lo cuenta.
/// </summary>
public sealed class DespachadorCorreos(
    IApplicationDbContext ctx,
    IEmailService email,
    IOptions<CorreoOptions> correo,
    TimeProvider reloj,
    ILogger<DespachadorCorreos> logger) : IDespachadorCorreos
{
    public const int MaxIntentos = 3;

    public async Task<int> DespacharAsync(CancellationToken ct = default)
    {
        var pendientes = await ctx.CorreosPendientes
            .Where(c => c.Estado == EstadoCorreoPendiente.Pendiente)
            .OrderBy(c => c.CreadoUtc)
            .ToListAsync(ct);
        if (pendientes.Count == 0) return 0;

        var ids = pendientes.Select(p => p.DestinatarioId).Distinct().ToList();
        var usuarios = await ctx.Usuarios.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Nombre, u.Correo, u.Activo })
            .ToDictionaryAsync(u => u.Id, ct);

        var ahora    = reloj.GetUtcNow().UtcDateTime;
        var enviados = 0;

        foreach (var grupo in pendientes.GroupBy(p => p.DestinatarioId))
        {
            if (!usuarios.TryGetValue(grupo.Key, out var u) || !u.Activo || string.IsNullOrWhiteSpace(u.Correo))
            {
                foreach (var p in grupo) p.Descartar("El destinatario no está activo o no tiene correo registrado.");
                await ctx.SaveChangesAsync(CancellationToken.None);
                continue;
            }

            var items = grupo.ToList();
            var (asunto, cuerpo) = Armar(u.Nombre, items);
            var resultado = await email.SendEmailAsync(u.Correo, asunto, cuerpo, TipoCorreo.ResumenAvisos, ct);

            foreach (var p in items)
            {
                if (resultado == ResultadoCorreo.Enviado) p.MarcarEnviado(ahora);
                else p.RegistrarFallo(resultado == ResultadoCorreo.SinConfigurar
                    ? "El servidor de correo no está configurado."
                    : "El servidor de correo rechazó el envío o no respondió.", MaxIntentos);
            }

            if (resultado == ResultadoCorreo.Enviado) enviados++;

            // Se guarda por persona: si el sitio se recicla a mitad de la vuelta, a quien ya le salió
            // su correo no se le vuelve a mandar en la siguiente. Sin el token: el correo ya salió y
            // anotarlo no puede quedarse a medias porque el host se esté apagando.
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        // Red de seguridad: normalmente ya no queda nada por guardar.
        await ctx.SaveChangesAsync(ct);
        if (enviados > 0) logger.LogInformation("Bandeja de correo: {Enviados} resúmenes enviados.", enviados);
        return enviados;
    }

    private (string Asunto, string Cuerpo) Armar(string nombre, IReadOnlyList<CorreoPendiente> items)
    {
        var sb = new StringBuilder()
            .Append($"<p>Estimado(a) <strong>{E(nombre)}</strong>,</p>")
            .Append($"<p>Tiene {items.Count} {(items.Count == 1 ? "aviso nuevo" : "avisos nuevos")} en {E(Marca.Nombre)}:</p>");

        foreach (var tipo in items.GroupBy(i => i.Tipo))
        {
            sb.Append($"<h3>{E(CatalogoNotificaciones.De(tipo.Key).Etiqueta)}</h3><ul>");
            foreach (var i in tipo)
                sb.Append(i.Url is { } url
                    ? $"<li><a href='{E(correo.Value.Enlace(url))}'>{E(i.Titulo)}</a></li>"
                    : $"<li>{E(i.Titulo)}</li>");
            sb.Append("</ul>");
        }

        var asunto = items.Count == 1 ? items[0].Titulo : $"{items.Count} avisos nuevos — {Marca.Nombre}";
        return (asunto, sb.ToString());
    }
}
