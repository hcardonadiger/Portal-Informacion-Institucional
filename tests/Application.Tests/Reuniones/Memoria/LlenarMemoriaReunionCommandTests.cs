using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Reuniones.Memoria;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones.Memoria;

/// <summary>
/// La memoria interpretada se escribe en la reunión (lo que usa el MCP). Lo que se protege:
/// «Generales» nunca se toca, sin «sobreescribir» un valor distinto no aplica NADA, los acuerdos
/// con seguimiento no se reemplazan ni sobreescribiendo, y reemplazar acuerdos no se lleva a los
/// asistentes ni a los adjuntos.
/// </summary>
public class LlenarMemoriaReunionCommandTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public LlenarMemoriaReunionCommandTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var usuario = Substitute.For<ICurrentUserService>();
        usuario.EsGlobal.Returns(true);
        _ctx = new AppDbContext(opts, usuario, Substitute.For<MediatR.IPublisher>());
    }

    public void Dispose() => _ctx.Dispose();

    private Reunion Sembrar(Action<Reunion>? ajustar = null)
    {
        var r = Reunion.Crear("Mesa técnica");
        r.Fecha = new DateOnly(2026, 10, 20);
        ajustar?.Invoke(r);
        _ctx.Reuniones.Add(r);
        _ctx.SaveChanges();
        _ctx.ChangeTracker.Clear();
        return r;
    }

    private Task Llenar(int id, string json, bool sobreescribir = false) =>
        new LlenarMemoriaReunionCommandHandler(new ReunionRepository(_ctx), _ctx, _ctx)
            .Handle(new LlenarMemoriaReunionCommand(id, json, sobreescribir), default);

    private Task<Reunion> Releer() =>
        _ctx.Reuniones.AsNoTracking().Include(r => r.Acuerdos).Include(r => r.Asistentes).SingleAsync();

    private const string JsonCompleto = """
        {
          "objetivo": "Revisar el avance del trámite",
          "desarrollo": "<p>Se revisó el avance.</p>",
          "documentos": ["Presentación.pdf", "Lista firmada"],
          "validador_diger": "Ana Pérez",
          "acuerdos": [
            { "compromiso": "Enviar el borrador", "responsable": "IHTT", "plazo": "2026-11-05" },
            { "compromiso": "Revisar requisitos", "responsable": "Luis Gómez" }
          ]
        }
        """;

    [Fact]
    public async Task Llena_una_reunion_vacia_y_guarda()
    {
        var r = Sembrar();

        await Llenar(r.Id, JsonCompleto);

        var fila = await Releer();
        fila.ObjetivoAgenda.Should().Be("Revisar el avance del trámite");
        fila.Desarrollo.Should().Be("<p>Se revisó el avance.</p>");
        fila.DocsRecursos.Should().Be("Presentación.pdf\nLista firmada");
        fila.ValDiger.Should().Be("Ana Pérez");
        var acuerdos = fila.Acuerdos.OrderBy(a => a.Orden).ToList();
        acuerdos.Select(a => (a.Orden, a.Compromiso, a.Responsable, a.Plazo)).Should().Equal(
            (0, "Enviar el borrador", "IHTT", new DateOnly(2026, 11, 5)),
            (1, "Revisar requisitos", "Luis Gómez", (DateOnly?)null));
        acuerdos.Should().OnlyContain(a => a.ResponsableContactoId == null && a.Estado == EstadoCompromiso.Pendiente);
    }

    [Fact]
    public async Task Generales_no_se_toca()
    {
        var r = Sembrar();

        await Llenar(r.Id, """{ "titulo": "Otro título", "fecha": "2027-01-01", "tipo": "Capacitación", "objetivo": "X" }""");

        var fila = await Releer();
        fila.Titulo.Should().Be("Mesa técnica");
        fila.Fecha.Should().Be(new DateOnly(2026, 10, 20));
        fila.Tipo.Should().BeNull();
        fila.ObjetivoAgenda.Should().Be("X");
    }

    [Fact]
    public async Task Sin_sobreescribir_un_valor_distinto_no_aplica_nada()
    {
        var r = Sembrar(x => x.ObjetivoAgenda = "Objetivo ya escrito");

        var acto = () => Llenar(r.Id, JsonCompleto);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*«sobreescribir»*");
        var fila = await Releer();
        fila.ObjetivoAgenda.Should().Be("Objetivo ya escrito");
        fila.Desarrollo.Should().BeNull();
        fila.Acuerdos.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_sobreescribir_el_mismo_valor_o_un_campo_en_blanco_no_choca()
    {
        var r = Sembrar(x => { x.ObjetivoAgenda = "  Revisar el avance del trámite "; x.Desarrollo = "   "; });

        await Llenar(r.Id, JsonCompleto);

        var fila = await Releer();
        fila.Desarrollo.Should().Be("<p>Se revisó el avance.</p>");
        fila.Acuerdos.Should().HaveCount(2);
    }

    [Fact]
    public async Task Sin_sobreescribir_acuerdos_existentes_bloquean()
    {
        var r = Sembrar(x => x.Agregar(new AcuerdoReunion { Compromiso = "Acuerdo previo" }));

        var acto = () => Llenar(r.Id, JsonCompleto);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*«sobreescribir»*");
        var fila = await Releer();
        fila.ObjetivoAgenda.Should().BeNull();
        fila.Acuerdos.Select(a => a.Compromiso).Should().Equal("Acuerdo previo");
    }

    [Fact]
    public async Task Con_sobreescribir_reemplaza_campos_y_acuerdos_sin_seguimiento()
    {
        var r = Sembrar(x =>
        {
            x.ObjetivoAgenda = "Objetivo viejo";
            x.ValDiger = "Otro validador";
            x.Agregar(new AcuerdoReunion { Compromiso = "Acuerdo viejo", Orden = 0 });
        });

        await Llenar(r.Id, JsonCompleto, sobreescribir: true);

        var fila = await Releer();
        fila.ObjetivoAgenda.Should().Be("Revisar el avance del trámite");
        fila.ValDiger.Should().Be("Ana Pérez");
        fila.Acuerdos.OrderBy(a => a.Orden).Select(a => a.Compromiso)
            .Should().Equal("Enviar el borrador", "Revisar requisitos");
        (await _ctx.Acuerdos.CountAsync()).Should().Be(2, "el acuerdo viejo se borra, no queda huérfano");
    }

    [Fact]
    public async Task Acuerdos_con_seguimiento_no_se_reemplazan_ni_sobreescribiendo()
    {
        var r = Sembrar(x => x.Agregar(new AcuerdoReunion { Compromiso = "En curso", Estado = EstadoCompromiso.EnProgreso }));

        var acto = () => Llenar(r.Id, JsonCompleto, sobreescribir: true);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*seguimiento*");
        var fila = await Releer();
        fila.Acuerdos.Select(a => a.Compromiso).Should().Equal("En curso");
        fila.ObjetivoAgenda.Should().BeNull("no se aplica nada a medias");
    }

    [Fact]
    public async Task Un_comentario_tambien_es_seguimiento()
    {
        var r = Sembrar(x => x.Agregar(new AcuerdoReunion { Compromiso = "Comentado" }));
        var acuerdoId = (await _ctx.Acuerdos.AsNoTracking().SingleAsync()).Id;
        _ctx.ComentariosCompromisos.Add(new ComentarioCompromiso { AcuerdoReunionId = acuerdoId, Comentario = "Avance", CreadoPor = "Ana" });
        await _ctx.SaveChangesAsync();
        _ctx.ChangeTracker.Clear();

        var acto = () => Llenar(r.Id, JsonCompleto, sobreescribir: true);

        await acto.Should().ThrowAsync<DomainException>().WithMessage("*seguimiento*");
        (await Releer()).Acuerdos.Select(a => a.Compromiso).Should().Equal("Comentado");
    }

    [Fact]
    public async Task Sin_acuerdos_en_el_json_los_existentes_se_quedan_aunque_tengan_seguimiento()
    {
        var r = Sembrar(x => x.Agregar(new AcuerdoReunion { Compromiso = "En curso", Estado = EstadoCompromiso.EnProgreso }));

        await Llenar(r.Id, """{ "objetivo": "Nuevo objetivo" }""");

        var fila = await Releer();
        fila.ObjetivoAgenda.Should().Be("Nuevo objetivo");
        fila.Acuerdos.Select(a => a.Compromiso).Should().Equal("En curso");
    }

    [Fact]
    public async Task El_bloque_de_capacitacion_solo_en_capacitacion()
    {
        var r = Sembrar(x => x.Tipo = "Taller");

        await Llenar(r.Id, """{ "convocados": 30, "compromisos": ["Replicar"], "objetivo": "X" }""");

        var fila = await Releer();
        fila.Convocados.Should().BeNull();
        fila.Compromisos.Should().BeNull();
        fila.ObjetivoAgenda.Should().Be("X");
    }

    [Fact]
    public async Task En_capacitacion_el_bloque_se_aplica_y_los_numeros_se_comparan_como_valores()
    {
        var r = Sembrar(x => { x.Tipo = MemoriaDesdeTranscripcion.TipoCapacitacion; x.Convocados = 30; });

        await Llenar(r.Id, """{ "convocados": "30", "num_asistentes": 24, "pct_asistencia": 80 }""");

        var fila = await Releer();
        fila.Convocados.Should().Be(30);
        fila.NumAsistentes.Should().Be(24);
        fila.PctAsistencia.Should().Be(80);
    }

    [Fact]
    public async Task Validador_institucion_solo_con_la_encuesta_encendida()
    {
        var apagada = Sembrar();
        await Llenar(apagada.Id, """{ "validador_institucion": "Luis" }""");
        (await Releer()).ValInst.Should().BeNull();
    }

    [Fact]
    public async Task Reunion_inexistente()
    {
        var acto = () => Llenar(999, JsonCompleto);

        await acto.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Llenar_no_borra_asistentes_ni_adjuntos()
    {
        var r = Sembrar(x =>
        {
            x.PreRegistrar("Invitada", null, "IHTT", null, "invitada@ihtt.gob.hn", null);
            x.Agregar(ReunionAdjunto.ParaEnlace(SeccionAdjuntoReunion.Documento, 0, "Presentación", null, "https://diger.gob.hn/p"));
            x.Agregar(new AcuerdoReunion { Compromiso = "Acuerdo viejo" });
        });

        await Llenar(r.Id, JsonCompleto, sobreescribir: true);

        var fila = await _ctx.Reuniones.AsNoTracking()
            .Include(x => x.Asistentes).Include(x => x.Adjuntos).Include(x => x.Acuerdos).SingleAsync();
        fila.Asistentes.Should().ContainSingle(a => a.Nombre == "Invitada");
        fila.Adjuntos.Should().ContainSingle();
        fila.Acuerdos.Should().HaveCount(2);
    }
}
