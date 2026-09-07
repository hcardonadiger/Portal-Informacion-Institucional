using Diger.TramitesEstado.Web.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// <c>WebExceptionHandler</c> frente a los rechazos de la base de datos.
///
/// <para>Solo distinguía NotFound, Validation y Domain; todo lo demás caía al comodín y, fuera de
/// Development, se mostraba como «Ocurrió un error inesperado». Un choque contra un índice único
/// —el caso que rompió el alta de trámites SIGER— dejaba al usuario sin saber qué campo corregir
/// y con el formulario perdido. Estas pruebas fijan que cada rechazo de la base llegue traducido.</para>
/// </summary>
public sealed class ErroresDeBaseDeDatosTests
{
    /// <summary>Production: es donde el comodín borraba el detalle. En Development el manejador ya
    /// mostraba el tipo y el mensaje, así que probar ahí no verificaría nada.</summary>
    private sealed class EntornoProduccion : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Pruebas";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<string> DestinoAsync(Exception ex)
    {
        var handler = new WebExceptionHandler(
            NullLogger<WebExceptionHandler>.Instance, new EntornoProduccion());

        var ctx = new DefaultHttpContext();
        (await handler.TryHandleAsync(ctx, ex, CancellationToken.None)).Should().BeTrue();

        return Uri.UnescapeDataString(ctx.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Un_choque_de_indice_unico_deja_de_ser_un_error_inesperado()
    {
        var destino = await DestinoAsync(new DbUpdateException("Error al guardar",
            new Exception("UNIQUE constraint failed: TramitesSiger.Codigo")));

        destino.Should().Contain("code=409", "es un dato que el usuario puede corregir, no una falla del portal");
        destino.Should().NotContain("inesperado");
        destino.Should().Contain("no admite repetidos");
    }

    [Fact]
    public async Task Un_campo_obligatorio_en_nulo_se_explica()
    {
        var destino = await DestinoAsync(new DbUpdateException("Error al guardar",
            new Exception("Cannot insert the value NULL into column 'Nombre'")));

        destino.Should().Contain("code=409");
        destino.Should().Contain("obligatorio");
    }

    [Fact]
    public async Task Una_edicion_pisada_por_otro_usuario_se_distingue_del_resto()
    {
        var destino = await DestinoAsync(new DbUpdateConcurrencyException("Concurrencia"));

        destino.Should().Contain("code=409");
        destino.Should().ContainEquivalentOf("otro usuario");
    }

    [Fact]
    public async Task Lo_que_no_es_de_la_base_sigue_cayendo_al_mensaje_generico()
    {
        // La contraparte: traducir los rechazos de la base no puede haber convertido cualquier
        // excepción en un 409 con detalle. Un fallo genuinamente interno sigue sin revelar nada.
        var destino = await DestinoAsync(new InvalidOperationException("referencia nula en el mapeador"));

        destino.Should().Contain("code=500");
        destino.Should().Contain("inesperado");
        destino.Should().NotContain("mapeador");
    }
}
