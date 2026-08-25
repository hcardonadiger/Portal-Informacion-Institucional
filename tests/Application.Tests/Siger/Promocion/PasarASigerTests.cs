using Diger.TramitesEstado.Application.Siger.Historial;
using Diger.TramitesEstado.Application.Siger.Promocion;
using Diger.TramitesEstado.Application.Siger.Promocion.Commands.PasarASiger;
using Diger.TramitesEstado.Application.Siger.Promocion.Queries.GetVistaPreviaPase;
using Diger.TramitesEstado.Application.Siger.Publico;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Promocion;

/// <summary>
/// Pasar un trámite del expediente a su ficha SIGER: la primera vez creándola, las siguientes
/// sobrescribiéndola.
///
/// Lo que se protege acá son tres cosas, y las tres se rompen en silencio si fallan:
/// que <b>actualizar no saque una ficha del portal ciudadano</b>, que <b>antes de sobrescribir
/// quede una foto</b> de lo que había, y que las colecciones <b>se reemplacen y no se acumulen</b>.
/// </summary>
public class PasarASigerTests : IDisposable
{
    private readonly AppDbContext _ctx;

    public PasarASigerTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new Publico.FakeGlobalCurrentUser(), NSubstitute.Substitute.For<MediatR.IPublisher>());
    }

    public void Dispose() => _ctx.Dispose();

    // ── Crear ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task El_primer_pase_crea_la_ficha_y_la_deja_enlazada()
    {
        var (e, t) = await ExpedienteConTramiteAsync();

        var r = await Pasar(e.Id, t.TramiteIndex);

        r.FueCreada.Should().BeTrue();
        r.VersionArchivada.Should().BeNull("no había ficha que archivar");

        var ficha = await _ctx.TramitesSiger.FirstAsync(f => f.Id == r.TramiteSigerId);
        ficha.Nombre.Should().Be("Permiso de importación");
        ficha.Publicado.Should().BeFalse("promover y publicar son actos distintos");
        ficha.IdSiger.Should().BeNull();

        var tramite = await _ctx.Tramites.FirstAsync(x => x.Id == t.Id);
        tramite.TramiteSigerId.Should().Be(ficha.Id, "el enlace es lo que activa el bloqueo de D-17");
    }

    /// <summary>El código hereda el prefijo de la institución y lleva la marca «-P», que delata
    /// a simple vista que la ficha no vino del inventario de SIGER.</summary>
    [Fact]
    public async Task El_codigo_hereda_el_prefijo_de_la_institucion_con_la_marca_de_promovida()
    {
        var (e, t) = await ExpedienteConTramiteAsync();
        _ctx.TramitesSiger.Add(Ficha("400-019", "Otra de la misma institución"));
        await _ctx.SaveChangesAsync();

        var r = await Pasar(e.Id, t.TramiteIndex);

        r.Codigo.Should().Be("400-P01");
    }

    [Fact]
    public async Task Dos_promociones_en_la_misma_institucion_no_repiten_codigo()
    {
        var (e, t) = await ExpedienteConTramiteAsync();
        _ctx.TramitesSiger.Add(Ficha("400-019", "Existente"));
        await _ctx.SaveChangesAsync();

        var primera = await Pasar(e.Id, t.TramiteIndex);

        _ctx.Tramites.Add(new ExpedienteTramite
        {
            ExpedienteId = e.Id, TramiteIndex = 1, ClaveEstable = Guid.NewGuid(),
            NombreTramite = "Segundo trámite", FechaCreacion = DateOnly.FromDateTime(DateTime.Today)
        });
        await _ctx.SaveChangesAsync();

        var segunda = await Pasar(e.Id, 1);

        primera.Codigo.Should().Be("400-P01");
        segunda.Codigo.Should().Be("400-P02");
    }

    [Fact]
    public async Task Las_tres_colecciones_viajan_numeradas_desde_uno()
    {
        var (e, t) = await ExpedienteConTramiteAsync();
        _ctx.Requisitos.Add(new TramiteRequisito { ExpedienteId = e.Id, TramiteIndex = 0, Orden = 0, Requisito = "Cédula" });
        _ctx.EntregablesTramite.Add(new ExpedienteTramiteEntregable { ExpedienteId = e.Id, TramiteIndex = 0, Orden = 0, Entregable = "Constancia" });
        _ctx.LugaresTramite.Add(new ExpedienteTramiteLugar { ExpedienteId = e.Id, TramiteIndex = 0, Orden = 0, Lugar = "Ventanilla" });
        await _ctx.SaveChangesAsync();

        var r = await Pasar(e.Id, t.TramiteIndex);

        var ficha = await _ctx.TramitesSiger
            .Include(f => f.Requisitos).Include(f => f.Entregables).Include(f => f.LugaresAtencion)
            .FirstAsync(f => f.Id == r.TramiteSigerId);

        ficha.Requisitos.Single().Numero.Should().Be(1);
        ficha.Entregables.Single().Entregable.Should().Be("Constancia");
        ficha.LugaresAtencion.Single().Lugar.Should().Be("Ventanilla");
    }

    // ── Actualizar ────────────────────────────────────────────────────────────

    /// <summary>
    /// La prueba que sostiene la fase. Una ficha que lleva meses publicada no puede salir del
    /// portal porque alguien corrigió una tilde en el expediente. Publicar lo decide una persona
    /// en su pantalla (D-10), no un efecto secundario de guardar.
    /// </summary>
    [Fact]
    public async Task Volver_a_pasar_no_despublica_ni_pierde_lo_que_decidio_SIGER()
    {
        var (e, t) = await ExpedienteConTramiteAsync();
        var creada = await Pasar(e.Id, t.TramiteIndex);

        var ficha = await _ctx.TramitesSiger.FirstAsync(f => f.Id == creada.TramiteSigerId);
        ficha.Publicado   = true;
        ficha.EsPopular   = true;
        ficha.EstadoSiger = ReglaPublicacion.Aprobado;
        ficha.IdSiger     = 9911;
        await _ctx.SaveChangesAsync();

        var tramite = await _ctx.Tramites.FirstAsync(x => x.Id == t.Id);
        tramite.NombreTramite = "Permiso de importación (corregido)";
        await _ctx.SaveChangesAsync();

        var r = await Pasar(e.Id, t.TramiteIndex);

        r.FueCreada.Should().BeFalse();
        var despues = await _ctx.TramitesSiger.FirstAsync(f => f.Id == creada.TramiteSigerId);
        despues.Nombre.Should().Be("Permiso de importación (corregido)");
        despues.Publicado.Should().BeTrue("actualizar no puede sacar una ficha del portal");
        despues.EsPopular.Should().BeTrue();
        despues.EstadoSiger.Should().Be(ReglaPublicacion.Aprobado);
        despues.IdSiger.Should().Be(9911);
        despues.Codigo.Should().Be(creada.Codigo, "el código se genera una vez");
    }

    /// <summary>
    /// Antes de sobrescribir queda una foto de lo que había. Es lo que permite responder «qué
    /// decía esta ficha antes del pase», y sin ella cada pase borraría el pasado sin dejar rastro.
    /// </summary>
    [Fact]
    public async Task Antes_de_sobrescribir_se_archiva_lo_que_habia()
    {
        var (e, t) = await ExpedienteConTramiteAsync();
        var creada = await Pasar(e.Id, t.TramiteIndex);

        var tramite = await _ctx.Tramites.FirstAsync(x => x.Id == t.Id);
        tramite.NombreTramite = "Nombre nuevo";
        await _ctx.SaveChangesAsync();

        var r = await Pasar(e.Id, t.TramiteIndex);

        r.VersionArchivada.Should().Be(1, "la 0 está reservada para el inventario original de SIGER");

        var foto = await _ctx.FotosTramiteSiger.AsNoTracking()
            .SingleAsync(f => f.TramiteSigerId == creada.TramiteSigerId);

        foto.Origen.Should().Be(OrigenFoto.PaseDesdeExpediente);
        foto.Contenido.Should().Contain("Permiso de importación",
            "la foto guarda lo que se reemplazó, no lo nuevo");
        foto.Contenido.Should().NotContain("Nombre nuevo");
    }

    [Fact]
    public async Task Cada_pase_deja_su_propia_version()
    {
        var (e, t) = await ExpedienteConTramiteAsync();
        await Pasar(e.Id, t.TramiteIndex);

        var segunda = await Pasar(e.Id, t.TramiteIndex);
        var tercera = await Pasar(e.Id, t.TramiteIndex);

        segunda.VersionArchivada.Should().Be(1);
        tercera.VersionArchivada.Should().Be(2);
        (await _ctx.FotosTramiteSiger.CountAsync()).Should().Be(2);
    }

    /// <summary>
    /// Las colecciones se reemplazan en bloque. Si se acumularan, pasar tres veces dejaría el
    /// mismo requisito repetido tres veces en la ficha que ve el ciudadano.
    /// </summary>
    [Fact]
    public async Task Las_colecciones_se_reemplazan_y_no_se_acumulan()
    {
        var (e, t) = await ExpedienteConTramiteAsync();
        _ctx.Requisitos.Add(new TramiteRequisito { ExpedienteId = e.Id, TramiteIndex = 0, Orden = 0, Requisito = "Cédula" });
        await _ctx.SaveChangesAsync();

        var creada = await Pasar(e.Id, t.TramiteIndex);
        await Pasar(e.Id, t.TramiteIndex);
        await Pasar(e.Id, t.TramiteIndex);

        var ficha = await _ctx.TramitesSiger.Include(f => f.Requisitos)
            .FirstAsync(f => f.Id == creada.TramiteSigerId);

        ficha.Requisitos.Should().HaveCount(1);
    }

    /// <summary>Los pasos son de SIGER y no viajan (D-11): el expediente modela el flujo con otro
    /// vocabulario y volcarlo encima los destruiría.</summary>
    [Fact]
    public async Task Los_pasos_de_la_ficha_sobreviven_al_pase()
    {
        var (e, t) = await ExpedienteConTramiteAsync();
        var creada = await Pasar(e.Id, t.TramiteIndex);

        var ficha = await _ctx.TramitesSiger.FirstAsync(f => f.Id == creada.TramiteSigerId);
        _ctx.PasosSiger.Add(new PasoSiger { TramiteSigerId = ficha.Id, NumeroPaso = 1, Descripcion = "Recibir" });
        await _ctx.SaveChangesAsync();

        await Pasar(e.Id, t.TramiteIndex);

        (await _ctx.PasosSiger.CountAsync(p => p.TramiteSigerId == ficha.Id)).Should().Be(1);
    }

    // ── La vista previa ───────────────────────────────────────────────────────

    [Fact]
    public async Task La_vista_previa_de_una_ficha_nueva_la_declara_nueva()
    {
        var (e, t) = await ExpedienteConTramiteAsync();

        var previa = await VistaPrevia(e.Id, t.TramiteIndex);

        previa.EsNueva.Should().BeTrue();
        previa.HayAlgoQuePasar.Should().BeTrue();
        previa.Cambios.Should().Contain(c => c.Campo == "Nombre" && c.Despues == "Permiso de importación");
    }

    /// <summary>
    /// Si nada cambió, el diálogo tiene que decirlo. Enseñar una lista de cambios vacía y aun así
    /// invitar a confirmar haría que la gente confirmara por costumbre.
    /// </summary>
    [Fact]
    public async Task Pasar_dos_veces_sin_tocar_nada_no_reporta_cambios()
    {
        var (e, t) = await ExpedienteConTramiteAsync();
        await Pasar(e.Id, t.TramiteIndex);

        var previa = await VistaPrevia(e.Id, t.TramiteIndex);

        previa.EsNueva.Should().BeFalse();
        previa.Cambios.Should().BeEmpty();
        previa.HayAlgoQuePasar.Should().BeFalse();
    }

    [Fact]
    public async Task La_vista_previa_dice_de_que_a_que_cambia_cada_campo()
    {
        var (e, t) = await ExpedienteConTramiteAsync();
        await Pasar(e.Id, t.TramiteIndex);

        var tramite = await _ctx.Tramites.FirstAsync(x => x.Id == t.Id);
        tramite.NombreTramite = "Permiso corregido";
        await _ctx.SaveChangesAsync();

        var previa = await VistaPrevia(e.Id, t.TramiteIndex);

        var cambio = previa.Cambios.Single(c => c.Campo == "Nombre");
        cambio.Antes.Should().Be("Permiso de importación");
        cambio.Despues.Should().Be("Permiso corregido");
    }

    /// <summary>
    /// La vista previa no puede mentir sobre lo que el pase va a hacer: si la lista de campos que
    /// compara y la que escribe se separaran, el diálogo diría «no cambia nada» mientras el pase
    /// sobrescribe. Se comprueba corriendo los dos y contrastando.
    /// </summary>
    [Fact]
    public async Task Lo_que_anuncia_la_vista_previa_es_lo_que_el_pase_escribe()
    {
        var (e, t) = await ExpedienteConTramiteAsync();
        var creada = await Pasar(e.Id, t.TramiteIndex);

        var tramite = await _ctx.Tramites.FirstAsync(x => x.Id == t.Id);
        tramite.NombreTramite   = "Otro nombre";
        tramite.Objetivo        = "Otro objetivo";
        tramite.EsGratuito      = true;
        tramite.Temporalidad    = "Estacional";
        await _ctx.SaveChangesAsync();

        var previa = await VistaPrevia(e.Id, t.TramiteIndex);
        await Pasar(e.Id, t.TramiteIndex);

        var ficha = await _ctx.TramitesSiger.AsNoTracking().FirstAsync(f => f.Id == creada.TramiteSigerId);

        previa.Cambios.Single(c => c.Campo == "Nombre").Despues.Should().Be(ficha.Nombre);
        previa.Cambios.Single(c => c.Campo == "Objetivo").Despues.Should().Be(ficha.Objetivo);
        previa.Cambios.Single(c => c.Campo == "¿Es gratuito?").Despues.Should().Be("Sí");
        ficha.CostoEsGratuito.Should().BeTrue();
        previa.Cambios.Single(c => c.Campo == "Temporalidad").Despues.Should().Be(ficha.Temporalidad);
    }

    // ── Armado ────────────────────────────────────────────────────────────────

    private Task<ResultadoPase> Pasar(int expedienteId, int tramiteIndex) =>
        new PasarASigerCommandHandler(_ctx).Handle(
            new PasarASigerCommand(expedienteId, tramiteIndex), CancellationToken.None);

    private Task<VistaPreviaPase> VistaPrevia(int expedienteId, int tramiteIndex) =>
        new GetVistaPreviaPaseQueryHandler(_ctx).Handle(
            new GetVistaPreviaPaseQuery(expedienteId, tramiteIndex), CancellationToken.None);

    private async Task<(Expediente, ExpedienteTramite)> ExpedienteConTramiteAsync()
    {
        _ctx.Instituciones.Add(Institucion.Crear("ADUANAS", "Aduanas"));

        var e = Expediente.Crear("EXP-100", "ADUANAS", null, null, "Aduanas", "Analista");
        _ctx.Expedientes.Add(e);
        await _ctx.SaveChangesAsync();

        var t = new ExpedienteTramite
        {
            ExpedienteId = e.Id,
            TramiteIndex = 0,
            ClaveEstable = Guid.NewGuid(),
            NombreTramite = "Permiso de importación",
            FechaCreacion = DateOnly.FromDateTime(DateTime.Today),
            Objetivo = "Autorizar la importación",
            TiempoReal = "5 días hábiles",
            Modalidad = ModalidadPublica.Hibrido
        };
        _ctx.Tramites.Add(t);
        await _ctx.SaveChangesAsync();

        return (e, t);
    }

    private static TramiteSiger Ficha(string codigo, string nombre) => new()
    {
        Codigo = codigo, Nombre = nombre, Institucion = "Aduanas",
        InstitucionId = "ADUANAS", Sigla = "ADUANAS", EstadoSiger = "Registrado"
    };
}
