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
/// El orden de la estructura, posteado de verdad contra la página.
///
/// <para>Existe por un supuesto que ninguna prueba de la capa de aplicación puede comprobar: que
/// <b>el binder de ASP.NET arma la colección en el orden en que se postean los <c>Entregables.Index</c></b>,
/// y no ordenada por el número del índice. Todo el guardado del orden descansa en eso —mover una
/// fila en el DOM no cambia su índice, solo su posición en el envío—. Si algún día el binder
/// empezara a ordenar por el valor del índice, el orden volvería a no guardarse y el portal no
/// diría nada: exactamente el síntoma que esta pantalla tenía cuando el orden viajaba aparte.</para>
///
/// <para>Lo otro que se fija acá es que ya <b>no hay</b> un botón «Guardar orden»: eran dos botones
/// para un solo formulario, y el que la gente apretaba era el otro.</para>
/// </summary>
public sealed class OrdenEstructuraProyectoTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _proyectoId;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Administrador", "Proyectos.Ver", "Proyectos.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var proyecto = Proyecto.Crear("PRY-2026-77", "Proyecto con estructura");
        proyecto.PrioridadId   = PortalFactory.PrioridadPorDefecto;
        proyecto.InstitucionId = "DIGER";

        foreach (var (nombre, orden) in new[] { ("Primero", 1), ("Segundo", 2), ("Tercero", 3) })
            proyecto.Agregar(EntregableProyecto.Crear(nombre, orden));

        db.Proyectos.Add(proyecto);
        await db.SaveChangesAsync();

        _proyectoId = proyecto.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private async Task<int[]> IdsPorOrdenAsync()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ProyectoEntregables.Where(e => e.ProyectoId == _proyectoId)
            .OrderBy(e => e.Orden).Select(e => e.Id).ToArrayAsync();
    }

    [Fact]
    public async Task Guardar_cambios_graba_el_orden_en_que_quedaron_las_filas()
    {
        var cliente = _portal.ClienteComo("Administrador");
        var html    = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoId}");
        var token   = Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;
        token.Should().NotBeEmpty();

        var original = await IdsPorOrdenAsync();

        // El editor postea las filas en el orden en que quedaron en pantalla, conservando el índice
        // con el que se pintaron: mover una fila no la renumera. Eso es justamente lo que se simula
        // acá — índices 2, 0, 1 en ese orden de envío.
        var campos = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("Nombre",      "Proyecto con estructura"),
            new("PrioridadId", PortalFactory.PrioridadPorDefecto.ToString())
        };

        foreach (var (indice, entregableId) in new[] { (2, original[2]), (0, original[0]), (1, original[1]) })
        {
            campos.Add(new("Entregables.Index", indice.ToString()));
            campos.Add(new($"Entregables[{indice}].Id", entregableId.ToString()));
            campos.Add(new($"Entregables[{indice}].Nombre", $"Entregable {indice}"));
            campos.Add(new($"Entregables[{indice}].Estado", nameof(Domain.Enums.EstadoEntregable.Pendiente)));
        }

        var respuesta = await cliente.PostAsync(
            $"/Proyectos/Editor/{_proyectoId}?handler=Guardar", new FormUrlEncodedContent(campos));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Redirect, "el guardado redirige a la ficha");

        // El orden que se graba es el del envío, no el del número de índice.
        (await IdsPorOrdenAsync()).Should().Equal(original[2], original[0], original[1]);
    }

    [Fact]
    public async Task La_ficha_ya_no_ofrece_un_boton_de_guardar_orden()
    {
        var html = await _portal.ClienteComo("Administrador")
            .GetStringAsync($"/Proyectos/Editor/{_proyectoId}");

        html.Should().Contain("id=\"barra-guardar\"");
        html.Should().Contain(">Guardar cambios<");
        html.Should().NotContain("Guardar orden", "el orden lo guarda «Guardar cambios»");
        html.Should().NotContain("handler=ReordenarEntregables");
        html.Should().NotContain("handler=ReordenarActividades");
    }

    [Fact]
    public async Task Las_flechas_de_orden_se_sirven_para_quien_puede_moverlas()
    {
        var html = await _portal.ClienteComo("Administrador")
            .GetStringAsync($"/Proyectos/Editor/{_proyectoId}");

        // Sin las flechas no hay forma de cambiar el orden: el número no se teclea en ningún lado.
        html.Should().Contain("moverEntregable(this, -1)");
        html.Should().Contain("id=\"aviso-orden\"");
    }
}
