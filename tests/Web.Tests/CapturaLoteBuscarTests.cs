using System.Net;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La búsqueda por texto de la captura por lotes. Una institución trae cientos de trámites y
/// encontrar los que se quieren tocar recorriendo la tabla a ojo era el cuello de botella.
///
/// Se prueba sobre el HTML que sale del servidor porque el filtro también decide qué casillas
/// existen: "marcar todos" solo puede alcanzar lo que la búsqueda dejó en la tabla.
/// </summary>
public sealed class CapturaLoteBuscarTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Administrador", "HondurasSimple.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Instituciones.Add(Institucion.Crear("IDP", "Instituto de Prueba"));
        var categoria = new CategoriaTramite { Nombre = "Licencias", Orden = 1 };
        db.CategoriasTramite.Add(categoria);
        await db.SaveChangesAsync();

        db.TramitesSiger.AddRange(
            Ficha(9101, "910-001", "Constancia de inscripción"),
            Ficha(9102, "910-002", "Permiso de operación", categoriaId: categoria.Id),
            Ficha(9103, "910-003", "Renovación de carné", tiempo: "5 días hábiles"));
        await db.SaveChangesAsync();
    }

    private static TramiteSiger Ficha(int idSiger, string codigo, string nombre, int? categoriaId = null, string? tiempo = null) => new()
    {
        IdSiger = idSiger, Codigo = codigo, Institucion = "Instituto de Prueba", Sigla = "IDP",
        Nombre = nombre, EstadoSiger = "Aprobado", InstitucionId = "IDP",
        CategoriaId = categoriaId, TiempoTexto = tiempo
    };

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> LeerAsync(string ruta)
    {
        var respuesta = await _portal.ClienteComo("Administrador").GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, "la ruta {0} debe responder", ruta);
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Sin_busqueda_salen_todos()
    {
        var html = await LeerAsync("/HondurasSimple/CapturaLote");

        html.Should().Contain("910-001").And.Contain("910-002").And.Contain("910-003");
    }

    [Fact]
    public async Task Busca_por_nombre_sin_importar_tildes_ni_mayusculas()
    {
        var html = await LeerAsync("/HondurasSimple/CapturaLote?Buscar=INSCRIPCION");

        html.Should().Contain("910-001");
        html.Should().NotContain("910-002").And.NotContain("910-003");
    }

    [Fact]
    public async Task Busca_por_codigo()
    {
        var html = await LeerAsync("/HondurasSimple/CapturaLote?Buscar=910-003");

        html.Should().Contain("Renovación de carné");
        html.Should().NotContain("910-001").And.NotContain("910-002");
    }

    [Fact]
    public async Task Busca_tambien_en_categoria_y_tiempo()
    {
        var porCategoria = await LeerAsync("/HondurasSimple/CapturaLote?Buscar=licencias");
        porCategoria.Should().Contain("910-002");
        porCategoria.Should().NotContain("910-001");

        var porTiempo = await LeerAsync("/HondurasSimple/CapturaLote?Buscar=habiles");
        porTiempo.Should().Contain("910-003");
        porTiempo.Should().NotContain("910-001");
    }

    [Fact]
    public async Task Todas_las_palabras_deben_aparecer()
    {
        // "permiso inscripcion" no está junto en ninguna ficha: buscar es acotar, no sumar.
        var html = await LeerAsync("/HondurasSimple/CapturaLote?Buscar=permiso%20inscripcion");

        html.Should().NotContain("910-001").And.NotContain("910-002");
    }

    [Fact]
    public async Task La_busqueda_viaja_con_el_formulario_de_aplicar()
    {
        // Si el POST no la lleva, al aplicar el lote la tabla vuelve a mostrar todo y el
        // técnico pierde el hilo de lo que estaba trabajando.
        var html = await LeerAsync("/HondurasSimple/CapturaLote?Buscar=permiso");

        html.Should().Contain("<input type=\"hidden\" name=\"Buscar\" value=\"permiso\" />");
        html.Should().Contain("value=\"permiso\"");
    }
}
