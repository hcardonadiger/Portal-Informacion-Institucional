using System.Net;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Reuniones.Memoria;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Outlook;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Outlook;

/// <summary>Fase 2, parte B: la transcripción de Teams, de Graph al texto que recibe la IA.</summary>
public class TranscripcionesTeamsTests
{
    // ── VTT → texto ───────────────────────────────────────────────

    [Fact]
    public void Vtt_queda_como_hablante_y_texto_y_junta_frases_seguidas()
    {
        const string vtt = """
            WEBVTT

            1a2b/12-0
            00:00:00.000 --> 00:00:04.200
            <v Ana López>Buenos días a todos.</v>

            1a2b/13-0
            00:00:04.200 --> 00:00:06.000
            <v Ana López>Empezamos con el primer punto.</v>

            00:00:06.000 --> 00:00:09.500
            <v Luis Pérez>De acuerdo &amp; listo.</v>
            """;

        TranscripcionVtt.ATexto(vtt).Should().Be(
            "Ana López: Buenos días a todos. Empezamos con el primer punto.\r\n".Replace("\r\n", Environment.NewLine) +
            "Luis Pérez: De acuerdo & listo.");
    }

    [Fact]
    public void Texto_que_no_es_vtt_se_devuelve_tal_cual()
    {
        TranscripcionVtt.ATexto("  Ana: hola  ").Should().Be("Ana: hola");
    }

    // ── Graph ─────────────────────────────────────────────────────

