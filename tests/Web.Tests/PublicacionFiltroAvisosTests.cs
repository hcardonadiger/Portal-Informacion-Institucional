using System.Net;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El filtro «Avisos» de Publicado (14-10-2026). Se decide en SQL y la columna en memoria, así
/// que lo que importa es que los dos digan lo mismo: hay una ficha por cada aviso posible, y
/// ninguna de ellas puede colarse en «Sin avisos».
/// </summary>
public sealed class PublicacionFiltroAvisosTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Administrador", "HondurasSimple.Publicacion.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var categoria = new CategoriaTramite { Nombre = "Pruebas", Orden = 1 };
        db.CategoriasTramite.Add(categoria);
        await db.SaveChangesAsync();

        TramiteSiger Completa(string codigo, string nombre, string estado = "Aprobado") => new()
        {
            Codigo = codigo, Nombre = nombre, EstadoSiger = estado, Institucion = "Instituto de Prueba",
            CategoriaId = categoria.Id, Modalidad = "Presencial", TiempoTexto = "5 dias habiles", CostoEsGratuito = true
        };

        var sinCategoria = Completa("920-003", "Ficha sin categoria");   sinCategoria.CategoriaId = null;
        var sinModalidad = Completa("920-004", "Ficha sin modalidad");   sinModalidad.Modalidad = null;
        var sinTiempo    = Completa("920-005", "Ficha sin tiempo");      sinTiempo.TiempoTexto = null;
        var sinCosto     = Completa("920-006", "Ficha sin costo");       sinCosto.CostoEsGratuito = null;

        db.TramitesSiger.AddRange(
            Completa("920-001", "Ficha aprobada y completa"),
            Completa("920-002", "Ficha completa pero Registrado", "Registrado"),
            sinCategoria, sinModalidad, sinTiempo, sinCosto,
            Completa("920-007", "Ficha completa en estado Completo", "Completo"));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Sin_avisos_deja_solo_las_fichas_listas_y_completas()
    {
        var html = await LeerAsync("/HondurasSimple/Publicacion?tab=todas&avisos=sin");

        html.Should().Contain("Ficha aprobada y completa");
        html.Should().Contain("Ficha completa en estado Completo");
        foreach (var conAviso in new[] { "Ficha completa pero Registrado", "Ficha sin categoria",
                                         "Ficha sin modalidad", "Ficha sin tiempo", "Ficha sin costo" })
            html.Should().NotContain(conAviso, "la columna Avisos le pinta un aviso");
    }

    [Fact]
    public async Task Con_avisos_deja_justo_las_demas()
    {
        var html = await LeerAsync("/HondurasSimple/Publicacion?tab=todas&avisos=con");

        html.Should().NotContain("Ficha aprobada y completa");
        html.Should().NotContain("Ficha completa en estado Completo");
        html.Should().NotContain("sin observaciones", "todas las que quedan tienen un aviso");
        foreach (var conAviso in new[] { "Ficha completa pero Registrado", "Ficha sin categoria",
                                         "Ficha sin modalidad", "Ficha sin tiempo", "Ficha sin costo" })
            html.Should().Contain(conAviso);
    }

    [Fact]
    public async Task El_filtro_se_conserva_al_cambiar_de_pestana()
    {
        var html = await LeerAsync("/HondurasSimple/Publicacion?tab=todas&avisos=sin");

        html.Should().Contain("avisos=sin", "las pestañas tienen que llevar el filtro puesto");
    }

    private async Task<string> LeerAsync(string ruta)
    {
        var respuesta = await _portal.ClienteComo("Administrador").GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
    }
}
