using System.Net;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// De dónde salió cada ficha, visto en las pantallas.
///
/// El catálogo mezcla dos cosas que se parecen y no lo son: fichas que llegaron del inventario de
/// SIGER y fichas que nacieron en este portal desde un expediente. Quien busque una promovida en
/// SIGER no la encuentra y concluye que falta un dato; no falta, nunca estuvo ahí. La diferencia
/// la da <c>IdSiger</c> vacío, y estas pruebas comprueban que las pantallas la digan.
/// </summary>
public sealed class OrigenDeLaFichaTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    private int _delInventario;
    private int _promovida;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Administrador", "Siger.Ver", "Siger.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Instituciones.Add(Institucion.Crear("IDP", "Instituto de Prueba"));
        await db.SaveChangesAsync();

        var delInventario = Ficha("930-001", "Vino del inventario");
        delInventario.IdSiger = 4455;

        var promovida = Ficha("930-P01", "Nacio en el portal");
        promovida.IdSiger = null;

        db.TramitesSiger.AddRange(delInventario, promovida);
        await db.SaveChangesAsync();

        _delInventario = delInventario.Id;
        _promovida     = promovida.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    // ── El aviso en el detalle ────────────────────────────────────────────────

    [Fact]
    public async Task Una_ficha_promovida_lo_dice_en_su_detalle()
    {
        var html = await LeerAsync($"/Siger/Detalle/{_promovida}");

        html.Should().Contain("Ficha creada desde un expediente");
        html.Should().Contain("no existe en el inventario de SIGER");
    }

    /// <summary>El aviso solo tiene sentido cuando es cierto; en una ficha del inventario diría
    /// una falsedad y además sembraría dudas sobre todas las demás.</summary>
    [Fact]
    public async Task Una_ficha_del_inventario_no_lleva_ese_aviso()
    {
        var html = await LeerAsync($"/Siger/Detalle/{_delInventario}");

        html.Should().NotContain("Ficha creada desde un expediente");
    }

    // ── El filtro del inventario ──────────────────────────────────────────────

    [Fact]
    public async Task El_inventario_puede_quedarse_solo_con_las_promovidas()
    {
        var html = await LeerAsync("/Siger/Index?Origen=Promovida");

        html.Should().Contain("Nacio en el portal");
        html.Should().NotContain("Vino del inventario");
    }

    [Fact]
    public async Task El_inventario_puede_quedarse_solo_con_las_del_inventario()
    {
        var html = await LeerAsync("/Siger/Index?Origen=Siger");

        html.Should().Contain("Vino del inventario");
        html.Should().NotContain("Nacio en el portal");
    }

    [Fact]
    public async Task Sin_filtro_de_origen_salen_las_dos()
    {
        var html = await LeerAsync("/Siger/Index");

        html.Should().Contain("Vino del inventario");
        html.Should().Contain("Nacio en el portal");
    }

    // ── Apoyo ─────────────────────────────────────────────────────────────────

    private async Task<string> LeerAsync(string ruta)
    {
        var respuesta = await _portal.ClienteComo("Administrador").GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        // Razor codifica los acentos; sin decodificar, cualquier aserción sobre texto con tilde
        // falla aunque la página esté bien.
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
    }

    private static TramiteSiger Ficha(string codigo, string nombre) => new()
    {
        Codigo = codigo, Nombre = nombre, EstadoSiger = "Registrado",
        Institucion = "Instituto de Prueba", Sigla = "IDP", InstitucionId = "IDP"
    };
}
