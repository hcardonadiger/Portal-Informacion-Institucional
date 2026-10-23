using System.Net;
using System.Text.Json;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Infrastructure.Outlook;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Outlook;

public class GraphOutlookCalendarioTests
{
    private sealed class Manejador(HttpStatusCode codigo, string cuerpo) : HttpMessageHandler
    {
        public HttpRequestMessage? Pedido;
        public string? CuerpoEnviado;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Pedido = r;
            CuerpoEnviado = r.Content is null ? null : await r.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(codigo) { Content = new StringContent(cuerpo) };
        }
    }

    private static readonly Guid Org = Guid.NewGuid();
    private static readonly EventoOutlook Evento = new("Mesa técnica", new DateTime(2026, 10, 23, 9, 0, 0),
        new DateTime(2026, 10, 23, 10, 0, 0), "Sala 2", "<p>x</p>", [new("ana@ihtt.gob.hn", "Ana")], true);

    /// <summary>Guarda los mensajes ya formateados, para ver qué llega al log (y qué no).</summary>
    private sealed class LogCapturado : ILogger<GraphOutlookCalendario>
    {
        public readonly List<(LogLevel Nivel, string Mensaje)> Entradas = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entradas.Add((logLevel, formatter(state, exception)));
    }

    private static (GraphOutlookCalendario cal, Manejador m) Armar(HttpStatusCode codigo, string cuerpo = "{}",
        ILogger<GraphOutlookCalendario>? log = null)
    {
        var m = new Manejador(codigo, cuerpo);
        var conexion = Substitute.For<IConexionMicrosoft>();
        conexion.ObtenerTokenAsync(Org, Arg.Any<CancellationToken>()).Returns("tok-123");
        var http = new HttpClient(m) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        return (new GraphOutlookCalendario(http, conexion, log ?? NullLogger<GraphOutlookCalendario>.Instance), m);
    }

    [Fact]
    public async Task Crear_manda_el_evento_completo_con_transaccion_y_Teams()
    {
        var (cal, m) = Armar(HttpStatusCode.Created, """{"id":"AAMk-1","onlineMeeting":{"joinUrl":"https://teams/abc"}}""");
        var tx = Guid.NewGuid();

        var res = await cal.CrearAsync(Org, Evento, tx, default);

        res.Should().Be(new ResultadoOutlook("AAMk-1", "https://teams/abc"));
        m.Pedido!.Method.Should().Be(HttpMethod.Post);
        m.Pedido.RequestUri!.ToString().Should().Be("https://graph.microsoft.com/v1.0/me/events");
        m.Pedido.Headers.Authorization!.ToString().Should().Be("Bearer tok-123");

        using var j = JsonDocument.Parse(m.CuerpoEnviado!);
        var raiz = j.RootElement;
        raiz.GetProperty("transactionId").GetString().Should().Be(tx.ToString());
        raiz.GetProperty("isOnlineMeeting").GetBoolean().Should().BeTrue();
        raiz.GetProperty("onlineMeetingProvider").GetString().Should().Be("teamsForBusiness");
        raiz.GetProperty("start").GetProperty("dateTime").GetString().Should().Be("2026-10-23T09:00:00");
        raiz.GetProperty("start").GetProperty("timeZone").GetString().Should().Be("Central America Standard Time");
        raiz.GetProperty("location").GetProperty("displayName").GetString().Should().Be("Sala 2");
        raiz.GetProperty("attendees")[0].GetProperty("emailAddress").GetProperty("address").GetString().Should().Be("ana@ihtt.gob.hn");
        raiz.GetProperty("body").GetProperty("contentType").GetString().Should().Be("HTML");
    }

    [Fact]
    public async Task Sin_Teams_no_pide_reunion_en_linea()
    {
        var (cal, m) = Armar(HttpStatusCode.Created, """{"id":"AAMk-1"}""");

        var res = await cal.CrearAsync(Org, Evento with { EsTeams = false }, Guid.NewGuid(), default);

        res.EnlaceTeams.Should().BeNull();
        JsonDocument.Parse(m.CuerpoEnviado!).RootElement.TryGetProperty("isOnlineMeeting", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Actualizar_hace_PATCH_sin_transaccion()
    {
        var (cal, m) = Armar(HttpStatusCode.OK, """{"id":"AAMk-1","onlineMeeting":{"joinUrl":"https://teams/abc"}}""");

        await cal.ActualizarAsync(Org, "AAMk-1", Evento, default);

        m.Pedido!.Method.Should().Be(HttpMethod.Patch);
        m.Pedido.RequestUri!.ToString().Should().Be("https://graph.microsoft.com/v1.0/me/events/AAMk-1");
        JsonDocument.Parse(m.CuerpoEnviado!).RootElement.TryGetProperty("transactionId", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Cancelar_hace_POST_a_cancel_con_comentario()
    {
        var (cal, m) = Armar(HttpStatusCode.Accepted);

        await cal.CancelarAsync(Org, "AAMk-1", "Se canceló.", default);

        m.Pedido!.RequestUri!.ToString().Should().Be("https://graph.microsoft.com/v1.0/me/events/AAMk-1/cancel");
        JsonDocument.Parse(m.CuerpoEnviado!).RootElement.GetProperty("comment").GetString().Should().Be("Se canceló.");
    }

    [Fact]
    public async Task Un_401_es_conexion_vencida()
    {
        var (cal, _) = Armar(HttpStatusCode.Unauthorized);

        var acto = () => cal.CrearAsync(Org, Evento, Guid.NewGuid(), default);

        await acto.Should().ThrowAsync<OutlookConexionVencidaException>();
    }

    [Fact]
    public async Task Un_404_al_actualizar_dice_que_la_cita_ya_no_existe()
    {
        var (cal, _) = Armar(HttpStatusCode.NotFound);

        var acto = () => cal.ActualizarAsync(Org, "AAMk-1", Evento, default);

        await acto.Should().ThrowAsync<OutlookNoDisponibleException>().WithMessage("*ya no existe*");
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Otros_fallos_son_no_disponible(HttpStatusCode codigo)
    {
        var (cal, _) = Armar(codigo);

        var acto = () => cal.CrearAsync(Org, Evento, Guid.NewGuid(), default);

        await acto.Should().ThrowAsync<OutlookNoDisponibleException>();
    }

    [Fact]
    public async Task Un_403_registra_el_codigo_de_Graph_y_nada_mas()
    {
        var log = new LogCapturado();
        var (cal, _) = Armar(HttpStatusCode.Forbidden,
            """{"error":{"code":"ErrorAccessDenied","message":"detalle-del-cuerpo"}}""", log);

        var acto = () => cal.ActualizarAsync(Org, "AAMk-1", Evento, default);

        await acto.Should().ThrowAsync<OutlookNoDisponibleException>();
        var aviso = log.Entradas.Should().ContainSingle(e => e.Nivel == LogLevel.Warning).Subject.Mensaje;
        aviso.Should().Be("Graph respondió 403 (ErrorAccessDenied) en PATCH me/events/AAMk-1");
        aviso.Should().NotContain("tok-123").And.NotContain("detalle-del-cuerpo");
    }

    [Fact]
    public async Task Un_error_con_cuerpo_ilegible_se_registra_sin_codigo()
    {
        var log = new LogCapturado();
        var (cal, _) = Armar(HttpStatusCode.ServiceUnavailable, "<html>no es json</html>", log);

        var acto = () => cal.CrearAsync(Org, Evento, Guid.NewGuid(), default);

        await acto.Should().ThrowAsync<OutlookNoDisponibleException>();
        log.Entradas.Should().ContainSingle(e => e.Nivel == LogLevel.Warning)
            .Which.Mensaje.Should().Be("Graph respondió 503 (sin código) en POST me/events");
    }

    [Fact]
    public async Task Sin_red_es_no_disponible()
    {
        var conexion = Substitute.For<IConexionMicrosoft>();
        conexion.ObtenerTokenAsync(Org, Arg.Any<CancellationToken>()).Returns("tok");
        var http = new HttpClient(new FallaRed()) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };

        var acto = () => new GraphOutlookCalendario(http, conexion, NullLogger<GraphOutlookCalendario>.Instance).CrearAsync(Org, Evento, Guid.NewGuid(), default);

        await acto.Should().ThrowAsync<OutlookNoDisponibleException>();
    }

    [Fact]
    public async Task Una_respuesta_ilegible_es_no_disponible()
    {
        var (cal, _) = Armar(HttpStatusCode.Created, "no es json");

        var acto = () => cal.CrearAsync(Org, Evento, Guid.NewGuid(), default);

        await acto.Should().ThrowAsync<OutlookNoDisponibleException>();
    }

    private sealed class FallaRed : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
            throw new HttpRequestException("No such host is known.");
    }
}
