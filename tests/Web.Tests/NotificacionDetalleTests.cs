using System.Net;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

public sealed class NotificacionDetalleTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _id;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var empleado = await db.AsignacionesUsuario.Where(a => a.Rol == "Empleado").Select(a => a.UsuarioId).FirstAsync();
        var n = Notificacion.Crear(empleado, TipoNotificacion.CompromisoVencido, "Compromiso vencido: Acta firmada", "/c/1");
        n.Sumar("/c/2", "Compromiso vencido: Informe mensual", "/c/2", x => $"Tiene {x} compromisos vencidos", DateTime.UtcNow);
        db.Notificaciones.Add(n);
        await db.SaveChangesAsync();
        _id = n.Id;
    }

    public Task DisposeAsync() { _portal.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task El_dueno_ve_los_elementos_del_grupo()
    {
        var html = await _portal.ClienteComo("Empleado").GetStringAsync($"/Notificaciones/{_id}");

        html.Should().Contain("Tiene 2 compromisos vencidos");
        html.Should().Contain("Acta firmada").And.Contain("Informe mensual");
        html.Should().Contain("href=\"/c/2\"");
    }

    [Fact]
    public async Task Otra_persona_recibe_404()
    {
        (await _portal.ClienteComo("Consultor").GetAsync($"/Notificaciones/{_id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