    /// <summary>Responde según el principio de la ruta pedida, y anota cada ruta.</summary>
    private sealed class Graph(params (string Ruta, HttpStatusCode Codigo, string Cuerpo)[] respuestas) : HttpMessageHandler
    {
        public readonly List<string> Pedidas = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            var ruta = r.RequestUri!.PathAndQuery.Replace("/v1.0/", "");
            Pedidas.Add(ruta);
            r.Headers.Authorization!.ToString().Should().Be("Bearer tok-tr");
            var (_, codigo, cuerpo) = respuestas.Where(x => ruta.StartsWith(x.Ruta, StringComparison.Ordinal))
                .OrderByDescending(x => x.Ruta.Length).First();
            return Task.FromResult(new HttpResponseMessage(codigo) { Content = new StringContent(cuerpo) });
        }
    }

    private static readonly Guid Org = Guid.NewGuid();
    private const string Enlace = "https://teams.microsoft.com/l/meetup-join/19%3ameeting_x%40thread.v2/0?context=%7b%7d";

    private static (GraphTranscripcionesTeams t, Graph g) Armar(params (string, HttpStatusCode, string)[] respuestas)
    {
        var g = new Graph(respuestas);
        var conexion = Substitute.For<IConexionMicrosoft>();
        conexion.ObtenerTokenTranscripcionesAsync(Org, Arg.Any<CancellationToken>()).Returns("tok-tr");
        var http = new HttpClient(g) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        return (new GraphTranscripcionesTeams(http, conexion, NullLogger<GraphTranscripcionesTeams>.Instance), g);
    }

    [Fact]
    public async Task Busca_la_reunion_por_enlace_y_baja_la_transcripcion_mas_reciente()
    {
        var (t, g) = Armar(
            ("me/onlineMeetings?", HttpStatusCode.OK, """{"value":[{"id":"MSo1"}]}"""),
            ("me/onlineMeetings/MSo1/transcripts", HttpStatusCode.OK,
                """{"value":[{"id":"T-vieja","createdDateTime":"2026-10-28T15:00:00Z"},{"id":"T-nueva","createdDateTime":"2026-10-28T17:30:00Z"}]}"""),
            ("me/onlineMeetings/MSo1/transcripts/T-nueva/content", HttpStatusCode.OK,
                "WEBVTT\n\n00:00:00.000 --> 00:00:02.000\n<v Ana>Hola.</v>\n"));

        var res = await t.ObtenerUltimaAsync(Org, Enlace, default);

        res.Texto.Should().Be("Ana: Hola.");
        res.CreadaElUtc.Should().Be(new DateTime(2026, 10, 28, 17, 30, 0, DateTimeKind.Utc));
        Uri.UnescapeDataString(g.Pedidas[0]).Should().Be($"me/onlineMeetings?$filter=JoinWebUrl eq '{Enlace}'");
        g.Pedidas[2].Should().Be("me/onlineMeetings/MSo1/transcripts/T-nueva/content?$format=text/vtt");
    }

    [Fact]
    public async Task Sin_transcripciones_lo_dice_con_el_mensaje_de_Teams()
    {
        var (t, _) = Armar(
            ("me/onlineMeetings?", HttpStatusCode.OK, """{"value":[{"id":"MSo1"}]}"""),
            ("me/onlineMeetings/MSo1/transcripts", HttpStatusCode.OK, """{"value":[]}"""));

        var act = () => t.ObtenerUltimaAsync(Org, Enlace, default);

        await act.Should().ThrowAsync<TranscripcionNoDisponibleException>()
            .WithMessage(GraphTranscripcionesTeams.SinTranscripcion);
    }

    [Fact]
    public async Task Sin_reunion_en_linea_lo_dice()
    {
        var (t, _) = Armar(("me/onlineMeetings?", HttpStatusCode.OK, """{"value":[]}"""));

        var act = () => t.ObtenerUltimaAsync(Org, Enlace, default);

        await act.Should().ThrowAsync<TranscripcionNoDisponibleException>()
            .WithMessage(GraphTranscripcionesTeams.SinReunionEnLinea);
    }

    [Fact]
    public async Task Un_403_apunta_al_consentimiento_de_TI()
    {
        var (t, _) = Armar(("me/onlineMeetings?", HttpStatusCode.Forbidden, """{"error":{"code":"Forbidden"}}"""));

        var act = () => t.ObtenerUltimaAsync(Org, Enlace, default);

        await act.Should().ThrowAsync<TranscripcionNoDisponibleException>().WithMessage("*consentimiento*");
    }

    [Fact]
    public async Task Un_401_es_conexion_vencida()
    {
        var (t, _) = Armar(("me/onlineMeetings?", HttpStatusCode.Unauthorized, "{}"));

        var act = () => t.ObtenerUltimaAsync(Org, Enlace, default);

        await act.Should().ThrowAsync<OutlookConexionVencidaException>();
    }

    // ── La consulta ───────────────────────────────────────────────

    private static AppDbContext Ctx()
    {
        var usuario = Substitute.For<ICurrentUserService>();
        usuario.EsGlobal.Returns(true);
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(opts, usuario, Substitute.For<MediatR.IPublisher>());
    }

    private static async Task<int> SembrarAsync(AppDbContext ctx, Guid? organizador, string? enlace)
    {
        var r = Reunion.Crear("Mesa técnica");
        if (organizador is { } o) r.RegistrarEnvioOutlook("AAMk-1", o, enlace, "huella", DateTime.UtcNow);
        ctx.Reuniones.Add(r);
        await ctx.SaveChangesAsync();
        return r.Id;
    }

    [Fact]
    public async Task La_consulta_usa_la_conexion_del_organizador()
    {
        using var ctx = Ctx();
        var id = await SembrarAsync(ctx, Org, Enlace);
        var teams = Substitute.For<ITranscripcionesTeams>();
        var esperada = new TranscripcionTeams("Ana: Hola.", DateTime.UtcNow);
        teams.ObtenerUltimaAsync(Org, Enlace, Arg.Any<CancellationToken>()).Returns(esperada);

        var res = await new GetTranscripcionReunionQueryHandler(ctx, teams).Handle(new GetTranscripcionReunionQuery(id), default);

        res.Should().Be(esperada);
    }

    [Fact]
    public async Task Sin_Teams_no_llama_a_Microsoft()
    {
        using var ctx = Ctx();
        var id = await SembrarAsync(ctx, null, null);
        var teams = Substitute.For<ITranscripcionesTeams>();

        var act = () => new GetTranscripcionReunionQueryHandler(ctx, teams).Handle(new GetTranscripcionReunionQuery(id), default);

        await act.Should().ThrowAsync<TranscripcionNoDisponibleException>()
            .WithMessage(GetTranscripcionReunionQueryHandler.SinTeams);
        await teams.DidNotReceiveWithAnyArgs().ObtenerUltimaAsync(default, default!, default);
    }

    [Fact]
    public async Task Conexion_vencida_del_organizador_lo_nombra()
    {
        using var ctx = Ctx();
        var ana = Usuario.Crear("Ana Organizadora", "ana@diger.gob.hn", "hash");
        ctx.Usuarios.Add(ana);
        var id = await SembrarAsync(ctx, ana.Id, Enlace);
        var teams = Substitute.For<ITranscripcionesTeams>();
        teams.ObtenerUltimaAsync(ana.Id, Enlace, Arg.Any<CancellationToken>())
            .ThrowsAsync(new OutlookConexionVencidaException("401"));

        var act = () => new GetTranscripcionReunionQueryHandler(ctx, teams).Handle(new GetTranscripcionReunionQuery(id), default);

        await act.Should().ThrowAsync<TranscripcionNoDisponibleException>().WithMessage("*Ana Organizadora*reconectarse*");
    }

    [Fact]
    public async Task Reunion_que_no_existe_es_NotFound()
    {
        using var ctx = Ctx();

        var act = () => new GetTranscripcionReunionQueryHandler(ctx, Substitute.For<ITranscripcionesTeams>())
            .Handle(new GetTranscripcionReunionQuery(999), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
