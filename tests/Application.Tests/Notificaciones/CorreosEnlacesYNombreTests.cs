using System.Text.RegularExpressions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Application.Notificaciones.Commands.EnviarRecordatorioManual;
using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Application.Usuarios.Commands.SolicitarRecuperacionPassword;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Notificaciones;

/// <summary>
/// Entrega 2 (2026-10-22): los enlaces de los recordatorios eran relativos y no se abren desde el
/// programa de correo; ahora se arman con <c>Correo:UrlBase</c>. Y el nombre de la institución en
/// los asuntos y en la firma de la recuperación estaba escrito a mano («DIGER», «Dirección de
/// Gestión de Riesgos»); ahora sale de la sección <c>Institucion</c> de la configuración.
/// </summary>
public sealed class CorreosEnlacesYNombreTests : IDisposable
{
    private const string UrlBase = "https://portal.prueba.local/GestionGD";

    private static readonly InstitucionOptions Institucion = new()
    {
        Nombre = "Instituto de Prueba", NombreCorto = "INSTP",
    };

    private readonly AppDbContext _ctx = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

    private readonly IEmailService _email = Substitute.For<IEmailService>();
    private readonly List<(string Asunto, string Cuerpo)> _correos = [];

    private static readonly IOptions<CorreoOptions> Correo = Options.Create(new CorreoOptions { UrlBase = UrlBase });
    private static readonly IOptions<InstitucionOptions> Inst = Options.Create(Institucion);

    public CorreosEnlacesYNombreTests()
    {
        _email.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<TipoCorreo>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(c => _correos.Add((c.ArgAt<string>(1), c.ArgAt<string>(2))));
    }

    private static IEnumerable<string> Enlaces(string cuerpo) =>
        Regex.Matches(cuerpo, """href=['"]([^'"]+)['"]""").Select(m => m.Groups[1].Value);

    private void TodosAbsolutosYSinNombreAMano()
    {
        _correos.Should().NotBeEmpty();
        foreach (var (asunto, cuerpo) in _correos)
        {
            asunto.Should().NotContain("DIGER").And.EndWith("— INSTP");
            cuerpo.Should().NotContain("DIGER").And.NotContain("Gestión de Riesgos");
            // El correo al reportante de un ticket y al contacto de un expediente no llevan enlace.
            foreach (var enlace in Enlaces(cuerpo))
                enlace.Should().StartWith(UrlBase + "/", "un enlace relativo no se abre desde el programa de correo");
        }
    }

    [Fact]
    public async Task Recordatorio_de_ticket()
    {
        var agente = Usuario.Crear("Agente", "agente@prueba.local", "hash");
        _ctx.Usuarios.Add(agente);
        var t = Ticket.Crear("TCK-2026-0001", "Falla");
        t.PrioridadId = PrioridadesTicketDePrueba.Media(_ctx);
        t.EstablecerReportante("Reporta", "reporta@prueba.local", null);
        t.Asignar(agente.Id, agente.Nombre, "Sistema");
        _ctx.Tickets.Add(t);
        await _ctx.SaveChangesAsync();

        await new EnviarRecordatorioTicketCommandHandler(_ctx, Substitute.For<INotificacionService>(), _email, Correo, Inst)
            .Handle(new EnviarRecordatorioTicketCommand(t.Id), CancellationToken.None);

        _correos.Should().HaveCount(2);
        _correos.Should().Contain(c => c.Cuerpo.Contains($"{UrlBase}/Tickets/Detalle/{t.Id}"));
        TodosAbsolutosYSinNombreAMano();
    }

    [Fact]
    public async Task Recordatorio_de_expediente()
    {
        var analista = Usuario.Crear("Analista", "analista@prueba.local", "hash");
        _ctx.Usuarios.Add(analista);
        var exp = Expediente.Crear("EXP-1", "INST", null, null, "Institución", analista.Nombre);
        exp.AnalistaId     = analista.Id;
        exp.ContactoCorreo = "contacto@prueba.local";
        _ctx.Expedientes.Add(exp);
        await _ctx.SaveChangesAsync();

        await new EnviarRecordatorioExpedienteCommandHandler(_ctx, Substitute.For<INotificacionService>(), _email, Correo, Inst)
            .Handle(new EnviarRecordatorioExpedienteCommand(exp.Id), CancellationToken.None);

        _correos.Should().HaveCount(2);
        _correos.Should().Contain(c => c.Cuerpo.Contains($"{UrlBase}/Expedientes/Editor/{exp.Id}"));
        TodosAbsolutosYSinNombreAMano();
    }

    [Fact]
    public async Task Recordatorio_de_reunion()
    {
        var r = Reunion.Crear("Comité");
        r.RegistrarAsistente("Ana", null, null, null, "ana@prueba.local", null);
        _ctx.Reuniones.Add(r);
        await _ctx.SaveChangesAsync();

        await new EnviarRecordatorioReunionCommandHandler(_ctx, Substitute.For<INotificacionService>(), _email, Correo, Inst)
            .Handle(new EnviarRecordatorioReunionCommand(r.Id), CancellationToken.None);

        _correos.Should().ContainSingle().Which.Cuerpo.Should().Contain($"{UrlBase}/Reuniones/Acta/{r.Id}");
        TodosAbsolutosYSinNombreAMano();
    }

    [Fact]
    public async Task Recordatorio_de_compromiso()
    {
        var r = Reunion.Crear("Mesa técnica");
        _ctx.Reuniones.Add(r);
        await _ctx.SaveChangesAsync();
        var a = new AcuerdoReunion { ReunionId = r.Id, Orden = 1, Compromiso = "Enviar", Responsable = "resp@prueba.local" };
        _ctx.Acuerdos.Add(a);
        await _ctx.SaveChangesAsync();

        await new EnviarRecordatorioCompromisoCommandHandler(_ctx, Substitute.For<INotificacionService>(), _email, Correo, Inst)
            .Handle(new EnviarRecordatorioCompromisoCommand(a.Id), CancellationToken.None);

        _correos.Should().ContainSingle().Which.Cuerpo.Should().Contain($"{UrlBase}/Reuniones/CompromisoDetalle/{a.Id}");
        TodosAbsolutosYSinNombreAMano();
    }

    [Fact]
    public async Task La_firma_de_la_recuperacion_sale_de_la_configuracion()
    {
        _ctx.Usuarios.Add(Usuario.Crear("Juan", "juan@prueba.local", "hash"));
        await _ctx.SaveChangesAsync();

        await new SolicitarRecuperacionPasswordCommandHandler(_ctx, _ctx, _email, Options.Create(new AuthOptions()),
                Correo, Inst, NullLogger<SolicitarRecuperacionPasswordCommandHandler>.Instance)
            .Handle(new SolicitarRecuperacionPasswordCommand("juan@prueba.local"), CancellationToken.None);

        var cuerpo = _correos.Should().ContainSingle().Subject.Cuerpo;
        cuerpo.Should().NotContain("DIGER").And.NotContain("Gestión de Riesgos");
        cuerpo.Should().Contain("Instituto de Prueba (INSTP)");
    }

    [Fact]
    public void Sin_url_base_el_enlace_queda_como_antes()
    {
        new CorreoOptions().Enlace("/Tickets/Detalle/5").Should().Be("/Tickets/Detalle/5");
        new CorreoOptions { UrlBase = "https://p.local/" }.Enlace("/Tickets/Detalle/5")
            .Should().Be("https://p.local/Tickets/Detalle/5");
    }

    public void Dispose() => _ctx.Dispose();
}
