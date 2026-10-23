using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Application.Reuniones.Memoria;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El panel «Llenar automáticamente…» del editor de reuniones: el prompt y la lectura del JSON
/// salen de <see cref="MemoriaDesdeTranscripcion"/>, no de una copia en el JavaScript. El editor
/// sigue llenando el formulario —el handler interpreta y devuelve, nunca guarda— para que lo que
/// la persona tecleó sin guardar no se pierda.
/// </summary>
public sealed class MemoriaEditorTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _reunionId;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Reuniones.Ver", "Reuniones.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var r = Reunion.Crear("Mesa técnica");
        r.InstitucionId = "DIGER"; r.InstitucionDuenaId = "DIGER";
        r.AreaId = PortalFactory.AreaDeCasa; r.UnidadId = PortalFactory.UnidadDeCasa;
        r.Fecha = new DateOnly(2026, 10, 1); r.Hora = "09:00";
        r.Tipo = "Reunión técnica";
        r.ObjetivoAgenda = "Objetivo que escribió una persona";
        db.Reuniones.Add(r);
        await db.SaveChangesAsync();
        _reunionId = r.Id;
    }

    public Task DisposeAsync() { _portal.Dispose(); return Task.CompletedTask; }

    private HttpClient Jefe() => _portal.ClienteComo("JefeArea", "DIGER", PortalFactory.AreaDeCasa, PortalFactory.UnidadDeCasa);

    /// <summary>Lo mismo que hace el JavaScript: token del meta <c>csrf-token</c> del layout en el
    /// encabezado <c>RequestVerificationToken</c>, y los tres campos en un FormData.</summary>
    private async Task<HttpResponseMessage> InterpretarAsync(HttpClient c, string json, bool esCapacitacion, bool conEncuesta)
    {
        var html = await c.GetStringAsync($"/Reuniones/Editor/{_reunionId}");
        var token = Regex.Match(html, "<meta name=\"csrf-token\" content=\"([^\"]+)\"").Groups[1].Value;
        token.Should().NotBeEmpty();

        using var form = new MultipartFormDataContent
        {
            { new StringContent(json), "json" },
            { new StringContent(esCapacitacion ? "true" : "false"), "esCapacitacion" },
            { new StringContent(conEncuesta ? "true" : "false"), "conEncuesta" },
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/Reuniones/Editor/{_reunionId}?handler=InterpretarMemoria")
            { Content = form };
        req.Headers.Add("RequestVerificationToken", token);
        return await c.SendAsync(req);
    }

    [Fact]
    public async Task Interpretar_devuelve_la_propuesta_sin_tocar_la_base()
    {
        const string json = """
            {
              "titulo": "Otro título",
              "objetivo": "Revisar el avance del trámite",
              "desarrollo": "<p>Se revisó el avance.</p>",
              "acuerdos": [ { "compromiso": "Enviar el informe", "responsable": "Ana López", "plazo": "2026-11-15" } ],
              "convocados": 30
            }
            """;
        DateTime? antes;
        using (var scope = _portal.Services.CreateScope())
            antes = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Reuniones
                .IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == _reunionId)).UpdatedAt;

        var resp = await InterpretarAsync(Jefe(), json, esCapacitacion: false, conEncuesta: false);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var p = doc.RootElement;
        p.GetProperty("objetivo").GetString().Should().Be("Revisar el avance del trámite");
        p.GetProperty("desarrollo").GetString().Should().Be("<p>Se revisó el avance.</p>");
        p.GetProperty("convocados").ValueKind.Should().Be(JsonValueKind.Null, "no es una capacitación");
        var acuerdo = p.GetProperty("acuerdos")[0];
        acuerdo.GetProperty("compromiso").GetString().Should().Be("Enviar el informe");
        acuerdo.GetProperty("responsable").GetString().Should().Be("Ana López");
        acuerdo.GetProperty("plazo").GetString().Should().Be("2026-11-15", "el <input type=date> lo quiere así");
        p.GetProperty("mensaje").GetString().Should().Be(
            MemoriaDesdeTranscripcion.Interpretar(json, esCapacitacion: false, conEncuesta: false).Mensaje,
            "el mensaje es el de las reglas del servidor");

        using var verif = _portal.Services.CreateScope();
        var r = await verif.ServiceProvider.GetRequiredService<AppDbContext>().Reuniones
            .IgnoreQueryFilters().AsNoTracking().Include(x => x.Acuerdos).SingleAsync(x => x.Id == _reunionId);
        r.ObjetivoAgenda.Should().Be("Objetivo que escribió una persona", "el editor llena el formulario, no guarda");
        r.Titulo.Should().Be("Mesa técnica");
        r.Acuerdos.Should().BeEmpty();
        r.UpdatedAt.Should().Be(antes);
    }

    [Fact]
    public async Task En_una_capacitacion_con_encuesta_llegan_su_bloque_y_la_segunda_firma()
    {
        const string json = """
            { "convocados": 30, "num_asistentes": 24, "pct_asistencia": 80,
              "compromisos": ["Replicar el taller", "Entregar material"], "validador_institucion": "Luis Mejía" }
            """;

        var resp = await InterpretarAsync(Jefe(), json, esCapacitacion: true, conEncuesta: true);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var p = doc.RootElement;
        p.GetProperty("convocados").GetInt32().Should().Be(30);
        p.GetProperty("numAsistentes").GetInt32().Should().Be(24);
        p.GetProperty("pctAsistencia").GetInt32().Should().Be(80);
        p.GetProperty("compromisos").GetString().Should().Be("Replicar el taller\nEntregar material");
        p.GetProperty("validadorInstitucion").GetString().Should().Be("Luis Mejía");
    }

    [Fact]
    public async Task Un_json_invalido_devuelve_400_con_el_motivo()
    {
        var resp = await InterpretarAsync(Jefe(), "{ esto no es json", esCapacitacion: false, conEncuesta: false);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("error").GetString().Should().Be("El texto pegado no es un JSON válido.");
    }

    [Fact]
    public async Task El_editor_trae_las_cuatro_variantes()
    {
        var html = await Jefe().GetStringAsync($"/Reuniones/Editor/{_reunionId}");

        var m = Regex.Match(html, @"var CW_VARIANTES = (\{.*?\});\s*$", RegexOptions.Multiline);
        m.Success.Should().BeTrue("el panel lee las variantes del servidor");
        var blob = m.Groups[1].Value;
        blob.Should().NotContainAny(["<", ">"], "un </script> dentro del JSON cerraría el script");

        using var doc = JsonDocument.Parse(blob);
        var variantes = doc.RootElement;
        foreach (var (cap, enc) in new[] { (false, false), (false, true), (true, false), (true, true) })
        {
            var v = variantes.GetProperty($"cap={(cap ? 1 : 0)}|enc={(enc ? 1 : 0)}");
            var esperado = MemoriaDesdeTranscripcion.Instrucciones(cap, enc);
            v.GetProperty("variante").GetString().Should().Be(esperado.Variante);
            v.GetProperty("prompt").GetString().Should().Be(esperado.Prompt);
            v.GetProperty("formato").GetString().Should().Be(esperado.Formato);
            v.GetProperty("campos").EnumerateArray().Select(c => c.GetProperty("clave").GetString())
                .Should().Equal(esperado.Campos.Select(c => c.Clave));
        }
        variantes.GetProperty("cap=1|enc=1").GetProperty("prompt").GetString().Should()
            .StartWith("Eres un asistente que redacta la memoria de una CAPACITACIÓN impartida por la DIGER.");

        html.Should().NotContain("CW_REGLAS_COMUNES", "las reglas ya no viven en el JavaScript");
    }
}
