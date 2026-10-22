using Diger.TramitesEstado.Application.Siger.Promocion;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Una ficha nacida en el portal —promovida desde un expediente— no tiene identificador de
/// SIGER, y el vacío es justamente la marca de que no existe allá.
///
/// El índice único sobre IdSiger tiene que estar filtrado por IS NOT NULL: SQL Server solo
/// admite UN nulo en un índice único sin filtro, así que sin el filtro la segunda ficha
/// promovida fallaría al guardar. Se prueba acá y no en Application.Tests porque el proveedor
/// en memoria no aplica índices: la misma prueba pasaría allá sin probar nada.
/// </summary>
public sealed class FichaSinIdSigerTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public Task InitializeAsync() => _portal.PrepararAsync();

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Dos_fichas_sin_IdSiger_conviven()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.TramitesSiger.Add(Ficha(null, "400-P01", "Primera promovida"));
        db.TramitesSiger.Add(Ficha(null, "400-P02", "Segunda promovida"));

        var guardar = async () => await db.SaveChangesAsync();

        await guardar.Should().NotThrowAsync(
            "el índice único de IdSiger debe estar filtrado por IS NOT NULL");
    }

    [Fact]
    public async Task Dos_fichas_con_el_mismo_IdSiger_siguen_prohibidas()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.TramitesSiger.Add(Ficha(7001, "24-900", "Importada A"));
        db.TramitesSiger.Add(Ficha(7001, "24-901", "Importada B"));

        var guardar = async () => await db.SaveChangesAsync();

        await guardar.Should().ThrowAsync<Exception>(
            "el filtro solo debe relajar los nulos, no permitir IdSiger repetidos");
    }

    /// <summary>Una ficha promovida no se publica sola: nace Registrado.</summary>
    [Fact]
    public async Task Una_ficha_sin_IdSiger_se_guarda_sin_publicar()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ficha = Ficha(null, "400-P09", "Promovida sin publicar");
        db.TramitesSiger.Add(ficha);
        await db.SaveChangesAsync();

        var guardada = await db.TramitesSiger.FindAsync(ficha.Id);
        guardada!.IdSiger.Should().BeNull();
        guardada.Publicado.Should().BeFalse();
    }

    private static TramiteSiger Ficha(int? idSiger, string codigo, string nombre) => new()
    {
        IdSiger = idSiger,
        Codigo = codigo,
        Nombre = nombre,
        Institucion = "Instituto de Prueba",
        EstadoSiger = ReglaPublicacion.Registrado
    };
}
