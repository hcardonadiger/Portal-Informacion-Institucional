using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>Panel de control → Proyectos: archivar oculta el proyecto del portafolio sin borrarlo,
/// restaurar lo devuelve, y solo quien tiene la clave del panel puede hacer cualquiera de las dos.</summary>
public sealed class PanelControlProyectosTests : IAsyncLifetime
{
    private const string Ruta = "/PanelControl/Proyectos";
    private readonly PortalFactory _portal = new();
    private int _proyecto;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Consultor", "PanelControl.Proyectos.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var p = Proyecto.Crear("PRY-2026-901", "Proyecto para archivar");
        p.PrioridadId   = PortalFactory.PrioridadPorDefecto;
        p.InstitucionId = "DIGER";
        db.Proyectos.Add(p);
        await db.SaveChangesAsync();
        _proyecto = p.Id;
    }

    public Task DisposeAsync() { _portal.Dispose(); return Task.CompletedTask; }

    private static string Token(string html) =>
        Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

    private static FormUrlEncodedContent Form(string token, params (string, string)[] campos) =>
        new([new("__RequestVerificationToken", token), .. campos.Select(c => new KeyValuePair<string, string>(c.Item1, c.Item2))]);

    private async Task<Proyecto> LeerAsync()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Proyectos.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == _proyecto);
    }

    [Fact]
    public async Task Sin_la_clave_del_panel_se_niega()
    {
        (await _portal.ClienteComo("Empleado").GetAsync(Ruta)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Con_solo_Ver_no_ofrece_archivar_ni_lo_acepta()
    {
        var cliente = _portal.ClienteComo("Consultor");
        var html = await cliente.GetStringAsync(Ruta);
        html.Should().Contain("Proyecto para archivar").And.NotContain("Confirmar archivo");

        var post = await cliente.PostAsync($"{Ruta}?handler=Archivar&id={_proyecto}", Form(Token(html)));
        post.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await LeerAsync()).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task El_administrador_archiva_y_restaura_sin_borrar_nada()
    {
        var cliente = _portal.ClienteComo("Administrador");
        var html = await cliente.GetStringAsync(Ruta);

        var archivar = await cliente.PostAsync($"{Ruta}?handler=Archivar&id={_proyecto}",
            Form(Token(html), ("Motivo", "Duplicado")));
        archivar.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await LeerAsync()).IsDeleted.Should().BeTrue("archivar es ocultar, la fila sigue en la base");

        (await cliente.GetStringAsync("/Proyectos")).Should().NotContain("Proyecto para archivar",
            "un proyecto archivado sale del portafolio");
        (await cliente.GetStringAsync($"{Ruta}?archivados=true")).Should().Contain("Proyecto para archivar",
            "la pestaña de archivados es desde donde se recupera");

        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.BitacorasProyecto.IgnoreQueryFilters().AnyAsync(b => b.ProyectoId == _proyecto))
                .Should().BeTrue("la bitácora deja quién lo archivó y por qué");
        }

        var restaurar = await cliente.PostAsync($"{Ruta}?handler=Restaurar&id={_proyecto}", Form(Token(html)));
        restaurar.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await LeerAsync()).IsDeleted.Should().BeFalse();
        (await cliente.GetStringAsync("/Proyectos")).Should().Contain("Proyecto para archivar");
    }
}
