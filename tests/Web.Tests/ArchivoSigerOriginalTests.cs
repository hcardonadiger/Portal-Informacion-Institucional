using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El archivo del SIGER original guarda una foto por ficha y versión, y esa unicidad no puede
/// depender solo del código que la captura: dos corridas simultáneas de la captura leerían el
/// mismo «todavía no está» y escribirían la versión 0 dos veces. Con dos originales distintos,
/// el archivo deja de poder responder «cómo era esto al principio», que es lo único para lo que
/// existe.
///
/// Se prueba acá y no en Application.Tests porque el proveedor en memoria no aplica índices
/// únicos: la misma prueba pasaría allá sin probar nada.
/// </summary>
public sealed class ArchivoSigerOriginalTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public Task InitializeAsync() => _portal.PrepararAsync();

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Dos_originales_de_la_misma_ficha_estan_prohibidos()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.FotosTramiteSiger.Add(Foto(tramiteSigerId: 55, version: OrigenFoto.VersionOriginal));
        db.FotosTramiteSiger.Add(Foto(tramiteSigerId: 55, version: OrigenFoto.VersionOriginal));

        var guardar = async () => await db.SaveChangesAsync();

        await guardar.Should().ThrowAsync<Exception>(
            "el índice único sobre (TramiteSigerId, Version) es lo que hace irrepetible al original");
    }

    [Fact]
    public async Task Varias_versiones_de_la_misma_ficha_conviven()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.FotosTramiteSiger.Add(Foto(tramiteSigerId: 77, version: 0, origen: OrigenFoto.SigerOriginal));
        db.FotosTramiteSiger.Add(Foto(tramiteSigerId: 77, version: 1, origen: OrigenFoto.PaseDesdeExpediente));
        db.FotosTramiteSiger.Add(Foto(tramiteSigerId: 77, version: 2, origen: OrigenFoto.PaseDesdeExpediente));

        var guardar = async () => await db.SaveChangesAsync();

        await guardar.Should().NotThrowAsync(
            "el historial que viene en la Fase 8 apila versiones sobre la misma ficha");
    }

    /// <summary>
    /// El archivo tiene que sobrevivir a su propio sujeto: si borrar una ficha se llevara su foto
    /// por delante, la única copia de la información original se perdería justo cuando más
    /// importa conservarla. Por eso la tabla no tiene llave foránea a TramitesSiger.
    /// </summary>
    [Fact]
    public async Task Una_foto_sobrevive_a_que_borren_la_ficha()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ficha = new TramiteSiger
        {
            Codigo = "400-777", Nombre = "Ficha que se va a borrar",
            Institucion = "Instituto de Prueba", EstadoSiger = "Registrado"
        };
        db.TramitesSiger.Add(ficha);
        await db.SaveChangesAsync();

        db.FotosTramiteSiger.Add(Foto(ficha.Id, OrigenFoto.VersionOriginal, codigo: "400-777"));
        await db.SaveChangesAsync();

        db.TramitesSiger.Remove(ficha);
        await db.SaveChangesAsync();

        var sobreviviente = db.FotosTramiteSiger.SingleOrDefault(f => f.Codigo == "400-777");
        sobreviviente.Should().NotBeNull("el archivo no debe depender de que la ficha siga viva");
    }

    private static FotoTramiteSiger Foto(
        int tramiteSigerId, int version,
        string origen = OrigenFoto.SigerOriginal, string codigo = "400-001") => new()
    {
        TramiteSigerId = tramiteSigerId,
        Version        = version,
        Origen         = origen,
        Codigo         = codigo,
        CapturadaEl    = DateTime.UtcNow,
        Contenido      = "{}"
    };
}
