using System.Net;
using System.Text.RegularExpressions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Application.Notificaciones.Commands.EnviarRecordatorioManual;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Web.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Entrega 2 (2026-10-22): con el correo caído, la pantalla del recordatorio decía «enviado» igual.
/// Ahora dice que el aviso quedó en la campana y el correo no salió. Con el correo bien, cada
/// pantalla conserva su texto de siempre.
/// </summary>
public sealed class RecordatorioCorreoFallidoTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private readonly CorreosCapturados _correos = new();
    private int _ticketId, _reunionId, _compromisoId;

    private const string AdvertenciaFallo =
        "El aviso quedó en la campana de notificaciones, pero el correo no salió.";

    public async Task InitializeAsync()
    {
        _portal.ServiciosDePrueba = s =>
        {
            s.RemoveAll<IEmailService>();
            s.AddSingleton<IEmailService>(_correos);
        };
        await _portal.PrepararAsync();
        await _portal.OtorgarAsync("JefeArea", "Tickets.Ver", "Tickets.Editar", "Reuniones.Ver", "Reuniones.Editar");

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Una persona con usuario en el portal: recibe el aviso de campana y el correo.
        var persona = Usuario.Crear("Persona con usuario", "persona@pruebas.gob.hn", "hash");
        db.Usuarios.Add(persona);

        var t = Ticket.Crear("TCK-2026-0901", "Ticket del recordatorio");
        t.PrioridadId   = PortalFactory.PrioridadTicketPorDefecto;
        t.InstitucionId = "DIGER";
        t.Asignar(persona.Id, persona.Nombre, "Sistema");
        db.Tickets.Add(t);

        var r = Reunion.Crear("Reunión del recordatorio");
        r.InstitucionId      = "DIGER";
        r.InstitucionDuenaId = "DIGER";
        r.AreaId   = PortalFactory.AreaDeCasa;
        r.UnidadId = PortalFactory.UnidadDeCasa;
        r.RegistrarAsistente(persona.Nombre, null, null, null, persona.Correo, null);
        db.Reuniones.Add(r);
        await db.SaveChangesAsync();

        var a = new AcuerdoReunion { ReunionId = r.Id, Orden = 1, Compromiso = "Enviar el informe", Responsable = persona.Correo };
        db.Acuerdos.Add(a);
        await db.SaveChangesAsync();

        _ticketId = t.Id; _reunionId = r.Id; _compromisoId = a.Id;

        // Estas pruebas hablan del servidor de correo: el correo de los módulos va encendido en el
        // Panel de control (sin fila, el despliegue lo deja apagado).
        await CorreoEncendidoAsync(scope.ServiceProvider, ModuloNotificacion.Tickets, ModuloNotificacion.Reuniones);
    }

    /// <summary>Enciende el correo de los módulos en el Panel de control y descarta la caché.</summary>
    internal static async Task CorreoEncendidoAsync(IServiceProvider servicios, params ModuloNotificacion[] modulos)
    {
        var db = servicios.GetRequiredService<AppDbContext>();
        foreach (var m in modulos)
            db.InterruptoresNotificacion.Add(InterruptorNotificacion.Crear(m, correo: true));
        await db.SaveChangesAsync();
        servicios.GetRequiredService<IAjustesNotificaciones>().Invalidar();
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Pulsa «Recordar» en la pantalla y devuelve el HTML que ve la persona después, con
    /// las entidades resueltas.</summary>
    private async Task<string> RecordarAsync(string pagina)
    {
        var cliente = _portal.ClienteComo("JefeArea", area: PortalFactory.AreaDeCasa, unidad: PortalFactory.UnidadDeCasa);
        var html  = await cliente.GetStringAsync(pagina);
        var token = Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;
        token.Should().NotBeEmpty();

        var r = await cliente.PostAsync($"{pagina}?handler=EnviarRecordatorio",
            new FormUrlEncodedContent([new("__RequestVerificationToken", token)]));

        var final = r.StatusCode == HttpStatusCode.Redirect
            ? await cliente.GetStringAsync(r.Headers.Location)
            : await r.Content.ReadAsStringAsync();
        return WebUtility.HtmlDecode(final);
    }

    public static TheoryData<string, string> Pantallas => new()
    {
        { "ticket",     "Notificación de recordatorio enviada exitosamente." },
        { "acta",       "Notificaciones de recordatorio enviadas a los asistentes con correo." },
        { "compromiso", "Notificación de recordatorio enviada al responsable del compromiso." },
    };

    private string Pagina(string cual) => cual switch
    {
        "ticket" => $"/Tickets/Detalle/{_ticketId}",
        "acta"   => $"/Reuniones/Acta/{_reunionId}",
        _        => $"/Reuniones/CompromisoDetalle/{_compromisoId}",
    };

    [Theory]
    [MemberData(nameof(Pantallas))]
    public async Task Con_el_correo_caido_la_pantalla_lo_dice(string cual, string exito)
    {
        _correos.Resultado = ResultadoCorreo.Fallido;

        var html = await RecordarAsync(Pagina(cual));

        html.Should().Contain(AdvertenciaFallo);
        html.Should().Contain("alert-warn");
        html.Should().NotContain(exito, "no se puede decir «enviado» si el correo no salió");
    }

    [Theory]
    [MemberData(nameof(Pantallas))]
    public async Task Con_el_correo_bien_se_ve_igual_que_antes(string cual, string exito)
    {
        _correos.Resultado = ResultadoCorreo.Enviado;

        var html = await RecordarAsync(Pagina(cual));

        html.Should().Contain(exito);
        html.Should().NotContain(AdvertenciaFallo);
    }

    [Theory]
    [MemberData(nameof(Pantallas))]
    public async Task Sin_servidor_de_correo_la_pantalla_lo_dice(string cual, string exito)
    {
        _correos.Resultado = ResultadoCorreo.SinConfigurar;

        var html = await RecordarAsync(Pagina(cual));

        html.Should().Contain("No se envió correo porque el portal todavía no tiene configurado su servidor de correo.");
        html.Should().NotContain(exito);
    }
}

