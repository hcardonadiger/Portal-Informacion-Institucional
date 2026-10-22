using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Infrastructure.Email;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Notificaciones;

/// <summary>
/// S1 (2026-10-22): el registro de un correo nunca lleva su contenido. Antes, cada falla —y cada
/// envío simulado sin servidor— escribía el cuerpo completo, y con él el enlace con el token de
/// recuperación de contraseña. En IIS eso terminaba en el Visor de eventos.
/// </summary>
public sealed class CorreoSeguridadTests
{
    internal sealed class RegistroEnMemoria<T> : ILogger<T>
    {
        public List<string> Lineas { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel nivel, EventId id, TState estado, Exception? ex, Func<TState, Exception?, string> formato)
            => Lineas.Add($"{nivel}: {formato(estado, ex)} {ex}");
    }

    private const string Destino = "destino@prueba.local";
    private const string Cuerpo = "<p><a href='https://portal/Cuenta/RestablecerPassword?token=TOKEN-SECRETO'>x</a></p>";

    private static bool LlevaContenido(string linea) =>
        linea.Contains("TOKEN-SECRETO") || linea.Contains("RestablecerPassword") || linea.Contains("<p>")
        || linea.Contains(Destino);

    [Fact]
    public async Task El_registro_de_un_envio_fallido_no_lleva_cuerpo_ni_enlaces_ni_tokens()
    {
        // Servidor configurado pero inalcanzable: el puerto 9 de la propia máquina rechaza la
        // conexión en el acto.
        var registro = new RegistroEnMemoria<SmtpEmailService>();
        var smtp = new SmtpEmailService(Options.Create(new SmtpSettings
        {
            Host = "127.0.0.1", Port = 9, EnableSsl = false, Username = "usuario", Password = "x",
        }), registro);

        await smtp.SendEmailAsync(Destino, "Asunto", Cuerpo, TipoCorreo.RecuperacionContrasena, CancellationToken.None);

        registro.Lineas.Should().NotBeEmpty("el fallo se registra");
        registro.Lineas.Should().NotContain(l => LlevaContenido(l));
        registro.Lineas.Should().Contain(l => l.Contains(nameof(TipoCorreo.RecuperacionContrasena)),
            "lo que sí queda es el tipo de correo");
    }

    [Fact]
    public async Task El_registro_sin_servidor_configurado_no_lleva_el_cuerpo()
    {
        var registro = new RegistroEnMemoria<SmtpEmailService>();
        var smtp = new SmtpEmailService(Options.Create(new SmtpSettings { Host = "", Username = "" }), registro);

        await smtp.SendEmailAsync(Destino, "Asunto", Cuerpo, TipoCorreo.RecuperacionContrasena, CancellationToken.None);

        registro.Lineas.Should().NotBeEmpty("se deja constancia de que no salió");
        registro.Lineas.Should().NotContain(l => LlevaContenido(l));
    }
}
