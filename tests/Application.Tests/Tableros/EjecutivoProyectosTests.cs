using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Dashboards.Common;
using Diger.TramitesEstado.Application.Dashboards.Queries;
using Diger.TramitesEstado.Application.Dashboards.Queries.GetEjecutivoProyectos;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Application.Proyectos.Services;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Tableros;

/// <summary>
/// La consulta del tablero ejecutivo (<see cref="GetEjecutivoProyectosQuery"/>) sobre la consulta
/// del portafolio, con datos armados por los comandos reales del módulo.
/// </summary>
public class EjecutivoProyectosTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly CatalogoPrioridades _prio;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private readonly IInteresadosAutomaticosSync _sync = Substitute.For<IInteresadosAutomaticosSync>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly DateOnly _hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    public EjecutivoProyectosTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), Substitute.For<IPublisher>());
        _prio = PrioridadesDePrueba.Sembrar(_ctx);
        _usuario.Nombre.Returns("Henry Ortez");

        // El ejecutivo pide el portafolio por MediatR: aquí el envío va directo al handler real.
        _sender.Send(Arg.Any<GetProyectosDashboardQuery>(), Arg.Any<CancellationToken>())
            .Returns(c => new GetProyectosDashboardQueryHandler(_ctx)
                .Handle(c.Arg<GetProyectosDashboardQuery>(), c.Arg<CancellationToken>()));
    }

    private Task<EjecutivoProyectosDto> EjecutivoAsync(string? unidad = null) =>
        new GetEjecutivoProyectosQueryHandler(_sender)
            .Handle(new GetEjecutivoProyectosQuery(null, unidad), CancellationToken.None);

    private async Task<int> CrearAsync(string nombre, EstadoProyecto? estado = null)
    {
        var id = await new CrearProyectoCommandHandler(_ctx, _usuario, _sync)
            .Handle(new CrearProyectoCommand(nombre), CancellationToken.None);
        var cambiar = new CambiarEstadoProyectoCommandHandler(_ctx, _usuario);
        if (estado is EstadoProyecto.EnEjecucion or EstadoProyecto.Cerrado)
            await cambiar.Handle(new CambiarEstadoProyectoCommand(id, EstadoProyecto.EnEjecucion), CancellationToken.None);
        if (estado is EstadoProyecto.Cerrado)
            await cambiar.Handle(new CambiarEstadoProyectoCommand(id, EstadoProyecto.Cerrado), CancellationToken.None);
        return id;
    }

    private Task EstructurarAsync(int id, string nombre, DateOnly? finPlan, params ActividadInput[] actividades) =>
        new ActualizarProyectoCommandHandler(_ctx, _usuario, _sync).Handle(new ActualizarProyectoCommand(
            id, nombre, null, null, null, null, null, _prio.Media, null, null, null, finPlan,
            [new EntregableInput(0, "Único", null, null, EstadoEntregable.EnProceso, null, null, actividades)]),
            CancellationToken.None);

    private ActividadInput Act(string nombre, int iniDesdeHoy, int finDesdeHoy, int avance) =>
        new(0, nombre, null, _hoy.AddDays(iniDesdeHoy), _hoy.AddDays(finDesdeHoy), avance, false, null, null);

    private async Task PonerUnidadAsync(int id, string unidad)
    {
        var p = await _ctx.Proyectos.SingleAsync(x => x.Id == id);
        p.UnidadId = unidad;
        await _ctx.SaveChangesAsync();
    }

    /// <summary>El caso de la especificación (§9.6, 3): uno sin estructura, uno cerrado y uno en
    /// ejecución con bloqueo.</summary>
    private async Task<(int SinEstructura, int Cerrado, int Bloqueado)> SembrarTresAsync()
    {
        var sinEstructura = await CrearAsync("Sin estructura");                       // Planificado, 0 entregables

        var cerrado = await CrearAsync("Cerrado");
        await EstructurarAsync(cerrado, "Cerrado", null, Act("Hecha", -20, -10, 100));
        await new CambiarEstadoProyectoCommandHandler(_ctx, _usuario)
            .Handle(new CambiarEstadoProyectoCommand(cerrado, EstadoProyecto.EnEjecucion), CancellationToken.None);
        await new CambiarEstadoProyectoCommandHandler(_ctx, _usuario)
            .Handle(new CambiarEstadoProyectoCommand(cerrado, EstadoProyecto.Cerrado), CancellationToken.None);

        var bloqueado = await CrearAsync("Bloqueado", EstadoProyecto.EnEjecucion);
        // Una actividad a mitad de su ventana con 20 %: esperado 50, desviación −30.
        await EstructurarAsync(bloqueado, "Bloqueado", _hoy.AddDays(60), Act("Desarrollo", -10, 10, 20));
        await new RegistrarAvanceCommandHandler(_ctx, _usuario).Handle(
            new RegistrarAvanceCommand(bloqueado, "Corte", Bloqueo: "Falta la firma del convenio"), CancellationToken.None);

        return (sinEstructura, cerrado, bloqueado);
    }

    [Fact]
    public async Task Los_KPI_salen_de_la_cartera_con_sus_definiciones()
    {
        await SembrarTresAsync();

        var d = await EjecutivoAsync();

        d.Total.Should().Be(3);
        d.Finalizados.Should().Be(1);
        d.EnProceso.Should().Be(1);
        d.NoIniciados.Should().Be(1);
        d.SinEstructura.Should().Be(1);
        d.ConEstructura.Should().Be(2);
        d.AvanceReal.Should().Be(60.0m, "(100 + 20) / 2 sobre los dos con estructura");
        d.ConLineaBase.Should().Be(2);
        d.AvanceEsperado.Should().Be(75.0m, "(100 + 50) / 2");
        d.Desviacion.Should().Be(-15.0m, "(0 + −30) / 2");
        d.AvanceRealConLineaBase.Should().Be(60.0m, "los dos con estructura tienen además línea base");
        d.BloqueosVigentes.Should().Be(1);
        d.DiasBloqueoMasAntiguo.Should().Be(0);
        d.Rojos.Should().Be(1);
        d.Neutros.Should().Be(2);
        d.EnProcesoEnRojo.Should().Be(1);
        d.UltimoReporte.Should().NotBeNull();
        d.PorEstado.Select(s => (s.Etiqueta, s.Cantidad))
            .Should().Equal(("En proceso", 1), ("No iniciado", 1), ("Finalizado", 1));
    }

    [Fact]
    public async Task La_lista_va_de_rojo_a_neutro_y_los_neutros_en_su_orden()
    {
        await SembrarTresAsync();

        var d = await EjecutivoAsync();

        d.Proyectos.Select(p => (p.Nombre, p.EtiquetaSalud))
            .Should().Equal(("Bloqueado", "Bloqueado"), ("Cerrado", "Finalizado"), ("Sin estructura", "Sin dato"));

        var b = d.Proyectos[0];
        b.Salud.Should().Be(SaludProyecto.Rojo);
        b.AvanceEsperado.Should().Be(50);
        b.Desviacion.Should().Be(-30);
        b.BloqueoVigente.Should().BeTrue();
        d.Proyectos[2].SinEstructura.Should().BeTrue();
    }

    [Fact]
    public async Task Las_decisiones_traen_el_bloqueo_con_texto_y_autor()
    {
        var (_, _, bloqueado) = await SembrarTresAsync();

        var d = await EjecutivoAsync();

        d.Decisiones.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Tipo = TipoDecision.BloqueoVigente,
            ProyectoId = bloqueado,
            Texto = "Falta la firma del convenio",
            Autor = "Henry Ortez",
            Dias = 0
        });
    }

    [Fact]
    public async Task Un_cierre_planificado_vencido_es_una_decision_y_pinta_vencido()
    {
        var id = await CrearAsync("Vencido", EstadoProyecto.EnEjecucion);
        await EstructurarAsync(id, "Vencido", _hoy.AddDays(-3), Act("Algo", -30, -5, 80));

        var d = await EjecutivoAsync();

        d.Proyectos.Single().EtiquetaSalud.Should().Be("Vencido");
        d.Decisiones.Should().ContainSingle().Which.Tipo.Should().Be(TipoDecision.CierreVencido);
        d.Decisiones[0].Dias.Should().Be(3);
    }

    [Fact]
    public async Task El_filtro_de_unidad_recorta_la_lista_los_KPI_y_las_decisiones()
    {
        var (sinEstructura, cerrado, bloqueado) = await SembrarTresAsync();
        await PonerUnidadAsync(sinEstructura, "DAI");
        await PonerUnidadAsync(cerrado, "DAI");
        await PonerUnidadAsync(bloqueado, "DITRA");

        var dai = await EjecutivoAsync("DAI");

        dai.Total.Should().Be(2);
        dai.Proyectos.Select(p => p.ProyectoId).Should().BeEquivalentTo([sinEstructura, cerrado]);
        dai.BloqueosVigentes.Should().Be(0);
        dai.Decisiones.Should().BeEmpty("el bloqueo es de un proyecto de otra unidad");

        var ditra = await EjecutivoAsync("DITRA");
        ditra.Proyectos.Should().ContainSingle().Which.ProyectoId.Should().Be(bloqueado);
        ditra.Decisiones.Should().ContainSingle();
    }

    [Fact]
    public async Task Sin_linea_base_el_esperado_y_la_desviacion_son_null()
    {
        var id = await CrearAsync("Sin fechas", EstadoProyecto.EnEjecucion);
        await EstructurarAsync(id, "Sin fechas", _hoy.AddDays(30),
            new ActividadInput(0, "Sin ventana", null, null, null, 40, false, null, null));

        var d = await EjecutivoAsync();

        d.ConLineaBase.Should().Be(0);
        d.AvanceEsperado.Should().BeNull();
        d.Desviacion.Should().BeNull();
        d.Proyectos.Single().AvanceEsperado.Should().BeNull();
    }

    /// <summary>R1: el real que se pone junto al esperado sale de la MISMA subpoblación con línea
    /// base; el número grande sigue siendo el de todos los que tienen estructura.</summary>
    [Fact]
    public async Task El_real_junto_al_esperado_se_promedia_solo_sobre_los_que_tienen_linea_base()
    {
        var conPlan = await CrearAsync("Con plan", EstadoProyecto.EnEjecucion);
        await EstructurarAsync(conPlan, "Con plan", _hoy.AddDays(60), Act("Mitad", -10, 10, 20));   // real 20, esperado 50

        var sinPlan = await CrearAsync("Sin plan", EstadoProyecto.EnEjecucion);
        await EstructurarAsync(sinPlan, "Sin plan", _hoy.AddDays(60),
            new ActividadInput(0, "Sin fechas", null, null, null, 80, false, null, null));        // real 80, sin esperado

        var d = await EjecutivoAsync();

        d.ConEstructura.Should().Be(2);
        d.AvanceReal.Should().Be(50.0m, "(20 + 80) / 2, todos los que tienen estructura");
        d.ConLineaBase.Should().Be(1);
        d.AvanceRealConLineaBase.Should().Be(20.0m, "solo el que tiene plan");
        d.AvanceEsperado.Should().Be(50.0m);
        d.Desviacion.Should().Be(-30.0m);
        (d.AvanceRealConLineaBase - d.AvanceEsperado).Should().Be(d.Desviacion,
            "las tres cifras de la línea 3 del KPI cuadran entre sí");
    }

    /// <summary>R3: dentro del rojo manda la etiqueta —Vencido, Bloqueado, Atrasado— y solo después
    /// la desviación. El atrasado tiene la peor desviación y aun así va último.</summary>
    [Fact]
    public async Task Dentro_del_rojo_van_vencidos_luego_bloqueados_luego_atrasados()
    {
        var atrasado = await CrearAsync("A atrasado", EstadoProyecto.EnEjecucion);
        await EstructurarAsync(atrasado, "A atrasado", _hoy.AddDays(60), Act("Pasada", -30, -10, 0));   // −100

        var bloqueado = await CrearAsync("B bloqueado", EstadoProyecto.EnEjecucion);
        await EstructurarAsync(bloqueado, "B bloqueado", _hoy.AddDays(60), Act("Mitad", -10, 10, 40));  // −10
        await new RegistrarAvanceCommandHandler(_ctx, _usuario).Handle(
            new RegistrarAvanceCommand(bloqueado, "Corte", Bloqueo: "Sin firma"), CancellationToken.None);

        var vencido = await CrearAsync("C vencido", EstadoProyecto.EnEjecucion);
        await EstructurarAsync(vencido, "C vencido", _hoy.AddDays(-2), Act("Casi", -30, -5, 90));       // −10

        var d = await EjecutivoAsync();

        d.Proyectos.Select(p => p.EtiquetaSalud).Should().Equal("Vencido", "Bloqueado", "Atrasado");
    }

    /// <summary>R7: la fila muestra el nombre corto de la unidad.</summary>
    [Fact]
    public async Task La_unidad_viaja_con_su_nombre_corto()
    {
        _ctx.Unidades.Add(Unidad.Crear("UNI-DPD", "GOBDIG", "Desarrollo de proyectos de transformacion", nombreCorto: "dpd"));
        await _ctx.SaveChangesAsync();
        var id = await CrearAsync("Con unidad", EstadoProyecto.EnEjecucion);
        await PonerUnidadAsync(id, "UNI-DPD");

        var d = await EjecutivoAsync();

        var fila = d.Proyectos.Single();
        fila.UnidadNombreCorto.Should().Be("DPD");
        fila.UnidadEtiqueta.Should().Be("DPD", "el corto gana al nombre largo");
    }

    [Fact]
    public async Task Cartera_vacia_da_ceros_y_listas_vacias()
    {
        var d = await EjecutivoAsync();

        d.Total.Should().Be(0);
        d.AvanceReal.Should().Be(0m);
        d.AvanceEsperado.Should().BeNull();
        d.AvanceRealConLineaBase.Should().BeNull();
        d.UltimoReporte.Should().BeNull();
        d.PorEstado.Should().BeEmpty();
        d.Proyectos.Should().BeEmpty();
        d.Decisiones.Should().BeEmpty();
    }

    /// <summary>§9.6, 4: el ejecutivo y el tablero de un proyecto calculan el mismo esperado sobre
    /// la misma entrada: cuatro ventanas en las cuatro situaciones de la interpolación.</summary>
    [Fact]
    public async Task El_esperado_del_ejecutivo_es_el_mismo_que_el_del_tablero_del_proyecto()
    {
        var id = await CrearAsync("Comparado", EstadoProyecto.EnEjecucion);
        await EstructurarAsync(id, "Comparado", _hoy.AddDays(90),
            Act("Pasada",   -40, -10, 100),   // 100
            Act("Mitad",     -5,   5,  30),   // 50
            Act("Futura",    10,  40,   0),   // 0
            Act("Un cuarto", -1,   3,  10));  // 25  → promedio 43.75

        var tablero = await new GetTableroProyectoQueryHandler(_ctx)
            .Handle(new GetTableroProyectoQuery(id), CancellationToken.None);
        var ejecutivo = await EjecutivoAsync();

        var fila = ejecutivo.Proyectos.Single();
        fila.AvanceEsperado.Should().Be(tablero!.AvanceEsperado);
        fila.AvanceEsperado.Should().Be(44);
        fila.Desviacion.Should().Be(tablero.AvanceReal - tablero.AvanceEsperado);
    }

    public void Dispose() => _ctx.Dispose();
}
