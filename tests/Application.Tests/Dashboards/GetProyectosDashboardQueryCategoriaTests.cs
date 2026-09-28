using Diger.TramitesEstado.Application.Dashboards.Queries;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Dashboards;

/// <summary>
/// El recorte del tablero del portafolio por categoría temática, y el desglose que lo acompaña.
///
/// <para>Mismo criterio que el filtro de áreas que vive al lado: es un filtro de <b>presentación</b>
/// —recorta lo que el alcance ya dejó pasar, nunca lo amplía— y <c>null</c> significa «sin acotar»,
/// no «sin categoría». Si se confundieran, el tablero saldría vacío para quien no ha clasificado
/// nada, que hoy es casi todo el portafolio.</para>
/// </summary>
public class GetProyectosDashboardQueryCategoriaTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public GetProyectosDashboardQueryCategoriaTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
    }

    [Fact]
    public async Task FiltraPorCategoria()
    {
        var cat = await SembrarAsync();

        var resultado = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(CategoriaId: cat.Q3), CancellationToken.None);

        resultado.Semaforo.Select(s => s.Codigo).Should().BeEquivalentTo("PRY-2026-28", "PRY-2026-30");
    }

    [Fact]
    public async Task SinCategoriaNoFiltra()
    {
        await SembrarAsync();

        var resultado = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(), CancellationToken.None);

        // null = «sin acotar». El proyecto sin clasificar sigue estando.
        resultado.Semaforo.Select(s => s.Codigo)
            .Should().BeEquivalentTo("PRY-2026-28", "PRY-2026-30", "PRY-2026-32", "PRY-2026-34");
    }

    [Fact]
    public async Task CategoriaDesconocidaNoDevuelveNada()
    {
        await SembrarAsync();

        var resultado = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(CategoriaId: 9999), CancellationToken.None);

        resultado.Semaforo.Should().BeEmpty();
    }

    [Fact]
    public async Task ElFiltroDeCategoriaSeCombinaConLosDemas()
    {
        var cat = await SembrarAsync();

        // PRY-2026-30 es el único Q3 que además está planificado.
        var resultado = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(Estado: EstadoProyecto.Planificado, CategoriaId: cat.Q3),
            CancellationToken.None);

        resultado.Semaforo.Select(s => s.Codigo).Should().BeEquivalentTo("PRY-2026-30");
    }

    [Fact]
    public async Task PorCategoriaCuentaEnElOrdenDelCatalogo()
    {
        await SembrarAsync();

        var resultado = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(), CancellationToken.None);

        // El orden es el del catálogo (Q3 va primero, como la ronda final de la F1), no el
        // alfabético ni el de mayor conteo: el desglose tiene que leerse igual que el desplegable.
        resultado.PorCategoria.Select(c => c.Etiqueta)
            .Should().ContainInOrder("Q3", "Q2", "Sin categoría");
        resultado.PorCategoria.Should().Contain(c => c.Etiqueta == "Q3" && c.Cantidad == 2);
        resultado.PorCategoria.Should().Contain(c => c.Etiqueta == "Q2" && c.Cantidad == 1);
    }

    [Fact]
    public async Task SinCategoriaEsUnBaldeAlFinalYNoUnaCategoriaMas()
    {
        await SembrarAsync();

        var resultado = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(), CancellationToken.None);

        // Sin este balde el desglose sumaría 3 sobre un portafolio de 4 y se leería como si
        // faltaran proyectos. Va al final porque no es una clasificación: es su ausencia.
        resultado.PorCategoria.Last().Etiqueta.Should().Be("Sin categoría");
        resultado.PorCategoria.Last().Cantidad.Should().Be(1);
        resultado.PorCategoria.Sum(c => c.Cantidad).Should().Be(resultado.Semaforo.Count);
    }

    [Fact]
    public async Task UnaCategoriaSinProyectosNoAparece()
    {
        await SembrarAsync();

        var resultado = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(), CancellationToken.None);

        // Q1 existe en el catálogo pero nadie la usa. El desglose cuenta lo que hay, no lo que
        // podría haber: una barra en cero por cada categoría del catálogo es ruido.
        resultado.PorCategoria.Should().NotContain(c => c.Etiqueta == "Q1");
    }

    [Fact]
    public async Task ElBaldeDesapareceCuandoTodosEstanClasificados()
    {
        var cat = await SembrarAsync();

        var sinClasificar = await _ctx.Proyectos.SingleAsync(p => p.Codigo == "PRY-2026-34");
        sinClasificar.CategoriaId = cat.Q1;
        await _ctx.SaveChangesAsync();

        var resultado = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(), CancellationToken.None);

        resultado.PorCategoria.Should().NotContain(c => c.Etiqueta == "Sin categoría");
    }

    [Fact]
    public async Task LaCategoriaViajaEnElSemaforoConSuColor()
    {
        await SembrarAsync();

        var resultado = await new GetProyectosDashboardQueryHandler(_ctx).Handle(
            new GetProyectosDashboardQuery(), CancellationToken.None);

        var clasificado = resultado.Semaforo.Single(s => s.Codigo == "PRY-2026-28");
        clasificado.Categoria.Should().Be("Q3");
        clasificado.CategoriaColor.Should().Be(ColorEtiqueta.Verde);

        // Sin clasificar va en null por los dos lados: es lo que le permite a la vista no pintar
        // nada en vez de una insignia «Sin clasificar» en cada fila vieja.
        var sinClasificar = resultado.Semaforo.Single(s => s.Codigo == "PRY-2026-34");
        sinClasificar.Categoria.Should().BeNull();
        sinClasificar.CategoriaColor.Should().BeNull();
    }

    /// <summary>
    /// El catálogo real de categorías —las tres rondas de clasificación de la F1— y cuatro
    /// proyectos: dos en Q3, uno en Q2 y uno sin clasificar. Q1 queda sembrada y sin usar, a
    /// propósito.
    /// </summary>
    private async Task<CatalogoCategorias> SembrarAsync()
    {
        var q3 = CategoriaProyecto.Crear("Q3", 1, ColorEtiqueta.Verde);
        var q2 = CategoriaProyecto.Crear("Q2", 2, ColorEtiqueta.Azul);
        var q1 = CategoriaProyecto.Crear("Q1", 3, ColorEtiqueta.Gris);
        _ctx.CategoriasProyecto.AddRange(q3, q2, q1);
        await _ctx.SaveChangesAsync();

        var prioridad = PrioridadesDePrueba.Media(_ctx);

        var unoQ3 = Proyecto.Crear("PRY-2026-28", "DEMO — Conectividad en ejecución");
        unoQ3.PrioridadId = prioridad; unoQ3.CategoriaId = q3.Id;
        unoQ3.CambiarEstado(EstadoProyecto.EnEjecucion, "pruebas");

        var otroQ3 = Proyecto.Crear("PRY-2026-30", "DEMO — Inteligencia artificial planificada");
        otroQ3.PrioridadId = prioridad; otroQ3.CategoriaId = q3.Id;

        var unoQ2 = Proyecto.Crear("PRY-2026-32", "DEMO — Gestión por resultados");
        unoQ2.PrioridadId = prioridad; unoQ2.CategoriaId = q2.Id;

        var sinCategoria = Proyecto.Crear("PRY-2026-34", "Mesa técnica de simplificación");
        sinCategoria.PrioridadId = prioridad;

        _ctx.Proyectos.AddRange(unoQ3, otroQ3, unoQ2, sinCategoria);
        await _ctx.SaveChangesAsync();

        return new CatalogoCategorias(q3.Id, q2.Id, q1.Id);
    }

    private sealed record CatalogoCategorias(int Q3, int Q2, int Q1);

    public void Dispose() => _ctx.Dispose();
}
