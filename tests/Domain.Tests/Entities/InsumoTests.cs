using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Domain.Tests.Entities;

/// <summary>
/// Entrega 4 (2026-10-22): el catálogo de insumos y los insumos de cada actividad. Lo que se
/// prueba acá es la regla que vive en la actividad: un insumo NUEVO tiene que estar disponible,
/// pero uno que ya estaba asignado se conserva aunque después se haya desactivado.
/// </summary>
public class InsumoTests
{
    private static readonly DateTime Ahora = new(2026, 10, 22, 15, 0, 0, DateTimeKind.Utc);

    private static ActividadProyecto Actividad() => ActividadProyecto.Crear("Instalar el servidor", 1);

    // ── Catálogo ──────────────────────────────────────────────────

    [Fact]
    public void La_categoria_nace_activa_y_con_el_nombre_limpio()
    {
        var c = CategoriaInsumo.Crear("  Herramientas ", 3);

        c.Nombre.Should().Be("Herramientas");
        c.Orden.Should().Be(3);
        c.Activo.Should().BeTrue();
    }

    [Fact]
    public void La_categoria_no_admite_nombre_vacio_ni_orden_negativo()
    {
        FluentActions.Invoking(() => CategoriaInsumo.Crear("  ", 1)).Should().Throw<DomainException>();
        FluentActions.Invoking(() => CategoriaInsumo.Crear("Otros", -1)).Should().Throw<DomainException>();
    }

    [Fact]
    public void El_insumo_nace_activo_y_una_descripcion_en_blanco_queda_vacia()
    {
        var i = Insumo.Crear(7, " Cable UTP ", "   ");

        i.CategoriaId.Should().Be(7);
        i.Nombre.Should().Be("Cable UTP");
        i.Descripcion.Should().BeNull();
        i.Activo.Should().BeTrue();
    }

    [Fact]
    public void El_insumo_rechaza_nombre_vacio_o_demasiado_largo()
    {
        FluentActions.Invoking(() => Insumo.Crear(1, "", null)).Should().Throw<DomainException>();
        FluentActions.Invoking(() => Insumo.Crear(1, new string('x', Insumo.MaxNombre + 1), null))
            .Should().Throw<DomainException>();
        FluentActions.Invoking(() => Insumo.Crear(1, "Cable", new string('x', Insumo.MaxDescripcion + 1)))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void Desactivar_un_insumo_no_lo_borra()
    {
        var i = Insumo.Crear(1, "Cable UTP", null);

        i.Actualizar(1, "Cable UTP", null, activo: false);

        i.Activo.Should().BeFalse();
        i.Nombre.Should().Be("Cable UTP");
    }

    // ── Insumos de la actividad ───────────────────────────────────

    [Fact]
    public void Asignar_insumos_disponibles_deja_quien_y_cuando()
    {
        var a = Actividad();

        var cambio = a.FijarInsumos([10, 11], elegibles: new HashSet<int> { 10, 11 }, "Ana", Ahora);

        cambio.Should().BeTrue();
        a.InsumoIds.Should().BeEquivalentTo([10, 11]);
        a.Insumos.Should().AllSatisfy(x =>
        {
            x.AgregadoPor.Should().Be("Ana");
            x.AgregadoEl.Should().Be(Ahora);
        });
    }

    [Fact]
    public void Un_insumo_nuevo_que_no_esta_disponible_se_rechaza_y_no_cambia_nada()
    {
        var a = Actividad();
        a.FijarInsumos([10], new HashSet<int> { 10 }, "Ana", Ahora);

        var act = () => a.FijarInsumos([10, 99], new HashSet<int> { 10 }, "Ana", Ahora);

        act.Should().Throw<DomainException>().WithMessage("*Instalar el servidor*");
        a.InsumoIds.Should().BeEquivalentTo([10]);
    }

    [Fact]
    public void Un_insumo_ya_asignado_que_se_desactivo_se_conserva()
    {
        var a = Actividad();
        a.FijarInsumos([10, 11], new HashSet<int> { 10, 11 }, "Ana", Ahora);

        // El 10 se desactivó en el catálogo: ya no es elegible, pero la actividad lo tenía.
        var cambio = a.FijarInsumos([10, 11, 12], new HashSet<int> { 11, 12 }, "Beto", Ahora.AddDays(1));

        cambio.Should().BeTrue();
        a.InsumoIds.Should().BeEquivalentTo([10, 11, 12]);
        a.Insumos.Single(x => x.InsumoId == 10).AgregadoPor.Should().Be("Ana", "conservarlo no lo vuelve a agregar");
        a.Insumos.Single(x => x.InsumoId == 12).AgregadoPor.Should().Be("Beto");
    }

    [Fact]
    public void Un_insumo_desactivado_se_puede_quitar()
    {
        var a = Actividad();
        a.FijarInsumos([10, 11], new HashSet<int> { 10, 11 }, "Ana", Ahora);

        a.FijarInsumos([11], new HashSet<int> { 11 }, "Ana", Ahora);

        a.InsumoIds.Should().BeEquivalentTo([11]);
    }

    [Fact]
    public void Repetidos_y_sin_cambios_no_cuentan_como_cambio()
    {
        var a = Actividad();
        a.FijarInsumos([10, 10, 11], new HashSet<int> { 10, 11 }, "Ana", Ahora).Should().BeTrue();

        a.FijarInsumos([11, 10], new HashSet<int>(), "Ana", Ahora).Should().BeFalse();
        a.Insumos.Should().HaveCount(2);
    }

    [Fact]
    public void Null_significa_no_tocar_los_insumos()
    {
        var a = Actividad();
        a.FijarInsumos([10], new HashSet<int> { 10 }, "Ana", Ahora);

        a.FijarInsumos(null, new HashSet<int>(), "Ana", Ahora).Should().BeFalse();
        a.InsumoIds.Should().BeEquivalentTo([10]);
    }

    [Fact]
    public void Una_lista_vacia_los_quita_todos()
    {
        var a = Actividad();
        a.FijarInsumos([10, 11], new HashSet<int> { 10, 11 }, "Ana", Ahora);

        a.FijarInsumos([], new HashSet<int>(), "Ana", Ahora).Should().BeTrue();
        a.Insumos.Should().BeEmpty();
    }
}
