using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Security;
using Diger.TramitesEstado.Web.Mcp;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// S3 (2026-10-22), de punta a punta: la ficha manda el proyecto de la ruta y un riesgo de otro
/// proyecto responde «no encontrado»; la herramienta MCP <c>cambiar_estado_riesgo</c>, que solo
/// recibe el número del riesgo, queda protegida por el comando sin tocar su archivo.
/// </summary>
public sealed class RiesgosAlcancePaginaYMcpTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private int _proyectoA;
    private int _riesgoDeB;
    private int _riesgoDeA;
    private int _riesgoDeOtraInstitucion;

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Proyectos.Ver", "Proyectos.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var a    = Proyecto("PRY-2026-701", "DIGER");
        var b    = Proyecto("PRY-2026-702", "DIGER");
        var otra = Proyecto("PRY-2026-703", "COPECO");
        db.Proyectos.AddRange(a, b, otra);
        await db.SaveChangesAsync();

        var ra = Riesgo(a.Id);
        var rb = Riesgo(b.Id);
        var ro = Riesgo(otra.Id);
        db.ProyectoRiesgos.AddRange(ra, rb, ro);
        await db.SaveChangesAsync();

        _proyectoA = a.Id;
        _riesgoDeA = ra.Id;
        _riesgoDeB = rb.Id;
        _riesgoDeOtraInstitucion = ro.Id;
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private static Proyecto Proyecto(string codigo, string institucion)
    {
        var p = Diger.TramitesEstado.Domain.Entities.Proyecto.Crear(codigo, $"Proyecto {codigo}");
        p.PrioridadId   = PortalFactory.PrioridadPorDefecto;
        p.InstitucionId = institucion;
        return p;
    }

    private static RiesgoProyecto Riesgo(int proyectoId) =>
        RiesgoProyecto.Crear(proyectoId, "Falta designar contraparte", CategoriaRiesgo.Institucional,
            NivelCualitativo.Media, NivelCualitativo.Alta, EstrategiaRiesgo.Mitigar, "siembra");

    private async Task<RiesgoProyecto?> LeerAsync(int riesgoId)
    {
        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ProyectoRiesgos.AsNoTracking().SingleOrDefaultAsync(r => r.Id == riesgoId);
    }

    // ── La ficha ──────────────────────────────────────────────────────────

    private async Task<(HttpClient Cliente, string Token)> SesionAsync()
    {
        var cliente = _portal.ClienteComo("JefeArea");
        var html = await cliente.GetStringAsync($"/Proyectos/Editor/{_proyectoA}");
        var token = Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;
        token.Should().NotBeEmpty();
        return (cliente, token);
    }

    private static FormUrlEncodedContent Form(string token, params (string, string)[] campos) =>
        new([new("__RequestVerificationToken", token), .. campos.Select(c => new KeyValuePair<string, string>(c.Item1, c.Item2))]);

    [Fact]
    public async Task Desde_la_ficha_de_un_proyecto_no_se_toca_el_riesgo_de_otro()
    {
        var (cliente, token) = await SesionAsync();

        var estado = await cliente.PostAsync($"/Proyectos/Editor/{_proyectoA}?handler=EstadoRiesgo",
            Form(token, ("RiesgoId", _riesgoDeB.ToString()), ("RiesgoEstado", nameof(EstadoRiesgo.Cerrado))));
        var editar = await cliente.PostAsync($"/Proyectos/Editor/{_proyectoA}?handler=GuardarRiesgo",
            Form(token, ("RiesgoId", _riesgoDeB.ToString()), ("RiesgoDescripcion", "Pisado")));
        var borrar = await cliente.PostAsync($"/Proyectos/Editor/{_proyectoA}?handler=EliminarRiesgo",
            Form(token, ("RiesgoId", _riesgoDeB.ToString())));

        estado.StatusCode.Should().Be(HttpStatusCode.NotFound);
        editar.StatusCode.Should().Be(HttpStatusCode.NotFound);
        borrar.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var r = await LeerAsync(_riesgoDeB);
        r.Should().NotBeNull("no se eliminó");
        r!.Estado.Should().Be(EstadoRiesgo.Abierto);
        r.Descripcion.Should().NotBe("Pisado");
    }

    [Fact]
    public async Task Desde_la_ficha_el_riesgo_propio_cambia_de_estado_como_antes()
    {
        var (cliente, token) = await SesionAsync();

        var estado = await cliente.PostAsync($"/Proyectos/Editor/{_proyectoA}?handler=EstadoRiesgo",
            Form(token, ("RiesgoId", _riesgoDeA.ToString()), ("RiesgoEstado", nameof(EstadoRiesgo.EnTratamiento))));

        estado.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await LeerAsync(_riesgoDeA))!.Estado.Should().Be(EstadoRiesgo.EnTratamiento);
    }

    // ── La herramienta MCP ────────────────────────────────────────────────

    /// <summary>Ejecuta la herramienta con la misma forma de identidad que emite un token MCP:
    /// los claims del login, que es de donde CurrentUserService y el filtro de alcance leen.</summary>
    private async Task ComoMcpAsync(Func<ProyectosTools, Task> accion)
    {
        using var scope = _portal.Services.CreateScope();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier,   Guid.NewGuid().ToString()),
            new(AppClaims.UserId,            Guid.NewGuid().ToString()),
            new(ClaimTypes.Name,             "Usuario de prueba"),
            new(ClaimTypes.Role,             "JefeArea"),
            new(AppClaims.ActiveRol,         "JefeArea"),
            new(AppClaims.ActiveInstitucion, "DIGER")
        };
        var contexto = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        contexto.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "McpBearer"));
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = contexto;

        await accion(ActivatorUtilities.CreateInstance<ProyectosTools>(scope.ServiceProvider));
    }

    [Fact]
    public async Task La_herramienta_MCP_no_cambia_el_estado_de_un_riesgo_fuera_de_alcance()
    {
        var accion = () => ComoMcpAsync(t => t.CambiarEstadoRiesgo(_riesgoDeOtraInstitucion, EstadoRiesgo.Cerrado));

        await accion.Should().ThrowAsync<NotFoundException>();
        (await LeerAsync(_riesgoDeOtraInstitucion))!.Estado.Should().Be(EstadoRiesgo.Abierto);
    }

    [Fact]
    public async Task La_herramienta_MCP_cambia_el_estado_de_un_riesgo_en_alcance_como_antes()
    {
        await ComoMcpAsync(t => t.CambiarEstadoRiesgo(_riesgoDeB, EstadoRiesgo.Cerrado));

        (await LeerAsync(_riesgoDeB))!.Estado.Should().Be(EstadoRiesgo.Cerrado);
    }
}
