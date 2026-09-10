using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Siger.Publico.Queries.GetCambiosPublicos;
using Diger.TramitesEstado.Application.Siger.Publico.Queries.GetCategoriasPublicas;
using Diger.TramitesEstado.Application.Siger.Publico.Queries.GetCodigosPublicados;
using Diger.TramitesEstado.Application.Siger.Publico.Queries.GetInstitucionesPublicas;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Publico;

/// <summary>Usuario NO global, con alcance restringido a una sola institución — para probar
/// que la API pública (M-08) no depende de que el alcance por defecto sea global.</summary>
internal sealed class FakeScopedCurrentUser : ICurrentUserService
{
    public Guid?       UserId               => Guid.NewGuid();
    public string?     Nombre               => "test-institucional";
    public string?     Correo               => "empleado@diger.gob.hn";
    public string?     Rol                  => "Empleado";
    public bool        IsAuthenticated      => true;
    public bool        EsGlobal             => false;
    public NivelAlcance NivelAlcance        => NivelAlcance.Institucion;
    public bool        EsSoloLectura        => false;
    public bool        EsSupervisor         => false;
    public bool        EsTecnicoSoporte     => false;
    public string?     ActiveInstitucionId  => "INPREMA";
    public string?     ActiveAreaId         => null;
    public string?     ActiveUnidadId       => null;
    public IReadOnlyCollection<string> InstitucionesAsignadas => ["INPREMA"];
    public bool        PuedeAccederInstitucion(string? institucionId) => institucionId == "INPREMA";
}

public class GetInstitucionesPublicasQueryTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public GetInstitucionesPublicasQueryTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        // A propósito: usuario NO global. La API pública no tiene sesión, así que no puede
        // depender de que el alcance por defecto sea global — ver M-08.
        _ctx = new AppDbContext(opts, new FakeScopedCurrentUser(), NSubstitute.Substitute.For<MediatR.IPublisher>());
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task Handle_SinAlcanceGlobal_DevuelveTodasLasInstitucionesActivas()
    {
        _ctx.Instituciones.Add(Institucion.Crear("INPREMA", "Instituto Nacional de Previsión del Magisterio"));
        _ctx.Instituciones.Add(Institucion.Crear("IHTT", "Instituto Hondureño del Transporte Terrestre"));
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetInstitucionesPublicasQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetInstitucionesPublicasQuery(), CancellationToken.None);

        resultado.Should().HaveCount(2, "IgnoreQueryFilters() debe traer todas, no solo INPREMA (la del usuario de prueba)");
    }

    [Fact]
    public async Task Handle_CalculaConteoDeTramitesPublicadosPorInstitucion()
    {
        _ctx.Instituciones.Add(Institucion.Crear("INPREMA", "Instituto Nacional de Previsión del Magisterio"));
        _ctx.TramitesSiger.Add(new TramiteSiger { Codigo = "1", Nombre = "A", Institucion = "INPREMA", InstitucionId = "INPREMA", Publicado = true });
        _ctx.TramitesSiger.Add(new TramiteSiger { Codigo = "2", Nombre = "B", Institucion = "INPREMA", InstitucionId = "INPREMA", Publicado = true });
        _ctx.TramitesSiger.Add(new TramiteSiger { Codigo = "3", Nombre = "C", Institucion = "INPREMA", InstitucionId = "INPREMA", Publicado = false });
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetInstitucionesPublicasQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetInstitucionesPublicasQuery(), CancellationToken.None);

        resultado.Single(i => i.Id == "INPREMA").ConteoTramitesPublicados.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ExcluyeInstitucionesInactivas()
    {
        var activa = Institucion.Crear("INPREMA", "Instituto Nacional de Previsión del Magisterio");
        var inactiva = Institucion.Crear("IHTT", "Instituto Hondureño del Transporte Terrestre");
        inactiva.Desactivar();
        _ctx.Instituciones.AddRange(activa, inactiva);
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetInstitucionesPublicasQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetInstitucionesPublicasQuery(), CancellationToken.None);

        resultado.Should().ContainSingle(i => i.Id == "INPREMA");
    }
}

public class GetCategoriasPublicasQueryTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public GetCategoriasPublicasQueryTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _ctx = new AppDbContext(opts, new FakeGlobalCurrentUser(), NSubstitute.Substitute.For<MediatR.IPublisher>());
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task Handle_DevuelveCategoriasActivasOrdenadasPorOrden_ConConteo()
    {
        var salud = new CategoriaTramite { Nombre = "Salud", Orden = 20 };
        _ctx.CategoriasTramite.Add(salud);
        _ctx.CategoriasTramite.Add(new CategoriaTramite { Nombre = "Educación", Orden = 10 });
        _ctx.CategoriasTramite.Add(new CategoriaTramite { Nombre = "Inactiva", Orden = 5, Activo = false });
        await _ctx.SaveChangesAsync(CancellationToken.None);
        _ctx.TramitesSiger.Add(new TramiteSiger { Codigo = "1", Nombre = "A", Institucion = "X", CategoriaId = salud.Id, Publicado = true });
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCategoriasPublicasQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetCategoriasPublicasQuery(), CancellationToken.None);

        resultado.Should().HaveCount(2);
        resultado[0].Nombre.Should().Be("Educación", "Orden=10 va antes que Orden=20");
        resultado.Single(c => c.Id == salud.Id).ConteoTramitesPublicados.Should().Be(1);
    }
}

public class GetCodigosPublicadosQueryTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public GetCodigosPublicadosQueryTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _ctx = new AppDbContext(opts, new FakeGlobalCurrentUser(), NSubstitute.Substitute.For<MediatR.IPublisher>());
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task Handle_SoloDevuelveCodigosDeTramitesPublicados()
    {
        _ctx.TramitesSiger.Add(new TramiteSiger { Codigo = "PUB-1", Nombre = "A", Institucion = "X", Publicado = true });
        _ctx.TramitesSiger.Add(new TramiteSiger { Codigo = "NOPUB-1", Nombre = "B", Institucion = "X", Publicado = false });
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCodigosPublicadosQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetCodigosPublicadosQuery(), CancellationToken.None);

        resultado.Should().BeEquivalentTo(["PUB-1"]);
    }
}

public class GetCambiosPublicosQueryTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public GetCambiosPublicosQueryTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _ctx = new AppDbContext(opts, new FakeGlobalCurrentUser(), NSubstitute.Substitute.For<MediatR.IPublisher>());
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task Handle_DevuelveSoloPublicadosModificadosDesdeLaFecha()
    {
        var corte = new DateTime(2026, 6, 1);
        _ctx.TramitesSiger.Add(new TramiteSiger { Codigo = "RECIENTE", Nombre = "A", Institucion = "X", Publicado = true, CreatedAt = new DateTime(2026, 1, 1), UpdatedAt = new DateTime(2026, 7, 1) });
        _ctx.TramitesSiger.Add(new TramiteSiger { Codigo = "VIEJO", Nombre = "B", Institucion = "X", Publicado = true, CreatedAt = new DateTime(2025, 1, 1), UpdatedAt = new DateTime(2025, 2, 1) });
        _ctx.TramitesSiger.Add(new TramiteSiger { Codigo = "NO-PUBLICADO", Nombre = "C", Institucion = "X", Publicado = false, CreatedAt = new DateTime(2026, 1, 1), UpdatedAt = new DateTime(2026, 7, 1) });
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCambiosPublicosQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetCambiosPublicosQuery(corte), CancellationToken.None);

        resultado.Codigos.Should().BeEquivalentTo(["RECIENTE"]);
    }

    [Fact]
    public async Task Handle_SinUpdatedAt_UsaCreatedAtComoRespaldo()
    {
        var corte = new DateTime(2026, 6, 1);
        _ctx.TramitesSiger.Add(new TramiteSiger { Codigo = "NUEVO-SIN-TOCAR", Nombre = "A", Institucion = "X", Publicado = true, CreatedAt = new DateTime(2026, 7, 1), UpdatedAt = null });
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCambiosPublicosQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetCambiosPublicosQuery(corte), CancellationToken.None);

        resultado.Codigos.Should().Contain("NUEVO-SIN-TOCAR");
    }
}
