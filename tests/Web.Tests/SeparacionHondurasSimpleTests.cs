using System.Net;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El invariante por el que existe la separación del 3 de septiembre de 2026: quien solo tiene
/// el inventario SIGER no alcanza el trabajo de Honduras Simple.
///
/// El inventario va a abrirse a gente de fuera. Antes, darle "Siger.Ver" a alguien le daba de
/// paso la conciliación, la publicación, el llenado y la edición de fichas, porque las siete
/// pantallas colgaban del mismo módulo. Esta prueba es la que se rompe si alguien vuelve a
/// colgar una de ellas de "Siger", que es la única forma real de deshacer la separación sin
/// darse cuenta.
/// </summary>
public sealed class SeparacionHondurasSimpleTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        // "Consultor" hace de gente de fuera: el inventario y nada más.
        await _portal.OtorgarAsync("Consultor", "Siger.Ver");

        // "JefeArea" hace del equipo de Honduras Simple: el trabajo, y también el inventario
        // porque se llega a las fichas desde él.
        await _portal.OtorgarAsync("JefeArea",
            "Siger.Ver",
            "HondurasSimple.Ver", "HondurasSimple.Editar", "HondurasSimple.Eliminar",
            "HondurasSimple.Llenado.Ver", "HondurasSimple.Llenado.Editar",
            "HondurasSimple.Conciliacion.Editar",
            "HondurasSimple.Publicacion.Ver", "HondurasSimple.Publicacion.Editar");
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("/HondurasSimple/Editor")]
    [InlineData("/HondurasSimple/Archivo")]
    [InlineData("/HondurasSimple/CapturaLote")]
    [InlineData("/HondurasSimple/Completitud")]
    [InlineData("/HondurasSimple/Llenado")]
    [InlineData("/HondurasSimple/Conciliacion")]
    [InlineData("/HondurasSimple/Publicacion")]
    public async Task Con_solo_el_inventario_no_se_alcanza_Honduras_Simple(string ruta)
    {
        var respuesta = await _portal.ClienteComo("Consultor").GetAsync(ruta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "el inventario va a abrirse a terceros y el trabajo del portal ciudadano no");
    }

    [Theory]
    [InlineData("/Siger/Index")]
    [InlineData("/Siger/Tablero")]
    public async Task Con_solo_el_inventario_sigue_viendose_el_inventario(string ruta)
    {
        var respuesta = await _portal.ClienteComo("Consultor").GetAsync(ruta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
            "separar no es cerrar: lo que se abre a terceros tiene que seguir abierto");
    }

    [Theory]
    [InlineData("/HondurasSimple/Editor")]
    [InlineData("/HondurasSimple/Archivo")]
    [InlineData("/HondurasSimple/CapturaLote")]
    [InlineData("/HondurasSimple/Completitud")]
    [InlineData("/HondurasSimple/Llenado")]
    [InlineData("/HondurasSimple/Conciliacion")]
    [InlineData("/HondurasSimple/Publicacion")]
    public async Task Con_las_claves_nuevas_se_entra_sin_ser_administrador(string ruta)
    {
        // Sin esto la separación sería inútil de otra manera: el equipo que hace el trabajo se
        // quedaría fuera y solo el Administrador —que aprueba por código— podría entrar. Es el
        // caso que destapó un [Authorize(Policy = "Siger.Conciliacion.Editar")] olvidado, que
        // el Administrador no notaba porque no pasa por la matriz.
        var respuesta = await _portal.ClienteComo("JefeArea").GetAsync(ruta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
            "quien tiene la clave nueva entra, sea o no administrador");
    }

    [Theory]
    [InlineData("/Siger/Editor", "/HondurasSimple/Editor")]
    [InlineData("/Siger/Archivo", "/HondurasSimple/Archivo")]
    [InlineData("/Siger/CapturaLote", "/HondurasSimple/CapturaLote")]
    [InlineData("/Siger/Completitud", "/HondurasSimple/Completitud")]
    [InlineData("/Siger/Llenado", "/HondurasSimple/Llenado")]
    [InlineData("/Siger/Conciliacion", "/HondurasSimple/Conciliacion")]
    [InlineData("/Siger/Publicacion", "/HondurasSimple/Publicacion")]
    public async Task La_ruta_vieja_lleva_a_la_nueva(string vieja, string nueva)
    {
        var respuesta = await _portal.ClienteComo("JefeArea").GetAsync(vieja);

        respuesta.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        respuesta.Headers.Location!.OriginalString.Should().Be(nueva);
    }

    [Fact]
    public async Task La_ruta_vieja_no_pierde_los_filtros()
    {
        // La mitad de estas pantallas se comparte con los filtros puestos. Llegar a la
        // pantalla sin el filtro que le prometieron a uno no es mucho mejor que un 404.
        var respuesta = await _portal.ClienteComo("JefeArea")
            .GetAsync("/Siger/Publicacion?tab=candidatas&buscar=agua");

        respuesta.Headers.Location!.OriginalString
            .Should().Be("/HondurasSimple/Publicacion?tab=candidatas&buscar=agua");
    }
}
