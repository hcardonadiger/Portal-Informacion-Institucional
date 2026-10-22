using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Infrastructure.Email;
using Diger.TramitesEstado.Application.Notificaciones;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
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
        var smtp = new SmtpEmailService(new AjustesDePrueba
        {
            Smtp = AjustesDePrueba.ConSmtp("127.0.0.1", "usuario") with { Puerto = 9, Ssl = false },
        }, registro);

        var resultado = await smtp.SendEmailAsync(Destino, "Asunto", Cuerpo, TipoCorreo.RecuperacionContrasena, CancellationToken.None);

        resultado.Should().Be(ResultadoCorreo.Fallido);
        registro.Lineas.Should().NotBeEmpty("el fallo se registra");
        registro.Lineas.Should().NotContain(l => LlevaContenido(l));
        registro.Lineas.Should().Contain(l => l.Contains(nameof(TipoCorreo.RecuperacionContrasena)),
            "lo que sí queda es el tipo de correo");
    }

    /// <summary>
    /// Entrega 2 (2026-10-22): el envío no tenía tiempo máximo. Con un servidor que acepta la
    /// conexión y nunca contesta, la página se quedaba esperando. Ahora corta a los
    /// <c>Smtp:TimeoutSegundos</c> (15 por defecto) y lo deja en el registro.
    /// </summary>
    [Fact]
    public async Task Un_servidor_que_no_responde_se_corta_al_tiempo_maximo()
    {
        var mudo = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        mudo.Start();
        try
        {
            var puerto   = ((System.Net.IPEndPoint)mudo.LocalEndpoint).Port;
            var registro = new RegistroEnMemoria<SmtpEmailService>();
            var smtp = new SmtpEmailService(new AjustesDePrueba
            {
                Smtp = AjustesDePrueba.ConSmtp("127.0.0.1", "usuario") with { Puerto = puerto, Ssl = false, TimeoutSegundos = 1 },
            }, registro);

            var envio  = smtp.SendEmailAsync(Destino, "Asunto", Cuerpo, TipoCorreo.RecordatorioTicket, CancellationToken.None);
            var limite = Task.Delay(TimeSpan.FromSeconds(10));

            (await Task.WhenAny(envio, limite)).Should().BeSameAs(envio, "el envío tiene que cortarse solo");
            registro.Lineas.Should().ContainSingle(l => l.StartsWith("Error"))
                .Which.Should().Contain("no respondió");
        }
        finally { mudo.Stop(); }
    }

    [Fact]
    public void El_tiempo_maximo_por_defecto_es_de_15_segundos() =>
        new SmtpSettings().TimeoutSegundos.Should().Be(15);

    [Fact]
    public async Task El_registro_sin_servidor_configurado_no_lleva_el_cuerpo()
    {
        var registro = new RegistroEnMemoria<SmtpEmailService>();
        var smtp = new SmtpEmailService(new AjustesDePrueba { Smtp = AjustesDePrueba.ConSmtp("", "") }, registro);

        var resultado = await smtp.SendEmailAsync(Destino, "Asunto", Cuerpo, TipoCorreo.RecuperacionContrasena, CancellationToken.None);

        resultado.Should().Be(ResultadoCorreo.SinConfigurar, "sin servidor no se intenta");
        registro.Lineas.Should().NotBeEmpty("se deja constancia de que no salió");
        registro.Lineas.Should().NotContain(l => LlevaContenido(l));
    }

    /// <summary>
    /// Desde que la configuración se lee de la base, leerla puede fallar (caché fría y base caída).
    /// Eso no puede escaparse del envío: <see cref="IEmailService"/> promete no lanzar, y de eso
    /// dependen la recuperación de contraseña y los recordatorios manuales.
    /// </summary>
    [Fact]
    public async Task Si_no_se_puede_leer_la_configuracion_el_envio_falla_sin_lanzar()
    {
        var ajustes = Substitute.For<IAjustesNotificaciones>();
        ajustes.SmtpAsync(Arg.Any<CancellationToken>())
            .Returns<Task<SmtpVigente>>(_ => throw new InvalidOperationException("la base no responde"));
        var registro = new RegistroEnMemoria<SmtpEmailService>();
        var smtp = new SmtpEmailService(ajustes, registro);

        var envio  = await smtp.SendEmailAsync(Destino, "Asunto", Cuerpo, TipoCorreo.RecuperacionContrasena, CancellationToken.None);
        var prueba = await smtp.ProbarAsync(Destino, CancellationToken.None);

        envio.Should().Be(ResultadoCorreo.Fallido);
        prueba.Resultado.Should().Be(ResultadoCorreo.Fallido);
        prueba.Detalle.Should().NotBeNullOrWhiteSpace();
        registro.Lineas.Should().NotBeEmpty("la falla se registra");
        registro.Lineas.Should().NotContain(l => LlevaContenido(l));
    }
}
