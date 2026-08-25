using Diger.TramitesEstado.Application.Common.Models;
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

    /// <summary>Host fijo y conocido para que las direcciones compuestas se puedan afirmar
    /// letra por letra. En producción sale de la sección «Sol» de appsettings.</summary>
    private static readonly Microsoft.Extensions.Options.IOptions<SolOptions> Sol =
        Microsoft.Extensions.Options.Options.Create(new SolOptions { UrlBase = "https://sol.gob.hn" });

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
        var handler = new GetTramitePublicoQueryHandler(_ctx, Sol);
        var resultado = await handler.Handle(new GetTramitePublicoQuery("NO-EXISTE"), CancellationToken.None);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task Handle_TramiteNoPublicado_DevuelveNull()
    {
        _ctx.TramitesSiger.Add(new TramiteSiger { Codigo = "100-001", Nombre = "Sin publicar", Institucion = "INPREMA", Publicado = false });
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTramitePublicoQueryHandler(_ctx, Sol);
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

        var handler = new GetTramitePublicoQueryHandler(_ctx, Sol);
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

        var handler = new GetTramitePublicoQueryHandler(_ctx, Sol);
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

        var handler = new GetTramitePublicoQueryHandler(_ctx, Sol);
        var resultado = await handler.Handle(new GetTramitePublicoQuery("100-001"), CancellationToken.None);

        resultado!.UltimaRevision.Should().Be(legado);
    }

    // ── La dirección en SOL (Fase 7) ──────────────────────────────────────────

    /// <summary>
    /// El campo <c>solUrl</c> de la API <b>sigue siendo una URL absoluta</b> aunque la ficha ya
    /// solo guarde el tramo. Es el riesgo que el plan marcó para esta fase: si la API pasara a
    /// emitir el tramo suelto, HondurasÁgil pintaría enlaces relativos contra su propio dominio y
    /// los botones de «hacer el trámite en línea» llevarían a ninguna parte, sin error en ningún
    /// registro.
    /// </summary>
    [Fact]
    public async Task Handle_ConTramo_ComponeLaDireccionAbsolutaConLaRutaDeLaInstitucion()
    {
        _ctx.Instituciones.Add(Institucion.Crear("CONSUCOOP", "Consejo Supervisor de Cooperativas"));
        _ctx.TramitesSiger.Add(new TramiteSiger
        {
            Codigo = "506-010", Nombre = "Licencia", Institucion = "CONSUCOOP", InstitucionId = "CONSUCOOP",
            Publicado = true, EstaEnSol = true, SolTramo = "licencia-de-operacion"
        });
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTramitePublicoQueryHandler(_ctx, Sol);
        var resultado = await handler.Handle(new GetTramitePublicoQuery("506-010"), CancellationToken.None);

        resultado!.SolUrl.Should().Be("https://sol.gob.hn/CONSUCOOP/licencia-de-operacion");
    }

    /// <summary>Corregir la ruta de la institución cambia la dirección de todos sus trámites a la
    /// vez, que es justo para lo que existe D-20.</summary>
    [Fact]
    public async Task Handle_ConRutaCorregida_LaUsaEnLugarDeLaLlave()
    {
        var inst = Institucion.Crear("CANATURHIHT", "CANATURH / IHT");
        inst.FijarRutaSol("canaturh");
        _ctx.Instituciones.Add(inst);
        _ctx.TramitesSiger.Add(new TramiteSiger
        {
            Codigo = "700-001", Nombre = "Registro", Institucion = "CANATURH / IHT", InstitucionId = "CANATURHIHT",
            Publicado = true, EstaEnSol = true, SolTramo = "registro"
        });
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTramitePublicoQueryHandler(_ctx, Sol);
        var resultado = await handler.Handle(new GetTramitePublicoQuery("700-001"), CancellationToken.None);

        resultado!.SolUrl.Should().Be("https://sol.gob.hn/canaturh/registro",
            "la llave dice CANATURHIHT, pero la ruta real de SOL es otra");
    }

    /// <summary>Sin tramo se emite la dirección heredada tal cual (D-14).</summary>
    [Fact]
    public async Task Handle_SinTramo_EmiteLaDireccionHeredadaSinTocarla()
    {
        _ctx.TramitesSiger.Add(new TramiteSiger
        {
            Codigo = "400-002", Nombre = "Viejo", Institucion = "ADUANAS", InstitucionId = "ADUANAS",
            Publicado = true, EstaEnSol = true, SolUrl = "https://otro.sitio.hn/x?y=1"
        });
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTramitePublicoQueryHandler(_ctx, Sol);
        var resultado = await handler.Handle(new GetTramitePublicoQuery("400-002"), CancellationToken.None);

        resultado!.SolUrl.Should().Be("https://otro.sitio.hn/x?y=1");
    }
}
