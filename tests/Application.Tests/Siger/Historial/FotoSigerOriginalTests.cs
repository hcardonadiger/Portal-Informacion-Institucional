using Diger.TramitesEstado.Application.Siger.Historial;
using Diger.TramitesEstado.Application.Siger.Historial.Commands.CapturarFotosOriginales;
using Diger.TramitesEstado.Application.Siger.Historial.Queries.GetFotoOriginal;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Historial;

public class FotoSigerOriginalTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public FotoSigerOriginalTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(),
            NSubstitute.Substitute.For<MediatR.IPublisher>());
    }

    public void Dispose() => _ctx.Dispose();

    // ── Serialización ─────────────────────────────────────────────────────────

    /// <summary>
    /// El archivo está hecho para que una persona lo pueda abrir y leer dentro de años. Si las
    /// tildes salen escapadas, «Prórroga de estadía» queda ilegible y el archivo cumple la letra
    /// de «no perder la información» traicionando su intención.
    /// </summary>
    [Fact]
    public void Serializar_ConservaLasTildesSinEscapar()
    {
        var json = FotoSigerSerializador.Serializar(
            FotoSigerSerializador.Retratar(Ficha(nombre: "Prórroga de estadía")));

        json.Should().Contain("Prórroga de estadía");
        json.Should().NotContain("\\u00");
    }

    [Fact]
    public void IdaYVuelta_ConservaLasSeisColecciones()
    {
        var original = FotoSigerSerializador.Retratar(FichaCompleta());

        var leida = FotoSigerSerializador.Leer(FotoSigerSerializador.Serializar(original));

        leida.Should().BeEquivalentTo(original);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ esto no es json valido")]
    public void Leer_DocumentoInservible_DevuelveNullEnVezDeReventar(string? contenido)
        => FotoSigerSerializador.Leer(contenido).Should().BeNull();

    // ── Captura ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Capturar_RetrataTodasLasFichasConSusHijos()
    {
        _ctx.TramitesSiger.Add(FichaCompleta());
        _ctx.TramitesSiger.Add(Ficha(codigo: "400-002", nombre: "Otra"));
        await _ctx.SaveChangesAsync(default);

        var r = await Capturar();

        r.Capturadas.Should().Be(2);
        r.YaTenian.Should().Be(0);
        r.Total.Should().Be(2);

        var foto = await Leer("400-001");
        foto!.Ficha!.Requisitos.Should().HaveCount(2);
        foto.Ficha.Pasos.Should().HaveCount(1);
        foto.Ficha.TareasDigitalizacion.Should().HaveCount(1);
    }

    /// <summary>
    /// Correrla dos veces no debe hacer nada la segunda. Es lo que permite ejecutarla sin miedo
    /// desde la pantalla, y lo que evita que una corrida a destiempo repise el original.
    /// </summary>
    [Fact]
    public async Task Capturar_DosVeces_NoDuplicaNiRecaptura()
    {
        _ctx.TramitesSiger.Add(FichaCompleta());
        await _ctx.SaveChangesAsync(default);

        await Capturar();
        var segunda = await Capturar();

        segunda.Capturadas.Should().Be(0);
        segunda.YaTenian.Should().Be(1);
        _ctx.FotosTramiteSiger.Count().Should().Be(1);
    }

    [Fact]
    public async Task Capturar_FichaNueva_RetrataSoloLaQueFalta()
    {
        _ctx.TramitesSiger.Add(FichaCompleta());
        await _ctx.SaveChangesAsync(default);
        await Capturar();

        _ctx.TramitesSiger.Add(Ficha(codigo: "400-009", nombre: "Llegó después"));
        await _ctx.SaveChangesAsync(default);

        var r = await Capturar();

        r.Capturadas.Should().Be(1);
        r.YaTenian.Should().Be(1);
        _ctx.FotosTramiteSiger.Count().Should().Be(2);
    }

    /// <summary>
    /// La garantía que sostiene toda la Fase 2: editar la ficha después de archivarla no debe
    /// tocar lo archivado. Si esta prueba se cae, el archivo dejó de ser un archivo.
    /// </summary>
    [Fact]
    public async Task EditarLaFichaDespues_NoAlteraLoArchivado()
    {
        _ctx.TramitesSiger.Add(FichaCompleta());
        await _ctx.SaveChangesAsync(default);
        await Capturar();

        var ficha = await _ctx.TramitesSiger.FirstAsync(t => t.Codigo == "400-001");
        ficha.Nombre      = "Nombre cambiado por el portal";
        ficha.Modalidad   = "Virtual";
        ficha.EstadoSiger = "Completo";
        await _ctx.SaveChangesAsync(default);

        var foto = await Leer("400-001");

        foto!.Ficha!.Nombre.Should().Be("Permiso de operación");
        foto.Ficha.Modalidad.Should().BeNull();
        foto.Ficha.EstadoSiger.Should().Be("Registrado");
    }

    [Fact]
    public async Task FotoOriginal_DeFichaNoArchivada_DevuelveNull()
    {
        _ctx.TramitesSiger.Add(FichaCompleta());
        await _ctx.SaveChangesAsync(default);

        var id = _ctx.TramitesSiger.First().Id;

        var foto = await new GetFotoOriginalQueryHandler(_ctx)
            .Handle(new GetFotoOriginalQuery(id), default);

        foto.Should().BeNull();
    }

    // ── Armado ────────────────────────────────────────────────────────────────

    private Task<ResultadoCapturaOriginal> Capturar()
        => new CapturarFotosOriginalesCommandHandler(_ctx)
            .Handle(new CapturarFotosOriginalesCommand(), default);

    private async Task<FotoOriginalDto?> Leer(string codigo)
    {
        var id = await _ctx.TramitesSiger.Where(t => t.Codigo == codigo)
            .Select(t => t.Id).FirstAsync();
        return await new GetFotoOriginalQueryHandler(_ctx)
            .Handle(new GetFotoOriginalQuery(id), default);
    }

    private static TramiteSiger Ficha(string codigo = "400-001", string nombre = "Permiso de operación") => new()
    {
        IdSiger     = 1234,
        Codigo      = codigo,
        Nombre      = nombre,
        Institucion = "SECRETARÍA DE SALUD",
        EstadoSiger = "Registrado"
    };

    private static TramiteSiger FichaCompleta()
    {
        var t = Ficha();
        t.Pasos                = [new PasoSiger { NumeroPaso = 1, Descripcion = "Presentar solicitud" }];
        t.Requisitos           = [new RequisitoSiger { Numero = 1, Requisito = "Copia de identidad" },
                                  new RequisitoSiger { Numero = 2, Requisito = "Solicitud firmada" }];
        t.Entregables          = [new EntregableSiger { Numero = 1, Entregable = "Constancia" }];
        t.LugaresAtencion      = [new LugarAtencionSiger { Numero = 1, Lugar = "Oficina central", Ciudad = "Tegucigalpa" }];
        t.Enlaces              = [new EnlaceSiger { Numero = 1, Url = "https://salud.gob.hn" }];
        t.TareasDigitalizacion = [new TareaDigitalizacionSiger { NumeroTarea = 1, Descripcion = "Levantar el flujo" }];
        return t;
    }
}
