using System.Net;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La ficha tiene que enseñar los cuatro datos que escribe el llenado asistido.
///
/// <para>Hallazgo H-04 de la corrida de pruebas: se aprobó un tiempo en Llenado asistido y la
/// ficha seguía enseñando «No registrada». No era que la aprobación no guardara —guardaba— sino
/// que el detalle no pintaba el campo. Enseñaba <c>Temporalidad</c>, que es otro dato que viene
/// de SIGER, y quien probaba leyó ese como si fuera el suyo.</para>
///
/// <para>Al comparar campo por campo apareció que el hueco no era solo del tiempo: <b>ninguno</b>
/// de los cuatro campos que aprueba el llenado —categoría, modalidad, tiempo y costo— estaba en
/// la ficha. La página solo sabía decir cuáles <i>faltaban</i>; una vez llenos desaparecían de la
/// vista. Por eso las dos pruebas miran el HTML servido y no el modelo de página: el modelo ya
/// traía los cuatro valores desde antes del fallo.</para>
/// </summary>
public sealed class FichaMuestraLoQueEscribeElLlenadoTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    private int _idLlena;
    private int _idVacia;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Administrador", "Siger.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Instituciones.Add(Institucion.Crear("IDP", "Instituto de Prueba"));
        var categoria = new CategoriaTramite { Nombre = "Certificaciones", Orden = 1 };
        db.CategoriasTramite.Add(categoria);
        await db.SaveChangesAsync();

        // El caso exacto del informe: Temporalidad dice una cosa y el tiempo aprobado dice otra.
        // Si la ficha solo pintara uno de los dos, la prueba lo delata.
        var llena = new TramiteSiger
        {
            IdSiger = 9101, Codigo = "910-001", Institucion = "Instituto de Prueba", Sigla = "IDP",
            Nombre = "Constancia con el llenado ya aprobado",
            EstadoSiger = "Aprobado", InstitucionId = "IDP",
            Temporalidad = "No registrada",
            TiempoTexto = "46 dias habiles",
            Modalidad = "Presencial",
            CategoriaId = categoria.Id,
            CostoEsGratuito = true
        };

        var vacia = new TramiteSiger
        {
            IdSiger = 9102, Codigo = "910-002", Institucion = "Instituto de Prueba", Sigla = "IDP",
            Nombre = "Constancia sin llenar",
            EstadoSiger = "Aprobado", InstitucionId = "IDP"
        };

        db.TramitesSiger.AddRange(llena, vacia);
        await db.SaveChangesAsync();

        _idLlena = llena.Id;
        _idVacia = vacia.Id;
    }

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
    public async Task El_tiempo_aprobado_se_ve_en_la_ficha_y_no_se_confunde_con_la_temporalidad()
    {
        var html = await LeerAsync($"/Siger/Detalle/{_idLlena}");

        html.Should().Contain("46 dias habiles",
            "es el valor que escribe el llenado asistido y el informe decía que no aparecía");

        // Los dos datos conviven, cada uno con su etiqueta: son cosas distintas y el informe
        // demuestra que se confunden cuando solo se ve una.
        html.Should().Contain("Temporalidad");
        html.Should().Contain("No registrada");
        html.Should().Contain("Tiempo de respuesta");
    }

    [Fact]
    public async Task Los_otros_tres_campos_del_llenado_tambien_se_ven()
    {
        var html = await LeerAsync($"/Siger/Detalle/{_idLlena}");

        html.Should().Contain("Certificaciones", "la categoría se enseña por nombre, no por id");
        html.Should().Contain("Presencial");
        html.Should().Contain("Gratuito");
    }

    [Fact]
    public async Task Una_ficha_sin_llenar_dice_que_no_hay_dato_en_vez_de_callarse()
    {
        var html = await LeerAsync($"/Siger/Detalle/{_idVacia}");

        // La alerta de ficha incompleta sigue siendo la que empuja a completarla; las filas
        // nuevas no la sustituyen, la acompañan.
        html.Should().Contain("Esta ficha está incompleta");
        html.Should().Contain("Tiempo de respuesta");
    }
}
