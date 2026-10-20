using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Application.Siger.Bloqueo;
using Diger.TramitesEstado.Application.Siger.Importacion;
using Diger.TramitesEstado.Application.Siger.Importacion.Commands.ImportarFicha;
using Diger.TramitesEstado.Application.Siger.Promocion.Commands.PasarASiger;
using Diger.TramitesEstado.Application.Siger.Publico;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Importacion;

/// <summary>
/// Traer una ficha SIGER a un expediente (D-05, D-06, D-21) y el bloqueo que eso produce (D-17).
///
/// El enlace no es un adorno: es lo que decide <b>dónde se edita</b> la ficha a partir de ese
/// momento. Por eso lo que más se prueba acá es que una ficha no acabe enlazada dos veces —dos
/// expedientes creyendo que mandan sobre ella, y el último pase ganando en silencio— y que el
/// contenedor de importados no se cuele en los listados como si fuera un levantamiento.
/// </summary>
public class ImportarFichaTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public ImportarFichaTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeGlobalCurrentUser(), NSubstitute.Substitute.For<MediatR.IPublisher>());
    }

    public void Dispose() => _ctx.Dispose();

    // ── El bucket ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sin_destino_elegido_la_ficha_va_al_contenedor_de_importados()
    {
        var ficha = await FichaAsync();

        var r = await Importar(ficha.Id);

        r.EnBucket.Should().BeTrue();
        r.BucketCreado.Should().BeTrue();
        r.ExpedienteCodigo.Should().Be("SIGER-ADUANAS");
    }

    /// <summary>Un bucket por institución, no uno por importación.</summary>
    [Fact]
    public async Task El_contenedor_se_reutiliza_en_la_siguiente_importacion()
    {
        var primera = await Importar((await FichaAsync("400-001")).Id);
        var segunda = await Importar((await FichaAsync("400-002")).Id);

        segunda.ExpedienteId.Should().Be(primera.ExpedienteId);
        segunda.BucketCreado.Should().BeFalse();
        segunda.TramiteIndex.Should().Be(1, "se agrega detrás del que ya estaba");

        (await _ctx.Expedientes.CountAsync()).Should().Be(1);
    }

    /// <summary>
    /// D-21: el contenedor no es un levantamiento y no debe contarse como tal. Si apareciera en
    /// los listados inflaría las cifras de trabajo en curso con carpetas que nadie abrió, y
    /// además quedaría atrapado en «En exploración» para siempre, porque un bucket nunca avanza
    /// de etapa.
    /// </summary>
    [Fact]
    public async Task El_contenedor_no_sale_en_los_listados_de_expedientes()
    {
        await Importar((await FichaAsync()).Id);

        _ctx.Expedientes.Should().HaveCount(1);
        (await _ctx.Expedientes.AsNoTracking().SinBuckets().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_expediente_normal_si_sale_en_los_listados()
    {
        _ctx.Expedientes.Add(Expediente.Crear("EXP-1", "ADUANAS", null, null, "Aduanas", "Analista"));
        await _ctx.SaveChangesAsync();

        (await _ctx.Expedientes.AsNoTracking().SinBuckets().CountAsync()).Should().Be(1);
    }

    // ── El destino elegido ────────────────────────────────────────────────────

    [Fact]
    public async Task Con_destino_elegido_la_ficha_va_a_ese_expediente()
    {
        var ficha = await FichaAsync();
        var e = Expediente.Crear("EXP-1", "ADUANAS", null, null, "Aduanas", "Analista");
        _ctx.Expedientes.Add(e);
        await _ctx.SaveChangesAsync();

        var r = await Importar(ficha.Id, e.Id);

        r.ExpedienteId.Should().Be(e.Id);
        r.EnBucket.Should().BeFalse();
    }

    /// <summary>
    /// Una ficha en un expediente de otra institución diría pertenecer a dos a la vez, y el pase
    /// de vuelta le cambiaría la institución a la ficha sin que nadie lo pidiera.
    /// </summary>
    [Fact]
    public async Task No_se_puede_traer_una_ficha_a_un_expediente_de_otra_institucion()
    {
        var ficha = await FichaAsync();
        _ctx.Instituciones.Add(Institucion.Crear("SALUD", "Salud"));
        var ajeno = Expediente.Crear("EXP-9", "SALUD", null, null, "Salud", "Analista");
        _ctx.Expedientes.Add(ajeno);
        await _ctx.SaveChangesAsync();

        var acto = async () => await Importar(ficha.Id, ajeno.Id);

        await acto.Should().ThrowAsync<DomainException>();
        (await _ctx.Tramites.CountAsync()).Should().Be(0, "no quedó nada a medias");
    }

    // ── La guarda contra doble importación ────────────────────────────────────

    /// <summary>
    /// La prueba que sostiene el bloqueo. Dos trámites enlazados a la misma ficha significan dos
    /// expedientes creyendo que mandan sobre ella, y el último que pase gana sin que nadie lo sepa.
    /// </summary>
    [Fact]
    public async Task Una_ficha_no_se_puede_importar_dos_veces()
    {
        var ficha = await FichaAsync();
        await Importar(ficha.Id);

        var acto = async () => await Importar(ficha.Id);

        (await acto.Should().ThrowAsync<DomainException>())
            .WithMessage("*ya está en el expediente*");

        (await _ctx.Tramites.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Desenlazarla_permite_volver_a_importarla()
    {
        var ficha = await FichaAsync();
        await Importar(ficha.Id);

        foreach (var t in await _ctx.Tramites.ToListAsync()) t.TramiteSigerId = null;
        await _ctx.SaveChangesAsync();

        var acto = async () => await Importar(ficha.Id);
        await acto.Should().NotThrowAsync();
    }

    // ── Qué viaja y qué no ────────────────────────────────────────────────────

    [Fact]
    public async Task El_contenido_de_la_ficha_llega_al_tramite()
    {
        var ficha = await FichaAsync();
        ficha.Objetivo    = "Autorizar la importación";
        ficha.CategoriaId = 3;
        ficha.Modalidad   = ModalidadPublica.Hibrido;
        ficha.TiempoTexto = "5 días hábiles";
        ficha.CostoEsGratuito = true;
        ficha.Temporalidad = "Permanente";
        ficha.EstaEnSol    = true;
        ficha.SolTramo     = "permiso";
        await _ctx.SaveChangesAsync();

        await Importar(ficha.Id);

        var t = await _ctx.Tramites.SingleAsync();
        t.NombreTramite.Should().Be(ficha.Nombre);
        t.Objetivo.Should().Be("Autorizar la importación");
        t.CategoriaId.Should().Be(3);
        t.Modalidad.Should().Be(ModalidadPublica.Hibrido);
        t.TiempoReal.Should().Be("5 días hábiles");
        t.EsGratuito.Should().BeTrue();
        t.Temporalidad.Should().Be("Permanente");
        t.EstaEnSol.Should().BeTrue();
        t.SolTramo.Should().Be("permiso");
        t.TramiteSigerId.Should().Be(ficha.Id, "el enlace es lo que produce el bloqueo");
    }

    /// <summary>
    /// D-11: los pasos siguen siendo de SIGER. El expediente modela el flujo con otro vocabulario
    /// —nodos, fases, retornos— y volcar unos sobre otros produciría un flujo falso.
    /// </summary>
    [Fact]
    public async Task Los_pasos_del_proceso_no_viajan()
    {
        var ficha = await FichaAsync();
        _ctx.PasosSiger.Add(new PasoSiger { TramiteSigerId = ficha.Id, NumeroPaso = 1, Descripcion = "Recibir" });
        await _ctx.SaveChangesAsync();

        await Importar(ficha.Id);

        (await _ctx.Flujos.CountAsync()).Should().Be(0, "el flujo del expediente no se fabrica desde los pasos");
    }

    [Fact]
    public async Task Los_requisitos_entregables_y_lugares_viajan_renumerados_desde_cero()
    {
        var ficha = await FichaAsync();
        ficha.Requisitos.Add(new RequisitoSiger { Numero = 1, Requisito = "Cédula" });
        ficha.Entregables.Add(new EntregableSiger { Numero = 1, Entregable = "Constancia" });
        ficha.LugaresAtencion.Add(new LugarAtencionSiger { Numero = 1, Lugar = "Ventanilla" });
        await _ctx.SaveChangesAsync();

        await Importar(ficha.Id);

        (await _ctx.Requisitos.SingleAsync()).Orden.Should().Be(0, "en el expediente el orden empieza en 0");
        (await _ctx.EntregablesTramite.SingleAsync()).Entregable.Should().Be("Constancia");
        (await _ctx.LugaresTramite.SingleAsync()).Lugar.Should().Be("Ventanilla");
    }

    // ── El bloqueo ────────────────────────────────────────────────────────────

    /// <summary>
    /// D-23: la captura en lote y el llenado asistido dejan de ver la ficha en cuanto queda
    /// enlazada. Escribir en ella desde allí sería trabajo que el siguiente pase desde el
    /// expediente borraría sin dejar rastro.
    /// </summary>
    [Fact]
    public async Task Una_ficha_enlazada_desaparece_de_las_pantallas_que_la_editaban()
    {
        var ficha = await FichaAsync();

        (await _ctx.TramitesSiger.AsNoTracking().SinBloqueadas(_ctx.Tramites.AsNoTracking()).CountAsync())
            .Should().Be(1, "antes de importarla se edita acá");

        await Importar(ficha.Id);

        (await _ctx.TramitesSiger.AsNoTracking().SinBloqueadas(_ctx.Tramites.AsNoTracking()).CountAsync())
            .Should().Be(0, "después se edita en el expediente");
    }

    // ── Ida y vuelta ──────────────────────────────────────────────────────────

    /// <summary>
    /// La prueba que ata las dos direcciones. Importar y volver a pasar son inversas: si no lo
    /// fueran, traer una ficha a un expediente y devolverla sin tocar nada la <b>cambiaría</b>, y
    /// nadie sabría qué campo se deformó ni cuándo.
    /// </summary>
    [Fact]
    public async Task Importar_y_devolver_sin_tocar_nada_no_cambia_la_ficha()
    {
        var ficha = await FichaAsync();
        ficha.Objetivo    = "Autorizar la importación";
        ficha.Descripcion = "Permite ingresar mercadería";
        ficha.DirigidoA   = "Importadores";
        ficha.CategoriaId = 3;
        ficha.Modalidad   = ModalidadPublica.Hibrido;
        ficha.TiempoTexto = "5 días hábiles";
        ficha.CostoEsGratuito = true;
        ficha.VigenciaDocumento = "2 años";
        ficha.Temporalidad = "Permanente";
        ficha.EstaEnSol   = true;
        ficha.SolTramo    = "permiso";
        await _ctx.SaveChangesAsync();

        var r = await Importar(ficha.Id);

        await new PasarASigerCommandHandler(_ctx)
            .Handle(new PasarASigerCommand(r.ExpedienteId, r.TramiteIndex), CancellationToken.None);

        var despues = await _ctx.TramitesSiger.AsNoTracking().FirstAsync(f => f.Id == ficha.Id);

        despues.Objetivo.Should().Be("Autorizar la importación");
        despues.Descripcion.Should().Be("Permite ingresar mercadería");
        despues.DirigidoA.Should().Be("Importadores");
        despues.CategoriaId.Should().Be(3);
        despues.Modalidad.Should().Be(ModalidadPublica.Hibrido);
        despues.TiempoTexto.Should().Be("5 días hábiles");
        despues.CostoEsGratuito.Should().BeTrue();
        despues.VigenciaDocumento.Should().Be("2 años");
        despues.Temporalidad.Should().Be("Permanente");
        despues.EstaEnSol.Should().BeTrue();
        despues.SolTramo.Should().Be("permiso");
    }

    // ── Armado ────────────────────────────────────────────────────────────────

    private Task<ResultadoImportacion> Importar(int fichaId, int? expedienteId = null) =>
        new ImportarFichaCommandHandler(_ctx, new FakeGlobalCurrentUser())
            .Handle(new ImportarFichaCommand(fichaId, expedienteId), CancellationToken.None);

    private async Task<TramiteSiger> FichaAsync(string codigo = "400-001")
    {
        if (!await _ctx.Instituciones.AnyAsync(i => i.Id == "ADUANAS"))
            _ctx.Instituciones.Add(Institucion.Crear("ADUANAS", "Aduanas"));

        var ficha = new TramiteSiger
        {
            Codigo = codigo, Nombre = "Permiso de importación", EstadoSiger = "Registrado",
            Institucion = "Aduanas", Sigla = "ADUANAS", InstitucionId = "ADUANAS"
        };
        _ctx.TramitesSiger.Add(ficha);
        await _ctx.SaveChangesAsync();
        return ficha;
    }
}
