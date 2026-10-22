using Diger.TramitesEstado.Web.Common;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Entrega 2 (2026-10-22): al arrancar se advierte en el registro lo que falta para que los correos
/// funcionen —la dirección pública del portal y el servidor de correo—, sin impedir el arranque.
/// </summary>
public sealed class AvisosDeConfiguracionTests
{
    private sealed class Registro : ILogger
    {
        public List<string> Advertencias { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel nivel, EventId id, TState estado, Exception? ex, Func<TState, Exception?, string> formato)
        {
            if (nivel == LogLevel.Warning) Advertencias.Add(formato(estado, ex));
        }
    }

    private static List<string> Revisar(Dictionary<string, string?> valores)
    {
        var registro = new Registro();
        AvisosDeConfiguracion.Revisar(new ConfigurationBuilder().AddInMemoryCollection(valores).Build(), registro);
        return registro.Advertencias;
    }

    private static readonly Dictionary<string, string?> Completa = new()
    {
        ["Correo:UrlBase"] = "https://portal.prueba.local",
        ["Smtp:Host"]      = "smtp.prueba.local",
        ["Smtp:Username"]  = "cuenta@prueba.local",
    };

    [Fact]
    public void Con_todo_configurado_no_advierte_nada() => Revisar(Completa).Should().BeEmpty();

    [Fact]
    public void Sin_url_base_lo_advierte()
    {
        var sinUrl = new Dictionary<string, string?>(Completa) { ["Correo:UrlBase"] = "" };

        Revisar(sinUrl).Should().ContainSingle().Which.Should().Contain("Correo:UrlBase");
    }

    [Fact]
    public void Una_url_base_mal_escrita_tambien_se_advierte()
    {
        var malEscrita = new Dictionary<string, string?>(Completa) { ["Correo:UrlBase"] = "portal-sin-esquema" };

        Revisar(malEscrita).Should().ContainSingle().Which.Should().Contain("Correo:UrlBase");
    }

    [Fact]
    public void Sin_usuario_de_correo_advierte_que_no_saldran_correos()
    {
        var sinSmtp = new Dictionary<string, string?>(Completa) { ["Smtp:Username"] = "" };

        Revisar(sinSmtp).Should().ContainSingle().Which.Should().Contain("servidor de correo");
    }
}
