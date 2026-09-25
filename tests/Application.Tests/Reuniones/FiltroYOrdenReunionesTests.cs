using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Reuniones.Queries.GetReuniones;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

/// <summary>
/// Filtro por columna y orden configurable en el listado de reuniones.
/// El filtro corre en el servidor sobre todas las reuniones —no sobre la página
/// visible— y cualquier criterio de orden cierra con la última creación como
/// desempate, para que la paginación no baile entre páginas cuando hay empates.
/// </summary>
public class FiltroYOrdenReunionesTests
{
    private sealed class UsuarioGlobal : ICurrentUserService
    {
        public Guid?        UserId               => Guid.NewGuid();
        public string?      Nombre               => "test";
        public string?      Correo               => "test@diger.gob.hn";
        public string?      Rol                  => "Administrador";
        public bool         IsAuthenticated      => true;
        public bool         EsGlobal             => true;
        public NivelAlcance NivelAlcance         => NivelAlcance.Global;
        public bool         EsSoloLectura        => false;
        public bool         EsSupervisor         => true;
        public bool         EsTecnicoSoporte     => true;
        public bool         EsJefeDeArea         => false;
        public bool         EsPmo                => false;
        public string?      ActiveInstitucionId  => null;
        public string?      ActiveAreaId         => null;
        public string?      ActiveUnidadId       => null;
        public IReadOnlyCollection<string> InstitucionesAsignadas => [];
        public bool PuedeAccederInstitucion(string? institucionId) => true;
    }