/// <summary>Lo mismo en el editor de expedientes, que antes no mostraba ningún resultado del
/// recordatorio. El éxito sigue sin pintarse ahí, como antes.</summary>
public sealed class RecordatorioExpedienteCorreoFallidoTests : IAsyncLifetime
{
    private readonly PortalFactory _portal = new();
    private readonly CorreosCapturados _correos = new() { Resultado = ResultadoCorreo.Fallido };
    private int _expedienteId;

    public async Task InitializeAsync()
    {
        _portal.ServiciosDePrueba = s =>
        {
            s.RemoveAll<IEmailService>();
            s.AddSingleton<IEmailService>(_correos);
        };
        await _portal.PrepararAsync();

        using var scope = _portal.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var analista = Usuario.Crear("Analista con usuario", "analista@pruebas.gob.hn", "hash");
        db.Usuarios.Add(analista);
        var exp = Expediente.Crear("EXP-2026-901", "DIGER", null, null, "DIGER", analista.Nombre);
        exp.AnalistaId = analista.Id;
        db.Expedientes.Add(exp);
        await db.SaveChangesAsync();
        _expedienteId = exp.Id;

        await RecordatorioCorreoFallidoTests.CorreoEncendidoAsync(scope.ServiceProvider, ModuloNotificacion.Expedientes);
    }

    public Task DisposeAsync()
    {
        _portal.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Con_el_correo_caido_el_editor_lo_dice()
    {
        var cliente = _portal.ClienteComo("Administrador");
        var pagina  = $"/Expedientes/Editor/{_expedienteId}";
        var html    = await cliente.GetStringAsync(pagina);
        var token   = Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)""").Groups[1].Value;
        token.Should().NotBeEmpty();

        var r = await cliente.PostAsync($"{pagina}?handler=EnviarRecordatorio",
            new FormUrlEncodedContent([new("__RequestVerificationToken", token)]));
        r.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var final = WebUtility.HtmlDecode(await cliente.GetStringAsync(r.Headers.Location));
        final.Should().Contain("El aviso quedó en la campana de notificaciones, pero el correo no salió.");
    }
}

/// <summary>Las reglas de <see cref="AvisoRecordatorio"/>, caso por caso.</summary>
public sealed class AvisoRecordatorioTests
{
    private const string Exito = "Texto de siempre.";

    private static (AvisoRecordatorio.Nivel, string) Para(int campana, int intentados, int enviados, bool sinConfigurar = false, bool unico = false) =>
        AvisoRecordatorio.Para(new ResultadoRecordatorio(campana, intentados, enviados, sinConfigurar), Exito, unico);

    [Fact]
    public void Todo_bien_conserva_el_texto_de_la_pantalla() =>
        Para(1, 2, 2).Should().Be((AvisoRecordatorio.Nivel.Exito, Exito));

    [Fact]
    public void Solo_campana_sin_correos_es_exito() =>
        Para(1, 0, 0).Item1.Should().Be(AvisoRecordatorio.Nivel.Exito);

    [Fact]
    public void Campana_y_correo_caido_es_advertencia() =>
        Para(1, 1, 0).Item2.Should().StartWith("El aviso quedó en la campana de notificaciones, pero el correo no salió.");

    [Fact]
    public void Algunos_correos_caidos_dice_cuantos()
    {
        Para(1, 3, 1).Item2.Should().Contain("no salieron 2 de los 3 correos");
        Para(1, 3, 2).Item2.Should().Contain("no salió 1 de los 3 correos");
    }

    [Fact]
    public void Sin_campana_y_algunos_correos_caidos_dice_a_cuantos_salio() =>
        Para(0, 3, 2).Item2.Should().StartWith("El recordatorio salió por correo a 2 de 3 destinatarios");

    [Fact]
    public void Sin_servidor_con_campana_es_advertencia() =>
        Para(1, 1, 0, sinConfigurar: true).Should().Be((AvisoRecordatorio.Nivel.Advertencia,
            "El aviso quedó en la campana de notificaciones. No se envió correo porque el portal todavía no tiene configurado su servidor de correo."));

    [Fact]
    public void Nadie_recibio_nada_es_error()
    {
        Para(0, 1, 0, sinConfigurar: true).Item1.Should().Be(AvisoRecordatorio.Nivel.Error);
        Para(0, 1, 0).Item1.Should().Be(AvisoRecordatorio.Nivel.Error);
        Para(0, 0, 0).Item2.Should().Contain("ni correo registrado");
    }

    [Fact]
    public void En_un_compromiso_los_errores_hablan_del_responsable()
    {
        Para(0, 1, 0, unico: true).Item2.Should().Contain("el responsable no tiene usuario en el portal");
        Para(0, 0, 0, unico: true).Item2.Should().Contain("el compromiso no tiene un responsable");
    }
}
