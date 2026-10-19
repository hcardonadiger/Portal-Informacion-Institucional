using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Usuarios.Commands.EstablecerPasswordObligatoria;
using Diger.TramitesEstado.Application.Usuarios.Commands.RestablecerPasswordUsuario;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using Diger.TramitesEstado.Infrastructure.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Usuarios;

/// <summary>
/// El cambio obligatorio por única vez: lo que exige la pantalla del portón, y la vuelta atrás
/// que provoca un restablecimiento hecho por un administrador.
/// </summary>
public class PasswordObligatoriaTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly IPasswordHasher _hasher = new PasswordHasher();
    private readonly IUsuarioRepository _repo;

    public PasswordObligatoriaTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var fakeCurrentUser = Substitute.For<ICurrentUserService>();
        fakeCurrentUser.EsGlobal.Returns(true);

        _ctx  = new AppDbContext(opts, fakeCurrentUser, Substitute.For<MediatR.IPublisher>());
        _repo = new UsuarioRepository(_ctx);
    }

    private async Task<Usuario> SembrarConTemporalAsync(string temporal = "Temporal#2026")
    {
        var u = Usuario.Crear("Ana Prueba", "ana@diger.gob.hn", _hasher.Hash(temporal));
        await _ctx.Usuarios.AddAsync(u);
        await _ctx.SaveChangesAsync();
        return u;
    }

    private EstablecerPasswordObligatoriaCommandHandler Handler() => new(_repo, _hasher, _ctx);

    [Fact]
    public async Task EstablecerLaPasswordEstampaLaFechaYDevuelveLaMarca()
    {
        var u = await SembrarConTemporalAsync();

        var estampa = await Handler().Handle(
            new EstablecerPasswordObligatoriaCommand(u.Id, "ClaveNueva#2026"), CancellationToken.None);

        var guardado = await _ctx.Usuarios.AsNoTracking().FirstAsync(x => x.Id == u.Id);
        guardado.PasswordCambiadoEl.Should().NotBeNull();
        estampa.Should().Be(guardado.PasswordCambiadoEl!.Value,
            "la sesión usa ese valor para refrescar su claim y dejar de ser retenida");
        _hasher.Verify("ClaveNueva#2026", guardado.PasswordHash).Should().BeTrue();
        guardado.RequiereCambioPassword(DateTime.UtcNow.AddMinutes(-1)).Should().BeFalse();
    }

    [Fact]
    public async Task RepetirLaMismaPasswordNoCuentaComoCambio()
    {
        var u = await SembrarConTemporalAsync();

        var accion = () => Handler().Handle(
            new EstablecerPasswordObligatoriaCommand(u.Id, "Temporal#2026"), CancellationToken.None);

        (await accion.Should().ThrowAsync<DomainException>())
            .WithMessage("*distinta*");

        var guardado = await _ctx.Usuarios.AsNoTracking().FirstAsync(x => x.Id == u.Id);
        guardado.PasswordCambiadoEl.Should().BeNull("no hubo cambio, así que sigue pendiente");
    }

    [Fact]
    public async Task UsuarioInexistenteNoSeInventa()
    {
        var accion = () => Handler().Handle(
            new EstablecerPasswordObligatoriaCommand(Guid.NewGuid(), "ClaveNueva#2026"),
            CancellationToken.None);

        await accion.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Corta7x")]
    public void ElValidadorExigeOchoCaracteres(string password)
    {
        var validador = new EstablecerPasswordObligatoriaCommandValidator();

        var resultado = validador.Validate(
            new EstablecerPasswordObligatoriaCommand(Guid.NewGuid(), password));

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ElValidadorAceptaOchoCaracteres()
    {
        var validador = new EstablecerPasswordObligatoriaCommandValidator();

        var resultado = validador.Validate(
            new EstablecerPasswordObligatoriaCommand(Guid.NewGuid(), "OchoChar"));

        resultado.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ElRestablecimientoDelAdministradorDejaElCambioPendienteOtraVez()
    {
        var u = await SembrarConTemporalAsync();
        await Handler().Handle(
            new EstablecerPasswordObligatoriaCommand(u.Id, "ClaveNueva#2026"), CancellationToken.None);

        await new RestablecerPasswordUsuarioCommandHandler(_repo, _hasher, _ctx).Handle(
            new RestablecerPasswordUsuarioCommand(u.Id, "OtraTemporal#2026"), CancellationToken.None);

        var guardado = await _ctx.Usuarios.AsNoTracking().FirstAsync(x => x.Id == u.Id);
        guardado.PasswordCambiadoEl.Should().BeNull(
            "la contraseña vigente la puso un tercero: el dueño todavía no eligió la suya");
        guardado.RequiereCambioPassword(DateTime.UtcNow.AddMinutes(-1)).Should().BeTrue();
    }

    public void Dispose() => _ctx.Dispose();
}
