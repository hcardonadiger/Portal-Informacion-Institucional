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
/// La relación opcional entre un proyecto y sus reuniones y expedientes.
///
/// <para>Lo que hay que probar acá no es el alta —eso es una fila— sino <b>el desajuste de
/// alcance</b>. El vínculo se ancla en el proyecto, pero la reunión y el expediente llevan su
/// propio filtro, y no es el mismo: el del proyecto deja ver al responsable y a los interesados
/// aunque caigan fuera de su institución. La consecuencia es que alguien puede abrir el proyecto y
/// no poder abrir lo que tiene colgado, y la pantalla lo dice con un conteo en vez de esconderlo.</para>
/// </summary>
public sealed class VinculosProyectoTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    private int _proyectoId;
    private int _reunionPropia;
    private int _reunionAjena;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea",
            "Proyectos.Ver", "Proyectos.Editar", "Reuniones.Ver", "Expedientes.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var proyecto = Proyecto.Crear("PRY-2026-80", "Nodo de interconexión");
        proyecto.InstitucionId = "DIGER";
        db.Proyectos.Add(proyecto);

        var propia = Reunion.Crear("Mesa de trabajo del nodo central");
        propia.InstitucionId = "DIGER";

        // La ajena es de otra institución: el proyecto sí se ve, la reunión no.
        var ajena = Reunion.Crear("Acta reservada de CONSUCOOP");
        ajena.InstitucionId = "CONSUCOOP";

        db.Reuniones.AddRange(propia, ajena);
        await db.SaveChangesAsync();

        _proyectoId    = proyecto.Id;
        _reunionPropia = propia.Id;
        _reunionAjena  = ajena.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private static readonly Regex TokenRx = new(
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.Compiled);

    private async Task<(HttpClient Cliente, string Token)> SesionAsync(string rol = "JefeArea")
    {
        var cliente = _portal.ClienteComo(rol);
        var html = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoId}");
        var token = TokenRx.Match(html).Groups[1].Value;
        token.Should().NotBeEmpty();
        return (cliente, token);
    }

    private static FormUrlEncodedContent Form(string token, params (string, string)[] campos)
    {
        var datos = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token) };
        foreach (var (k, v) in campos) datos.Add(new KeyValuePair<string, string>(k, v));
        return new FormUrlEncodedContent(datos);
    }

    private async Task VincularDirectoAsync(int reunionId)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ProyectoReuniones.Add(ProyectoReunion.Crear(_proyectoId, reunionId, "siembra"));
        await db.SaveChangesAsync();
    }

    // Los títulos de prueba van SIN acentos a propósito: Razor codifica todo lo que sale del
    // Basic Latin, así que «técnica» llega al HTML como «t&#xE9;cnica». Un Contain con acentos no
    // coincide nunca —y, peor, un NotContain con acentos pasa siempre, aunque el texto esté—.

    // ── Alta y baja ───────────────────────────────────────────────
    [Fact]
    public async Task Vincular_una_reunion_la_deja_en_la_ficha()
    {
        var (cliente, token) = await SesionAsync();

        var r = await cliente.PostAsync(
            $"/Proyectos/Editor/{_proyectoId}?handler=VincularReunion",
            Form(token, ("VinculoReunionId", _reunionPropia.ToString()),
                        ("VinculoNota", "se acordo el alcance del enlace")));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var ficha = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoId}");
        ficha.Should().Contain("Mesa de trabajo del nodo central");
        ficha.Should().Contain("se acordo el alcance del enlace");
    }

    [Fact]
    public async Task El_mismo_par_no_se_vincula_dos_veces()
    {
        var (cliente, token) = await SesionAsync();
        var url = $"/Proyectos/Editor/{_proyectoId}?handler=VincularReunion";

        await cliente.PostAsync(url, Form(token, ("VinculoReunionId", _reunionPropia.ToString())));
        await cliente.PostAsync(url, Form(token, ("VinculoReunionId", _reunionPropia.ToString())));

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ProyectoReuniones.IgnoreQueryFilters()
            .CountAsync(x => x.ProyectoId == _proyectoId)).Should().Be(1);
    }

    [Fact]
    public async Task Quitar_el_vinculo_no_borra_la_reunion()
    {
        var (cliente, token) = await SesionAsync();
        await cliente.PostAsync($"/Proyectos/Editor/{_proyectoId}?handler=VincularReunion",
            Form(token, ("VinculoReunionId", _reunionPropia.ToString())));

        int vinculoId;
        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            vinculoId = await db.ProyectoReuniones.IgnoreQueryFilters()
                .Where(x => x.ProyectoId == _proyectoId).Select(x => x.Id).SingleAsync();
        }

        await cliente.PostAsync($"/Proyectos/Editor/{_proyectoId}?handler=QuitarVinculoReunion",
            Form(token, ("VinculoId", vinculoId.ToString())));

        using var scope2 = _portal.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db2.ProyectoReuniones.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await db2.Reuniones.IgnoreQueryFilters().AnyAsync(r => r.Id == _reunionPropia))
            .Should().BeTrue("quitar el vínculo es una afirmación sobre el proyecto, no un borrado");
    }

    // ── Alcance ───────────────────────────────────────────────────
    [Fact]
    public async Task No_se_puede_vincular_una_reunion_fuera_del_alcance()
    {
        // Sin esto, pasar el Id en el formulario permitiría colgar del proyecto —y de paso
        // averiguar que existe— una reunión que la persona no puede ni abrir.
        var (cliente, token) = await SesionAsync();

        await cliente.PostAsync($"/Proyectos/Editor/{_proyectoId}?handler=VincularReunion",
            Form(token, ("VinculoReunionId", _reunionAjena.ToString())));

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ProyectoReuniones.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_vinculo_fuera_de_alcance_se_cuenta_pero_no_se_muestra()
    {
        // Sembrado por detrás: el vínculo existe, pero su reunión es de otra institución.
        await VincularDirectoAsync(_reunionAjena);

        var cliente = _portal.ClienteComo("JefeArea");
        var ficha = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoId}");

        ficha.Should().NotContain("Acta reservada de CONSUCOOP",
            "el título de una reunión ajena no puede filtrarse por la ficha del proyecto");
        ficha.Should().Contain("fuera de su alcance",
            "pero se dice que hay algo vinculado que no se está viendo");
    }

    [Fact]
    public async Task El_selector_no_ofrece_reuniones_ajenas()
    {
        var cliente = _portal.ClienteComo("JefeArea");
        var ficha = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoId}");

        ficha.Should().Contain("Mesa de trabajo del nodo central");
        ficha.Should().NotContain("Acta reservada de CONSUCOOP");
    }

    [Fact]
    public async Task La_pestana_de_vinculos_esta_cableada_al_conmutador()
    {
        // El panel se pinta con el HTML pero las pestañas de esta ficha se abren con un onclick
        // explícito, no con un manejador delegado. Sin él, el botón se ve y no hace nada — que es
        // exactamente como salió la primera vez, y ni la compilación ni las pruebas lo veían.
        var ficha = await _portal.ClienteComo("JefeArea").GetStringAsync($"/Proyectos/Editor/{_proyectoId}");

        ficha.Should().Contain("abrirPestana('vinculos')",
            "el boton de la pestaña tiene que llamar al conmutador, como los otros siete");
        ficha.Should().Contain("id=\"panel-vinculos\"");
    }

    // ── El mismo vínculo desde el otro extremo ────────────────────
    [Fact]
    public async Task Se_puede_vincular_desde_la_ficha_de_la_reunion()
    {
        var cliente = _portal.ClienteComo("JefeArea");
        var html = await cliente.GetStringAsync($"/Reuniones/Acta/{_reunionPropia}");
        var token = TokenRx.Match(html).Groups[1].Value;

        var r = await cliente.PostAsync(
            $"/Reuniones/Acta/{_reunionPropia}?handler=VincularProyecto",
            Form(token, ("VinculoProyectoId", _proyectoId.ToString()),
                        ("VinculoNota", "se trato el alcance del enlace")));

        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Y se ve desde los dos lados: es la misma fila.
        var acta = await cliente.GetStringAsync($"/Reuniones/Acta/{_reunionPropia}");
        acta.Should().Contain("PRY-2026-80");

        var ficha = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoId}");
        ficha.Should().Contain("Mesa de trabajo del nodo central");
    }

    [Fact]
    public async Task Desde_la_reunion_tampoco_se_alcanza_un_proyecto_ajeno()
    {
        // El selector se alimenta del filtro de Proyectos; forzar un Id por el formulario no
        // puede colgar la reunión de un proyecto que la persona no ve.
        int ajeno;
        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var p2 = Proyecto.Crear("PRY-2026-81", "Proyecto de otra institucion");
            p2.InstitucionId = "CONSUCOOP";
            db.Proyectos.Add(p2);
            await db.SaveChangesAsync();
            ajeno = p2.Id;
        }

        var cliente = _portal.ClienteComo("JefeArea");
        var html = await cliente.GetStringAsync($"/Reuniones/Acta/{_reunionPropia}");
        var token = TokenRx.Match(html).Groups[1].Value;

        await cliente.PostAsync($"/Reuniones/Acta/{_reunionPropia}?handler=VincularProyecto",
            Form(token, ("VinculoProyectoId", ajeno.ToString())));

        using var scope2 = _portal.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db2.ProyectoReuniones.IgnoreQueryFilters()
            .CountAsync(x => x.ProyectoId == ajeno)).Should().Be(0);

        html.Should().NotContain("Proyecto de otra institucion",
            "el selector no puede ofrecer proyectos ajenos");
    }

    // ── Permisos ──────────────────────────────────────────────────
    [Fact]
    public async Task Sin_Proyectos_Editar_no_se_vincula()
    {
        await _portal.OtorgarAsync("Consultor", "Proyectos.Ver", "Reuniones.Ver");
        var cliente = _portal.ClienteComo("Consultor");
        var html = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoId}");
        var token = TokenRx.Match(html).Groups[1].Value;

        var r = await cliente.PostAsync($"/Proyectos/Editor/{_proyectoId}?handler=VincularReunion",
            Form(token, ("VinculoReunionId", _reunionPropia.ToString())));

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
