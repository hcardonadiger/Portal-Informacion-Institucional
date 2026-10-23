using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Application.PanelControl.Notificaciones;
using Diger.TramitesEstado.Application.Tests.Expedientes;
using Diger.TramitesEstado.Application.Tests.Notificaciones;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.PanelControl;

public sealed class PanelNotificacionesTests : IDisposable
{
    private readonly AppDbContext _ctx = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

    private readonly AjustesDePrueba _ajustes = new();
    private readonly ProtectorSecretos _protector = new(new EphemeralDataProtectionProvider());
    private readonly SenalRevision _senal = new();
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();

    public PanelNotificacionesTests() => _usuario.Nombre.Returns("Henry Cardona");

    private Task GuardarRevisionAsync(int h, int m) =>
        new GuardarRevisionCommandHandler(_ctx, _ajustes, _senal, _usuario, TimeProvider.System)
            .Handle(new GuardarRevisionCommand(h, m), CancellationToken.None);

    private Task GuardarCorreoAsync(string? password, bool quitar = false) =>
        new GuardarCorreoCommandHandler(_ctx, _ajustes, _protector, _usuario, TimeProvider.System)
            .Handle(new GuardarCorreoCommand("smtp.office365.com", 587, true, "no-reply@diger.gob.hn",
                password, quitar, "no-reply@diger.gob.hn", null), CancellationToken.None);

    [Fact]
    public async Task Quince_minutos_se_guarda_despierta_al_revisor_y_queda_en_la_bitacora()
    {
        var despierto = _senal.EsperarAsync(TimeSpan.FromMinutes(1), CancellationToken.None);

        await GuardarRevisionAsync(0, 15);

        (await _ctx.AjustesNotificaciones.SingleAsync()).IntervaloMinutos.Should().Be(15);
        (await despierto).Should().BeTrue();
        _ajustes.Invalidaciones.Should().Be(1);
        (await _ctx.PanelControlBitacora.SingleAsync()).Detalle.Should().Be("intervalo: 4 h → 15 min");
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(24, 1)]
    public async Task Fuera_de_rango_se_rechaza(int h, int m)
    {
        var act = () => GuardarRevisionAsync(h, m);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public void El_validador_rechaza_cero_horas_cero_minutos()
    {
        new GuardarRevisionValidator().Validate(new GuardarRevisionCommand(0, 0)).IsValid.Should().BeFalse();
        new GuardarRevisionValidator().Validate(new GuardarRevisionCommand(0, 15)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task La_contrasena_queda_cifrada_y_la_bitacora_no_la_dice()
    {
        await GuardarCorreoAsync("Secreta#123");

        var fila = await _ctx.AjustesNotificaciones.SingleAsync();
        fila.SmtpPasswordCifrada.Should().NotBe("Secreta#123");
        _protector.Descifrar(fila.SmtpPasswordCifrada!).Should().Be("Secreta#123");
        (await _ctx.PanelControlBitacora.AnyAsync(b => b.Detalle.Contains("Secreta#123"))).Should().BeFalse();
        (await _ctx.PanelControlBitacora.AnyAsync(b => b.Detalle.Contains("contraseña cambiada"))).Should().BeTrue();
    }

    [Fact]
    public async Task Vacia_conserva_la_anterior_y_quitar_la_borra()
    {
        await GuardarCorreoAsync("Secreta#123");
        var antes = (await _ctx.AjustesNotificaciones.SingleAsync()).SmtpPasswordCifrada;

        await GuardarCorreoAsync(null);
        (await _ctx.AjustesNotificaciones.SingleAsync()).SmtpPasswordCifrada.Should().Be(antes);

        await GuardarCorreoAsync(null, quitar: true);
        (await _ctx.AjustesNotificaciones.SingleAsync()).SmtpPasswordCifrada.Should().BeNull();
    }

    [Fact]
    public async Task La_primera_vez_exige_la_contrasena()
    {
        var act = () => GuardarCorreoAsync(null);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Soporte_no_queda_con_correo_aunque_se_pida()
    {
        await new GuardarModulosCommandHandler(_ctx, _ajustes, _usuario, TimeProvider.System).Handle(
            new GuardarModulosCommand([new InterruptoresInput(ModuloNotificacion.Soporte, true, true, true),
                                       new InterruptoresInput(ModuloNotificacion.Reuniones, true, true, true)]),
            CancellationToken.None);

        (await _ctx.InterruptoresNotificacion.SingleAsync(i => i.Modulo == ModuloNotificacion.Soporte)).CorreoActivo.Should().BeFalse();
        (await _ctx.InterruptoresNotificacion.SingleAsync(i => i.Modulo == ModuloNotificacion.Reuniones)).CorreoActivo.Should().BeTrue();
        (await _ctx.PanelControlBitacora.SingleAsync()).Detalle.Should().Be("Reuniones: correo encendido");
    }

    [Fact]
    public async Task El_panel_nunca_trae_la_contrasena()
    {
        _ajustes.Smtp = AjustesDePrueba.ConSmtp("smtp.office365.com", "u@diger.gob.hn", "Secreta#123");

        var dto = await new GetPanelNotificacionesQueryHandler(_ajustes, _ctx).Handle(new GetPanelNotificacionesQuery(), CancellationToken.None);

        dto.Correo.HayPassword.Should().BeTrue();
        dto.ToString().Should().NotContain("Secreta#123");
        dto.Modulos.Should().HaveCount(5);
    }

    private static GuardarCorreoCommand CorreoValido(string host = "smtp.office365.com", string remitente = "no-reply@diger.gob.hn") =>
        new(host, 587, true, "no-reply@diger.gob.hn", "Secreta#123", false, remitente, "Gestión Digital");

    [Fact]
    public void Remitente_vacio_da_un_solo_error_y_en_espanol()
    {
        var r = new GuardarCorreoValidator().Validate(CorreoValido(remitente: ""));

        r.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Falta el correo remitente.");
    }

    [Fact]
    public void Servidor_demasiado_largo_se_rechaza_en_espanol()
    {
        var r = new GuardarCorreoValidator().Validate(CorreoValido(host: new string('a', 201)));

        r.Errors.Should().ContainSingle().Which.ErrorMessage.Should().NotContain("must")
            .And.Be("El servidor no puede pasar de 200 caracteres.");
    }

    [Fact]
    public void Un_correo_completo_es_valido() =>
        new GuardarCorreoValidator().Validate(CorreoValido()).IsValid.Should().BeTrue();

    public void Dispose() => _ctx.Dispose();
}
