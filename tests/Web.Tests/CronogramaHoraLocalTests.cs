using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El cronograma de la ficha decide qué está vencido con el día de Honduras, no con el de UTC.
/// A las 7:00 p. m. en Honduras ya es el día siguiente en UTC, y una actividad que vence hoy salía
/// como vencida (corregido el 2026-10-22).
/// </summary>
public sealed class CronogramaHoraLocalTests : IAsyncLifetime
{
    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    // 19 de octubre de 2026, 7:00 p. m. en Honduras = 20 de octubre, 01:00 UTC.
    private static readonly DateTimeOffset SieteDeLaNoche = new(2026, 10, 20, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 10, 19);

    private readonly PortalFactory _portal = new();
    private int _proyectoId;

    public async Task InitializeAsync()
    {
        _portal.ServiciosDePrueba = s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(new RelojFijo(SieteDeLaNoche));
        };
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Proyectos.Ver");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var proyecto = Proyecto.Crear("PRY-2026-721", "Proyecto del cronograma");
        proyecto.PrioridadId   = PortalFactory.PrioridadPorDefecto;
        proyecto.InstitucionId = "DIGER";

        var entregable = EntregableProyecto.Crear("Entregable", 1);
        var actividad  = ActividadProyecto.Crear("Vence hoy", 1);
        actividad.Definir("Vence hoy", null, Hoy.AddDays(-10), Hoy, null, null);
        entregable.Agregar(actividad);
        proyecto.Agregar(entregable);

        db.Proyectos.Add(proyecto);
        await db.SaveChangesAsync();
        _proyectoId = proyecto.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_las_siete_de_la_noche_la_actividad_que_vence_hoy_no_sale_vencida()
    {
        var html = await _portal.ClienteComo("JefeArea").GetStringAsync($"/Proyectos/Editor/{_proyectoId}");

        html.Should().Contain("gantt-barra", "la actividad tiene fechas y se dibuja");
        html.Should().NotContain("gantt-barra vencida");
    }
}
