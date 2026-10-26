using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// S4 (2026-10-22), de punta a punta: la ficha manda el proyecto de la ruta, y editar o quitar el
/// interesado de otro proyecto responde «no encontrado» sin tocarlo.
/// </summary>
public sealed class InteresadosAlcancePaginaTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _proyectoA;
    private int _interesadoDeA;
    private int _interesadoDeB;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Proyectos.Ver", "Proyectos.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var a = Proyecto.Crear("PRY-2026-711", "Proyecto A");
        var b = Proyecto.Crear("PRY-2026-712", "Proyecto B");
        foreach (var p in new[] { a, b })
        {
            p.PrioridadId   = PortalFactory.PrioridadPorDefecto;
            p.InstitucionId = "DIGER";
        }
        db.Proyectos.AddRange(a, b);

        var persona = Usuario.Crear("Persona interesada", "persona@pruebas.gob.hn", "hash");
        db.Usuarios.Add(persona);
        await db.SaveChangesAsync();

        var ia = InteresadoProyecto.Crear(a.Id, persona.Id, persona.Nombre, RolInteresado.Beneficiario, "siembra");
        var ib = InteresadoProyecto.Crear(b.Id, persona.Id, persona.Nombre, RolInteresado.Beneficiario, "siembra");
        db.ProyectoInteresados.AddRange(ia, ib);
        await db.SaveChangesAsync();

        _proyectoA     = a.Id;
        _interesadoDeA = ia.Id;
        _interesadoDeB = ib.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private async Task<InteresadoProyecto?> LeerAsync(int id)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ProyectoInteresados.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id);
    }

    private async Task<(HttpClient Cliente, string Token)> SesionAsync()
    {
        var cliente = _portal.ClienteComo("JefeArea");
        var html = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoA}");
        var token = Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;
        token.Should().NotBeEmpty();
        return (cliente, token);
    }

    private static FormUrlEncodedContent Form(string token, params (string, string)[] campos) =>
        new([new("__RequestVerificationToken", token), .. campos.Select(c => new KeyValuePair<string, string>(c.Item1, c.Item2))]);

    [Fact]
    public async Task Desde_la_ficha_de_un_proyecto_no_se_edita_ni_quita_el_interesado_de_otro()
    {
        var (cliente, token) = await SesionAsync();

        var editar = await cliente.PostAsync($"/Proyectos/Editor/{_proyectoA}?handler=GuardarInteresado",
            Form(token, ("IntId", _interesadoDeB.ToString()), ("IntRol", nameof(RolInteresado.Regulador))));
        var quitar = await cliente.PostAsync($"/Proyectos/Editor/{_proyectoA}?handler=QuitarInteresado",
            Form(token, ("IntId", _interesadoDeB.ToString())));

        editar.StatusCode.Should().Be(HttpStatusCode.NotFound);
        quitar.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var i = await LeerAsync(_interesadoDeB);
        i.Should().NotBeNull("no se quitó");
        i!.Rol.Should().Be(RolInteresado.Beneficiario);
    }

    [Fact]
    public async Task Desde_la_ficha_el_interesado_propio_se_edita_y_quita_como_antes()
    {
        var (cliente, token) = await SesionAsync();

        var editar = await cliente.PostAsync($"/Proyectos/Editor/{_proyectoA}?handler=GuardarInteresado",
            Form(token, ("IntId", _interesadoDeA.ToString()), ("IntRol", nameof(RolInteresado.Regulador))));
        editar.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await LeerAsync(_interesadoDeA))!.Rol.Should().Be(RolInteresado.Regulador);

        var quitar = await cliente.PostAsync($"/Proyectos/Editor/{_proyectoA}?handler=QuitarInteresado",
            Form(token, ("IntId", _interesadoDeA.ToString())));
        quitar.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await LeerAsync(_interesadoDeA)).Should().BeNull();
    }

    // ── Paso de equipo del alta: ficha → equipo → estructura ───────────────

    private async Task<Guid> UsuarioNuevoAsync()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var u = Usuario.Crear("Integrante nuevo", $"integrante{Guid.NewGuid():N}@pruebas.gob.hn", "hash");
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    [Fact]
    public async Task En_el_alta_la_ficha_ofrece_el_paso_de_equipo_y_omitirlo()
    {
        var html = await _portal.ClienteComo("JefeArea").GetStringAsync($"/Proyectos/Editor/{_proyectoA}?alta=true");

        html.Should().Contain("id=\"paso-equipo\"");
        html.Should().Contain("Guardar y continuar a la estructura");
        html.Should().Contain($"/Proyectos/Editor/{_proyectoA}#estructura", "el paso se puede saltar hacia la estructura");

        var normal = await _portal.ClienteComo("JefeArea").GetStringAsync($"/Proyectos/Editor/{_proyectoA}");
        normal.Should().NotContain("id=\"paso-equipo\"", "fuera del alta la ficha no muestra el recorrido");
    }

    [Fact]
    public async Task Guardar_y_continuar_lleva_a_la_estructura()
    {
        var (cliente, token) = await SesionAsync();

        var r = await cliente.PostAsync($"/Proyectos/Editor/{_proyectoA}?handler=GuardarInteresado",
            Form(token, ("Alta", "true"), ("IntContinuar", "true"),
                ("IntUsuarioId", (await UsuarioNuevoAsync()).ToString()), ("IntRol", nameof(RolInteresado.Ejecutor))));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);
        r.Headers.Location!.OriginalString.Should().EndWith("#estructura").And.NotContain("alta");
    }

    [Fact]
    public async Task Guardar_y_agregar_otro_se_queda_en_el_paso_de_equipo()
    {
        var (cliente, token) = await SesionAsync();

        var r = await cliente.PostAsync($"/Proyectos/Editor/{_proyectoA}?handler=GuardarInteresado",
            Form(token, ("Alta", "true"),
                ("IntUsuarioId", (await UsuarioNuevoAsync()).ToString()), ("IntRol", nameof(RolInteresado.Ejecutor))));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);
        r.Headers.Location!.OriginalString.Should().Contain("alta=True").And.EndWith("#interesados");
    }

    [Fact]
    public async Task Un_rechazo_en_el_paso_de_equipo_no_avanza_a_la_estructura()
    {
        var (cliente, token) = await SesionAsync();

        // Sin usuario elegido: el handler lo rechaza y la persona tiene que verlo en el mismo paso.
        var r = await cliente.PostAsync($"/Proyectos/Editor/{_proyectoA}?handler=GuardarInteresado",
            Form(token, ("Alta", "true"), ("IntContinuar", "true")));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);
        r.Headers.Location!.OriginalString.Should().Contain("alta=True").And.EndWith("#interesados");
    }
}
