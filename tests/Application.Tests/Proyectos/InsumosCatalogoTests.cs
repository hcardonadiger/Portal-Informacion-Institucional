using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Proyectos.Insumos;
using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

/// <summary>
/// Entrega 4 (2026-10-22): el catálogo de insumos. Lo que lo distingue del de categorías de
/// proyecto es que no se borra —solo se desactiva— y que el nombre del insumo es único dentro de
/// su categoría, no en todo el catálogo.
/// </summary>
public sealed class InsumosCatalogoTests : IDisposable
{
    private readonly AppDbContext _ctx = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new FakeCurrentUser(), Substitute.For<IPublisher>());

    public void Dispose() => _ctx.Dispose();

    private Task<int> CategoriaAsync(string nombre, int orden = 1) =>
        new CrearCategoriaInsumoCommandHandler(_ctx).Handle(new CrearCategoriaInsumoCommand(nombre, orden), default);

    private Task<int> InsumoAsync(int categoriaId, string nombre, string? descripcion = null) =>
        new CrearInsumoCommandHandler(_ctx).Handle(new CrearInsumoCommand(categoriaId, nombre, descripcion), default);

    private Task ActualizarInsumoAsync(int id, int categoriaId, string nombre, bool activo) =>
        new ActualizarInsumoCommandHandler(_ctx).Handle(
            new ActualizarInsumoCommand(id, categoriaId, nombre, null, activo), default);

    private Task ActualizarCategoriaAsync(int id, string nombre, int orden, bool activo) =>
        new ActualizarCategoriaInsumoCommandHandler(_ctx).Handle(
            new ActualizarCategoriaInsumoCommand(id, nombre, orden, activo), default);

    // ── Categorías ────────────────────────────────────────────────

    [Fact]
    public async Task No_se_repite_el_nombre_de_una_categoria()
    {
        await CategoriaAsync("Herramientas");

        var act = () => CategoriaAsync(" Herramientas ");

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Herramientas*");
    }

    [Fact]
    public async Task Renombrar_una_categoria_al_nombre_de_otra_se_rechaza()
    {
        var a = await CategoriaAsync("Materiales");
        await CategoriaAsync("Servicios", 2);

        var act = () => ActualizarCategoriaAsync(a, "Servicios", 1, true);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task La_categoria_se_desactiva_y_cuenta_sus_insumos()
    {
        var c = await CategoriaAsync("Materiales");
        await InsumoAsync(c, "Cemento");
        await InsumoAsync(c, "Varilla");

        await ActualizarCategoriaAsync(c, "Materiales", 1, activo: false);

        var fila = (await new GetCategoriasInsumoQueryHandler(_ctx).Handle(new GetCategoriasInsumoQuery(), default))
            .Single();
        fila.Activo.Should().BeFalse();
        fila.Insumos.Should().Be(2);
    }

    // ── Insumos ───────────────────────────────────────────────────

    [Fact]
    public async Task El_nombre_se_repite_entre_categorias_pero_no_dentro_de_una()
    {
        var materiales   = await CategoriaAsync("Materiales");
        var herramientas = await CategoriaAsync("Herramientas", 2);
        await InsumoAsync(materiales, "Cinta");

        await InsumoAsync(herramientas, "Cinta");   // otra categoría: vale
        var act = () => InsumoAsync(materiales, "cinta ");

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Cinta*Materiales*");
    }

    [Fact]
    public async Task No_se_crea_un_insumo_en_una_categoria_desactivada_ni_en_una_que_no_existe()
    {
        var c = await CategoriaAsync("Materiales");
        await ActualizarCategoriaAsync(c, "Materiales", 1, activo: false);

        await FluentActions.Invoking(() => InsumoAsync(c, "Cemento"))
            .Should().ThrowAsync<DomainException>().WithMessage("*desactivada*");
        await FluentActions.Invoking(() => InsumoAsync(999, "Cemento"))
            .Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Un_insumo_se_desactiva_sin_moverlo_aunque_su_categoria_este_desactivada()
    {
        var c  = await CategoriaAsync("Materiales");
        var id = await InsumoAsync(c, "Cemento");
        await ActualizarCategoriaAsync(c, "Materiales", 1, activo: false);

        // Solo se exige categoría activa cuando la categoría CAMBIA: desactivar o renombrar un
        // insumo de una categoría ya retirada tiene que poder hacerse.
        await ActualizarInsumoAsync(id, c, "Cemento gris", activo: false);

        var i = await _ctx.Insumos.AsNoTracking().SingleAsync(x => x.Id == id);
        i.Activo.Should().BeFalse();
        i.Nombre.Should().Be("Cemento gris");
    }

    [Fact]
    public async Task Mover_un_insumo_a_una_categoria_desactivada_se_rechaza()
    {
        var a  = await CategoriaAsync("Materiales");
        var b  = await CategoriaAsync("Otros", 2);
        var id = await InsumoAsync(a, "Cemento");
        await ActualizarCategoriaAsync(b, "Otros", 2, activo: false);

        var act = () => ActualizarInsumoAsync(id, b, "Cemento", true);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*desactivada*");
    }

    [Fact]
    public async Task Actualizar_un_insumo_que_no_existe_responde_no_encontrado()
    {
        var c = await CategoriaAsync("Materiales");

        var act = () => ActualizarInsumoAsync(999, c, "Cemento", true);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public void El_catalogo_no_tiene_comando_para_borrar()
    {
        // «Sin borrado: solo desactivar». Si alguien agrega un Eliminar, esta prueba lo hace
        // explícito en vez de que pase en silencio.
        typeof(CrearInsumoCommand).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(CrearInsumoCommand).Namespace)
            .Select(t => t.Name)
            .Should().NotContain(n => n.StartsWith("Eliminar") || n.StartsWith("Borrar"));
    }

    // ── Lo que se ofrece para elegir ──────────────────────────────

    [Fact]
    public async Task Para_elegir_salen_solo_los_activos_de_categorias_activas_en_orden()
    {
        var servicios  = await CategoriaAsync("Servicios", 2);
        var materiales = await CategoriaAsync("Materiales", 1);
        var otros      = await CategoriaAsync("Otros", 3);

        await InsumoAsync(servicios, "Transporte");
        await InsumoAsync(materiales, "Varilla");
        await InsumoAsync(materiales, "Cemento");
        var retirado = await InsumoAsync(materiales, "Asbesto");
        await ActualizarInsumoAsync(retirado, materiales, "Asbesto", activo: false);
        await InsumoAsync(otros, "Varios");
        await ActualizarCategoriaAsync(otros, "Otros", 3, activo: false);

        var elegibles = await new GetInsumosElegiblesQueryHandler(_ctx).Handle(new GetInsumosElegiblesQuery(), default);

        elegibles.Select(e => $"{e.Categoria}/{e.Nombre}").Should().Equal(
            "Materiales/Cemento", "Materiales/Varilla", "Servicios/Transporte");
    }

    [Fact]
    public async Task El_listado_del_catalogo_trae_todos_con_su_categoria()
    {
        var c  = await CategoriaAsync("Materiales");
        var id = await InsumoAsync(c, "Cemento", "Bolsa de 42.5 kg");
        await InsumoAsync(c, "Arena");
        await ActualizarInsumoAsync(id, c, "Cemento", activo: false);

        var filas = await new GetInsumosQueryHandler(_ctx).Handle(new GetInsumosQuery(), default);

        filas.Should().HaveCount(2, "el desactivado también se administra");
        filas.Single(f => f.Id == id).Should().Match<InsumoDto>(f =>
            f.Categoria == "Materiales" && !f.Activo && f.Actividades == 0);
    }
}
