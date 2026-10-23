using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Application.Notificaciones.Commands.EnviarRecordatorioManual;
using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        _notifSvc.EncolarAsync(default, default, default!).ReturnsForAnyArgs(true);
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
        t.PrioridadId = PrioridadesTicketDePrueba.Media(_ctx);
        t.EstablecerReportante("Cliente Test", "cliente@test.com", "+504 99998888");
        t.Asignar(agenteId, "Agente Soporte", "Sistema");
        _ctx.Tickets.Add(t);
        await _ctx.SaveChangesAsync();

        var handler = new EnviarRecordatorioTicketCommandHandler(_ctx, _notifSvc, _emailSvc, Microsoft.Extensions.Options.Options.Create(new Diger.TramitesEstado.Application.Common.Models.CorreoOptions()), Microsoft.Extensions.Options.Options.Create(new Diger.TramitesEstado.Application.Common.Models.InstitucionOptions()), AjustesDePrueba.CorreoEncendido());
        var result = await handler.Handle(new EnviarRecordatorioTicketCommand(t.Id, "Recordatorio urgente"), CancellationToken.None);

        result.AvisosCampana.Should().BeGreaterThan(0);
        await _notifSvc.Received(1).EncolarAsync(agenteId, Domain.Enums.TipoNotificacion.RecordatorioManualTicket, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _emailSvc.Received(1).SendEmailAsync("cliente@test.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TipoCorreo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnviarRecordatorioReunion_NotificaAsistentes()
    {
        var r = Reunion.Crear("Reunión de Avance");
        r.RegistrarAsistente("Asistente 1", "Cargo", "DIGER", "Tecnología", "asistente1@diger.gob.hn", "+504 88887777");
        _ctx.Reuniones.Add(r);
        await _ctx.SaveChangesAsync();

        var handler = new EnviarRecordatorioReunionCommandHandler(_ctx, _notifSvc, _emailSvc, Microsoft.Extensions.Options.Options.Create(new Diger.TramitesEstado.Application.Common.Models.CorreoOptions()), Microsoft.Extensions.Options.Options.Create(new Diger.TramitesEstado.Application.Common.Models.InstitucionOptions()), AjustesDePrueba.CorreoEncendido());
        var result = await handler.Handle(new EnviarRecordatorioReunionCommand(r.Id, "Mensaje personalizado"), CancellationToken.None);

        // El asistente no tiene usuario en el portal: no hay aviso de campana, solo correo.
        result.CorreosIntentados.Should().Be(1);
        await _emailSvc.Received(1).SendEmailAsync("asistente1@diger.gob.hn", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TipoCorreo>(), Arg.Any<CancellationToken>());
    }

    public void Dispose() => _ctx.Dispose();
}
