using System.Net;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El pase de un trámite del expediente a SIGER, visto desde la web.
///
/// La lógica del pase se prueba aparte; lo que se protege acá es <b>quién puede dispararlo</b>.
/// Pasar un trámite crea o sobrescribe una ficha del inventario que ve el ciudadano, y poder
/// modelar un expediente no es lo mismo que poder escribir en ese catálogo. Sin esta separación,
/// cualquiera con permiso de expedientes podría reescribir el portal público.
/// </summary>
public sealed class PaseASigerWebTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    private int _expedienteId;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        // El Administrador puede las dos cosas; el Empleado solo expedientes. Esa diferencia es
        // justo lo que se está probando.
        await _portal.OtorgarAsync("Administrador", "Expedientes.Ver", "Expedientes.Editar", "Siger.Ver", "Siger.Editar");
        await _portal.OtorgarAsync("Empleado", "Expedientes.Ver", "Expedientes.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Instituciones.Add(Institucion.Crear("IDP", "Instituto de Prueba"));
        await db.SaveChangesAsync();

        var e = Expediente.Crear("EXP-900", "IDP", null, null, "Instituto de Prueba", "Analista");
        db.Expedientes.Add(e);
        await db.SaveChangesAsync();

        db.Tramites.Add(new ExpedienteTramite
        {
            ExpedienteId = e.Id,
            TramiteIndex = 0,
            ClaveEstable = Guid.NewGuid(),
            NombreTramite = "Trámite a promover",
            FechaCreacion = DateOnly.FromDateTime(DateTime.Today)
        });
        await db.SaveChangesAsync();

        _expedienteId = e.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Quien_puede_editar_SIGER_ve_la_vista_previa()
    {
        var respuesta = await _portal.ClienteComo("Administrador")
            .GetAsync($"/Expedientes/Editor?handler=VistaPreviaPase&id={_expedienteId}&tramiteIndex=0");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await respuesta.Content.ReadAsStringAsync();
        json.Should().Contain("esNueva");
    }

    /// <summary>
    /// La prueba que justifica el permiso extra. Modelar un expediente y escribir en el catálogo
    /// que ve el ciudadano son dos cosas distintas.
    /// </summary>
    [Fact]
    public async Task Quien_solo_puede_editar_expedientes_no_puede_pasar_a_SIGER()
    {
        var cliente = _portal.ClienteComo("Empleado");

        var previa = await cliente.GetAsync(
            $"/Expedientes/Editor?handler=VistaPreviaPase&id={_expedienteId}&tramiteIndex=0");

        previa.StatusCode.Should().BeOneOf(new[] { HttpStatusCode.Forbidden, HttpStatusCode.Redirect },
            "no tiene permiso sobre el inventario SIGER");

        (await FichasAsync()).Should().Be(0, "no se creó ninguna ficha");
    }

    /// <summary>Un trámite que no existe no puede devolver una vista previa inventada.</summary>
    [Fact]
    public async Task Un_tramite_inexistente_da_404()
    {
        var respuesta = await _portal.ClienteComo("Administrador")
            .GetAsync($"/Expedientes/Editor?handler=VistaPreviaPase&id={_expedienteId}&tramiteIndex=99");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── El historial ──────────────────────────────────────────────────────────

    /// <summary>
    /// La pantalla del archivo pasó de enseñar solo la versión 0 a enseñar cualquiera. Sin
    /// versión pedida sigue enseñando la original, que es el enlace que ya existía en el detalle.
    /// </summary>
    [Fact]
    public async Task El_archivo_sin_version_pedida_sigue_enseñando_la_original()
    {
        var fichaId = await ConFotosAsync();

        var html = await LeerAsync($"/Siger/Original/{fichaId}");

        html.Should().Contain("Cómo llegó esta ficha desde SIGER");
    }

    [Fact]
    public async Task El_archivo_puede_enseñar_una_version_posterior()
    {
        var fichaId = await ConFotosAsync();

        var html = await LeerAsync($"/Siger/Original/{fichaId}?version=1");

        html.Should().Contain("antes del pase");
        html.Should().Contain("Versiones archivadas", "con más de una versión aparece la lista");
    }


    private async Task<string> LeerAsync(string ruta)
    {
        var respuesta = await _portal.ClienteComo("Administrador").GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        // Razor codifica los acentos («Cómo» sale como «C&#xF3;mo»); sin decodificar, cualquier
        // aserción sobre texto con tilde falla aunque la página esté bien.
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
    }

    private async Task<int> FichasAsync()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TramitesSiger.CountAsync();
    }

    /// <summary>Una ficha con dos versiones archivadas: la original y la de un pase.</summary>
    private async Task<int> ConFotosAsync()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ficha = new TramiteSiger
        {
            Codigo = "920-500", Nombre = "Con historial", EstadoSiger = "Registrado",
            Institucion = "Instituto de Prueba", Sigla = "IDP", InstitucionId = "IDP"
        };
        db.TramitesSiger.Add(ficha);
        await db.SaveChangesAsync();

        db.FotosTramiteSiger.AddRange(
            Foto(ficha.Id, OrigenFoto.VersionOriginal, OrigenFoto.SigerOriginal),
            Foto(ficha.Id, 1, OrigenFoto.PaseDesdeExpediente));
        await db.SaveChangesAsync();

        return ficha.Id;
    }

    private static FotoTramiteSiger Foto(int fichaId, int version, string origen) => new()
    {
        TramiteSigerId = fichaId,
        Version = version,
        Origen = origen,
        Codigo = "920-500",
        CapturadaEl = DateTime.UtcNow,
        Contenido = "{}"
    };
}
