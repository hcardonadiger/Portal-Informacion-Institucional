using System.Net;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;
using Diger.TramitesEstado.Infrastructure.Outlook;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Outlook;

public class GraphCalendarioOutlookLecturaTests
{
    private sealed class Graph(params (HttpStatusCode Codigo, string Cuerpo)[] respuestas) : HttpMessageHandler
    {
        private int _i;
        public readonly List<HttpRequestMessage> Pedidos = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Pedidos.Add(r);
            var (codigo, cuerpo) = respuestas[Math.Min(_i++, respuestas.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(codigo) { Content = new StringContent(cuerpo) });
        }
    }

    private static readonly Guid Ana = Guid.NewGuid();

    private static (GraphCalendarioOutlookLectura l, Graph g) Armar(params (HttpStatusCode, string)[] respuestas)
    {
        var g = new Graph(respuestas);
        var conexion = Substitute.For<IConexionMicrosoft>();
        conexion.ObtenerTokenAsync(Ana, Arg.Any<CancellationToken>()).Returns("tok-cal");
        var http = new HttpClient(g) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        return (new GraphCalendarioOutlookLectura(http, conexion, NullLogger<GraphCalendarioOutlookLectura>.Instance), g);
    }

    private const string Evento = """
        {"id":"AAMk-1","iCalUId":"ical-1","subject":"Mesa","isAllDay":false,"isOrganizer":true,"isCancelled":false,
         "lastModifiedDateTime":"2026-10-29T18:00:00Z",
         "start":{"dateTime":"2026-11-03T09:30:00.0000000","timeZone":"Central America Standard Time"},
         "end":{"dateTime":"2026-11-03T10:30:00.0000000","timeZone":"Central America Standard Time"},
         "location":{"displayName":"Sala 2"},"onlineMeeting":{"joinUrl":"https://teams/x"},
         "attendees":[{"type":"required","emailAddress":{"name":"Marta","address":"marta@copeco.gob.hn"}},
                      {"type":"resource","emailAddress":{"name":"Sala 2","address":"sala2@diger.gob.hn"}}]}
        """;

    [Fact]
    public async Task Pide_la_ventana_en_hora_de_Honduras_y_sigue_las_paginas()
    {
        var (l, g) = Armar(
            (HttpStatusCode.OK, $$"""{"value":[{{Evento}}],"@odata.nextLink":"https://graph.microsoft.com/v1.0/me/calendarView?$skiptoken=abc"}"""),
            (HttpStatusCode.OK, """{"value":[]}"""));

        var citas = await l.ListarAsync(Ana, new DateOnly(2026, 11, 2), new DateOnly(2027, 1, 31), default);

        citas.Should().ContainSingle();
        // BeEquivalentTo y no Be: el record lleva una lista, y la igualdad de record la compara por referencia.
        citas[0].Should().BeEquivalentTo(new CitaOutlook("AAMk-1", "ical-1", "Mesa", new DateTime(2026, 11, 3, 9, 30, 0), new DateTime(2026, 11, 3, 10, 30, 0),
            false, "Sala 2", "https://teams/x", true, false, new DateTime(2026, 10, 29, 18, 0, 0, DateTimeKind.Utc),
            [new InvitadoCita("Marta", "marta@copeco.gob.hn", false), new InvitadoCita("Sala 2", "sala2@diger.gob.hn", true)]));

        var primero = Uri.UnescapeDataString(g.Pedidos[0].RequestUri!.ToString());
        primero.Should().Contain("me/calendarView?startDateTime=2026-11-02T00:00:00-06:00&endDateTime=2027-01-31T23:59:59-06:00");
        g.Pedidos[0].Headers.GetValues("Prefer").Single().Should().Be("outlook.timezone=\"Central America Standard Time\"");
        g.Pedidos[0].Headers.Authorization!.ToString().Should().Be("Bearer tok-cal");
        g.Pedidos[1].RequestUri!.ToString().Should().EndWith("$skiptoken=abc");
    }

    [Fact]
    public async Task Obtener_devuelve_null_si_la_cita_ya_no_existe()
    {
        var (l, _) = Armar((HttpStatusCode.NotFound, """{"error":{"code":"ErrorItemNotFound"}}"""));

        (await l.ObtenerAsync(Ana, "AAMk-1", default)).Should().BeNull();
    }

    [Fact]
    public async Task Un_401_es_conexion_vencida()
    {
        var (l, _) = Armar((HttpStatusCode.Unauthorized, "{}"));

        var act = () => l.ListarAsync(Ana, new DateOnly(2026, 11, 2), new DateOnly(2027, 1, 31), default);

        await act.Should().ThrowAsync<OutlookConexionVencidaException>();
    }
}
