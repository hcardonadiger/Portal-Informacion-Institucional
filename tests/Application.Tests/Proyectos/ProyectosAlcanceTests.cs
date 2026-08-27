using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

/// <summary>
/// El filtro de alcance de <see cref="Proyecto"/>. Hasta el 2026-08-23 esta entidad no tenía
/// filtro: cualquiera con <c>Proyectos.Ver</c> —incluidos los usuarios de instituciones externas
/// con rol Empleado— veía el portafolio completo.
///
/// <para>El contexto se arma por prueba porque el filtro se resuelve con los valores que
/// <see cref="ICurrentUserService"/> tenía al construir el <see cref="AppDbContext"/>.</para>
/// </summary>
public class ProyectosAlcanceTests : IDisposable
{
    private readonly List<AppDbContext> _contextos = [];
    private readonly string _bd = Guid.NewGuid().ToString();

    private static readonly Guid Duenio   = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Invitado = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private AppDbContext ContextoDe(
        string? institucion, string? area = null, string? unidad = null,
        NivelAlcance nivel = NivelAlcance.Institucion, bool global = false, Guid? usuario = null)
    {
        var u = Substitute.For<ICurrentUserService>();
        u.EsGlobal.Returns(global);
        u.NivelAlcance.Returns(nivel);
        u.ActiveInstitucionId.Returns(institucion);
        u.ActiveAreaId.Returns(area);
        u.ActiveUnidadId.Returns(unidad);
        u.UserId.Returns(usuario);

        var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_bd).Options,
            u, Substitute.For<MediatR.IPublisher>());
        _contextos.Add(ctx);
        return ctx;
    }

    /// <summary>Siembra con un contexto global, que no filtra nada.</summary>
    private void Sembrar(params (string codigo, string? inst, string? area, string? unidad, Guid? resp)[] filas)
    {
        var ctx = ContextoDe(null, global: true, nivel: NivelAlcance.Global);
        foreach (var (codigo, inst, area, unidad, resp) in filas)
        {
            var p = Proyecto.Crear(codigo, $"Proyecto {codigo}");
            p.InstitucionId = inst;
            p.AreaId        = area;
            p.UnidadId      = unidad;
            p.ResponsableId = resp;
            ctx.Proyectos.Add(p);
        }
        ctx.SaveChanges();
    }

    private static Task<string[]> CodigosAsync(AppDbContext ctx) =>
        ctx.Proyectos.AsNoTracking().OrderBy(p => p.Codigo).Select(p => p.Codigo).ToArrayAsync();

    [Fact]
    public async Task UnUsuarioDeOtraInstitucionNoVeElPortafolioDeDiger()
    {
        Sembrar(
            ("PRY-2026-01", "DIGER",     null, null, null),
            ("PRY-2026-02", "DIGER",     null, null, null),
            ("PRY-2026-03", "CONSUCOOP", null, null, null));

        var deConsucoop = await CodigosAsync(ContextoDe("CONSUCOOP"));

        deConsucoop.Should().Equal("PRY-2026-03");
    }

    [Fact]
    public async Task ElAlcanceGlobalSigueViendoTodo()
    {
        Sembrar(
            ("PRY-2026-01", "DIGER",     null, null, null),
            ("PRY-2026-02", "CONSUCOOP", null, null, null));

        var todos = await CodigosAsync(ContextoDe(null, global: true, nivel: NivelAlcance.Global));

        todos.Should().Equal("PRY-2026-01", "PRY-2026-02");
    }

    [Fact]
    public async Task LosTransversalesLosVeTodaLaInstitucionYLosDeOtraAreaNo()
    {
        Sembrar(
            ("PRY-2026-01", "DIGER", null,     null, null),   // transversal
            ("PRY-2026-02", "DIGER", "GOBDIG", null, null),
            ("PRY-2026-03", "DIGER", "OTRA",   null, null));

        var deGobdig = await CodigosAsync(
            ContextoDe("DIGER", area: "GOBDIG", nivel: NivelAlcance.Area));

        deGobdig.Should().Equal("PRY-2026-01", "PRY-2026-02");
    }

    [Fact]
    public async Task ElNivelUnidadSoloVeSuUnidadYLoTransversal()
    {
        Sembrar(
            ("PRY-2026-01", "DIGER", "GOBDIG", null,     null),   // sin unidad: transversal al área
            ("PRY-2026-02", "DIGER", "GOBDIG", "DITRA",  null),
            ("PRY-2026-03", "DIGER", "GOBDIG", "PRYESP", null));

        var deDitra = await CodigosAsync(
            ContextoDe("DIGER", area: "GOBDIG", unidad: "DITRA", nivel: NivelAlcance.Unidad));

        deDitra.Should().Equal("PRY-2026-01", "PRY-2026-02");
    }

    [Fact]
    public async Task ElResponsableVeSuProyectoAunqueCaigaFueraDeSuAlcance()
    {
        Sembrar(
            ("PRY-2026-01", "DIGER",     null, null, Duenio),   // de otra institución que la suya
            ("PRY-2026-02", "DIGER",     null, null, null));

        var suyo = await CodigosAsync(ContextoDe("CONSUCOOP", usuario: Duenio));

        // Sin esta excepción sería responsable de un proyecto que no puede abrir.
        suyo.Should().Equal("PRY-2026-01");
    }

    [Fact]
    public async Task ElInteresadoVeSuProyectoAunqueCaigaFueraDeSuAlcance()
    {
        Sembrar(
            ("PRY-2026-01", "DIGER", null, null, null),
            ("PRY-2026-02", "DIGER", null, null, null));

        // El interesado se registra sobre el primero, con un contexto global.
        var siembra = ContextoDe(null, global: true, nivel: NivelAlcance.Global);
        var proyecto = await siembra.Proyectos.SingleAsync(p => p.Codigo == "PRY-2026-01");
        siembra.ProyectoInteresados.Add(InteresadoProyecto.Crear(
            proyecto.Id, Invitado, "Invitado de otra institución",
            RolInteresado.ContraparteTecnica, "siembra"));
        await siembra.SaveChangesAsync();

        var visibles = await CodigosAsync(ContextoDe("CONSUCOOP", usuario: Invitado));

        // Es la razón de que InteresadoProyecto.UsuarioId sea obligatorio: sin cuenta no hay a
        // quién abrirle el proyecto, y registrar al interesado no significaría nada.
        visibles.Should().Equal("PRY-2026-01");
    }

    [Fact]
    public async Task QuitarAlInteresadoLeQuitaElAcceso()
    {
        Sembrar(("PRY-2026-01", "DIGER", null, null, null));

        var siembra = ContextoDe(null, global: true, nivel: NivelAlcance.Global);
        var proyecto = await siembra.Proyectos.SingleAsync();
        var registro = InteresadoProyecto.Crear(
            proyecto.Id, Invitado, "Invitado", RolInteresado.Ejecutor, "siembra");
        siembra.ProyectoInteresados.Add(registro);
        await siembra.SaveChangesAsync();

        (await CodigosAsync(ContextoDe("CONSUCOOP", usuario: Invitado))).Should().Equal("PRY-2026-01");

        siembra.ProyectoInteresados.Remove(registro);
        await siembra.SaveChangesAsync();

        (await CodigosAsync(ContextoDe("CONSUCOOP", usuario: Invitado))).Should().BeEmpty();
    }

    [Fact]
    public async Task UnProyectoSinInstitucionNoSeLeEscapaANadieFueraDeGlobal()
    {
        // Los proyectos anteriores al anclaje quedaban con InstitucionId nulo. Que no calcen
        // con nadie es preferible a que se vean desde cualquier institución.
        Sembrar(("PRY-2026-01", null, null, null, null));

        (await CodigosAsync(ContextoDe("DIGER"))).Should().BeEmpty();
        (await CodigosAsync(ContextoDe(null, global: true, nivel: NivelAlcance.Global)))
            .Should().Equal("PRY-2026-01");
    }

    public void Dispose()
    {
        foreach (var c in _contextos) c.Dispose();
    }
}
