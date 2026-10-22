using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Entrega 3 (2026-10-22), de punta a punta: el selector de responsable de un riesgo ofrece solo a
/// los interesados del proyecto y a su responsable, y el servidor rechaza a cualquier otro aunque
/// se mande a mano en la petición.
/// </summary>
public sealed class RiesgosResponsablePaginaTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _proyecto;
    private Guid _responsable, _interesado, _ajeno;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _responsable = await IdAsync(db, "jefearea@pruebas.gob.hn");
        _interesado  = await IdAsync(db, "empleado@pruebas.gob.hn");
        _ajeno       = await IdAsync(db, "consultor@pruebas.gob.hn");

        // El responsable a propósito NO figura como interesado, como en los proyectos anteriores a
        // que se registrara solo.
        var p = Proyecto.Crear("PRY-2026-951", "Proyecto con equipo");
        p.PrioridadId   = PortalFactory.PrioridadPorDefecto;
        p.InstitucionId = "DIGER";
        p.ResponsableId = _responsable;
        p.Responsable   = "Usuario jefearea";
        db.Proyectos.Add(p);
        await db.SaveChangesAsync();

        db.ProyectoInteresados.Add(InteresadoProyecto.Crear(
            p.Id, _interesado, "Usuario empleado", RolInteresado.Ejecutor, "siembra"));
        await db.SaveChangesAsync();
        _proyecto = p.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private static Task<Guid> IdAsync(AppDbContext db, string correo) =>
        db.Usuarios.Where(u => u.Correo == correo).Select(u => u.Id).SingleAsync();

    private static string Token(string html) =>
        Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

    [Fact]
    public async Task El_selector_ofrece_solo_a_los_interesados_y_al_responsable_sin_duplicados()
    {
        var html = await _portal.ClienteComo("Administrador").GetStringAsync($"/Proyectos/Editor/{_proyecto}");

        var select = Regex.Match(html, """<select id="r-resp"[\s\S]*?</select>""").Value;
        select.Should().NotBeEmpty();
        var ofrecidos = Regex.Matches(select, """<option value="([0-9a-fA-F-]{36})"[^>]*>""")
            .Select(m => Guid.Parse(m.Groups[1].Value)).ToList();

        // Todos los interesados que tenga el proyecto (los automáticos también lo son) más el
        // responsable; nadie más y cada uno una vez.
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var elegibles = await db.ProyectoInteresados.Where(i => i.ProyectoId == _proyecto)
            .Select(i => i.UsuarioId).ToListAsync();
        elegibles.Add(_responsable);

        ofrecidos.Should().OnlyHaveUniqueItems();
        ofrecidos.Should().BeEquivalentTo(elegibles.Distinct());
        ofrecidos.Should().Contain(_responsable).And.Contain(_interesado).And.NotContain(_ajeno);
    }

    [Fact]
    public async Task Si_el_responsable_tambien_es_interesado_aparece_una_sola_vez()
    {
        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ProyectoInteresados.Add(InteresadoProyecto.Crear(
                _proyecto, _responsable, "Usuario jefearea", RolInteresado.Patrocinador, "siembra"));
            await db.SaveChangesAsync();
        }

        var html   = await _portal.ClienteComo("Administrador").GetStringAsync($"/Proyectos/Editor/{_proyecto}");
        var select = Regex.Match(html, """<select id="r-resp"[\s\S]*?</select>""").Value;

        Regex.Matches(select, _responsable.ToString(), RegexOptions.IgnoreCase).Should().HaveCount(1);
    }

    [Fact]
    public async Task El_servidor_rechaza_un_responsable_fuera_del_equipo_aunque_se_mande_a_mano()
    {
        var cliente = _portal.ClienteComo("Administrador");
        var token   = Token(await cliente.GetStringAsync($"/Proyectos/Editor/{_proyecto}"));

        await cliente.PostAsync($"/Proyectos/Editor/{_proyecto}?handler=GuardarRiesgo", new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token),
            new("RiesgoDescripcion", "Riesgo con responsable ajeno"),
            new("RiesgoResponsable", _ajeno.ToString()),
        ]));

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ProyectoRiesgos.AnyAsync(r => r.ProyectoId == _proyecto)).Should().BeFalse();
    }

    [Fact]
    public async Task Con_un_interesado_el_riesgo_se_registra_como_antes()
    {
        var cliente = _portal.ClienteComo("Administrador");
        var token   = Token(await cliente.GetStringAsync($"/Proyectos/Editor/{_proyecto}"));

        await cliente.PostAsync($"/Proyectos/Editor/{_proyecto}?handler=GuardarRiesgo", new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token),
            new("RiesgoDescripcion", "Riesgo con responsable del equipo"),
            new("RiesgoResponsable", _interesado.ToString()),
        ]));

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var riesgo = await db.ProyectoRiesgos.SingleAsync(r => r.ProyectoId == _proyecto);
        riesgo.ResponsableId.Should().Be(_interesado);
    }
}
