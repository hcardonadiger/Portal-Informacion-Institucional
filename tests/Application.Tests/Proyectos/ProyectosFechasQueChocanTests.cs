using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Application.Proyectos.Services;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

/// <summary>
/// Fechas que se contradicen entre sí dentro de un proyecto.
///
/// <para>Se escribió para contestar una pregunta concreta: si la entrega comprometida de un
/// entregable choca con las fechas de sus actividades, ¿el guardado lo rechaza y hace perder lo
/// tecleado? <b>No.</b> Y no es un olvido: el plan puede estar en revisión, y cerrarle la puerta
/// obligaría a maquillar fechas para poder guardar. La ficha lo señala —<c>DesbordaElPlan</c>,
/// <c>DesfaseIncoherente</c>, <c>Bloqueada</c>— y ahí termina.</para>
///
/// <para>Estas pruebas fijan esa decisión. Lo único que el guardado sí rechaza por fechas es la
/// ventana invertida —fin antes que inicio— de un proyecto o de una actividad, que es un dato
/// imposible y no un plan discutible.</para>
/// </summary>
public class ProyectosFechasQueChocanTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly CatalogoPrioridades _prio;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private readonly IInteresadosAutomaticosSync _sync = Substitute.For<IInteresadosAutomaticosSync>();

    public ProyectosFechasQueChocanTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
        _prio = PrioridadesDePrueba.Sembrar(_ctx);
        _usuario.Nombre.Returns("Henry Ortez");
    }

    public void Dispose() => _ctx.Dispose();

    private static DateOnly D(string iso) => DateOnly.Parse(iso);

    private Task<int> CrearAsync() =>
        new CrearProyectoCommandHandler(_ctx, _usuario, _sync)
            .Handle(new CrearProyectoCommand("SOL — institución de prueba"), CancellationToken.None);

    private Task GuardarAsync(int id, IReadOnlyList<EntregableInput> entregables) =>
        new ActualizarProyectoCommandHandler(_ctx, _usuario, _sync).Handle(new ActualizarProyectoCommand(
            id, "SOL — institución de prueba", null, null, null, null,
            _prio.Media, null, null, null, null, entregables), CancellationToken.None);

    private Task<ProyectoDetailDto?> FichaAsync(int id) =>
        new GetProyectoQueryHandler(_ctx).Handle(new GetProyectoQuery(id), CancellationToken.None);

    [Fact]
    public async Task La_entrega_del_entregable_puede_quedar_antes_de_que_terminen_sus_actividades()
    {
        var id = await CrearAsync();

        // Entrega comprometida en agosto; la actividad termina en octubre. El plan se contradice.
        var guardar = async () => await GuardarAsync(id,
        [
            new EntregableInput(0, "Diagnóstico", null, D("2026-08-20"), EstadoEntregable.EnProceso, null, null,
            [
                new ActividadInput(0, "Levantamiento", null, D("2026-10-05"), D("2026-10-17"), 0, false, null, null)
            ])
        ]);

        await guardar.Should().NotThrowAsync("el plan puede estar en revisión: se guarda y se avisa");

        var ficha = await FichaAsync(id);
        var entregable = ficha!.Entregables.Single();

        entregable.FechaPlan.Should().Be(D("2026-08-20"), "la fecha se guardó tal cual se mandó");
        entregable.FinActividades.Should().Be(D("2026-10-17"));
        entregable.DesbordaElPlan.Should().BeTrue("la ficha lo señala, que es todo lo que hace");
    }

    [Fact]
    public async Task Dos_actividades_del_mismo_entregable_pueden_solaparse()
    {
        var id = await CrearAsync();

        var guardar = async () => await GuardarAsync(id,
        [
            new EntregableInput(0, "Diagnóstico", null, null, EstadoEntregable.EnProceso, null, null,
            [
                new ActividadInput(0, "Levantamiento", null, D("2026-10-05"), D("2026-10-17"), 0, false, null, null),
                new ActividadInput(0, "Validación",   null, D("2026-10-15"), D("2026-10-20"), 0, false, null, null)
            ])
        ]);

        await guardar.Should().NotThrowAsync("solaparse no es un dato imposible, es una decisión de plan");

        var acts = (await FichaAsync(id))!.Entregables.Single().Actividades;
        acts.Should().HaveCount(2);
    }

    [Fact]
    public async Task Una_actividad_puede_empezar_antes_de_que_termine_su_predecesora()
    {
        var id = await CrearAsync();

        await GuardarAsync(id,
        [
            new EntregableInput(0, "Diagnóstico", null, null, EstadoEntregable.EnProceso, null, null,
            [
                new ActividadInput(0, "Levantamiento", null, D("2026-10-05"), D("2026-10-17"), 0, false, null, null),
                new ActividadInput(0, "Validación",   null, D("2026-10-10"), D("2026-10-20"), 0, false, null, null)
            ])
        ]);

        var ficha = await FichaAsync(id);
        var entregable = ficha!.Entregables.Single();
        var levantamiento = entregable.Actividades.Single(a => a.Nombre == "Levantamiento");
        var validacion    = entregable.Actividades.Single(a => a.Nombre == "Validación");

        // Se declara la dependencia: «Validación» no debería arrancar hasta que termine
        // «Levantamiento», pero su ventana empieza una semana antes.
        //
        // El entregable viaja con su Id, no con 0: con 0 se daría de alta uno nuevo y el anterior
        // —ausente de la lista— se borraría con sus actividades, así que la predecesora apuntaría
        // a una fila que ya no existe y el fallo sería otro.
        var guardar = async () => await GuardarAsync(id,
        [
            new EntregableInput(entregable.Id, "Diagnóstico", null, null, EstadoEntregable.EnProceso, null, null,
            [
                new ActividadInput(levantamiento.Id, "Levantamiento", null, D("2026-10-05"), D("2026-10-17"), 0, false, null, null),
                new ActividadInput(validacion.Id, "Validación", null, D("2026-10-10"), D("2026-10-20"), 0, false, null, null,
                                   [levantamiento.Id])
            ])
        ]);

        await guardar.Should().NotThrowAsync("el desfase se avisa, no se bloquea");

        var despues = (await FichaAsync(id))!.Entregables.Single()
            .Actividades.Single(a => a.Nombre == "Validación");

        despues.DesfaseIncoherente.Should().BeTrue(
            "empieza antes de que termine su predecesora: el conflicto ya está en el cronograma");
    }

    [Fact]
    public async Task La_ventana_invertida_de_una_actividad_si_se_rechaza()
    {
        var id = await CrearAsync();

        // El contraste con las tres pruebas anteriores: esto no es un plan discutible, es una
        // ventana que termina antes de empezar. Es lo único que el guardado corta por fechas —y
        // por eso es lo que el navegador revisa antes de postear (ver prevalidacion.js).
        var guardar = async () => await GuardarAsync(id,
        [
            new EntregableInput(0, "Diagnóstico", null, null, EstadoEntregable.EnProceso, null, null,
            [
                new ActividadInput(0, "Levantamiento", null, D("2026-10-17"), D("2026-10-05"), 0, false, null, null)
            ])
        ]);

        (await guardar.Should().ThrowAsync<Diger.TramitesEstado.Domain.Common.DomainException>())
            .WithMessage("*la fecha de fin no puede ser anterior a la de inicio*");
    }
}
