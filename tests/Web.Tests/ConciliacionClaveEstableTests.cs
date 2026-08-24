using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Las reglas de base que sostienen la conciliación después de la Fase 3.
///
/// Se prueban acá y no en Application.Tests porque el proveedor en memoria no aplica índices
/// únicos ni borrado en cascada: las mismas pruebas pasarían allá sin probar nada.
/// </summary>
public sealed class ConciliacionClaveEstableTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public Task InitializeAsync() => _portal.PrepararAsync();

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Una sola decisión vigente por trámite: si se pudieran acumular, la bandeja no
    /// sabría cuál respetar y volvería a preguntar por algo ya revisado.</summary>
    [Fact]
    public async Task Dos_decisiones_para_el_mismo_tramite_estan_prohibidas()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expedienteId = await CrearExpedienteAsync(db, "EXP-101");

        var clave = Guid.NewGuid();
        db.ConciliacionesSiger.Add(Decision(clave, expedienteId, DecisionConciliacion.Descartado));
        db.ConciliacionesSiger.Add(Decision(clave, expedienteId, DecisionConciliacion.ProponerFichaNueva));

        var guardar = async () => await db.SaveChangesAsync();

        await guardar.Should().ThrowAsync<Exception>(
            "el índice único sobre ClaveTramite es lo que mantiene una sola decisión vigente");
    }

    [Fact]
    public async Task Decisiones_de_tramites_distintos_conviven()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expedienteId = await CrearExpedienteAsync(db, "EXP-102");

        db.ConciliacionesSiger.Add(Decision(Guid.NewGuid(), expedienteId, DecisionConciliacion.Descartado));
        db.ConciliacionesSiger.Add(Decision(Guid.NewGuid(), expedienteId, DecisionConciliacion.Descartado));

        var guardar = async () => await db.SaveChangesAsync();

        await guardar.Should().NotThrowAsync();
    }

    /// <summary>
    /// La cascada cuelga del expediente, que es estable, y no del trámite, que se borra y se
    /// reinserta en cada guardado. Ese cambio de anclaje es el arreglo entero de la Fase 3.
    /// </summary>
    [Fact]
    public async Task Borrar_el_expediente_se_lleva_sus_decisiones()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expedienteId = await CrearExpedienteAsync(db, "EXP-103");

        db.ConciliacionesSiger.Add(Decision(Guid.NewGuid(), expedienteId, DecisionConciliacion.Descartado));
        await db.SaveChangesAsync();

        var expediente = await db.Expedientes.FirstAsync(e => e.Id == expedienteId);
        db.Expedientes.Remove(expediente);
        await db.SaveChangesAsync();

        var quedan = await db.ConciliacionesSiger.CountAsync(c => c.ExpedienteId == expedienteId);
        quedan.Should().Be(0);
    }

    /// <summary>Dos trámites no pueden compartir identidad, ni por error de programación.</summary>
    [Fact]
    public async Task Dos_tramites_con_la_misma_clave_estable_estan_prohibidos()
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expedienteId = await CrearExpedienteAsync(db, "EXP-104");

        var clave = Guid.NewGuid();
        db.Tramites.Add(new ExpedienteTramite
        {
            ExpedienteId = expedienteId, TramiteIndex = 0,
            NombreTramite = "Uno", ClaveEstable = clave
        });
        db.Tramites.Add(new ExpedienteTramite
        {
            ExpedienteId = expedienteId, TramiteIndex = 1,
            NombreTramite = "Otro", ClaveEstable = clave
        });

        var guardar = async () => await db.SaveChangesAsync();

        await guardar.Should().ThrowAsync<Exception>();
    }
    /// <summary>El expediente tiene llave foránea a la institución, así que hay que crearla antes.</summary>
    private static async Task<int> CrearExpedienteAsync(AppDbContext db, string codigo)
    {
        const string institucionId = "PRUEBA";
        if (!await db.Instituciones.AnyAsync(i => i.Id == institucionId))
        {
            db.Instituciones.Add(Institucion.Crear(institucionId, "Instituto de Prueba"));
            await db.SaveChangesAsync();
        }

        var e = Expediente.Crear(codigo, institucionId, null, null, "Instituto de Prueba", "Analista");
        db.Expedientes.Add(e);
        await db.SaveChangesAsync();
        return e.Id;
    }
    private static ConciliacionSiger Decision(Guid clave, int expedienteId, DecisionConciliacion d) => new()
    {
        ClaveTramite = clave,
        ExpedienteId = expedienteId,
        Decision     = d
    };
}
