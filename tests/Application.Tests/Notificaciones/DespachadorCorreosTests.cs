using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Notifications;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Notificaciones;

public sealed class DespachadorCorreosTests : IDisposable
{
    private readonly string _base = Guid.NewGuid().ToString();
    private readonly AppDbContext _ctx;

    private AppDbContext Contexto() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_base).Options,
        new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

    private readonly IEmailService _email = Substitute.For<IEmailService>();
    private readonly Usuario _ana = Usuario.Crear("Ana", "ana@prueba.local", "hash");

    public DespachadorCorreosTests()
    {
        _ctx = Contexto();
        _ctx.Usuarios.Add(_ana);
        _ctx.SaveChanges();
    }

    private DespachadorCorreos Despachador() => new(_ctx, _email, Options.Create(new CorreoOptions()),
        TimeProvider.System, NullLogger<DespachadorCorreos>.Instance);

    private void Pendiente(Guid para, string url) => _ctx.CorreosPendientes.Add(CorreoPendiente.Crear(
        para, ModuloNotificacion.Reuniones, TipoNotificacion.CompromisoVencido, $"Compromiso {url}", url, url, DateTime.UtcNow));

    [Fact]
    public async Task Tres_pendientes_de_una_persona_salen_en_un_correo()
    {
        Pendiente(_ana.Id, "/c/1"); Pendiente(_ana.Id, "/c/2"); Pendiente(_ana.Id, "/c/3");
        await _ctx.SaveChangesAsync();
        _email.SendEmailAsync(default!, default!, default!, default, default).ReturnsForAnyArgs(ResultadoCorreo.Enviado);

        (await Despachador().DespacharAsync()).Should().Be(1);

        await _email.Received(1).SendEmailAsync("ana@prueba.local", Arg.Any<string>(),
            Arg.Is<string>(b => b.Contains("Compromiso /c/1") && b.Contains("Compromiso /c/3")),
            TipoCorreo.ResumenAvisos, Arg.Any<CancellationToken>());
        (await _ctx.CorreosPendientes.AllAsync(c => c.Estado == EstadoCorreoPendiente.Enviado)).Should().BeTrue();
    }

    [Fact]
    public async Task Al_tercer_fallo_queda_fallido()
    {
        Pendiente(_ana.Id, "/c/1");
        await _ctx.SaveChangesAsync();
        _email.SendEmailAsync(default!, default!, default!, default, default).ReturnsForAnyArgs(ResultadoCorreo.Fallido);

        for (var i = 0; i < 3; i++) await Despachador().DespacharAsync();

        var c = await _ctx.CorreosPendientes.SingleAsync();
        c.Estado.Should().Be(EstadoCorreoPendiente.Fallido);
        c.Intentos.Should().Be(DespachadorCorreos.MaxIntentos);
    }

    [Fact]
    public async Task Sin_usuario_activo_se_descarta_sin_intentar()
    {
        Pendiente(Guid.NewGuid(), "/c/1");
        await _ctx.SaveChangesAsync();

        await Despachador().DespacharAsync();

        await _email.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!, default, default);
        (await _ctx.CorreosPendientes.SingleAsync()).Estado.Should().Be(EstadoCorreoPendiente.Fallido);
    }

    [Fact]
    public async Task El_cuerpo_escapa_lo_que_viene_de_las_personas()
    {
        _ctx.CorreosPendientes.Add(CorreoPendiente.Crear(_ana.Id, ModuloNotificacion.Reuniones,
            TipoNotificacion.CompromisoVencido, "<b>x</b>", "/c/1", "/c/1", DateTime.UtcNow));
        await _ctx.SaveChangesAsync();
        _email.SendEmailAsync(default!, default!, default!, default, default).ReturnsForAnyArgs(ResultadoCorreo.Enviado);

        await Despachador().DespacharAsync();

        await _email.Received(1).SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Is<string>(b => b.Contains("&lt;b&gt;x&lt;/b&gt;") && !b.Contains("<b>x</b>")),
            Arg.Any<TipoCorreo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_la_vuelta_se_cancela_a_mitad_lo_ya_enviado_queda_guardado()
    {
        var beto = Usuario.Crear("Beto", "beto@prueba.local", "hash");
        _ctx.Usuarios.Add(beto);
        var antes = DateTime.UtcNow.AddMinutes(-10);
        _ctx.CorreosPendientes.Add(CorreoPendiente.Crear(_ana.Id, ModuloNotificacion.Reuniones,
            TipoNotificacion.CompromisoVencido, "Compromiso /c/1", "/c/1", "/c/1", antes));
        _ctx.CorreosPendientes.Add(CorreoPendiente.Crear(beto.Id, ModuloNotificacion.Reuniones,
            TipoNotificacion.CompromisoVencido, "Compromiso /c/2", "/c/2", "/c/2", antes.AddMinutes(1)));
        await _ctx.SaveChangesAsync();
        _email.SendEmailAsync("ana@prueba.local", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TipoCorreo>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoCorreo.Enviado);
        _email.SendEmailAsync("beto@prueba.local", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TipoCorreo>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ResultadoCorreo>(new OperationCanceledException()));

        var act = () => Despachador().DespacharAsync();
        await act.Should().ThrowAsync<OperationCanceledException>();

        // Otro contexto: lo que cuenta es lo que llegó a la base, no lo que quedó en memoria.
        await using var otro = Contexto();
        (await otro.CorreosPendientes.SingleAsync(c => c.DestinatarioId == _ana.Id)).Estado.Should().Be(EstadoCorreoPendiente.Enviado);
        (await otro.CorreosPendientes.SingleAsync(c => c.DestinatarioId == beto.Id)).Estado.Should().Be(EstadoCorreoPendiente.Pendiente);
    }

    public void Dispose() => _ctx.Dispose();
}
