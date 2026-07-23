using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Usuarios.Commands.RestablecerPassword;
using Diger.TramitesEstado.Application.Usuarios.Commands.SolicitarRecuperacionPassword;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Usuarios;

public class PasswordRecoveryTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;

    public PasswordRecoveryTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var fakeCurrentUser = Substitute.For<ICurrentUserService>();
        fakeCurrentUser.EsGlobal.Returns(true);

        _ctx = new AppDbContext(opts, fakeCurrentUser);
        _emailService = Substitute.For<IEmailService>();
        _passwordHasher = new PasswordHasher();
    }

    [Fact]
    public async Task SolicitarRecuperacion_UsuarioExistente_GeneraTokenYEnviaCorreo()
    {
        // Arrange
        var usuario = Usuario.Crear("Juan Perez", "juan.perez@diger.gob.hn", _passwordHasher.Hash("PasswordOld123"));
        await _ctx.Usuarios.AddAsync(usuario);
        await _ctx.SaveChangesAsync();

        var handler = new SolicitarRecuperacionPasswordCommandHandler(_ctx, _ctx, _emailService);

        // Act
        var result = await handler.Handle(
            new SolicitarRecuperacionPasswordCommand("juan.perez@diger.gob.hn", "https://localhost:49175"),
            CancellationToken.None);

        // Assert
        var savedUser = await _ctx.Usuarios.FirstAsync(u => u.Id == usuario.Id);
        savedUser.PasswordResetToken.Should().NotBeNullOrEmpty();
        savedUser.PasswordResetTokenExpiration.Should().BeAfter(DateTime.UtcNow);

        await _emailService.Received(1).SendEmailAsync(
            Arg.Is<string>(e => e == "juan.perez@diger.gob.hn"),
            Arg.Is<string>(s => s.Contains("Restablecimiento")),
            Arg.Is<string>(b => b.Contains(savedUser.PasswordResetToken!)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SolicitarRecuperacion_UsuarioInexistente_NoGeneraErrorNiEnviaCorreo()
    {
        // Arrange
        var handler = new SolicitarRecuperacionPasswordCommandHandler(_ctx, _ctx, _emailService);

        // Act
        var act = async () => await handler.Handle(
            new SolicitarRecuperacionPasswordCommand("inexistente@diger.gob.hn", "https://localhost:49175"),
            CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await _emailService.DidNotReceive().SendEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestablecerPassword_TokenValido_CambiaPasswordYLimpiaToken()
    {
        // Arrange
        var usuario = Usuario.Crear("Maria Lopez", "maria.lopez@diger.gob.hn", _passwordHasher.Hash("ClaveAnterior123"));
        usuario.GenerarTokenRecuperacion("token-secreto-valido", TimeSpan.FromMinutes(20));
        await _ctx.Usuarios.AddAsync(usuario);
        await _ctx.SaveChangesAsync();

        var handler = new RestablecerPasswordCommandHandler(_ctx, _ctx, _passwordHasher);

        // Act
        await handler.Handle(
            new RestablecerPasswordCommand("maria.lopez@diger.gob.hn", "token-secreto-valido", "NuevaPassword456", "NuevaPassword456"),
            CancellationToken.None);

        // Assert
        var savedUser = await _ctx.Usuarios.FirstAsync(u => u.Id == usuario.Id);
        savedUser.PasswordResetToken.Should().BeNull();
        savedUser.PasswordResetTokenExpiration.Should().BeNull();
        _passwordHasher.Verify("NuevaPassword456", savedUser.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task RestablecerPassword_TokenExpirado_LanzaDomainException()
    {
        // Arrange
        var usuario = Usuario.Crear("Carlos Ruiz", "carlos.ruiz@diger.gob.hn", _passwordHasher.Hash("ClaveAnterior123"));
        usuario.GenerarTokenRecuperacion("token-expirado", TimeSpan.FromMinutes(-10)); // Expiró hace 10 min
        await _ctx.Usuarios.AddAsync(usuario);
        await _ctx.SaveChangesAsync();

        var handler = new RestablecerPasswordCommandHandler(_ctx, _ctx, _passwordHasher);

        // Act
        var act = async () => await handler.Handle(
            new RestablecerPasswordCommand("carlos.ruiz@diger.gob.hn", "token-expirado", "NuevaPassword456", "NuevaPassword456"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*expirado*");
    }

    [Fact]
    public async Task RestablecerPassword_TokenInvalido_LanzaDomainException()
    {
        // Arrange
        var usuario = Usuario.Crear("Ana Gomez", "ana.gomez@diger.gob.hn", _passwordHasher.Hash("ClaveAnterior123"));
        usuario.GenerarTokenRecuperacion("token-correcto", TimeSpan.FromMinutes(20));
        await _ctx.Usuarios.AddAsync(usuario);
        await _ctx.SaveChangesAsync();

        var handler = new RestablecerPasswordCommandHandler(_ctx, _ctx, _passwordHasher);

        // Act
        var act = async () => await handler.Handle(
            new RestablecerPasswordCommand("ana.gomez@diger.gob.hn", "token-incorrecto", "NuevaPassword456", "NuevaPassword456"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*inválido*");
    }

    public void Dispose() => _ctx.Dispose();
}
