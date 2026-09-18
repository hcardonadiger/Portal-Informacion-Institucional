using Diger.TramitesEstado.Application.Proyectos.Prioridades;
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
/// El catálogo de prioridades reemplazó a un enum. Un enum no se podía dejar vacío, ni borrarle un
/// miembro que alguien estuviera usando, ni quedarse sin valor por defecto: el compilador lo
/// impedía. Estas pruebas cubren esas mismas garantías, que ahora son reglas de datos.
/// </summary>
public class PrioridadesCatalogoTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly CatalogoPrioridades _prio;

    public PrioridadesCatalogoTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
        _prio = PrioridadesDePrueba.Sembrar(_ctx);
    }

    // ── Alta ──────────────────────────────────────────────────────
    [Fact]
    public async Task Crear_agrega_la_prioridad_al_catalogo()
    {
        var id = await new CrearPrioridadProyectoCommandHandler(_ctx)
            .Handle(new CrearPrioridadProyectoCommand("Q3", 4, ColorEtiqueta.Verde), CancellationToken.None);

        var creada = await _ctx.PrioridadesProyecto.FindAsync(id);
        creada!.Nombre.Should().Be("Q3");
        creada.Color.Should().Be(ColorEtiqueta.Verde);
        creada.Activo.Should().BeTrue();
    }

    [Fact]
    public async Task Crear_rechaza_un_nombre_repetido()
    {
        var acto = () => new CrearPrioridadProyectoCommandHandler(_ctx)
            .Handle(new CrearPrioridadProyectoCommand("Alta", 9, ColorEtiqueta.Rojo), CancellationToken.None);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*Alta*");
    }

    // ── Predeterminada ────────────────────────────────────────────
    [Fact]
    public async Task Marcar_una_predeterminada_desmarca_la_anterior()
    {
        await new MarcarPrioridadProyectoPredeterminadaCommandHandler(_ctx)
            .Handle(new MarcarPrioridadProyectoPredeterminadaCommand(_prio.Alta), CancellationToken.None);

        var marcadas = await _ctx.PrioridadesProyecto
            .Where(p => p.EsPredeterminada).Select(p => p.Id).ToListAsync();

        marcadas.Should().ContainSingle().Which.Should().Be(_prio.Alta);
    }

    [Fact]
    public async Task No_se_puede_desactivar_la_predeterminada()
    {
        // Media es la predeterminada que deja el sembrado.
        var acto = () => Desactivar(_prio.Media, "Media");

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*predeterminada*");
    }

    [Fact]
    public async Task No_se_puede_quedar_sin_ninguna_activa()
    {
        await Desactivar(_prio.Alta, "Alta");
        await Desactivar(_prio.Baja, "Baja");

        // La última activa es además la predeterminada, así que el mensaje que gana es el de esa
        // regla. Lo que importa es que el catálogo nunca se queda sin nada que ofrecer.
        var acto = () => Desactivar(_prio.Media, "Media");
        await acto.Should().ThrowAsync<DomainException>();

        (await _ctx.PrioridadesProyecto.CountAsync(p => p.Activo)).Should().Be(1);
    }

    private Task Desactivar(int id, string nombre) =>
        new ActualizarPrioridadProyectoCommandHandler(_ctx)
            .Handle(new ActualizarPrioridadProyectoCommand(id, nombre, 1, ColorEtiqueta.Gris, Activo: false),
                    CancellationToken.None);

    // ── Baja ──────────────────────────────────────────────────────
    [Fact]
    public async Task No_se_puede_eliminar_una_prioridad_en_uso()
    {
        var p = Proyecto.Crear("PRY-2026-01", "Con prioridad Alta");
        p.PrioridadId = _prio.Alta;
        _ctx.Proyectos.Add(p);
        await _ctx.SaveChangesAsync();

        var acto = () => new EliminarPrioridadProyectoCommandHandler(_ctx)
            .Handle(new EliminarPrioridadProyectoCommand(_prio.Alta), CancellationToken.None);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*1 proyecto*");
    }

    [Fact]
    public async Task Un_proyecto_borrado_logicamente_tambien_retiene_su_prioridad()
    {
        // La llave foránea no distingue borrados lógicos: si el conteo los ignorara, el comando
        // prometería un borrado que la base terminaría rechazando.
        var p = Proyecto.Crear("PRY-2026-02", "Borrado");
        p.PrioridadId = _prio.Alta;
        p.IsDeleted = true;
        _ctx.Proyectos.Add(p);
        await _ctx.SaveChangesAsync();

        var acto = () => new EliminarPrioridadProyectoCommandHandler(_ctx)
            .Handle(new EliminarPrioridadProyectoCommand(_prio.Alta), CancellationToken.None);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*1 proyecto*");
    }

    [Fact]
    public async Task Se_elimina_la_que_nadie_usa()
    {
        await new EliminarPrioridadProyectoCommandHandler(_ctx)
            .Handle(new EliminarPrioridadProyectoCommand(_prio.Baja), CancellationToken.None);

        (await _ctx.PrioridadesProyecto.FindAsync(_prio.Baja)).Should().BeNull();
    }

    [Fact]
    public async Task No_se_puede_eliminar_la_predeterminada()
    {
        var acto = () => new EliminarPrioridadProyectoCommandHandler(_ctx)
            .Handle(new EliminarPrioridadProyectoCommand(_prio.Media), CancellationToken.None);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*predeterminada*");
    }

    // ── Resolución al crear un proyecto ───────────────────────────
    [Fact]
    public async Task Sin_prioridad_elegida_el_proyecto_toma_la_predeterminada()
    {
        var id = await PrioridadProyectoResolver.ResolverAsync(_ctx, null, CancellationToken.None);
        id.Should().Be(_prio.Media);
    }

    [Fact]
    public async Task Sin_predeterminada_marcada_toma_la_de_menor_orden_entre_las_activas()
    {
        var media = await _ctx.PrioridadesProyecto.FindAsync(_prio.Media);
        media!.FijarPredeterminada(false);
        await _ctx.SaveChangesAsync();

        var id = await PrioridadProyectoResolver.ResolverAsync(_ctx, null, CancellationToken.None);
        id.Should().Be(_prio.Alta, "Alta es la de orden 1");
    }

    [Fact]
    public async Task Una_prioridad_inexistente_se_rechaza()
    {
        var acto = () => PrioridadProyectoResolver.ResolverAsync(_ctx, 9999, CancellationToken.None);
        await acto.Should().ThrowAsync<DomainException>().WithMessage("*no existe*");
    }

    // ── Opciones para los desplegables ────────────────────────────
    [Fact]
    public async Task Las_opciones_no_ofrecen_las_inactivas()
    {
        await Desactivar(_prio.Baja, "Baja");

        var opciones = await new GetOpcionesPrioridadQueryHandler(_ctx)
            .Handle(new GetOpcionesPrioridadQuery(), CancellationToken.None);

        opciones.Select(o => o.Id).Should().NotContain(_prio.Baja);
    }

    [Fact]
    public async Task Pero_si_incluyen_la_inactiva_que_el_proyecto_ya_tiene()
    {
        // Si no apareciera, abrir un proyecto viejo y guardarlo le cambiaría la prioridad sin que
        // nadie lo hubiera pedido.
        await Desactivar(_prio.Baja, "Baja");

        var opciones = await new GetOpcionesPrioridadQueryHandler(_ctx)
            .Handle(new GetOpcionesPrioridadQuery(IncluirId: _prio.Baja), CancellationToken.None);

        opciones.Select(o => o.Id).Should().Contain(_prio.Baja);
    }

    // ── Catálogo para administración ──────────────────────────────
    [Fact]
    public async Task El_catalogo_cuenta_cuantos_proyectos_usa_cada_prioridad()
    {
        var p = Proyecto.Crear("PRY-2026-03", "Uno");
        p.PrioridadId = _prio.Alta;
        _ctx.Proyectos.Add(p);
        await _ctx.SaveChangesAsync();

        var filas = await new GetPrioridadesProyectoQueryHandler(_ctx)
            .Handle(new GetPrioridadesProyectoQuery(), CancellationToken.None);

        filas.Single(f => f.Id == _prio.Alta).Proyectos.Should().Be(1);
        filas.Single(f => f.Id == _prio.Baja).Proyectos.Should().Be(0);
    }

    [Fact]
    public async Task El_catalogo_sale_ordenado_por_orden()
    {
        var filas = await new GetPrioridadesProyectoQueryHandler(_ctx)
            .Handle(new GetPrioridadesProyectoQuery(), CancellationToken.None);

        filas.Select(f => f.Nombre).Should().ContainInOrder("Alta", "Media", "Baja");
    }

    public void Dispose() => _ctx.Dispose();
}
