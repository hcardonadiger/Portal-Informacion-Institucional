using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Common.Tiempo;
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Application.Notificaciones.Commands.EnviarRecordatorioManual;
using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Notificaciones;

public class EnviarRecordatorioTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly INotificacionService _notifSvc;
    private readonly IEmailService _emailSvc;

    public EnviarRecordatorioTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser(), NSubstitute.Substitute.For<MediatR.IPublisher>());
        _notifSvc = Substitute.For<INotificacionService>();
        _emailSvc = Substitute.For<IEmailService>();
    }

    [Fact]
    public async Task EnviarRecordatorioTicket_NotificaAgenteYReportante()
    {
        var agenteId = Guid.NewGuid();
        var agente = Usuario.Crear("Agente Soporte", "agente@diger.gob.hn", "hash123");
        // Forzar Id del agente vía reflección para que coincida
        typeof(Usuario).GetProperty("Id")!.SetValue(agente, agenteId);
        _ctx.Usuarios.Add(agente);

        var t = Ticket.Crear("TCK-2026-9999", "Ticket de prueba");
        t.EstablecerReportante("Cliente Test", "cliente@test.com", "+504 99998888");
        t.Asignar(agenteId, "Agente Soporte", "Sistema");
        _ctx.Tickets.Add(t);
        await _ctx.SaveChangesAsync();

        var handler = new EnviarRecordatorioTicketCommandHandler(_ctx, _notifSvc, _emailSvc);
        var result = await handler.Handle(new EnviarRecordatorioTicketCommand(t.Id, "Recordatorio urgente"), CancellationToken.None);

        result.Should().BeGreaterThan(0);
        _notifSvc.Received(1).Encolar(agenteId, Domain.Enums.TipoNotificacion.RecordatorioManualTicket, Arg.Any<string>(), Arg.Any<string>());
        await _emailSvc.Received(1).SendEmailAsync("cliente@test.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static EnviarRecordatorioReunionCommandHandler Handler(
        AppDbContext ctx, INotificacionService notif, IEmailService email) =>
        new(ctx, notif, email,
            new RelojInstitucional(Options.Create(new InstitucionOptions())),
            Options.Create(new InstitucionOptions()));

    [Fact]
    public async Task EnviarRecordatorioReunion_NotificaAsistentes()
    {
        var r = Reunion.Crear("Reunión de Avance");
        r.Fecha = DateOnly.FromDateTime(DateTime.Today).AddDays(3);
        r.Hora  = "09:00";
        r.RegistrarAsistente("Asistente 1", "Cargo", "DIGER", "Tecnología", "asistente1@diger.gob.hn", "+504 88887777");
        _ctx.Reuniones.Add(r);
        await _ctx.SaveChangesAsync();

        var handler = Handler(_ctx, _notifSvc, _emailSvc);
        var result = await handler.Handle(new EnviarRecordatorioReunionCommand(r.Id, "Mensaje personalizado"), CancellationToken.None);

        result.Should().BeGreaterThan(0);
        await _emailSvc.Received(1).SendEmailAsync(
            "asistente1@diger.gob.hn", Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyList<AdjuntoCorreo>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>El recordatorio lleva la cita adjunta: es lo que permite agregarla al calendario
    /// desde el propio correo, sin entrar al portal.</summary>
    [Fact]
    public async Task EnviarRecordatorioReunion_adjunta_el_archivo_de_calendario()
    {
        var r = Reunion.Crear("Taller de inducción");
        r.Fecha = new DateOnly(2026, 9, 15);
        r.Hora  = "09:00";
        r.DuracionMinutos = 90;
        r.RegistrarAsistente("Asistente 1", null, "DIGER", null, "asistente1@diger.gob.hn", null);
        _ctx.Reuniones.Add(r);
        await _ctx.SaveChangesAsync();

        IReadOnlyList<AdjuntoCorreo>? capturados = null;
        await _emailSvc.SendEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Do<IReadOnlyList<AdjuntoCorreo>>(a => capturados = a), Arg.Any<CancellationToken>());

        await Handler(_ctx, _notifSvc, _emailSvc)
            .Handle(new EnviarRecordatorioReunionCommand(r.Id), CancellationToken.None);

        capturados.Should().ContainSingle();
        capturados![0].TipoContenido.Should().Be("text/calendar");
        capturados![0].Nombre.Should().EndWith(".ics");

        var texto = System.Text.Encoding.UTF8.GetString(capturados![0].Contenido);
        texto.Should().Contain("BEGIN:VEVENT").And.Contain("SUMMARY:Taller de inducción");
        // Sin lista de asistentes: el archivo llega a gente de otras instituciones.
        texto.Should().NotContain("ATTENDEE");
    }

    /// <summary>Una reunión sin fecha no genera adjunto: un .ics sin evento solo confunde.</summary>
    [Fact]
    public async Task EnviarRecordatorioReunion_sin_fecha_no_adjunta_nada()
    {
        var r = Reunion.Crear("Reunión por agendar");
        r.RegistrarAsistente("Asistente 1", null, "DIGER", null, "asistente1@diger.gob.hn", null);
        _ctx.Reuniones.Add(r);
        await _ctx.SaveChangesAsync();

        IReadOnlyList<AdjuntoCorreo>? capturados = null;
        await _emailSvc.SendEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Do<IReadOnlyList<AdjuntoCorreo>>(a => capturados = a), Arg.Any<CancellationToken>());

        await Handler(_ctx, _notifSvc, _emailSvc)
            .Handle(new EnviarRecordatorioReunionCommand(r.Id), CancellationToken.None);

        capturados.Should().BeEmpty();
    }

    public void Dispose() => _ctx.Dispose();
}
