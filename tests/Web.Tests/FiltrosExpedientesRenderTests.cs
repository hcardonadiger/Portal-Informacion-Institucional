using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Que la lista de expedientes se PINTE con los filtros puestos.
///
/// <para>La lógica de los filtros está probada en Application
/// (FiltrosExpedientesTests). Lo que falta cubrir es lo que solo falla al
/// renderizar: un Razor que revienta deja un 500 en la RAÍZ del portal, que es
/// la primera pantalla que ve todo el mundo al entrar.</para>
///
/// <para>Se pide la página con cada filtro por separado y con todos a la vez,
/// incluyendo valores que no existen —el caso que deja la lista vacía— porque
/// ahí es donde el marcado del "sin resultados" se estrena.</para>
/// </summary>
public sealed class FiltrosExpedientesRenderTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Expedientes.Ver");
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private HttpClient Cliente() => _portal.ClienteComo("JefeArea",
        area: PortalFactory.AreaDeCasa, unidad: PortalFactory.UnidadDeCasa);

    [Theory]
    [InlineData("/")]
    [InlineData("/?Institucion=DIGER")]
    [InlineData("/?Encargado=Nadie%20Conocido")]
    [InlineData("/?Estado=2")]
    [InlineData("/?Accion=Simplificar")]
    [InlineData("/?Institucion=DIGER&Encargado=Nadie&Estado=3&Accion=Digitalizar")]
    [InlineData("/?q=siger&Institucion=DIGER&legado=False")]
    public async Task La_lista_se_pinta_con_cualquier_combinacion(string url)
    {
        var r = await Cliente().GetAsync(url);

        r.IsSuccessStatusCode.Should().BeTrue(
            $"«{url}» no puede devolver {(int)r.StatusCode}: es la raíz del portal");

        var html = await r.Content.ReadAsStringAsync();
        html.Should().Contain("filtros-exp", "el formulario de filtros tiene que estar");
    }

    [Fact]
    public async Task El_selector_de_estado_siempre_esta()
    {
        // Institución, Encargado y Acción se ocultan cuando no hay opciones que
        // ofrecer —un desplegable con un solo "— Todas —" miente sobre lo que
        // hay—, pero Estado sale del enum y por eso no depende de los datos.
        var html = await Cliente().GetStringAsync("/");

        html.Should().Contain("name=\"Estado\"");
        html.Should().Contain("— Todos —");
    }

    [Fact]
    public async Task Un_filtro_sin_resultados_no_invita_a_crear_un_expediente()
    {
        // Con filtros puestos, "No hay expedientes registrados" es falso: los
        // hay, pero no estos. Y manda a crear uno que no hacía falta.
        var html = await Cliente().GetStringAsync("/?Encargado=Nadie%20Conocido");

        html.Should().NotContain("No hay expedientes registrados");
        html.Should().Contain("Ningún expediente cumple los filtros puestos");
    }

    [Fact]
    public async Task Los_filtros_viajan_en_los_chips_de_periodo()
    {
        // Cambiar de periodo no puede tirar los filtros puestos.
        var html = await Cliente().GetStringAsync("/?Institucion=DIGER");

        html.Should().Contain("Institucion=DIGER",
            "los enlaces de Todos/Nuevos/Legados tienen que arrastrar el filtro");
    }
}
