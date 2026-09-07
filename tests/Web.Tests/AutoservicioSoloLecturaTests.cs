using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El autoservicio no puede depender de la matriz de permisos ni del alcance de escritura.
///
/// <para>Hallazgo H-02 de la corrida de pruebas: un rol de solo lectura no podía cerrar sesión.
/// <c>ConsultorReadOnlyPageFilter</c> decidía únicamente por el verbo HTTP, así que devolvía
/// <c>Forbid</c> a todo POST — incluido el de <c>/Cuenta/Logout</c>, que ni siquiera guarda
/// nada. El alcance real del fallo era mayor que el reportado: las diez páginas marcadas con
/// <c>[PermisoNoRequerido]</c> quedaban igual de cerradas, de modo que un Consultor tampoco
/// podía cambiar su propia contraseña.</para>
///
/// <para>La contraparte importa tanto como el arreglo: dejar pasar las excepciones declaradas no
/// puede convertirse en dejar pasar todo. Por eso la tercera prueba comprueba que una página de
/// mutación corriente sigue cerrada para el mismo rol, incluso teniendo el permiso otorgado.</para>
/// </summary>
public sealed class AutoservicioSoloLecturaTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _reunionId;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var reunion = Reunion.Crear("Mesa de trabajo de la matriz de permisos");
        reunion.InstitucionId = "DIGER";
        db.Reuniones.Add(reunion);
        await db.SaveChangesAsync();
        _reunionId = reunion.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>El token antifalsificación va atado a la identidad, no a la página: una sola
    /// lectura sirve para todos los POST del mismo cliente.</summary>
    private static async Task<string> TokenAsync(HttpClient cliente, string ruta = "/Cuenta/Contrasena")
    {
        var html = await cliente.GetStringAsync(ruta);
        var token = Regex.Match(html,
            """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;

        token.Should().NotBeEmpty($"la página {ruta} debe abrir para el rol de la prueba");
        return token;
    }

    private static FormUrlEncodedContent Form(string token, params (string, string)[] campos)
    {
        var datos = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token) };
        foreach (var (k, v) in campos) datos.Add(new KeyValuePair<string, string>(k, v));
        return new FormUrlEncodedContent(datos);
    }

    [Fact]
    public async Task Un_rol_de_solo_lectura_puede_cerrar_sesion()
    {
        var cliente = _portal.ClienteComo("Consultor");
        var token = await TokenAsync(cliente);

        var r = await cliente.PostAsync("/Cuenta/Logout", Form(token));

        r.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "cerrar sesión no muta datos y la página está marcada [AllowAnonymous]");

        // Que redirija al login es la prueba de que el handler corrió entero.
        r.StatusCode.Should().Be(HttpStatusCode.Redirect);
        r.Headers.Location!.ToString().Should().Contain("/Cuenta/Login");
    }

    [Fact]
    public async Task Un_rol_de_solo_lectura_puede_cambiar_su_propia_contrasena()
    {
        Guid usuarioId;
        using (var scope = _portal.Services.CreateScope())
        {
            var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var usuario = await db.Usuarios.SingleAsync(u => u.Correo == "consultor@pruebas.gob.hn");
            usuario.CambiarPassword(hasher.Hash("laDeAntes1"));
            await db.SaveChangesAsync();
            usuarioId = usuario.Id;
        }

        var cliente = _portal.ClienteComo("Consultor");
        var token   = await TokenAsync(cliente);

        var r = await cliente.PostAsync("/Cuenta/Contrasena", Form(token,
            ("PasswordActual",    "laDeAntes1"),
            ("PasswordNuevo",     "laDeAhora1"),
            ("PasswordConfirmar", "laDeAhora1")));

        r.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);

        // La página traga las excepciones y responde 200 con el mensaje en TempData, así que el
        // código de estado no basta: hay que mirar el dato.
        using var comprobacion = _portal.Services.CreateScope();
        var db2     = comprobacion.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher2 = comprobacion.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var guardado = await db2.Usuarios.AsNoTracking().SingleAsync(u => u.Id == usuarioId);
        hasher2.Verify("laDeAhora1", guardado.PasswordHash).Should().BeTrue(
            "la red de última línea de AppDbContext no debe impedir el autoservicio de la propia cuenta");
    }

    [Fact]
    public async Task Una_pagina_de_mutacion_corriente_le_sigue_estando_cerrada()
    {
        // Con el permiso otorgado a propósito: lo que cierra la puerta acá es la condición de
        // solo lectura del rol, no la falta de permiso. Si el arreglo de la Fase 1 se pasara de
        // ancho, esta prueba es la que lo delata.
        await _portal.OtorgarAsync("Consultor", "Reuniones.Ver", "Reuniones.Editar");

        var cliente = _portal.ClienteComo("Consultor");
        var token   = await TokenAsync(cliente);

        var r = await cliente.PostAsync(
            $"/Reuniones/Acta/{_reunionId}?handler=Enlazar",
            Form(token, ("otraReunionId", "0")));

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
