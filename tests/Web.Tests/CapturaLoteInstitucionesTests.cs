using System.Net;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// El selector de institución de Captura por lotes, visto por alguien que no es administrador.
///
/// Leía la tabla de instituciones, que pasa por el filtro de alcance: fuera del rol
/// Administrador solo dejaba ver la institución propia. Un técnico de DIGER encontraba «DIGER»
/// y nada más, y no podía acotar el lote a INPREMA aunque la lista de fichas de abajo sí le
/// mostrara las de INPREMA. Esconder los nombres no protegía nada; solo rompía el filtro.
/// </summary>
public sealed class CapturaLoteInstitucionesTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Empleado", "Siger.Ver", "HondurasSimple.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // DIGER, INPREMA e IHTT ya vienen en la semilla del modelo; DIGER es además la
        // institución del usuario de la prueba. Falta una que no tenga ninguna ficha.
        db.Instituciones.Add(Institucion.Crear("SINFICHAS", "Institucion sin fichas"));
        await db.SaveChangesAsync();

        db.TramitesSiger.AddRange(
            Ficha("603-001", "Afiliacion de docentes", "INPREMA"),
            Ficha("603-002", "Constancia de cotizaciones", "INPREMA"),
            Ficha("530-001", "Permiso de transporte", "IHTT"),
            Ficha("999-001", "Ficha sin institucion vinculada", null));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Quien_no_es_administrador_ve_las_instituciones_de_las_fichas()
    {
        var html = await LeerAsync("/HondurasSimple/CapturaLote");

        html.Should().Contain("<option value=\"INPREMA\"",
            "la lista de abajo le enseña las fichas de INPREMA; el selector no puede negarle ese filtro");
        html.Should().Contain("<option value=\"IHTT\"");
    }

    [Fact]
    public async Task El_selector_no_ofrece_instituciones_sin_fichas()
    {
        var html = await LeerAsync("/HondurasSimple/CapturaLote");

        html.Should().NotContain("<option value=\"SINFICHAS\"",
            "elegirla dejaría la lista vacía: no hay nada que capturar ahí");
        html.Should().NotContain("<option value=\"DIGER\"",
            "que sea la institución del usuario no la vuelve una opción si no tiene fichas");
    }

    [Fact]
    public async Task Elegir_una_institucion_acota_el_lote_a_sus_fichas()
    {
        var html = await LeerAsync("/HondurasSimple/CapturaLote?InstitucionId=INPREMA");

        html.Should().Contain("Afiliacion de docentes");
        html.Should().Contain("Constancia de cotizaciones");
        html.Should().NotContain("Permiso de transporte");
        html.Should().NotContain("Ficha sin institucion vinculada");
    }

    private async Task<string> LeerAsync(string ruta)
    {
        var respuesta = await _portal.ClienteComo("Empleado").GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return await respuesta.Content.ReadAsStringAsync();
    }

    private static TramiteSiger Ficha(string codigo, string nombre, string? institucionId) => new()
    {
        Codigo = codigo, Nombre = nombre, EstadoSiger = "Registrado",
        Institucion = institucionId ?? "Sin vincular", Sigla = institucionId, InstitucionId = institucionId
    };
}
