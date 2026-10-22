using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Application.Tests.Notificaciones;
using Diger.TramitesEstado.Application.Usuarios.Commands.SolicitarRecuperacionPassword;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Usuarios;

/// <summary>
/// S5 (2026-10-22): el enlace de recuperación sale solo de <c>Correo:UrlBase</c>. Si falta o no es
/// una dirección http/https válida, no se envía nada, no se genera token y queda una advertencia
/// en el registro (sin el correo de la persona).
/// </summary>
public sealed class RecuperacionUrlBaseTests : IDisposable
{
    private readonly AppDbContext _ctx = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

    private readonly IEmailService _email = Substitute.For<IEmailService>();
    private readonly CorreoSeguridadTests.RegistroEnMemoria<SolicitarRecuperacionPasswordCommandHandler> _registro = new();
    private const string Correo = "juan@prueba.local";

    public RecuperacionUrlBaseTests()
    {
        _ctx.Usuarios.Add(Usuario.Crear("Juan Pérez", Correo, "hash"));
        _ctx.SaveChanges();
    }

    private Task PedirAsync(string? urlBase) =>
        new SolicitarRecuperacionPasswordCommandHandler(_ctx, _ctx, _email, Options.Create(new AuthOptions()),
                Options.Create(new CorreoOptions { UrlBase = urlBase }), _registro)
            .Handle(new SolicitarRecuperacionPasswordCommand(Correo), CancellationToken.None);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("portal-sin-esquema")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://portal.prueba.local")]
    public async Task Sin_url_base_valida_no_se_envia_ni_se_genera_token_y_queda_una_advertencia(string? urlBase)
    {
        await PedirAsync(urlBase);

        await _email.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!, default, default);
        (await _ctx.Usuarios.AsNoTracking().SingleAsync()).PasswordResetToken.Should().BeNull();
        _registro.Lineas.Should().ContainSingle()
            .Which.Should().Contain("Correo:UrlBase").And.NotContain(Correo);
    }

    [Theory]
    [InlineData("https://portal.prueba.local",   "https://portal.prueba.local/Cuenta/RestablecerPassword?token=")]
    [InlineData("https://portal.prueba.local/",  "https://portal.prueba.local/Cuenta/RestablecerPassword?token=")]
    [InlineData("https://servidor/GestionGD/",   "https://servidor/GestionGD/Cuenta/RestablecerPassword?token=")]
    public async Task Con_url_base_el_enlace_se_arma_con_ella(string urlBase, string esperado)
    {
        string? cuerpo = null;
        await _email.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<string>(b => cuerpo = b),
            Arg.Any<TipoCorreo>(), Arg.Any<CancellationToken>());

        await PedirAsync(urlBase);

        cuerpo.Should().Contain(esperado);
        _registro.Lineas.Should().BeEmpty();
    }

    public void Dispose() => _ctx.Dispose();
}
