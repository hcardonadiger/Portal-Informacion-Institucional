using Diger.TramitesEstado.Application.Notificaciones;
using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Email;
using Diger.TramitesEstado.Infrastructure.Notifications;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Notificaciones;

public sealed class AjustesNotificacionesProviderTests : IDisposable
{
    private readonly AppDbContext _ctx = new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new FakeCurrentUser(), Substitute.For<MediatR.IPublisher>());

    private readonly ProtectorSecretos _protector = new(new EphemeralDataProtectionProvider());
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private AjustesNotificacionesProvider Proveedor() => new(
        _ctx, _cache, _protector,
        Options.Create(new NotificacionesOptions { IntervaloHoras = 6 }),
        Options.Create(new SmtpSettings { Host = "smtp.gmail.com", Username = "no-reply@diger.gob.hn", Password = "deappsettings" }));

    [Fact]
    public async Task Sin_nada_guardado_rige_appsettings()
    {
        var p = Proveedor();

        (await p.ObtenerAsync()).IntervaloMinutos.Should().Be(360);
        (await p.ObtenerAsync()).De(ModuloNotificacion.Reuniones).Should().Be(InterruptoresModulo.PorDefecto);
        var smtp = await p.SmtpAsync();
        smtp.DesdePanel.Should().BeFalse();
        smtp.Host.Should().Be("smtp.gmail.com");
        smtp.Password.Should().Be("deappsettings");
    }

    [Fact]
    public async Task Lo_guardado_en_el_panel_manda_y_la_contrasena_se_descifra()
    {
        var fila = AjustesNotificaciones.CrearPorDefecto(15);
        fila.ConfigurarSmtp("smtp.office365.com", 587, true, "no-reply@diger.gob.hn", "no-reply@diger.gob.hn", null);
        fila.FijarPasswordCifrada(_protector.Cifrar("Secreta#1"));
        _ctx.AjustesNotificaciones.Add(fila);
        _ctx.InterruptoresNotificacion.Add(InterruptorNotificacion.Crear(ModuloNotificacion.Reuniones, correo: true));
        await _ctx.SaveChangesAsync();

        var p = Proveedor();
        (await p.ObtenerAsync()).IntervaloMinutos.Should().Be(15);
        (await p.ObtenerAsync()).De(ModuloNotificacion.Reuniones).Correo.Should().BeTrue();
        var smtp = await p.SmtpAsync();
        smtp.DesdePanel.Should().BeTrue();
        smtp.Password.Should().Be("Secreta#1");
        smtp.ToString().Should().NotContain("Secreta#1");
    }

    [Fact]
    public async Task Soporte_nunca_queda_con_correo_aunque_la_fila_lo_diga()
    {
        _ctx.InterruptoresNotificacion.Add(InterruptorNotificacion.Crear(ModuloNotificacion.Soporte, correo: true));
        await _ctx.SaveChangesAsync();

        (await Proveedor().ObtenerAsync()).De(ModuloNotificacion.Soporte).Correo.Should().BeFalse();
    }

    [Fact]
    public async Task Una_contrasena_que_no_se_puede_descifrar_se_marca_ilegible()
    {
        var fila = AjustesNotificaciones.CrearPorDefecto();
        fila.ConfigurarSmtp("smtp.office365.com", 587, true, "u@diger.gob.hn", "u@diger.gob.hn", null);
        fila.FijarPasswordCifrada("cifrada-con-otras-claves");
        _ctx.AjustesNotificaciones.Add(fila);
        await _ctx.SaveChangesAsync();

        var smtp = await Proveedor().SmtpAsync();
        smtp.PasswordIlegible.Should().BeTrue();
        smtp.EstaConfigurado.Should().BeFalse();
    }

    [Fact]
    public async Task Invalidar_hace_releer_la_base()
    {
        var p = Proveedor();
        (await p.ObtenerAsync()).IntervaloMinutos.Should().Be(360);

        _ctx.AjustesNotificaciones.Add(AjustesNotificaciones.CrearPorDefecto(30));
        await _ctx.SaveChangesAsync();
        (await p.ObtenerAsync()).IntervaloMinutos.Should().Be(360, "sigue en caché");

        p.Invalidar();
        (await p.ObtenerAsync()).IntervaloMinutos.Should().Be(30);
    }

    [Fact]
    public async Task La_senal_despierta_al_que_espera()
    {
        var s = new SenalRevision();
        var espera = s.EsperarAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        s.Disparar();
        (await espera).Should().BeTrue();
        (await s.EsperarAsync(TimeSpan.FromMilliseconds(10), CancellationToken.None)).Should().BeFalse();
    }

    public void Dispose() { _ctx.Dispose(); _cache.Dispose(); }
}
