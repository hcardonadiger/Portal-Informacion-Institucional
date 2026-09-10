using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Siger.Publico.Queries.GetCatalogoPublico;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Publico;

internal sealed class FakeGlobalCurrentUser : ICurrentUserService
{
    public Guid?       UserId               => Guid.NewGuid();
    public string?     Nombre               => "test";
    public string?     Correo               => "test@diger.gob.hn";
    public string?     Rol                  => "Administrador";
    public bool        IsAuthenticated      => true;
    public bool        EsGlobal             => true;
    public NivelAlcance NivelAlcance        => NivelAlcance.Global;
    public bool        EsSoloLectura        => false;
    public bool        EsSupervisor         => true;
    public bool        EsTecnicoSoporte     => true;
    public string?     ActiveInstitucionId  => null;
    public string?     ActiveAreaId         => null;
    public string?     ActiveUnidadId       => null;
    public IReadOnlyCollection<string> InstitucionesAsignadas => [];
    public bool        PuedeAccederInstitucion(string? institucionId) => true;
}

public class GetCatalogoPublicoQueryTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public GetCatalogoPublicoQueryTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeGlobalCurrentUser(), NSubstitute.Substitute.For<MediatR.IPublisher>());
    }

    public void Dispose() => _ctx.Dispose();

    private static TramiteSiger Tramite(string codigo, string nombre, bool publicado, string institucionId = "INPREMA", string institucion = "Instituto Nacional de Previsión del Magisterio") => new()
    {
        Codigo = codigo, Nombre = nombre, Institucion = institucion, InstitucionId = institucionId,
        Publicado = publicado
    };

    [Fact]
    public async Task Handle_SoloDevuelveTramitesPublicados()
    {
        _ctx.TramitesSiger.AddRange(
            Tramite("100-001", "Trámite publicado", publicado: true),
            Tramite("100-002", "Trámite sin publicar", publicado: false));
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCatalogoPublicoQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetCatalogoPublicoQuery(), CancellationToken.None);

        resultado.Total.Should().Be(1);
        resultado.Items.Should().ContainSingle(t => t.Codigo == "100-001");
    }

    [Fact]
    public async Task Handle_FiltraPorInstitucion()
    {
        _ctx.TramitesSiger.AddRange(
            Tramite("100-001", "De INPREMA", true, "INPREMA"),
            Tramite("100-002", "De IHTT", true, "IHTT"));
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCatalogoPublicoQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetCatalogoPublicoQuery(Institucion: "IHTT"), CancellationToken.None);

        resultado.Items.Should().ContainSingle(t => t.Codigo == "100-002");
    }

    [Fact]
    public async Task Handle_ModalidadVirtual_TambienIncluyeHibridos()
    {
        var virtual_ = Tramite("100-001", "Trámite virtual", true);
        virtual_.Modalidad = "Virtual";
        var hibrido = Tramite("100-002", "Trámite híbrido", true);
        hibrido.Modalidad = "Hibrido";
        var presencial = Tramite("100-003", "Trámite presencial", true);
        presencial.Modalidad = "Presencial";
        _ctx.TramitesSiger.AddRange(virtual_, hibrido, presencial);
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCatalogoPublicoQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetCatalogoPublicoQuery(Modalidad: "Virtual"), CancellationToken.None);

        resultado.Items.Select(t => t.Codigo).Should().BeEquivalentTo(["100-001", "100-002"]);
    }

    [Fact]
    public async Task Handle_SoloGratuitos_FiltraPorCostoEsGratuitoTrue()
    {
        var gratuito = Tramite("100-001", "Gratuito", true);
        gratuito.CostoEsGratuito = true;
        var pago = Tramite("100-002", "Con costo", true);
        pago.CostoEsGratuito = false;
        var sinDato = Tramite("100-003", "Sin dato de costo", true);
        _ctx.TramitesSiger.AddRange(gratuito, pago, sinDato);
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCatalogoPublicoQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetCatalogoPublicoQuery(SoloGratuitos: true), CancellationToken.None);

        resultado.Items.Should().ContainSingle(t => t.Codigo == "100-001");
    }

    [Fact]
    public async Task Handle_SoloFichasCompletas_ExigeCategoriaModalidadTiempoYCosto()
    {
        var categoria = new CategoriaTramite { Nombre = "Salud" };
        _ctx.CategoriasTramite.Add(categoria);
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var completa = Tramite("100-001", "Ficha completa", true);
        completa.CategoriaId = categoria.Id; completa.Modalidad = "Virtual"; completa.TiempoTexto = "5 días"; completa.CostoEsGratuito = true;
        var incompleta = Tramite("100-002", "Ficha sin categoría", true);
        incompleta.Modalidad = "Virtual"; incompleta.TiempoTexto = "5 días"; incompleta.CostoEsGratuito = true;

        _ctx.TramitesSiger.AddRange(completa, incompleta);
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCatalogoPublicoQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetCatalogoPublicoQuery(SoloFichasCompletas: true), CancellationToken.None);

        resultado.Items.Should().ContainSingle(t => t.Codigo == "100-001");
    }

    [Fact]
    public async Task Handle_SoloFichasCompletas_SiEstaEnSolExigeSolUrl()
    {
        var categoria = new CategoriaTramite { Nombre = "Salud" };
        _ctx.CategoriasTramite.Add(categoria);
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var enSolSinUrl = Tramite("100-001", "En SOL sin URL", true);
        enSolSinUrl.CategoriaId = categoria.Id; enSolSinUrl.Modalidad = "Virtual"; enSolSinUrl.TiempoTexto = "1 día";
        enSolSinUrl.CostoEsGratuito = true; enSolSinUrl.EstaEnSol = true; enSolSinUrl.SolUrl = null;

        _ctx.TramitesSiger.Add(enSolSinUrl);
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCatalogoPublicoQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetCatalogoPublicoQuery(SoloFichasCompletas: true), CancellationToken.None);

        resultado.Total.Should().Be(0, "EstaEnSol=true sin SolUrl no cuenta como ficha completa");
    }

    [Fact]
    public async Task Handle_Busqueda_EncuentraPorNombreDescripcionOrObjetivo()
    {
        var porNombre = Tramite("100-001", "Registro de vehículo", true);
        var porDescripcion = Tramite("100-002", "Otro trámite", true);
        porDescripcion.Descripcion = "Contiene la palabra vehículo en la descripción";
        var sinCoincidencia = Tramite("100-003", "Nada que ver", true);

        _ctx.TramitesSiger.AddRange(porNombre, porDescripcion, sinCoincidencia);
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCatalogoPublicoQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetCatalogoPublicoQuery(Busqueda: "vehículo"), CancellationToken.None);

        resultado.Items.Select(t => t.Codigo).Should().BeEquivalentTo(["100-001", "100-002"]);
    }

    [Fact]
    public async Task Handle_Paginacion_RespetaPaginaYTamano()
    {
        for (var i = 1; i <= 5; i++)
            _ctx.TramitesSiger.Add(Tramite($"100-{i:000}", $"Trámite {i}", true));
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCatalogoPublicoQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetCatalogoPublicoQuery(Pagina: 2, Tamano: 2, Orden: "nombre"), CancellationToken.None);

        resultado.Total.Should().Be(5);
        resultado.Pagina.Should().Be(2);
        resultado.Tamano.Should().Be(2);
        resultado.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_IncluyeNombreDeCategoria()
    {
        var categoria = new CategoriaTramite { Nombre = "Salud y Seguridad Social" };
        _ctx.CategoriasTramite.Add(categoria);
        await _ctx.SaveChangesAsync(CancellationToken.None);
        var t = Tramite("100-001", "Trámite con categoría", true);
        t.CategoriaId = categoria.Id;
        _ctx.TramitesSiger.Add(t);
        await _ctx.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCatalogoPublicoQueryHandler(_ctx);
        var resultado = await handler.Handle(new GetCatalogoPublicoQuery(), CancellationToken.None);

        resultado.Items.Single().CategoriaNombre.Should().Be("Salud y Seguridad Social");
    }
}
