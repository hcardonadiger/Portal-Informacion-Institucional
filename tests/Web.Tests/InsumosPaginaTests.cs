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
/// Entrega 4 (2026-10-22), de punta a punta: el catálogo de insumos lo administra solo el
/// Administrador, la ventana de la actividad ofrece solo lo activo, lo ya asignado se conserva
/// aunque se desactive, y no se asignan insumos a un proyecto fuera de alcance aunque la petición
/// se arme a mano.
/// </summary>
public sealed class InsumosPaginaTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _proyecto, _actividad, _entregable;
    private int _ajeno, _actividadAjena, _entregableAjeno;
    private int _cemento, _retirado, _materiales;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        // JefeArea edita proyectos, pero no administra el catálogo.
        await _portal.OtorgarAsync("JefeArea", "Proyectos.Ver", "Proyectos.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var materiales = CategoriaInsumo.Crear("Materiales de obra", 7);
        db.CategoriasInsumo.Add(materiales);
        await db.SaveChangesAsync();

        var cemento  = Insumo.Crear(materiales.Id, "Cemento gris", null);
        var retirado = Insumo.Crear(materiales.Id, "Asbesto", null);
        db.Insumos.AddRange(cemento, retirado);
        await db.SaveChangesAsync();

        (_proyecto, _entregable, _actividad)       = await ProyectoAsync(db, "PRY-2026-971", "DIGER");
        (_ajeno, _entregableAjeno, _actividadAjena) = await ProyectoAsync(db, "PRY-2026-972", "COPECO");
        (_cemento, _retirado, _materiales) = (cemento.Id, retirado.Id, materiales.Id);
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private static async Task<(int Proyecto, int Entregable, int Actividad)> ProyectoAsync(
        AppDbContext db, string codigo, string institucion)
    {
        var p = Proyecto.Crear(codigo, $"Proyecto {codigo}");
        p.PrioridadId   = PortalFactory.PrioridadPorDefecto;
        p.InstitucionId = institucion;
        var e = EntregableProyecto.Crear("Infraestructura", 1);
        e.Agregar(ActividadProyecto.Crear("Instalar el servidor", 1));
        p.Agregar(e);
        db.Proyectos.Add(p);
        await db.SaveChangesAsync();
        return (p.Id, e.Id, e.Actividades.Single().Id);
    }

    private static string Token(string html) =>
        Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

    private async Task<List<int>> InsumosDeAsync(int actividadId)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ProyectoActividades.IgnoreQueryFilters().Where(a => a.Id == actividadId)
            .SelectMany(a => a.Insumos).Select(i => i.InsumoId).ToListAsync();
    }

    private async Task AsignarDirectoAsync(int actividadId, int insumoId)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = await db.ProyectoActividades.Include(x => x.Insumos).SingleAsync(x => x.Id == actividadId);
        a.FijarInsumos([.. a.InsumoIds, insumoId], new HashSet<int> { insumoId }, "siembra", DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    private async Task DesactivarAsync(int insumoId)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var i = await db.Insumos.SingleAsync(x => x.Id == insumoId);
        i.Actualizar(i.CategoriaId, i.Nombre, i.Descripcion, activo: false);
        await db.SaveChangesAsync();
    }

    /// <summary>El guardado de la ficha, armado a mano: una actividad con los insumos que se pidan.</summary>
    private static FormUrlEncodedContent Guardado(string token, int entregable, int actividad, params int[] insumos) =>
        new([
            new("__RequestVerificationToken", token),
            new("Nombre", "Proyecto con insumos"),
            new("PrioridadId", PortalFactory.PrioridadPorDefecto.ToString()),
            new("Entregables[0].Id", entregable.ToString()),
            new("Entregables[0].Nombre", "Infraestructura"),
            new("Entregables[0].Estado", "Pendiente"),
            new("Entregables[0].Actividades[0].Id", actividad.ToString()),
            new("Entregables[0].Actividades[0].Nombre", "Instalar el servidor"),
            .. insumos.Select(i => new KeyValuePair<string, string>("Entregables[0].Actividades[0].Insumos", i.ToString()))
        ]);

    // ── Solo el Administrador ─────────────────────────────────────

    [Theory]
    [InlineData("/Catalogos/CategoriasInsumo")]
    [InlineData("/Catalogos/Insumos")]
    public async Task El_administrador_abre_los_dos_catalogos(string ruta)
    {
        var r = await _portal.ClienteComo("Administrador").GetAsync(ruta);

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await r.Content.ReadAsStringAsync()).Should().Contain("← Catálogos");
    }

    [Theory]
    [InlineData("JefeArea", "/Catalogos/CategoriasInsumo")]
    [InlineData("JefeArea", "/Catalogos/Insumos")]
    [InlineData("Empleado", "/Catalogos/Insumos")]
    [InlineData("Consultor", "/Catalogos/Insumos")]
    public async Task Nadie_mas_entra_al_catalogo(string rol, string ruta)
    {
        var r = await _portal.ClienteComo(rol).GetAsync(ruta);

        r.StatusCode.Should().NotBe(HttpStatusCode.OK);
        (await r.Content.ReadAsStringAsync()).Should().NotContain("Cemento gris");
    }

    [Fact]
    public async Task Un_alta_mandada_a_mano_por_quien_no_es_administrador_no_crea_nada()
    {
        // El token se saca de una página que sí puede abrir.
        var cliente = _portal.ClienteComo("JefeArea");
        var token   = Token(await cliente.GetStringAsync($"/Proyectos/Editor/{_proyecto}"));

        await cliente.PostAsync("/Catalogos/Insumos?handler=Crear", new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token),
            new("NuevoCategoriaId", _materiales.ToString()),
            new("NuevoNombre", "Colado a mano"),
        ]));

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Insumos.AnyAsync(i => i.Nombre == "Colado a mano")).Should().BeFalse();
    }

    [Fact]
    public async Task La_portada_ofrece_los_dos_catalogos_solo_al_administrador()
    {
        var admin = await _portal.ClienteComo("Administrador").GetStringAsync("/Catalogos/Index");
        var otro  = await _portal.ClienteComo("JefeArea").GetStringAsync("/Catalogos/Index");

        admin.Should().Contain(@"href=""/Catalogos/CategoriasInsumo""").And.Contain(@"href=""/Catalogos/Insumos""");
        otro.Should().NotContain(@"href=""/Catalogos/Insumos""");
    }

    [Fact]
    public async Task El_administrador_crea_y_desactiva_un_insumo_desde_la_pantalla()
    {
        var cliente = _portal.ClienteComo("Administrador");
        var token   = Token(await cliente.GetStringAsync("/Catalogos/Insumos"));

        await cliente.PostAsync("/Catalogos/Insumos?handler=Crear", new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token),
            new("NuevoCategoriaId", _materiales.ToString()),
            new("NuevoNombre", "Varilla de 3/8"),
        ]));

        int id;
        using (var scope = _portal.Services.CreateScope())
            id = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .Insumos.Where(i => i.Nombre == "Varilla de 3/8").Select(i => i.Id).SingleAsync();

        // Sin «activo» en el formulario, la casilla quedó desmarcada.
        await cliente.PostAsync("/Catalogos/Insumos?handler=Actualizar", new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", token),
            new("id", id.ToString()),
            new("categoriaId", _materiales.ToString()),
            new("nombre", "Varilla de 3/8"),
        ]));

        using var s2 = _portal.Services.CreateScope();
        (await s2.ServiceProvider.GetRequiredService<AppDbContext>().Insumos.SingleAsync(i => i.Id == id))
            .Activo.Should().BeFalse("se desactiva, no se borra");
    }

    // ── La ventana de la actividad ────────────────────────────────

    [Fact]
    public async Task La_ventana_ofrece_solo_los_insumos_activos()
    {
        await DesactivarAsync(_retirado);

        var html = await _portal.ClienteComo("Administrador").GetStringAsync($"/Proyectos/Editor/{_proyecto}");

        html.Should().Contain("id=\"dlg-ins\"");
        var catalogo = Regex.Match(html, @"var catalogoIns = (\[.*?\]);").Groups[1].Value;
        catalogo.Should().Contain("Cemento gris").And.NotContain("Asbesto");
    }

    [Fact]
    public async Task Un_insumo_asignado_que_se_desactivo_sigue_en_la_ficha_marcado()
    {
        await AsignarDirectoAsync(_actividad, _retirado);
        await DesactivarAsync(_retirado);

        var html = await _portal.ClienteComo("Administrador").GetStringAsync($"/Proyectos/Editor/{_proyecto}");

        // Del chip hasta el botón «+ insumos…» de la misma fila.
        var chip = Regex.Match(html, $"""<span class="dep-chip baja" data-ins="{_retirado}"[\s\S]*?(?=class="dep-mas")""").Value;
        chip.Should().Contain("Asbesto").And.Contain("desactivado");
        chip.Should().Contain($"name=\"Entregables[0].Actividades[0].Insumos\" value=\"{_retirado}\"",
            "viaja en el formulario para que guardar no lo pierda");
    }

    [Fact]
    public async Task Guardar_la_ficha_asigna_el_insumo_elegido()
    {
        var cliente = _portal.ClienteComo("Administrador");
        var token   = Token(await cliente.GetStringAsync($"/Proyectos/Editor/{_proyecto}"));

        var r = await cliente.PostAsync($"/Proyectos/Editor/{_proyecto}?handler=Guardar",
            Guardado(token, _entregable, _actividad, _cemento));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await InsumosDeAsync(_actividad)).Should().Equal(_cemento);
    }

    [Fact]
    public async Task Un_insumo_desactivado_mandado_a_mano_no_se_asigna()
    {
        await DesactivarAsync(_retirado);
        var cliente = _portal.ClienteComo("Administrador");
        var token   = Token(await cliente.GetStringAsync($"/Proyectos/Editor/{_proyecto}"));

        await cliente.PostAsync($"/Proyectos/Editor/{_proyecto}?handler=Guardar",
            Guardado(token, _entregable, _actividad, _cemento, _retirado));

        (await InsumosDeAsync(_actividad)).Should().BeEmpty("el guardado entero se rechaza");
    }

    // ── Alcance ───────────────────────────────────────────────────

    [Fact]
    public async Task No_se_asignan_insumos_a_un_proyecto_fuera_de_alcance_aunque_se_mande_a_mano()
    {
        // JefeArea es de DIGER y el proyecto ajeno de COPECO. El token sale de su propio proyecto.
        var cliente = _portal.ClienteComo("JefeArea");
        var token   = Token(await cliente.GetStringAsync($"/Proyectos/Editor/{_proyecto}"));

        var r = await cliente.PostAsync($"/Proyectos/Editor/{_ajeno}?handler=Guardar",
            Guardado(token, _entregableAjeno, _actividadAjena, _cemento));

        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await InsumosDeAsync(_actividadAjena)).Should().BeEmpty();
    }

    [Fact]
    public async Task La_actividad_ajena_colada_en_el_guardado_del_proyecto_propio_no_recibe_nada()
    {
        var cliente = _portal.ClienteComo("JefeArea");
        var token   = Token(await cliente.GetStringAsync($"/Proyectos/Editor/{_proyecto}"));

        await cliente.PostAsync($"/Proyectos/Editor/{_proyecto}?handler=Guardar",
            Guardado(token, _entregable, _actividadAjena, _cemento));

        (await InsumosDeAsync(_actividadAjena)).Should().BeEmpty();
    }
}
