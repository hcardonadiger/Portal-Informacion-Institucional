using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.CuentaMicrosoft;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Outlook;

public class ConexionMicrosoftCommandsTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private readonly IConexionMicrosoft _conexion = Substitute.For<IConexionMicrosoft>();
    private readonly Usuario _ana = Usuario.Crear("Ana", "ana@diger.gob.hn", "hash");

    public ConexionMicrosoftCommandsTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _usuario.EsGlobal.Returns(true);
        _usuario.UserId.Returns(_ana.Id);
        _ctx = new AppDbContext(opts, _usuario, Substitute.For<MediatR.IPublisher>());
        _ctx.Usuarios.Add(_ana);
        _ctx.SaveChanges();
        _conexion.EstaConfigurada.Returns(true);
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task Completar_canjea_para_el_usuario_de_la_sesion()
    {
        await new CompletarConexionOutlookCommandHandler(_conexion, _usuario)
            .Handle(new CompletarConexionOutlookCommand("codigo", "verif", "https://x/cb"), default);

        await _conexion.Received(1).CompletarAsync(_ana.Id, "codigo", "verif", "https://x/cb", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Iniciar_sin_configuracion_se_traduce_a_aviso()
    {
        _conexion.IniciarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<InicioConexionDto>>(_ => throw new OutlookNoDisponibleException("no configurada"));

        var act = () => new IniciarConexionOutlookQueryHandler(_conexion)
            .Handle(new IniciarConexionOutlookQuery("https://x/cb"), default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*no configurada*");
    }

    [Fact]
    public async Task Desconectar_borra_la_fila_del_usuario()
    {
        _ctx.ConexionesMicrosoft.Add(ConexionMicrosoft.Crear(_ana.Id, "ana@diger.gob.hn", "h", [1], DateTime.UtcNow));
        await _ctx.SaveChangesAsync();

        await new DesconectarOutlookCommandHandler(_ctx, _usuario, _ctx).Handle(new DesconectarOutlookCommand(), default);

        (await _ctx.ConexionesMicrosoft.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Mi_conexion_sin_fila_dice_no_conectada_pero_configurada()
    {
        var dto = await new GetMiConexionOutlookQueryHandler(_ctx, _usuario, _conexion)
            .Handle(new GetMiConexionOutlookQuery(), default);

        dto.Configurada.Should().BeTrue();
        dto.Conectada.Should().BeFalse();
        dto.Upn.Should().BeNull();
    }
}
