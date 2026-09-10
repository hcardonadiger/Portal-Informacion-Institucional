using Diger.TramitesEstado.Application.Siger.Publico.Queries.GetTramitePublico;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Publico;

public class GetTramitePublicoQueryTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public GetTramitePublicoQueryTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeGlobalCurrentUser(), NSubstitute.Substitute.For<MediatR.IPublisher>());
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task Handle_CodigoInexistente_DevuelveNull()
    {
        var handler = new GetTramitePublicoQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetTramitePublicoQuery("NO-EXISTE"), CancellationToken.None);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task Handle_TramiteNoPublicado_DevuelveNull()
    {
        _ctx.TramitesSiger.Add(new TramiteSiger { Codigo = "100-001", Nombre = "Sin publicar", Institucion = "INPREMA", Publicado = false });
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTramitePublicoQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetTramitePublicoQuery("100-001"), CancellationToken.None);

        resultado.Should().BeNull("una API pública no distingue 'no existe' de 'no publicado'");
    }

    [Fact]
    public async Task Handle_TramitePublicado_DevuelveFichaConColeccionesHijas()
    {
        var t = new TramiteSiger { Codigo = "100-001", Nombre = "Trámite completo", Institucion = "INPREMA", InstitucionId = "INPREMA", Publicado = true };
        t.Pasos.Add(new PasoSiger { NumeroPaso = 2, Descripcion = "Segundo paso" });
        t.Pasos.Add(new PasoSiger { NumeroPaso = 1, Descripcion = "Primer paso" });
        t.Requisitos.Add(new RequisitoSiger { Numero = 1, Requisito = "DNI" });
        t.LugaresAtencion.Add(new LugarAtencionSiger { Numero = 1, Lugar = "Oficina central" });
        t.Enlaces.Add(new EnlaceSiger { Numero = 1, Url = "https://ejemplo.gob.hn" });
        _ctx.TramitesSiger.Add(t);
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTramitePublicoQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetTramitePublicoQuery("100-001"), CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.Pasos.Should().HaveCount(2);
        resultado.Pasos[0].Numero.Should().Be(1, "los pasos van ordenados por número");
        resultado.Requisitos.Should().ContainSingle(r => r.Requisito == "DNI");
        resultado.LugaresAtencion.Should().ContainSingle(l => l.Lugar == "Oficina central");
        resultado.Enlaces.Should().ContainSingle(e => e.Url == "https://ejemplo.gob.hn");
    }

    [Fact]
    public async Task Handle_UltimaRevision_UsaUpdatedAtCuandoExiste()
    {
        var actualizado = new DateTime(2026, 6, 1);
        var t = new TramiteSiger
        {
            Codigo = "100-001", Nombre = "Con revisión", Institucion = "INPREMA", Publicado = true,
            CreatedAt = new DateTime(2025, 1, 1),
            UltimaModificacion = new DateTime(2025, 6, 1),
            UpdatedAt = actualizado
        };
        _ctx.TramitesSiger.Add(t);
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTramitePublicoQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetTramitePublicoQuery("100-001"), CancellationToken.None);

        resultado!.UltimaRevision.Should().Be(actualizado, "M-05: UpdatedAt manda sobre el campo editable del formulario");
    }

    [Fact]
    public async Task Handle_UltimaRevision_SinUpdatedAt_UsaUltimaModificacionLegado()
    {
        var legado = new DateTime(2022, 9, 1);
        var t = new TramiteSiger
        {
            Codigo = "100-001", Nombre = "Legado", Institucion = "INPREMA", Publicado = true,
            CreatedAt = new DateTime(2020, 1, 1),
            UltimaModificacion = legado,
            UpdatedAt = null
        };
        _ctx.TramitesSiger.Add(t);
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTramitePublicoQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetTramitePublicoQuery("100-001"), CancellationToken.None);

        resultado!.UltimaRevision.Should().Be(legado);
    }
}
