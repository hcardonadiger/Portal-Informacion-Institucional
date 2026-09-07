using System.Net;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El aviso de la matriz de permisos tiene que distinguir «guardé tu cambio» de «no había nada
/// que guardar».
///
/// <para>Hallazgo H-06: se pulsó Guardar sin marcar una sola casilla y el portal contestó
/// «Permisos actualizados». Un aviso que sale siempre deja de ser información — quien lo lee ya
/// no sabe si su clic llegó a la casilla, que era justamente la duda de quien probaba.</para>
/// </summary>
public sealed class ElAvisoDePermisosDiceLaVerdadTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        // El catalogo de Permisos no se siembra en el host de pruebas, y el comando descarta
        // toda clave que no reconozca: sin esta fila, guardar "Siger.Ver" contaria como quitarla.
        using (var scope = _portal.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Permisos.Add(Permiso.Crear("Siger.Ver", "Ver el inventario SIGER", "Siger", AccionModulo.Ver));
            await db.SaveChangesAsync();
        }

        await _portal.OtorgarAsync("Empleado", "Siger.Ver");
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Guarda el rol Empleado con las claves indicadas y devuelve el HTML de la página a
    /// la que se llega, que es donde sale el aviso.</summary>
    private async Task<string> GuardarAsync(params string[] otorgadas)
    {
        var cliente = _portal.ClienteComo("Administrador");

        var pagina = await cliente.GetAsync("/Accesos/Permisos?rol=Empleado");
        pagina.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await pagina.Content.ReadAsStringAsync();

        var campos = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken",
                Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value),
            new("rol", "Empleado"),
            // "presentes" declara lo que estaba en pantalla: sin él el comando creería que se
            // filtró la vista y conservaría claves que la prueba quiere quitar.
            new("presentes", "Siger.Ver"),
        };

        foreach (var clave in otorgadas)
        {
            campos.Add(new("otorgadas", clave));
            if (!campos.Any(c => c.Key == "presentes" && c.Value == clave))
                campos.Add(new("presentes", clave));
        }

        var respuesta = await cliente.PostAsync("/Accesos/Permisos", new FormUrlEncodedContent(campos));
        respuesta.StatusCode.Should().BeOneOf(new[] { HttpStatusCode.Redirect, HttpStatusCode.Found });

        var destino = await cliente.GetAsync(respuesta.Headers.Location!.ToString());
        return WebUtility.HtmlDecode(await destino.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Guardar_sin_cambiar_nada_lo_dice()
    {
        var html = await GuardarAsync("Siger.Ver");

        html.Should().Contain("No habia cambios que guardar.");
        html.Should().NotContain("Permisos actualizados:");
    }

    [Fact]
    public async Task Guardar_quitando_una_clave_dice_cuantas()
    {
        var html = await GuardarAsync();

        html.Should().Contain("Permisos actualizados: 1 revocado.");
    }
}
