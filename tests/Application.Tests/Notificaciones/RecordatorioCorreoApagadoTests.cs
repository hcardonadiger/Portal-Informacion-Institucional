using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Common.Models;
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

/// <summary>
/// Panel de control → Notificaciones (2026-10-23): el recordatorio manual obedece el interruptor de
/// correo de su módulo. Con el correo apagado el aviso de la campana sigue saliendo, no se intenta
/// ningún correo y el resultado lo dice para que la pantalla no hable de un servidor caído.
/// </summary>
public sealed class RecordatorioCorreoApagadoTests : IDisposable
{
    private readonly AppDbContext _ctx = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

    private static readonly IOptions<CorreoOptions> Correo = Options.Create(new CorreoOptions { UrlBase = "https://portal.prueba.local" });
    private static readonly IOptions<InstitucionOptions> Inst = Options.Create(new InstitucionOptions { Nombre = "Instituto de Prueba", NombreCorto = "INSTP" });

    private readonly int _ticketId;

    public RecordatorioCorreoApagadoTests()
    {
        var agente = Usuario.Crear("Agente", "agente@prueba.local", "hash");
        _ctx.Usuarios.Add(agente);
        var t = Ticket.Crear("TCK-2026-0003", "Falla");
        t.PrioridadId = PrioridadesTicketDePrueba.Media(_ctx);
        t.EstablecerReportante("Reporta", "reporta@prueba.local", null);
        t.Asignar(agente.Id, agente.Nombre, "Sistema");
        _ctx.Tickets.Add(t);
        _ctx.SaveChanges();
        _ticketId = t.Id;
    }

    [Fact]
    public async Task Con_el_correo_de_Tickets_apagado_no_sale_correo_y_el_resultado_lo_dice()
    {
        var notif = Substitute.For<INotificacionService>();
        notif.EncolarAsync(default, default, default!).ReturnsForAnyArgs(true);
        var email = Substitute.For<IEmailService>();
        var ajustes = new AjustesDePrueba(); // correo apagado en todos, como el despliegue

        var r = await new EnviarRecordatorioTicketCommandHandler(_ctx, notif, email, Correo, Inst, ajustes)
            .Handle(new EnviarRecordatorioTicketCommand(_ticketId), CancellationToken.None);

        await email.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!, default, default);
        r.CorreoApagado.Should().BeTrue();
        r.AvisosCampana.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Con_el_correo_apagado_tampoco_salen_los_de_expediente_reunion_y_compromiso()
    {
        var analista = Usuario.Crear("Analista", "analista@prueba.local", "hash");
        _ctx.Usuarios.Add(analista);
        var exp = Expediente.Crear("EXP-1", "INST", null, null, "Institución", analista.Nombre);
        exp.AnalistaId     = analista.Id;
        exp.ContactoCorreo = "contacto@prueba.local";
        _ctx.Expedientes.Add(exp);
        var reunion = Reunion.Crear("Comité");
        reunion.RegistrarAsistente("Ana", null, null, null, "ana@prueba.local", null);
        _ctx.Reuniones.Add(reunion);
        await _ctx.SaveChangesAsync();
        var acuerdo = new AcuerdoReunion { ReunionId = reunion.Id, Orden = 1, Compromiso = "Enviar", Responsable = "resp@prueba.local" };
        _ctx.Acuerdos.Add(acuerdo);
        await _ctx.SaveChangesAsync();

        var notif = Substitute.For<INotificacionService>();
        notif.EncolarAsync(default, default, default!).ReturnsForAnyArgs(true);
        var email = Substitute.For<IEmailService>();
        var ajustes = new AjustesDePrueba();

        var resultados = new[]
        {
            await new EnviarRecordatorioExpedienteCommandHandler(_ctx, notif, email, Correo, Inst, ajustes)
                .Handle(new EnviarRecordatorioExpedienteCommand(exp.Id), CancellationToken.None),
            await new EnviarRecordatorioReunionCommandHandler(_ctx, notif, email, Correo, Inst, ajustes)
                .Handle(new EnviarRecordatorioReunionCommand(reunion.Id), CancellationToken.None),
            await new EnviarRecordatorioCompromisoCommandHandler(_ctx, notif, email, Correo, Inst, ajustes)
                .Handle(new EnviarRecordatorioCompromisoCommand(acuerdo.Id), CancellationToken.None),
        };

        await email.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!, default, default);
        resultados.Should().AllSatisfy(r =>
        {
            r.CorreoApagado.Should().BeTrue();
            r.CorreosIntentados.Should().Be(0);
        });
    }

    public void Dispose() => _ctx.Dispose();
}
