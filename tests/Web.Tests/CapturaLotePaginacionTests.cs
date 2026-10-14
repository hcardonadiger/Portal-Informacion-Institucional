using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Captura por lotes, por páginas (14-10-2026). Antes la lista salía entera: con «Todas las
/// instituciones» eran cientos de filas seguidas.
/// </summary>
public sealed class CapturaLotePaginacionTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("Administrador", "HondurasSimple.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var categoria = new CategoriaTramite { Nombre = "Pruebas", Orden = 1 };
        db.CategoriasTramite.Add(categoria);
        await db.SaveChangesAsync();

        // 30 fichas de INPREMA: las 20 primeras incompletas, las 10 últimas completas.
        for (var i = 1; i <= 30; i++)
        {
            var completa = i > 20;
            db.TramitesSiger.Add(new TramiteSiger
            {
                Codigo = $"930-{i:000}", Nombre = $"Tramite lote {i:000}", EstadoSiger = "Registrado",
                Institucion = "INPREMA", Sigla = "INPREMA", InstitucionId = "INPREMA",
                CategoriaId = completa ? categoria.Id : null,
                Modalidad = completa ? "Presencial" : null,
                TiempoTexto = completa ? "5 dias habiles" : null,
                CostoEsGratuito = completa ? true : null
            });
        }
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    private static int Filas(string html) => Regex.Matches(html, "class=\"chk-fila\"").Count;

    [Fact]
    public async Task La_lista_sale_de_25_en_25()
    {
        Filas(await LeerAsync("/HondurasSimple/CapturaLote")).Should().Be(25);
        Filas(await LeerAsync("/HondurasSimple/CapturaLote?pg=2")).Should().Be(5);
    }

    [Fact]
    public async Task Solo_incompletas_se_filtra_antes_de_paginar()
    {
        // Si se filtrara después de traer la página, la primera saldría con 20 y la segunda vacía
        // sin que el contador lo dijera.
        var html = await LeerAsync("/HondurasSimple/CapturaLote?SoloIncompletas=true");

        Filas(html).Should().Be(20);
        html.Should().Contain("de 20", "el contador cuenta solo las incompletas");
        html.Should().NotContain("Tramite lote 021");
    }

    [Fact]
    public async Task Buscar_acota_por_nombre_o_codigo()
    {
        var html = await LeerAsync("/HondurasSimple/CapturaLote?Buscar=930-007");

        Filas(html).Should().Be(1);
        html.Should().Contain("Tramite lote 007");
    }

    private async Task<string> LeerAsync(string ruta)
    {
        var respuesta = await _portal.ClienteComo("Administrador").GetAsync(ruta);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
    }
}
