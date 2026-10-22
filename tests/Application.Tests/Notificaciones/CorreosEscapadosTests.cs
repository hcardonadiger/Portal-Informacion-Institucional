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
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Notificaciones;

/// <summary>
/// S2 (2026-10-22): lo que escriben las personas —nombres, títulos, el mensaje del recordatorio—
/// entraba sin escapar al HTML de los correos. Un título con <c>&lt;b&gt;</c> o un mensaje con
/// <c>&lt;script&gt;</c> se volvía marcado. Ahora se escapa solo eso, y la plantilla queda igual.
/// </summary>
public sealed class CorreosEscapadosTests : IDisposable
{
    private readonly AppDbContext _ctx = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

    private readonly IEmailService _email = Substitute.For<IEmailService>();
    private readonly List<string> _cuerpos = [];

    public CorreosEscapadosTests()
    {
        _email.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<string>(b => _cuerpos.Add(b)),
            Arg.Any<TipoCorreo>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(ResultadoCorreo.Enviado));
    }

    private const string Peligroso = "<script>robar()</script>";
    private const string Escapado  = "&lt;script&gt;robar()&lt;/script&gt;";

    // ── El ayudante ──────────────────────────────────────────────────────────

    [Fact]
    public void E_escapa_los_cinco_caracteres_de_HTML()
    {
        PlantillasCorreo.E("a & b < c > d \" e ' f").Should().Be("a &amp; b &lt; c &gt; d &quot; e &#39; f");
    }

    [Fact]
    public void E_no_toca_las_tildes_ni_la_enie()
    {
        PlantillasCorreo.E("Revisión de Señales — Área Técnica ¿Ñandú?").Should().Be("Revisión de Señales — Área Técnica ¿Ñandú?");
    }

    [Fact]
    public void E_de_nulo_es_vacio() => PlantillasCorreo.E(null).Should().BeEmpty();

    // ── Los correos ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Recordatorio_de_ticket_escapa_nombre_titulo_y_mensaje_para_agente_y_reportante()
    {
        var agente = Usuario.Crear("Ana <b>Agente</b>", "agente@prueba.local", "hash");
        _ctx.Usuarios.Add(agente);
        var t = Ticket.Crear("TCK-2026-0001", "Falla <i>grave</i>");
        t.PrioridadId = PrioridadesTicketDePrueba.Media(_ctx);
        t.EstablecerReportante("Luis <u>Reporta</u>", "reporta@prueba.local", "+504 0000-0000");
        t.Asignar(agente.Id, agente.Nombre, "Sistema");
        _ctx.Tickets.Add(t);
        await _ctx.SaveChangesAsync();

        await new EnviarRecordatorioTicketCommandHandler(_ctx, Substitute.For<INotificacionService>(), _email, Options.Create(new CorreoOptions()), Options.Create(new InstitucionOptions()))
            .Handle(new EnviarRecordatorioTicketCommand(t.Id, Peligroso), CancellationToken.None);

        _cuerpos.Should().HaveCount(2);
        _cuerpos.Should().AllSatisfy(c =>
        {
            c.Should().NotContain("<script>").And.NotContain("<i>grave</i>");
            c.Should().Contain(Escapado).And.Contain("Falla &lt;i&gt;grave&lt;/i&gt;");
        });
        _cuerpos.Should().Contain(c => c.Contains("Ana &lt;b&gt;Agente&lt;/b&gt;"));
        _cuerpos.Should().Contain(c => c.Contains("Luis &lt;u&gt;Reporta&lt;/u&gt;"));
    }

    [Fact]
    public async Task Recordatorio_de_reunion_escapa_asistente_titulo_y_mensaje()
    {
        var r = Reunion.Crear("Revisión <b>x</b>");
        r.RegistrarAsistente("Ana <script>alert(1)</script>", "Cargo", "DIGER", "Área", "ana@prueba.local", "+504 0000-0000");
        _ctx.Reuniones.Add(r);
        await _ctx.SaveChangesAsync();

        await new EnviarRecordatorioReunionCommandHandler(_ctx, Substitute.For<INotificacionService>(), _email, Options.Create(new CorreoOptions()), Options.Create(new InstitucionOptions()))
            .Handle(new EnviarRecordatorioReunionCommand(r.Id, Peligroso), CancellationToken.None);

        var cuerpo = _cuerpos.Should().ContainSingle().Subject;
        cuerpo.Should().NotContain("<script>").And.NotContain("<b>x</b>");
        cuerpo.Should().Contain(Escapado).And.Contain("Revisión &lt;b&gt;x&lt;/b&gt;");
        cuerpo.Should().Contain("<p>Estimado(a) <strong>", "la plantilla sigue siendo HTML");
    }

    [Fact]
    public async Task Recordatorio_de_compromiso_escapa_responsable_reunion_compromiso_y_mensaje()
    {
        var r = Reunion.Crear("Mesa <b>técnica</b>");
        _ctx.Reuniones.Add(r);
        await _ctx.SaveChangesAsync();
        var a = new AcuerdoReunion
        {
            ReunionId = r.Id, Orden = 1, Compromiso = "Enviar <img src=x>",
            Responsable = "resp@prueba.local",
        };
        _ctx.Acuerdos.Add(a);
        await _ctx.SaveChangesAsync();

        await new EnviarRecordatorioCompromisoCommandHandler(_ctx, Substitute.For<INotificacionService>(), _email, Options.Create(new CorreoOptions()), Options.Create(new InstitucionOptions()))
            .Handle(new EnviarRecordatorioCompromisoCommand(a.Id, Peligroso), CancellationToken.None);

        var cuerpo = _cuerpos.Should().ContainSingle().Subject;
        cuerpo.Should().NotContain("<script>").And.NotContain("<img").And.NotContain("<b>técnica</b>");
        cuerpo.Should().Contain(Escapado).And.Contain("Enviar &lt;img src=x&gt;").And.Contain("Mesa &lt;b&gt;técnica&lt;/b&gt;");
    }

    [Fact]
    public async Task Recuperacion_de_contrasena_escapa_el_nombre_de_la_persona()
    {
        var u = Usuario.Crear("Juan <b>Pérez</b>", "juan@prueba.local", "hash");
        _ctx.Usuarios.Add(u);
        await _ctx.SaveChangesAsync();

        await new SolicitarRecuperacionPasswordCommandHandler(_ctx, _ctx, _email, Options.Create(new AuthOptions()),
                Options.Create(new CorreoOptions { UrlBase = "https://portal.prueba.local" }),
                Options.Create(new InstitucionOptions()),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SolicitarRecuperacionPasswordCommandHandler>.Instance)
            .Handle(new SolicitarRecuperacionPasswordCommand("juan@prueba.local"), CancellationToken.None);

        var cuerpo = _cuerpos.Should().ContainSingle().Subject;
        cuerpo.Should().NotContain("<b>Pérez</b>").And.Contain("Juan &lt;b&gt;Pérez&lt;/b&gt;");
    }

    [Fact]
    public async Task Recordatorio_de_expediente_escapa_analista_contacto_institucion_y_mensaje()
    {
        var analista = Usuario.Crear("Eva <b>Analista</b>", "analista@prueba.local", "hash");
        _ctx.Usuarios.Add(analista);
        var exp = Expediente.Crear("EXP-1", "INST", null, null, "Instituto <i>X</i>", analista.Nombre);
        exp.AnalistaId     = analista.Id;
        exp.ContactoNombre = "Raúl <u>Contacto</u>";
        exp.ContactoCorreo = "contacto@prueba.local";
        _ctx.Expedientes.Add(exp);
        await _ctx.SaveChangesAsync();

        await new EnviarRecordatorioExpedienteCommandHandler(_ctx, Substitute.For<INotificacionService>(), _email, Options.Create(new CorreoOptions()), Options.Create(new InstitucionOptions()))
            .Handle(new EnviarRecordatorioExpedienteCommand(exp.Id, Peligroso), CancellationToken.None);

        _cuerpos.Should().HaveCount(2);
        _cuerpos.Should().AllSatisfy(c =>
        {
            c.Should().NotContain("<script>").And.NotContain("<i>X</i>");
            c.Should().Contain(Escapado).And.Contain("Instituto &lt;i&gt;X&lt;/i&gt;");
        });
        _cuerpos.Should().Contain(c => c.Contains("Eva &lt;b&gt;Analista&lt;/b&gt;"));
        _cuerpos.Should().Contain(c => c.Contains("Raúl &lt;u&gt;Contacto&lt;/u&gt;"));
    }

    public void Dispose() => _ctx.Dispose();
}
