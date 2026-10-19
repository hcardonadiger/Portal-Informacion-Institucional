using Diger.TramitesEstado.Application.Expedientes.Queries.GetExpedientes;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Expedientes;

/// <summary>
/// Los cuatro filtros de la lista de expedientes (2026-10-19).
///
/// <para>La pantalla tenía solo búsqueda libre y un chip de periodo —dos
/// controles contra los siete de Proyectos— y por eso se veía vacía. Se le
/// añadieron Institución, Encargado, Estado y Acción, que son los ejes por los
/// que de verdad se reparte y se sigue el trabajo.</para>
///
/// <para>Acá se prueba lo que una captura no enseña: que la coincidencia sea
/// EXACTA en los campos de texto —vienen de un desplegable, no de un campo
/// libre, así que un Contains traería falsos positivos entre siglas que se
/// contienen unas a otras— y que Acción, que vive en los TRÁMITES y no en el
/// expediente, alcance al expediente que tenga al menos un trámite con ella.</para>
/// </summary>
public sealed class FiltrosExpedientesTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public FiltrosExpedientesTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(),
            NSubstitute.Substitute.For<MediatR.IPublisher>());
        Sembrar();
    }

    public void Dispose() => _ctx.Dispose();

    private void Sembrar()
    {
        _ctx.Expedientes.AddRange(
            Exp("EXP-1", "INA", "Mario Castejón", EstadoExpediente.EnLevantamiento, "Simplificar"),
            Exp("EXP-2", "INPREMA", "Mario Castejón", EstadoExpediente.EnModelado, "Digitalizar", "Simplificar"),
            Exp("EXP-3", "INPREMA", "Zenia Zelaya", EstadoExpediente.Cerrado, "Eliminar"),
            Exp("EXP-4", "CONSUCOOP", "Zenia Zelaya", EstadoExpediente.EnLevantamiento));
        _ctx.SaveChanges();
    }

    private static Expediente Exp(
        string codigo, string instId, string analista,
        EstadoExpediente estado, params string[] accionesDeTramites)
    {
        var e = Expediente.Crear(codigo, instId, null, null, instId, analista);

        // El estado no se asigna: se avanza. CambiarEstado solo deja pasar a la
        // etapa siguiente —es un invariante del dominio— así que para sembrar un
        // expediente cerrado hay que recorrer las cuatro. Hacerlo así y no con
        // un setter mantiene la prueba sobre datos que el portal puede producir.
        for (var paso = (int)e.EstadoExpediente; paso < (int)estado; paso++)
            e.CambiarEstado((EstadoExpediente)(paso + 1), "siembra de prueba");

        e.FechaApertura = new DateOnly(2026, 6, 1);
        for (var i = 0; i < accionesDeTramites.Length; i++)
            e.Agregar(new ExpedienteTramite
            {
                TramiteIndex  = i,
                NombreTramite = $"Trámite {i + 1}",
                Accion        = accionesDeTramites[i]
            });
        return e;
    }

    private async Task<IReadOnlyList<string>> CodigosAsync(GetExpedientesQuery q)
    {
        var r = await new GetExpedientesQueryHandler(_ctx).Handle(q, default);
        return r.Items.Select(i => i.Codigo).OrderBy(c => c).ToList();
    }

    [Fact]
    public async Task Sin_filtros_trae_todos()
    {
        (await CodigosAsync(new GetExpedientesQuery()))
            .Should().BeEquivalentTo(["EXP-1", "EXP-2", "EXP-3", "EXP-4"]);
    }

    [Fact]
    public async Task Filtra_por_institucion_con_coincidencia_exacta()
    {
        // "INA" está contenido dentro de "INPREMA": con un Contains, filtrar por
        // INA habría traído también los dos de INPREMA.
        (await CodigosAsync(new GetExpedientesQuery(Institucion: "INA")))
            .Should().BeEquivalentTo(["EXP-1"]);
    }

    [Fact]
    public async Task Filtra_por_encargado()
    {
        (await CodigosAsync(new GetExpedientesQuery(Analista: "Zenia Zelaya")))
            .Should().BeEquivalentTo(["EXP-3", "EXP-4"]);
    }

    [Fact]
    public async Task Filtra_por_estado()
    {
        (await CodigosAsync(new GetExpedientesQuery(Estado: EstadoExpediente.EnLevantamiento)))
            .Should().BeEquivalentTo(["EXP-1", "EXP-4"]);
    }

    [Fact]
    public async Task La_accion_vive_en_los_tramites_y_alcanza_al_expediente()
    {
        // EXP-1 tiene un trámite con "Simplificar"; EXP-2 tiene dos y uno la
        // lleva. Los dos cuentan.
        (await CodigosAsync(new GetExpedientesQuery(Accion: "Simplificar")))
            .Should().BeEquivalentTo(["EXP-1", "EXP-2"]);
    }

    [Fact]
    public async Task Un_expediente_sin_tramites_no_entra_por_accion()
    {
        var codigos = await CodigosAsync(new GetExpedientesQuery(Accion: "Eliminar"));
        codigos.Should().BeEquivalentTo(["EXP-3"]);
        codigos.Should().NotContain("EXP-4", "no tiene trámites, así que no tiene acción");
    }

    [Fact]
    public async Task Los_filtros_se_combinan()
    {
        (await CodigosAsync(new GetExpedientesQuery(
            Institucion: "INPREMA", Analista: "Mario Castejón")))
            .Should().BeEquivalentTo(["EXP-2"]);
    }

    [Fact]
    public async Task Un_filtro_vacio_no_filtra()
    {
        // La vista manda "" cuando el selector está en "— Todas —". Si eso se
        // tomara como un valor, la lista saldría siempre vacía.
        (await CodigosAsync(new GetExpedientesQuery(
            Institucion: "", Analista: "   ", Accion: "")))
            .Should().HaveCount(4);
    }

    [Fact]
    public async Task Las_opciones_salen_ordenadas_y_sin_repetir()
    {
        // El universo completo, no la página: si salieran de las filas cargadas,
        // al filtrar por una institución desaparecerían del selector todas las
        // demás y no habría forma de volver.
        var op = await new GetOpcionesExpedientesQueryHandler(_ctx)
            .Handle(new GetOpcionesExpedientesQuery(), default);

        op.Instituciones.Should().Equal("CONSUCOOP", "INA", "INPREMA");
        op.Encargados.Should().Equal("Mario Castejón", "Zenia Zelaya");
        op.Acciones.Should().Equal("Digitalizar", "Eliminar", "Simplificar");
    }
}