    private static AppDbContext NuevoCtx() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new UsuarioGlobal(), NSubstitute.Substitute.For<MediatR.IPublisher>());

    /// <summary>Siembra una reunión fijándole el CreatedAt (la auditoría lo pisa al insertar).</summary>
    private static async Task<int> SembrarAsync(AppDbContext ctx, string titulo, DateOnly? fecha, DateTime creadaEl,
                                                string? institucion = "CNBS", string? tipo = "Taller", int asistentes = 0)
    {
        var r = Reunion.Crear(titulo);
        r.Fecha = fecha;
        r.InstitucionId = institucion is null ? null : "7";
        r.Institucion = institucion;
        r.Tipo = tipo;
        ctx.Reuniones.Add(r);
        await ctx.SaveChangesAsync();
        for (var i = 0; i < asistentes; i++)
            r.RegistrarAsistente($"Asistente {i} de {titulo}", null, null, null, null, null);
        r.CreatedAt = creadaEl;
        await ctx.SaveChangesAsync();
        return r.Id;
    }

    private static Task<Application.Common.Models.PagedResult<ReunionListItemDto>> Listar(
        AppDbContext ctx, GetReunionesQuery q) =>
        new GetReunionesQueryHandler(ctx).Handle(q, CancellationToken.None);

    // ── Filtros por columna ────────────────────────────────────────────────

    [Fact]
    public async Task FiltraPorInstitucion()
    {
        await using var ctx = NuevoCtx();
        await SembrarAsync(ctx, "Con CNBS", new DateOnly(2026, 8, 1), new DateTime(2026, 9, 1), institucion: "CNBS");
        await SembrarAsync(ctx, "Con SENASA", new DateOnly(2026, 8, 2), new DateTime(2026, 9, 2), institucion: "SENASA");

        var res = await Listar(ctx, new GetReunionesQuery(Institucion: "CNBS"));

        res.Total.Should().Be(1);
        res.Items.Single().Titulo.Should().Be("Con CNBS");
    }

    [Fact]
    public async Task FiltraPorTipo()
    {
        await using var ctx = NuevoCtx();
        await SembrarAsync(ctx, "Un taller", new DateOnly(2026, 8, 1), new DateTime(2026, 9, 1), tipo: "Taller");
        await SembrarAsync(ctx, "Una capacitación", new DateOnly(2026, 8, 2), new DateTime(2026, 9, 2), tipo: "Capacitación");

        var res = await Listar(ctx, new GetReunionesQuery(Tipo: "Capacitación"));

        res.Total.Should().Be(1);
        res.Items.Single().Titulo.Should().Be("Una capacitación");
    }

    [Fact]
    public async Task FiltraPorTituloParcial()
    {
        await using var ctx = NuevoCtx();
        await SembrarAsync(ctx, "Taller de modelado CONSUCOOP", new DateOnly(2026, 8, 1), new DateTime(2026, 9, 1));
        await SembrarAsync(ctx, "Reunión de seguimiento", new DateOnly(2026, 8, 2), new DateTime(2026, 9, 2));

        var res = await Listar(ctx, new GetReunionesQuery(Titulo: "modelado"));

        res.Total.Should().Be(1);
        res.Items.Single().Titulo.Should().Be("Taller de modelado CONSUCOOP");
    }

    [Fact]
    public async Task FiltraPorRangoDeFechaDeReunion_ConAmbosExtremosIncluidos()
    {
        await using var ctx = NuevoCtx();
        await SembrarAsync(ctx, "Antes", new DateOnly(2026, 7, 31), new DateTime(2026, 9, 1));
        await SembrarAsync(ctx, "Borde inicial", new DateOnly(2026, 8, 1), new DateTime(2026, 9, 2));
        await SembrarAsync(ctx, "En medio", new DateOnly(2026, 8, 15), new DateTime(2026, 9, 3));
        await SembrarAsync(ctx, "Borde final", new DateOnly(2026, 8, 31), new DateTime(2026, 9, 4));
        await SembrarAsync(ctx, "Después", new DateOnly(2026, 9, 1), new DateTime(2026, 9, 5));

        var res = await Listar(ctx, new GetReunionesQuery(
            FechaDesde: new DateOnly(2026, 8, 1), FechaHasta: new DateOnly(2026, 8, 31)));

        res.Total.Should().Be(3);
        res.Items.Select(i => i.Titulo).Should().BeEquivalentTo(["Borde inicial", "En medio", "Borde final"]);
    }

    [Fact]
    public async Task ReunionSinFecha_QuedaFueraDelFiltroPorRango()
    {
        await using var ctx = NuevoCtx();
        await SembrarAsync(ctx, "Sin fecha", null, new DateTime(2026, 9, 1));
        await SembrarAsync(ctx, "Con fecha", new DateOnly(2026, 8, 15), new DateTime(2026, 9, 2));

        var res = await Listar(ctx, new GetReunionesQuery(FechaDesde: new DateOnly(2026, 8, 1)));

        res.Total.Should().Be(1);
        res.Items.Single().Titulo.Should().Be("Con fecha");
    }

    [Fact]
    public async Task FiltraPorRangoDeFechaDeCreacion_ConAmbosExtremosIncluidos()
    {
        await using var ctx = NuevoCtx();
        await SembrarAsync(ctx, "Registrada antes", new DateOnly(2026, 1, 1), new DateTime(2026, 8, 31, 23, 59, 0));
        await SembrarAsync(ctx, "Registrada el borde inicial", new DateOnly(2026, 1, 2), new DateTime(2026, 9, 1, 0, 1, 0));
        await SembrarAsync(ctx, "Registrada en medio", new DateOnly(2026, 1, 3), new DateTime(2026, 9, 15, 10, 0, 0));
        await SembrarAsync(ctx, "Registrada el borde final", new DateOnly(2026, 1, 4), new DateTime(2026, 9, 30, 8, 0, 0));
        await SembrarAsync(ctx, "Registrada después", new DateOnly(2026, 1, 5), new DateTime(2026, 10, 1, 0, 1, 0));

        var res = await Listar(ctx, new GetReunionesQuery(
            CreadaDesde: new DateOnly(2026, 9, 1), CreadaHasta: new DateOnly(2026, 9, 30)));

        res.Total.Should().Be(3);
        res.Items.Select(i => i.Titulo).Should().BeEquivalentTo(
            ["Registrada el borde inicial", "Registrada en medio", "Registrada el borde final"]);
    }

    [Fact]
    public async Task ElUltimoDiaDelRangoDeCreacionCuentaCompleto_NoSoloHastaLaMedianoche()
    {
        await using var ctx = NuevoCtx();
        // CreatedAt lleva hora: comparar contra la fecha pelada dejaría fuera todo lo
        // registrado después de las 00:00 del último día.
        await SembrarAsync(ctx, "Registrada al final del día", new DateOnly(2026, 1, 1), new DateTime(2026, 9, 30, 23, 59, 59));

        var res = await Listar(ctx, new GetReunionesQuery(CreadaHasta: new DateOnly(2026, 9, 30)));

        res.Total.Should().Be(1);
    }

    [Fact]
    public async Task LosDosRangosDeFechaSeCombinan()
    {
        await using var ctx = NuevoCtx();
        // Reunión vieja cargada tarde: es la que sólo aparece cruzando ambos rangos.
        await SembrarAsync(ctx, "Reunión de junio registrada en septiembre", new DateOnly(2026, 6, 10), new DateTime(2026, 9, 20));
        await SembrarAsync(ctx, "Reunión de junio registrada en junio", new DateOnly(2026, 6, 11), new DateTime(2026, 6, 12));
        await SembrarAsync(ctx, "Reunión de septiembre registrada en septiembre", new DateOnly(2026, 9, 5), new DateTime(2026, 9, 21));

        var res = await Listar(ctx, new GetReunionesQuery(
            FechaDesde: new DateOnly(2026, 6, 1), FechaHasta: new DateOnly(2026, 6, 30),
            CreadaDesde: new DateOnly(2026, 9, 1)));

        res.Total.Should().Be(1);
        res.Items.Single().Titulo.Should().Be("Reunión de junio registrada en septiembre");
    }

    [Fact]
    public async Task ElListadoExponeLaFechaDeRegistroDeCadaReunion()
    {
        await using var ctx = NuevoCtx();
        var creadaEl = new DateTime(2026, 9, 24, 16, 48, 10);
        await SembrarAsync(ctx, "Con registro", new DateOnly(2026, 6, 10), creadaEl);

        var res = await Listar(ctx, new GetReunionesQuery());

        res.Items.Single().CreadaEl.Should().Be(creadaEl);
    }

    [Fact]
    public async Task CombinaVariosFiltros()
    {
        await using var ctx = NuevoCtx();
        await SembrarAsync(ctx, "Taller CNBS agosto", new DateOnly(2026, 8, 10), new DateTime(2026, 9, 1), "CNBS", "Taller");
        await SembrarAsync(ctx, "Taller SENASA agosto", new DateOnly(2026, 8, 11), new DateTime(2026, 9, 2), "SENASA", "Taller");
        await SembrarAsync(ctx, "Reunión CNBS agosto", new DateOnly(2026, 8, 12), new DateTime(2026, 9, 3), "CNBS", "Reunión técnica");
        await SembrarAsync(ctx, "Taller CNBS julio", new DateOnly(2026, 7, 20), new DateTime(2026, 9, 4), "CNBS", "Taller");

        var res = await Listar(ctx, new GetReunionesQuery(
            Institucion: "CNBS", Tipo: "Taller", FechaDesde: new DateOnly(2026, 8, 1)));

        res.Total.Should().Be(1);
        res.Items.Single().Titulo.Should().Be("Taller CNBS agosto");
    }

    [Fact]
    public async Task ElFiltroAlcanzaReunionesFueraDeLaPrimeraPagina()
    {
        await using var ctx = NuevoCtx();
        // 25 reuniones de relleno; la buscada se creó de primera, así que con el orden
        // por última creación queda al fondo, fuera de la página 1 (20 filas).
        await SembrarAsync(ctx, "La que busco", new DateOnly(2026, 1, 5), new DateTime(2026, 1, 1), "SENASA");
        for (var i = 1; i <= 25; i++)
            await SembrarAsync(ctx, $"Relleno {i}", new DateOnly(2026, 8, 1), new DateTime(2026, 9, 1).AddMinutes(i), "CNBS");

        var res = await Listar(ctx, new GetReunionesQuery(Institucion: "SENASA"));

        res.Total.Should().Be(1);
        res.Items.Single().Titulo.Should().Be("La que busco");
    }

    // ── Orden configurable ─────────────────────────────────────────────────

    [Fact]
    public async Task PorDefecto_OrdenaPorFechaYHoraDeCreacionDescendente()
    {
        await using var ctx = NuevoCtx();
        await SembrarAsync(ctx, "Primera", new DateOnly(2026, 8, 1), new DateTime(2026, 9, 10, 8, 0, 0));
        await SembrarAsync(ctx, "Segunda", new DateOnly(2026, 8, 2), new DateTime(2026, 9, 10, 15, 30, 0));
        await SembrarAsync(ctx, "Tercera", new DateOnly(2026, 8, 3), new DateTime(2026, 9, 11, 7, 0, 0));

        var res = await Listar(ctx, new GetReunionesQuery());

        res.Items.Select(i => i.Titulo).Should().ContainInOrder("Tercera", "Segunda", "Primera");
    }

    [Fact]
    public async Task OrdenaPorFechaDeReunionAscendente()
    {
        await using var ctx = NuevoCtx();
        await SembrarAsync(ctx, "Marzo", new DateOnly(2026, 3, 1), new DateTime(2026, 9, 3));
        await SembrarAsync(ctx, "Enero", new DateOnly(2026, 1, 1), new DateTime(2026, 9, 1));
        await SembrarAsync(ctx, "Febrero", new DateOnly(2026, 2, 1), new DateTime(2026, 9, 2));

        var res = await Listar(ctx, new GetReunionesQuery(Ordenar: OrdenReuniones.Fecha, Desc: false));

        res.Items.Select(i => i.Titulo).Should().ContainInOrder("Enero", "Febrero", "Marzo");
    }

    [Fact]
    public async Task OrdenaPorTituloAscendente()
    {
        await using var ctx = NuevoCtx();
        await SembrarAsync(ctx, "Zapato", new DateOnly(2026, 8, 1), new DateTime(2026, 9, 3));
        await SembrarAsync(ctx, "Arbol", new DateOnly(2026, 8, 2), new DateTime(2026, 9, 2));
        await SembrarAsync(ctx, "Mesa", new DateOnly(2026, 8, 3), new DateTime(2026, 9, 1));

        var res = await Listar(ctx, new GetReunionesQuery(Ordenar: OrdenReuniones.Titulo, Desc: false));

        res.Items.Select(i => i.Titulo).Should().ContainInOrder("Arbol", "Mesa", "Zapato");
    }

    [Fact]
    public async Task OrdenaPorInstitucionAscendente()
    {
        await using var ctx = NuevoCtx();
        await SembrarAsync(ctx, "Tercera", new DateOnly(2026, 8, 1), new DateTime(2026, 9, 3), "SENASA");
        await SembrarAsync(ctx, "Primera", new DateOnly(2026, 8, 2), new DateTime(2026, 9, 2), "BANHPROVI");
        await SembrarAsync(ctx, "Segunda", new DateOnly(2026, 8, 3), new DateTime(2026, 9, 1), "CNBS");

        var res = await Listar(ctx, new GetReunionesQuery(Ordenar: OrdenReuniones.Institucion, Desc: false));

        res.Items.Select(i => i.Titulo).Should().ContainInOrder("Primera", "Segunda", "Tercera");
    }

    [Fact]
    public async Task OrdenaPorCantidadDeAsistentesDescendente()
    {
        await using var ctx = NuevoCtx();
        await SembrarAsync(ctx, "Pocos", new DateOnly(2026, 8, 1), new DateTime(2026, 9, 1), asistentes: 1);
        await SembrarAsync(ctx, "Muchos", new DateOnly(2026, 8, 2), new DateTime(2026, 9, 2), asistentes: 5);
        await SembrarAsync(ctx, "Medios", new DateOnly(2026, 8, 3), new DateTime(2026, 9, 3), asistentes: 3);

        var res = await Listar(ctx, new GetReunionesQuery(Ordenar: OrdenReuniones.Asistentes, Desc: true));

        res.Items.Select(i => i.Titulo).Should().ContainInOrder("Muchos", "Medios", "Pocos");
    }

    [Fact]
    public async Task ConEmpates_DesempataPorUltimaCreacionParaQueLaPaginacionNoBaile()
    {
        await using var ctx = NuevoCtx();
        // Misma fecha de reunión en las tres: el desempate lo decide la creación.
        var mismaFecha = new DateOnly(2026, 8, 15);
        await SembrarAsync(ctx, "Creada temprano", mismaFecha, new DateTime(2026, 9, 1, 8, 0, 0));
        await SembrarAsync(ctx, "Creada tarde", mismaFecha, new DateTime(2026, 9, 1, 18, 0, 0));
        await SembrarAsync(ctx, "Creada al mediodía", mismaFecha, new DateTime(2026, 9, 1, 12, 0, 0));

        var res = await Listar(ctx, new GetReunionesQuery(Ordenar: OrdenReuniones.Fecha, Desc: true));

        res.Items.Select(i => i.Titulo).Should().ContainInOrder(
            "Creada tarde", "Creada al mediodía", "Creada temprano");
    }

    [Fact]
    public async Task ElOrdenSeMantieneAlCambiarDePagina_SinRepetirNiPerderFilas()
    {
        await using var ctx = NuevoCtx();
        for (var i = 1; i <= 25; i++)
            await SembrarAsync(ctx, $"Reunión {i:00}", new DateOnly(2026, 8, 1), new DateTime(2026, 9, 1).AddMinutes(i));

        var p1 = await Listar(ctx, new GetReunionesQuery(Ordenar: OrdenReuniones.Fecha, Page: 1, Size: 20));
        var p2 = await Listar(ctx, new GetReunionesQuery(Ordenar: OrdenReuniones.Fecha, Page: 2, Size: 20));

        p1.Items.Should().HaveCount(20);
        p2.Items.Should().HaveCount(5);
        p1.Items.Select(i => i.Id).Intersect(p2.Items.Select(i => i.Id)).Should().BeEmpty();
        p1.Items.Concat(p2.Items).Select(i => i.Id).Distinct().Should().HaveCount(25);
    }

    // ── Opciones de los desplegables ───────────────────────────────────────

    [Fact]
    public async Task LasOpcionesDeFiltroSalenDeTodasLasReuniones_SinNulosNiRepetidos()
    {
        await using var ctx = NuevoCtx();
        await SembrarAsync(ctx, "A", new DateOnly(2026, 8, 1), new DateTime(2026, 9, 1), "SENASA", "Taller");
        await SembrarAsync(ctx, "B", new DateOnly(2026, 8, 2), new DateTime(2026, 9, 2), "CNBS", "Taller");
        await SembrarAsync(ctx, "C", new DateOnly(2026, 8, 3), new DateTime(2026, 9, 3), "CNBS", "Capacitación");
        await SembrarAsync(ctx, "D", new DateOnly(2026, 8, 4), new DateTime(2026, 9, 4), null, null);

        var res = await new GetOpcionesFiltroReunionesQueryHandler(ctx)
            .Handle(new GetOpcionesFiltroReunionesQuery(), CancellationToken.None);

        res.Instituciones.Should().ContainInOrder("CNBS", "SENASA").And.HaveCount(2);
        res.Tipos.Should().ContainInOrder("Capacitación", "Taller").And.HaveCount(2);
    }
}
