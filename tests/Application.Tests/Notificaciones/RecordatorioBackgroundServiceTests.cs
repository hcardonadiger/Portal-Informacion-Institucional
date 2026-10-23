using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Notifications;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Notificaciones;

/// <summary>Reloj fijo para probar a una hora determinada.</summary>
internal sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ahora;
}

/// <summary>
/// Las fechas del servicio de recordatorios automáticos. «Hoy» y «mañana» son los de Honduras
/// (UTC-6, sin horario de verano), no los de UTC: de 6:00 p. m. a medianoche UTC ya va por el día
/// siguiente, y el servicio daba por vencido lo que vence hoy y avisaba de la reunión de pasado
/// mañana como si fuera la de mañana (corregido el 2026-10-22).
/// </summary>
public sealed class RecordatorioBackgroundServiceTests : IDisposable
{
    // 19 de octubre de 2026, 7:00 p. m. en Honduras = 20 de octubre, 01:00 UTC.
    internal static readonly DateTimeOffset SieteDeLaNoche = new(2026, 10, 20, 1, 0, 0, TimeSpan.Zero);
    // 19 de octubre de 2026, 9:00 a. m. en Honduras = 19 de octubre, 15:00 UTC.
    internal static readonly DateTimeOffset NueveDeLaManana = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);

    internal static readonly DateOnly Hoy    = new(2026, 10, 19);
    internal static readonly DateOnly Manana = new(2026, 10, 20);

    private readonly AppDbContext _ctx;
    private readonly Usuario _ana = Usuario.Crear("Ana Responsable", "ana@prueba.local", "hash");

    public RecordatorioBackgroundServiceTests()
    {
        _ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());
        _ctx.Usuarios.Add(_ana);
        _ctx.SaveChanges();
    }

    private readonly AjustesDePrueba _ajustes = new();
    private readonly IDespachadorCorreos _despachador = Substitute.For<IDespachadorCorreos>();

    private RecordatorioBackgroundService Servicio(DateTimeOffset ahora, NotificacionesOptions? opciones = null)
    {
        var reloj = new RelojFijo(ahora);
        var opts  = opciones ?? new NotificacionesOptions();
        var services = new ServiceCollection();
        services.AddSingleton<IApplicationDbContext>(_ctx);
        services.AddSingleton<IAjustesNotificaciones>(_ajustes);
        services.AddSingleton<INotificacionService>(NotificacionesDePrueba.Servicio(_ctx, _ajustes, reloj, opts));
        services.AddSingleton(_despachador);
        var proveedor = services.BuildServiceProvider();

        return new RecordatorioBackgroundService(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(opts), reloj, new SenalRevision(),
            NullLogger<RecordatorioBackgroundService>.Instance);
    }

    private Reunion ReunionDeAna(DateOnly fecha, string titulo = "Comité")
    {
        var r = Reunion.Crear(titulo);
        r.Fecha = fecha;
        r.CreadoPorId = _ana.Id;
        _ctx.Reuniones.Add(r);
        _ctx.SaveChanges();
        return r;
    }

    private void CompromisoDeAna(DateOnly plazo)
    {
        var r = ReunionDeAna(new DateOnly(2026, 10, 1), "Reunión de origen");
        _ctx.Acuerdos.Add(new AcuerdoReunion
        {
            ReunionId   = r.Id,
            Orden       = 1,
            Compromiso  = "Enviar el informe",
            Responsable = _ana.Nombre,
            Plazo       = plazo,
            Estado      = EstadoCompromiso.Pendiente,
        });
        _ctx.SaveChanges();
    }

    private Task<List<TipoNotificacion>> AvisosAsync() =>
        _ctx.Notificaciones.AsNoTracking().Select(n => n.Tipo).ToListAsync();

    [Fact]
    public async Task A_las_siete_de_la_noche_un_compromiso_que_vence_hoy_no_sale_como_vencido()
    {
        CompromisoDeAna(Hoy);

        await Servicio(SieteDeLaNoche).ProcesarAsync(CancellationToken.None);

        var avisos = await AvisosAsync();
        avisos.Should().NotContain(TipoNotificacion.CompromisoVencido);
        avisos.Should().Contain(TipoNotificacion.CompromisoProximo, "vence hoy: está por vencer");
    }

    [Fact]
    public async Task A_las_siete_de_la_noche_el_compromiso_de_ayer_si_esta_vencido()
    {
        CompromisoDeAna(Hoy.AddDays(-1));

        await Servicio(SieteDeLaNoche).ProcesarAsync(CancellationToken.None);

        (await AvisosAsync()).Should().Contain(TipoNotificacion.CompromisoVencido);
    }

    [Fact]
    public async Task A_las_siete_de_la_noche_avisa_de_la_reunion_de_manana_y_no_de_la_de_pasado_manana()
    {
        var deManana       = ReunionDeAna(Manana, "Mañana");
        var dePasadoManana = ReunionDeAna(Manana.AddDays(1), "Pasado mañana");

        await Servicio(SieteDeLaNoche).ProcesarAsync(CancellationToken.None);

        var urls = await _ctx.Notificaciones.AsNoTracking()
            .Where(n => n.Tipo == TipoNotificacion.ReuniónMañana).Select(n => n.Url).ToListAsync();
        urls.Should().Equal($"/Reuniones/Editor/{deManana.Id}");
        urls.Should().NotContain($"/Reuniones/Editor/{dePasadoManana.Id}");
    }

    [Fact]
    public async Task En_la_manana_el_calculo_no_cambia()
    {
        CompromisoDeAna(Hoy);
        ReunionDeAna(Manana);

        await Servicio(NueveDeLaManana).ProcesarAsync(CancellationToken.None);

        var avisos = await AvisosAsync();
        avisos.Should().NotContain(TipoNotificacion.CompromisoVencido);
        avisos.Should().Contain([TipoNotificacion.CompromisoProximo, TipoNotificacion.ReuniónMañana]);
    }

    [Fact]
    public async Task Dos_corridas_el_mismo_dia_de_Honduras_no_repiten_avisos()
    {
        // La primera corrida es a las 9:00 a. m. y la segunda a las 7:00 p. m. del mismo día en
        // Honduras, que en UTC ya son fechas distintas. «Ya se avisó hoy» tiene que medirse con el
        // día de Honduras; si no, la segunda corrida repite el aviso.
        CompromisoDeAna(Hoy);

        await Servicio(NueveDeLaManana).ProcesarAsync(CancellationToken.None);
        await Servicio(SieteDeLaNoche).ProcesarAsync(CancellationToken.None);

        (await AvisosAsync()).Should().Equal([TipoNotificacion.CompromisoProximo],
            "un solo aviso en el día, y de «próximo a vencer»: hoy todavía no venció");
    }

    [Fact]
    public async Task La_zona_horaria_es_configurable()
    {
        // Con la zona en UTC, a la 01:00 UTC del 20 el compromiso del 19 sí está vencido.
        CompromisoDeAna(Hoy);

        await Servicio(SieteDeLaNoche, new NotificacionesOptions { ZonaHoraria = "UTC" })
            .ProcesarAsync(CancellationToken.None);

        (await AvisosAsync()).Should().Contain(TipoNotificacion.CompromisoVencido);
    }

    [Fact]
    public async Task Cada_revision_queda_anotada_y_despacha_el_correo()
    {
        await Servicio(NueveDeLaManana).ProcesarAsync(CancellationToken.None);

        (await _ctx.AjustesNotificaciones.SingleAsync()).UltimaRevisionUtc.Should().Be(NueveDeLaManana.UtcDateTime);
        await _despachador.Received(1).DespacharAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_primera_revision_crea_la_fila_con_el_intervalo_vigente()
    {
        _ajustes.IntervaloMinutos = 30;

        await Servicio(NueveDeLaManana).ProcesarAsync(CancellationToken.None);

        (await _ctx.AjustesNotificaciones.SingleAsync()).IntervaloMinutos.Should().Be(30);
    }

    // ── Etapas de cronograma de Expedientes ───────────────────────────────

    private void EtapaDeAna(DateOnly fin)
    {
        var exp = Expediente.Crear("EXP-1", "INST", null, null, "Institución", _ana.Nombre);
        _ctx.Expedientes.Add(exp);
        _ctx.SaveChanges();
        _ctx.EtapaCronogramas.Add(new ExpedienteEtapaCronograma
        {
            ExpedienteId = exp.Id, TramiteIndex = 0, EtapaNum = "I",
            FechaInicio  = fin.AddDays(-10), FechaFin = fin, Responsable = _ana.Nombre,
        });
        _ctx.SaveChanges();
    }

    [Fact]
    public async Task A_las_siete_de_la_noche_una_etapa_que_vence_hoy_no_sale_como_vencida()
    {
        EtapaDeAna(Hoy);

        await Servicio(SieteDeLaNoche).ProcesarAsync(CancellationToken.None);

        var avisos = await AvisosAsync();
        avisos.Should().NotContain(TipoNotificacion.EtapaCronogramaVencida);
        avisos.Should().Contain(TipoNotificacion.EtapaCronogramaProxima, "vence hoy: está por vencer");
    }

    [Fact]
    public async Task A_las_siete_de_la_noche_la_etapa_de_ayer_si_esta_vencida()
    {
        EtapaDeAna(Hoy.AddDays(-1));

        await Servicio(SieteDeLaNoche).ProcesarAsync(CancellationToken.None);

        (await AvisosAsync()).Should().Contain(TipoNotificacion.EtapaCronogramaVencida);
    }

    [Theory]
    [InlineData("America/Tegucigalpa")]
    [InlineData("Central America Standard Time")]
    public void La_zona_se_encuentra_por_nombre_IANA_o_de_Windows(string nombre)
    {
        var (zona, encontrada) = CalendarioLocal.Resolver(nombre);

        encontrada.Should().BeTrue();
        zona.GetUtcOffset(SieteDeLaNoche).Should().Be(TimeSpan.FromHours(-6));
    }

    [Fact]
    public void Una_zona_que_no_existe_cae_en_UTC_menos_seis_y_lo_avisa()
    {
        var (zona, encontrada) = CalendarioLocal.Resolver("Zona/Inventada");

        encontrada.Should().BeFalse("el servicio lo deja escrito en el registro al arrancar");
        CalendarioLocal.Dia(SieteDeLaNoche, zona).Hoy.Should().Be(Hoy);
    }

    [Fact]
    public void El_dia_local_empieza_a_las_seis_de_la_manana_UTC()
    {
        var (hoy, manana, inicio) = CalendarioLocal.Dia(SieteDeLaNoche, CalendarioLocal.Resolver(null).Zona);

        hoy.Should().Be(Hoy);
        manana.Should().Be(Manana);
        inicio.Should().Be(new DateTime(2026, 10, 19, 6, 0, 0, DateTimeKind.Utc));
        inicio.Kind.Should().Be(DateTimeKind.Utc);
    }

    public void Dispose() => _ctx.Dispose();
}
