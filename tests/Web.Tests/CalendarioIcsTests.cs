using System.Net;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La publicación del calendario en formato .ics: la descarga de una reunión y el feed personal
/// suscribible.
///
/// <para>Se prueba acá y no solo en Application porque lo que hay que verificar es el
/// <b>cableado web</b>: que la ruta con sufijo <c>.ics</c> enrute, que el feed quede exento de
/// sesión y que la descarga responda con el tipo de contenido correcto. Nada de eso se ve desde una
/// prueba del generador.</para>
/// </summary>
public sealed class CalendarioIcsTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _reunionId;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Reuniones.Ver");
        await _portal.OtorgarAsync("Consultor", "Tickets.Ver"); // sin Reuniones.Ver, a propósito

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var r = Reunion.Crear("Mesa técnica con SEFIN");
        // Sin institución, el filtro de alcance la esconde de cualquier rol no global.
        r.InstitucionId = "DIGER";
        r.Fecha = new DateOnly(2026, 9, 15);
        r.Hora = "09:00";
        r.DuracionMinutos = 90;
        r.Lugar = "Sala 3";
        db.Reuniones.Add(r);
        await db.SaveChangesAsync();
        _reunionId = r.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    // ── Descarga de una reunión ───────────────────────────────────────────────

    [Fact]
    public async Task La_reunion_se_descarga_como_calendario()
    {
        var respuesta = await _portal.ClienteComo("JefeArea")
            .GetAsync($"/Reuniones/Acta/{_reunionId}?handler=Ics");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        respuesta.Content.Headers.ContentType!.MediaType.Should().Be("text/calendar");

        var ics = await respuesta.Content.ReadAsStringAsync();
        ics.Should().StartWith("BEGIN:VCALENDAR");
        ics.Should().Contain("SUMMARY:Mesa técnica con SEFIN");
        ics.Should().Contain("DTSTART:20260915T150000Z");   // 09:00 en UTC−6
        ics.Should().Contain("LOCATION:Sala 3");
    }

    /// <summary>La descarga no abre una puerta nueva: exige el mismo permiso que ver la reunión.</summary>
    [Fact]
    public async Task Sin_permiso_de_reuniones_no_se_puede_descargar()
    {
        var respuesta = await _portal.ClienteComo("Consultor")
            .GetAsync($"/Reuniones/Acta/{_reunionId}?handler=Ics");

        respuesta.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    // ── Feed personal ─────────────────────────────────────────────────────────

    private async Task<Guid> TokenDeAsync(string correo)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var u = await db.Usuarios.FirstAsync(x => x.Correo == correo);
        var token = u.AsegurarTokenCalendario();
        await db.SaveChangesAsync();
        return token;
    }

    /// <summary>
    /// El feed lo pide el cliente de calendario sin sesión ni cookies: si exigiera autenticación,
    /// la suscripción no funcionaría en absoluto. El token de la URL es la credencial.
    /// </summary>
    [Fact]
    public async Task El_feed_se_sirve_sin_sesion_con_un_token_valido()
    {
        var token = await TokenDeAsync("jefearea@pruebas.gob.hn");

        var respuesta = await _portal.ClienteAnonimo().GetAsync($"/Calendario/Feed/{token}.ics");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        respuesta.Content.Headers.ContentType!.MediaType.Should().Be("text/calendar");
        (await respuesta.Content.ReadAsStringAsync()).Should().StartWith("BEGIN:VCALENDAR");
    }

    [Fact]
    public async Task Un_token_desconocido_responde_404_y_no_una_pantalla_de_sesion()
    {
        var respuesta = await _portal.ClienteAnonimo().GetAsync($"/Calendario/Feed/{Guid.NewGuid()}.ics");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>Regenerar el token es el remedio para un enlace que se compartió de más: el
    /// anterior tiene que dejar de servir.</summary>
    [Fact]
    public async Task Al_regenerar_el_token_el_enlace_anterior_deja_de_funcionar()
    {
        var anterior = await TokenDeAsync("jefearea@pruebas.gob.hn");

        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Usuarios.FirstAsync(x => x.Correo == "jefearea@pruebas.gob.hn");
            u.RegenerarTokenCalendario();
            await db.SaveChangesAsync();
        }

        var respuesta = await _portal.ClienteAnonimo().GetAsync($"/Calendario/Feed/{anterior}.ics");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>El feed trae solo las reuniones de esa persona. Una reunión en la que no participa
    /// no aparece, aunque sea de su misma institución.</summary>
    [Fact]
    public async Task El_feed_solo_trae_las_reuniones_propias()
    {
        var token = await TokenDeAsync("jefearea@pruebas.gob.hn");

        var ics = await _portal.ClienteAnonimo().GetStringAsync($"/Calendario/Feed/{token}.ics");

        // La reunión sembrada no tiene a este usuario ni como organizador ni como asistente.
        ics.Should().NotContain("Mesa técnica con SEFIN");
        ics.Should().StartWith("BEGIN:VCALENDAR");
    }

    [Fact]
    public async Task El_feed_incluye_la_reunion_donde_la_persona_asiste()
    {
        Guid token;
        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var u = await db.Usuarios.FirstAsync(x => x.Correo == "jefearea@pruebas.gob.hn");
            token = u.AsegurarTokenCalendario();

            // Dentro de la ventana del feed, que arranca en «hoy − 30 días».
            var r = Reunion.Crear("Comité de gobierno digital");
            r.Fecha = DateOnly.FromDateTime(DateTime.Today).AddDays(5);
            r.Hora = "10:00";
            r.RegistrarAsistente("Usuario jefearea", null, "DIGER", null, u.Correo, null);
            db.Reuniones.Add(r);

            await db.SaveChangesAsync();
        }

        var ics = await _portal.ClienteAnonimo().GetStringAsync($"/Calendario/Feed/{token}.ics");

        ics.Should().Contain("SUMMARY:Comité de gobierno digital");
    }
}
