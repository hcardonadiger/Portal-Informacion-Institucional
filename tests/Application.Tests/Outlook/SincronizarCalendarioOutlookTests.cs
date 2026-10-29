using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Outlook;

/// <summary>Las reglas del spec 2026-10-29 sobre la base en memoria y un calendario simulado.
/// «Hoy» es el 2026-11-02 en Honduras (18:00 UTC = 12:00 local).</summary>
public class SincronizarCalendarioOutlookTests : IDisposable
{
    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private readonly AppDbContext _ctx;
    private readonly ICalendarioOutlookLectura _cal = Substitute.For<ICalendarioOutlookLectura>();
    private readonly RelojFijo _reloj = new(new DateTimeOffset(2026, 11, 2, 18, 0, 0, TimeSpan.Zero));
    private readonly Usuario _ana = Usuario.Crear("Ana", "ana@diger.gob.hn", "hash");
    private static readonly DateOnly Hoy = new(2026, 11, 2);

    public SincronizarCalendarioOutlookTests()
    {
        var usuario = Substitute.For<ICurrentUserService>();
        usuario.EsGlobal.Returns(true);
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _ctx = new AppDbContext(opts, usuario, Substitute.For<MediatR.IPublisher>());
        _ctx.Usuarios.Add(_ana);
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(_ana.Id, "DIGER", "AREA1", "UNI1", "Empleado"));
        _ctx.ConexionesMicrosoft.Add(ConexionMicrosoft.Crear(_ana.Id, "ana@diger.gob.hn", "h", [1], DateTime.UtcNow));
        _ctx.SaveChanges();
    }

    public void Dispose() => _ctx.Dispose();

    private static CitaOutlook Cita(string id, string titulo = "Mesa", int dias = 1, string? ical = null,
        bool cancelada = false, string hora = "09:00", params string[] invitados) =>
        new(id, ical ?? "ical-" + id, titulo, Hoy.AddDays(dias).ToDateTime(TimeOnly.Parse(hora)),
            Hoy.AddDays(dias).ToDateTime(TimeOnly.Parse(hora)).AddHours(1), false, "Sala", null, false, cancelada,
            new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            invitados.Select(c => new InvitadoCita(null, c, false)).ToList());

    private void Calendario(params CitaOutlook[] citas) =>
        _cal.ListarAsync(_ana.Id, Hoy, Hoy.AddDays(90), Arg.Any<CancellationToken>()).Returns(citas);

    private Task<ResultadoSincronizacionCalendario> Sincronizar() =>
        new SincronizarCalendarioOutlookCommandHandler(_ctx, _cal, _ctx, _reloj)
            .Handle(new SincronizarCalendarioOutlookCommand(_ana.Id), default);

    private Task<List<Reunion>> Importadas() =>
        _ctx.Reuniones.IgnoreQueryFilters().Include(r => r.Asistentes)
            .Where(r => r.OutlookImportadaPorId == _ana.Id).ToListAsync();

    [Fact]
    public async Task Crea_privadas_con_la_asignacion_del_usuario_y_no_duplica()
    {
        Calendario(Cita("e1"), Cita("e2"));

        (await Sincronizar()).Should().Be(new ResultadoSincronizacionCalendario(2, 0, 0, 0));
        (await Sincronizar()).Should().Be(new ResultadoSincronizacionCalendario(0, 0, 0, 0));

        var rs = await Importadas();
        rs.Should().HaveCount(2);
        rs.Should().OnlyContain(r => r.Visibilidad == VisibilidadReunion.Privada && r.CreadoPorId == _ana.Id
                                     && r.InstitucionDuenaId == "DIGER" && r.AreaId == "AREA1" && r.UnidadId == "UNI1");
    }

    [Fact]
    public async Task Actualiza_generales_y_nunca_toca_lo_del_portal()
    {
        Calendario(Cita("e1"));
        await Sincronizar();
        var r = (await Importadas()).Single();
        r.ObjetivoAgenda = "Lo escribió Ana";
        await _ctx.SaveChangesAsync();

        Calendario(Cita("e1", titulo: "Mesa (movida)", hora: "15:00"));
        (await Sincronizar()).Actualizadas.Should().Be(1);

        r = (await Importadas()).Single();
        r.Titulo.Should().Be("Mesa (movida)");
        r.Hora.Should().Be("15:00");
        r.ObjetivoAgenda.Should().Be("Lo escribió Ana");
    }

    [Fact]
    public async Task Salta_las_reuniones_que_el_portal_envio_por_iCalUId_y_por_eventId()
    {
        var delPortal = Reunion.Crear("Del portal");
        delPortal.RegistrarEnvioOutlook("evento-del-portal", _ana.Id, null, "huella", DateTime.UtcNow);
        delPortal.RecordarICalUId("ical-compartido");
        _ctx.Reuniones.Add(delPortal);
        await _ctx.SaveChangesAsync();

        Calendario(Cita("otro-buzon", ical: "ical-compartido"), Cita("evento-del-portal", ical: "sin-registro"));

        (await Sincronizar()).Should().Be(new ResultadoSincronizacionCalendario(0, 0, 0, 2));
        (await Importadas()).Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelada_en_Outlook_se_marca_y_no_se_borra()
    {
        Calendario(Cita("e1"));
        await Sincronizar();

        Calendario(Cita("e1", cancelada: true));
        (await Sincronizar()).Canceladas.Should().Be(1);

        var r = (await Importadas()).Single();
        r.CanceladaEnOutlookEl.Should().NotBeNull();
        r.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Desaparecida_y_borrada_en_Outlook_se_marca_cancelada()
    {
        Calendario(Cita("e1"));
        await Sincronizar();
        Calendario();
        _cal.ObtenerAsync(_ana.Id, "e1", Arg.Any<CancellationToken>()).Returns((CitaOutlook?)null);

        (await Sincronizar()).Canceladas.Should().Be(1);
        (await Importadas()).Single().CanceladaEnOutlookEl.Should().NotBeNull();
    }

    [Fact]
    public async Task Desaparecida_porque_se_movio_mas_alla_de_la_ventana_solo_cambia_de_fecha()
    {
        Calendario(Cita("e1"));
        await Sincronizar();
        Calendario();
        _cal.ObtenerAsync(_ana.Id, "e1", Arg.Any<CancellationToken>()).Returns(Cita("e1", dias: 120));

        var res = await Sincronizar();

        res.Canceladas.Should().Be(0);
        res.Actualizadas.Should().Be(1);
        var r = (await Importadas()).Single();
        r.CanceladaEnOutlookEl.Should().BeNull();
        r.Fecha.Should().Be(Hoy.AddDays(120));
    }

    [Fact]
    public async Task Las_de_fecha_pasada_se_congelan()
    {
        var pasada = Reunion.ImportarDeOutlook("vieja", CitaOutlookTraductor.Traducir(Cita("vieja", dias: -3)), _ana.Id, "DIGER", null, null);
        _ctx.Reuniones.Add(pasada);
        await _ctx.SaveChangesAsync();
        Calendario();

        (await Sincronizar()).Should().Be(new ResultadoSincronizacionCalendario(0, 0, 0, 0));
        await _cal.DidNotReceive().ObtenerAsync(Arg.Any<Guid>(), "vieja", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reaparecida_quita_la_cancelacion()
    {
        Calendario(Cita("e1"));
        await Sincronizar();
        Calendario(Cita("e1", cancelada: true));
        await Sincronizar();

        Calendario(Cita("e1"));
        (await Sincronizar()).Actualizadas.Should().Be(1);

        (await Importadas()).Single().CanceladaEnOutlookEl.Should().BeNull();
    }

    [Fact]
    public async Task Los_invitados_entran_preregistrados_sin_la_duena_del_calendario()
    {
        Calendario(Cita("e1", invitados: ["marta@copeco.gob.hn", "ANA@diger.gob.hn"]));

        await Sincronizar();

        var a = (await Importadas()).Single().Asistentes.Should().ContainSingle().Subject;
        a.Correo.Should().Be("marta@copeco.gob.hn");
        a.EsPreregistro.Should().BeTrue();
        a.DeOutlook.Should().BeTrue();
    }

    [Fact]
    public async Task Cambio_de_invitados_actualiza_y_respeta_la_asistencia_marcada()
    {
        Calendario(Cita("e1", invitados: ["p@x.hn", "a@x.hn"]));
        await Sincronizar();
        (await Importadas()).Single().Asistentes.Single(x => x.Correo == "a@x.hn").Confirmado = true;
        await _ctx.SaveChangesAsync();

        Calendario(Cita("e1", invitados: ["n@x.hn"]));
        (await Sincronizar()).Actualizadas.Should().Be(1);

        (await Importadas()).Single().Asistentes.Select(x => x.Correo)
            .Should().BeEquivalentTo(["a@x.hn", "n@x.hn"]);
    }

    [Fact]
    public async Task Borrada_por_el_usuario_no_se_vuelve_a_crear()
    {
        Calendario(Cita("e1"));
        await Sincronizar();
        var r = (await Importadas()).Single();
        r.IsDeleted = true;
        await _ctx.SaveChangesAsync();

        (await Sincronizar()).Should().Be(new ResultadoSincronizacionCalendario(0, 0, 0, 1));
        (await Importadas()).Should().HaveCount(1);
    }
}
