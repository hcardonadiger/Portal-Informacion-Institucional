using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Web.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La plantilla «Proyecto genérico» y el alta que la aplica: se siembra una sola vez, el modal de
/// alta la propone, y crear el proyecto con ella deja la estructura copiada desde el primer momento.
/// </summary>
public sealed class PlantillaGenericaAltaTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _plantilla;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Proyectos.Ver", "Proyectos.Crear", "Proyectos.Editar");

        // El servicio de arranque corre antes de que PrepararAsync cree el esquema; acá se siembra
        // con el mismo método que usa él.
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await PlantillaGenericaSeedService.SembrarAsync(db, "DIGER", default);
        _plantilla = (await db.PlantillasProyecto.IgnoreQueryFilters()
            .SingleAsync(p => p.Nombre == PlantillaGenericaSeedService.Nombre)).Id;
    }

    public Task DisposeAsync() { _portal.Dispose(); return Task.CompletedTask; }

    private static string Token(string html) =>
        Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

    private async Task<int> CrearAsync(string nombre, string plantillaId)
    {
        var cliente = _portal.ClienteComo("JefeArea");
        var token   = Token(await cliente.GetStringAsync("/Proyectos"));
        var r = await cliente.PostAsync("/Proyectos?handler=Crear", new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token),
            new("NuevoNombre", nombre),
            new("NuevaPrioridadId", PortalFactory.PrioridadPorDefecto.ToString()),
            new("NuevaPlantillaId", plantillaId)
        ]));
        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Proyectos.IgnoreQueryFilters().SingleAsync(p => p.Nombre == nombre)).Id;
    }

    [Fact]
    public async Task La_siembra_deja_el_ciclo_de_vida_y_no_se_repite()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await PlantillaGenericaSeedService.SembrarAsync(db, "DIGER", default))
            .Should().BeFalse("ya existe: arrancar otra vez no la duplica");

        var p = await db.PlantillasProyecto.IgnoreQueryFilters()
            .Include(x => x.Entregables).ThenInclude(e => e.Actividades)
            .SingleAsync(x => x.Id == _plantilla);
        p.Nivel.Should().Be(NivelAlcance.Institucion, "la genérica la ve toda la institución");
        p.Entregables.Select(e => e.Nombre).Should().Equal(
            "Acta de constitución", "Plan del proyecto", "Producto del proyecto", "Informes de seguimiento", "Acta de cierre");
        p.Entregables.Sum(e => e.Actividades.Count).Should().Be(15);
    }

    [Fact]
    public async Task El_modal_de_alta_ofrece_la_generica_pero_llega_sin_plantilla()
    {
        var html = await _portal.ClienteComo("JefeArea").GetStringAsync("/Proyectos");
        html.Should().MatchRegex($"""<option value="{_plantilla}"[^>]*>Proyecto gen""",
            "la genérica se ofrece, pero sin marcar");
        Regex.Match(html, """<select id="n-plantilla"[\s\S]*?</select>""").Value
            .Should().NotContain("selected", "un proyecto que se arma de cero no puede llevarse una estructura que nadie pidió");
    }

    [Fact]
    public async Task Crear_con_la_generica_trae_su_estructura()
    {
        var id = await CrearAsync("Proyecto con plantilla", _plantilla.ToString());

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ProyectoEntregables.CountAsync(e => e.ProyectoId == id)).Should().Be(5);
        var entregables = db.ProyectoEntregables.Where(e => e.ProyectoId == id).Select(e => e.Id);
        (await db.ProyectoActividades.CountAsync(a => entregables.Contains(a.EntregableId))).Should().Be(15);
    }

    [Fact]
    public async Task Sin_plantilla_el_proyecto_nace_sin_estructura()
    {
        // Lo que manda el formulario sin tocar el campo: el valor vacío de «Sin plantilla».
        var id = await CrearAsync("Proyecto sin plantilla", "");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ProyectoEntregables.CountAsync(e => e.ProyectoId == id)).Should().Be(0);
    }
}
