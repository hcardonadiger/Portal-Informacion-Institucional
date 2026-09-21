using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Categorias;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Application.Proyectos.Services;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

/// <summary>
/// El catálogo de categorías. Se parece al de prioridades, pero la diferencia que importa es que
/// la categoría del proyecto es <b>opcional</b>: no hay predeterminada, no hace falta que quede
/// alguna activa, y «sin clasificar» es una respuesta y no un hueco a llenar.
/// </summary>
public class CategoriasCatalogoTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly CatalogoPrioridades _prio;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private readonly IInteresadosAutomaticosSync _sync = Substitute.For<IInteresadosAutomaticosSync>();

    public CategoriasCatalogoTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
        _prio = PrioridadesDePrueba.Sembrar(_ctx);
        _sync.CalcularDerechoVigenteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, RolInteresado>());
    }

    private async Task<int> CrearCategoriaAsync(string nombre, int orden = 1) =>
        await new CrearCategoriaProyectoCommandHandler(_ctx)
            .Handle(new CrearCategoriaProyectoCommand(nombre, orden, ColorEtiqueta.Azul),
                    CancellationToken.None);

    // ── Alta ──────────────────────────────────────────────────────
    [Fact]
    public async Task Crear_agrega_la_categoria()
    {
        var id = await CrearCategoriaAsync("Digitalización de trámites");

        var creada = await _ctx.CategoriasProyecto.FindAsync(id);
        creada!.Nombre.Should().Be("Digitalización de trámites");
        creada.Activo.Should().BeTrue();
    }

    [Fact]
    public async Task Crear_rechaza_un_nombre_repetido()
    {
        await CrearCategoriaAsync("Interoperabilidad");

        var acto = () => CrearCategoriaAsync("Interoperabilidad");

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*Interoperabilidad*");
    }

    // ── Lo que la distingue del catálogo de prioridades ───────────
    [Fact]
    public async Task Un_proyecto_puede_nacer_sin_categoria()
    {
        var id = await new CrearProyectoCommandHandler(_ctx, _usuario, _sync)
            .Handle(new CrearProyectoCommand("Sin clasificar"), CancellationToken.None);

        (await _ctx.Proyectos.FindAsync(id))!.CategoriaId.Should().BeNull();
    }

    [Fact]
    public async Task Se_puede_desactivar_la_ultima_categoria_activa()
    {
        // En prioridades esto se rechaza —un proyecto no puede quedarse sin prioridad—. Acá no:
        // un catálogo entero desactivado solo significa que por ahora nadie clasifica.
        var id = await CrearCategoriaAsync("Única");

        await new ActualizarCategoriaProyectoCommandHandler(_ctx).Handle(
            new ActualizarCategoriaProyectoCommand(id, "Única", 1, ColorEtiqueta.Azul, Activo: false),
            CancellationToken.None);

        (await _ctx.CategoriasProyecto.CountAsync(c => c.Activo)).Should().Be(0);
    }

    // ── Baja ──────────────────────────────────────────────────────
    [Fact]
    public async Task No_se_puede_eliminar_una_categoria_en_uso()
    {
        var idCat = await CrearCategoriaAsync("En uso");
        var p = Proyecto.Crear("PRY-2026-01", "Clasificado");
        p.PrioridadId = _prio.Media;
        p.CategoriaId = idCat;
        _ctx.Proyectos.Add(p);
        await _ctx.SaveChangesAsync();

        var acto = () => new EliminarCategoriaProyectoCommandHandler(_ctx)
            .Handle(new EliminarCategoriaProyectoCommand(idCat), CancellationToken.None);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*1 proyecto*");
    }

    [Fact]
    public async Task Se_elimina_la_que_nadie_usa()
    {
        var id = await CrearCategoriaAsync("Sobrante");

        await new EliminarCategoriaProyectoCommandHandler(_ctx)
            .Handle(new EliminarCategoriaProyectoCommand(id), CancellationToken.None);

        (await _ctx.CategoriasProyecto.FindAsync(id)).Should().BeNull();
    }

    // ── Resolución al guardar ─────────────────────────────────────
    [Fact]
    public async Task Sin_categoria_elegida_queda_en_nulo_y_no_se_inventa_una()
    {
        await CrearCategoriaAsync("Existe");

        var id = await CategoriaProyectoResolver.ResolverAsync(_ctx, null, CancellationToken.None);

        id.Should().BeNull("a diferencia de la prioridad, acá no hay predeterminada que rellene");
    }

    [Fact]
    public async Task Una_categoria_inexistente_se_rechaza()
    {
        var acto = () => CategoriaProyectoResolver.ResolverAsync(_ctx, 9999, CancellationToken.None);
        await acto.Should().ThrowAsync<DomainException>().WithMessage("*no existe*");
    }

    // ── Filtro y listado ──────────────────────────────────────────
    [Fact]
    public async Task El_listado_filtra_por_categoria()
    {
        var idCat = await CrearCategoriaAsync("Interoperabilidad");
        await ProyectoAsync("PRY-2026-01", idCat);
        await ProyectoAsync("PRY-2026-02", null);

        var conCategoria = await new GetProyectosQueryHandler(_ctx)
            .Handle(new GetProyectosQuery(CategoriaId: idCat), CancellationToken.None);

        conCategoria.Select(p => p.Codigo).Should().ContainSingle().Which.Should().Be("PRY-2026-01");
    }

    [Fact]
    public async Task El_listado_trae_el_nombre_y_el_color_para_la_insignia()
    {
        var idCat = await CrearCategoriaAsync("Interoperabilidad");
        await ProyectoAsync("PRY-2026-01", idCat);

        var fila = (await new GetProyectosQueryHandler(_ctx)
            .Handle(new GetProyectosQuery(), CancellationToken.None)).Single();

        fila.Categoria.Should().Be("Interoperabilidad");
        fila.CategoriaColor.Should().Be(ColorEtiqueta.Azul);
    }

    [Fact]
    public async Task Un_proyecto_sin_categoria_no_desaparece_del_listado()
    {
        // La unión contra el catálogo tiene que ser por la izquierda: si fuera interna, los
        // proyectos sin clasificar —que hoy son todos— se caerían de la lista.
        await ProyectoAsync("PRY-2026-01", null);

        var filas = await new GetProyectosQueryHandler(_ctx)
            .Handle(new GetProyectosQuery(), CancellationToken.None);

        filas.Should().ContainSingle();
        filas.Single().Categoria.Should().BeNull();
        filas.Single().CategoriaColor.Should().BeNull();
    }

    [Fact]
    public async Task El_catalogo_cuenta_cuantos_proyectos_usa_cada_categoria()
    {
        var idCat = await CrearCategoriaAsync("Contada");
        await ProyectoAsync("PRY-2026-01", idCat);

        var filas = await new GetCategoriasProyectoQueryHandler(_ctx)
            .Handle(new GetCategoriasProyectoQuery(), CancellationToken.None);

        filas.Single(f => f.Id == idCat).Proyectos.Should().Be(1);
    }

    private async Task ProyectoAsync(string codigo, int? categoriaId)
    {
        var p = Proyecto.Crear(codigo, $"Proyecto {codigo}");
        p.PrioridadId = _prio.Media;
        p.CategoriaId = categoriaId;
        _ctx.Proyectos.Add(p);
        await _ctx.SaveChangesAsync();
    }

    public void Dispose() => _ctx.Dispose();
}
