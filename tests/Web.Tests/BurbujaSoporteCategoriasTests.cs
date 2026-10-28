using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// La burbuja de soporte pedía solo un tema, opcional, en una lista plana. Como el «Asignar a…»
/// ofrece a quienes atienden el tema del chat, el chat tiene que llegar con tema: la burbuja pide
/// primero la categoría y, dentro de ella, el tema.
/// </summary>
public sealed class BurbujaSoporteCategoriasTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();

    public async Task InitializeAsync()
    {
        await _portal.PrepararAsync();

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var desarrollo = CategoriaTicket.Crear("Desarrollo");
        var vieja      = CategoriaTicket.Crear("Categoria retirada");
        vieja.Actualizar("Categoria retirada", activo: false);
        db.CategoriasTicket.AddRange(desarrollo, vieja);
        await db.SaveChangesAsync();

        db.TemasTicket.AddRange(
            TemaTicket.Crear("Portal de Datos", 0, desarrollo.Id),
            TemaTicket.Crear("Tema sin categoria", 0),
            TemaTicket.Crear("Tema de la retirada", 0, vieja.Id));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task La_burbuja_pide_la_categoria_y_luego_el_tema()
    {
        var html = await _portal.ClienteComo("Administrador").GetStringAsync("/Tickets");

        html.Should().Contain("id=\"cwpCategoria\"", "el tema se elige dentro de una categoría");
        Regex.IsMatch(html, """<option value="\d+">Desarrollo</option>""").Should().BeTrue();
        html.Should().NotContain(">Categoria retirada<", "una categoría inactiva no se ofrece");
        html.Should().Contain("Otros temas", "los temas sin categoría activa no quedan fuera");
        html.Should().Contain("id=\"cwpTema\"");
    }

    [Fact]
    public async Task Cada_tema_viaja_con_su_categoria_para_filtrar()
    {
        var html = await _portal.ClienteComo("Administrador").GetStringAsync("/Tickets");

        Regex.IsMatch(html, """\{"id":\d+,"nombre":"Portal de Datos","categoria":[1-9]\d*\}""").Should().BeTrue();
        Regex.IsMatch(html, """\{"id":\d+,"nombre":"Tema sin categoria","categoria":0\}""").Should().BeTrue();
        Regex.IsMatch(html, """\{"id":\d+,"nombre":"Tema de la retirada","categoria":0\}""").Should().BeTrue(
            "su categoría está inactiva: va con «Otros temas»");
    }
}
