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
/// Entrega 2 (2026-10-22): el recordatorio manual devuelve lo que pasó —avisos de campana y
/// correos intentados, enviados y fallidos— para que la pantalla no diga «enviado exitosamente»
/// cuando el correo no salió.
/// </summary>
public sealed class ResultadoRecordatorioTests : IDisposable
{
    private readonly AppDbContext _ctx = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

    private async Task<ResultadoRecordatorio> RecordarTicketAsync(ResultadoCorreo cadaEnvio)
    {
        var agente = Usuario.Crear("Agente", "agente@prueba.local", "hash");
        _ctx.Usuarios.Add(agente);
        var t = Ticket.Crear("TCK-2026-0002", "Falla");
        t.PrioridadId = PrioridadesTicketDePrueba.Media(_ctx);
        t.EstablecerReportante("Reporta", "reporta@prueba.local", null);
        t.Asignar(agente.Id, agente.Nombre, "Sistema");
        _ctx.Tickets.Add(t);
        await _ctx.SaveChangesAsync();

        var email = Substitute.For<IEmailService>();
        email.SendEmailAsync(default!, default!, default!, default, default).ReturnsForAnyArgs(cadaEnvio);

        return await new EnviarRecordatorioTicketCommandHandler(_ctx, Substitute.For<INotificacionService>(), email,
                Options.Create(new CorreoOptions()), Options.Create(new InstitucionOptions()))
            .Handle(new EnviarRecordatorioTicketCommand(t.Id), CancellationToken.None);
    }

    [Fact]
    public async Task Con_el_correo_bien_todo_sale()
    {
        var r = await RecordarTicketAsync(ResultadoCorreo.Enviado);

        r.AvisosCampana.Should().Be(1, "el agente tiene usuario; el reportante no");
        r.CorreosIntentados.Should().Be(2);
        r.CorreosEnviados.Should().Be(2);
        r.CorreosFallidos.Should().Be(0);
        r.ServidorSinConfigurar.Should().BeFalse();
    }

    [Fact]
    public async Task Con_el_correo_caido_la_campana_llega_y_los_correos_cuentan_como_fallidos()
    {
        var r = await RecordarTicketAsync(ResultadoCorreo.Fallido);

        r.AvisosCampana.Should().Be(1);
        r.CorreosFallidos.Should().Be(2);
        r.CorreosEnviados.Should().Be(0);
    }

    [Fact]
    public async Task Sin_servidor_configurado_se_dice_asi_y_no_cuenta_como_falla()
    {
        var r = await RecordarTicketAsync(ResultadoCorreo.SinConfigurar);

        r.ServidorSinConfigurar.Should().BeTrue();
        r.CorreosFallidos.Should().Be(0);
        r.AvisosCampana.Should().Be(1);
    }

    public void Dispose() => _ctx.Dispose();
}
