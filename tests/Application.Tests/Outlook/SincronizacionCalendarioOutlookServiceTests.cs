using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Reuniones.Outlook;
using Diger.TramitesEstado.Application.Reuniones.Outlook.Calendario;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Outlook;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Outlook;

public class SincronizacionCalendarioOutlookServiceTests
{
    [Fact]
    public async Task Sincroniza_a_cada_conectado_vigente_y_un_fallo_no_detiene_a_los_demas()
    {
        var ana = Guid.NewGuid(); var beto = Guid.NewGuid(); var vencida = Guid.NewGuid();
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var sender = Substitute.For<ISender>();
        sender.Send(new SincronizarCalendarioOutlookCommand(ana), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OutlookNoDisponibleException("Graph respondió 503"));
        sender.Send(new SincronizarCalendarioOutlookCommand(beto), Arg.Any<CancellationToken>())
            .Returns(new ResultadoSincronizacionCalendario(1, 0, 0, 0));

        var servicios = new ServiceCollection();
        servicios.AddScoped(_ => new AppDbContext(opts, Substitute.For<ICurrentUserService>(), Substitute.For<IPublisher>()));
        servicios.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        servicios.AddSingleton(sender);
        var sp = servicios.BuildServiceProvider();

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ConexionesMicrosoft.Add(ConexionMicrosoft.Crear(ana, "ana@diger.gob.hn", "h", [1], DateTime.UtcNow));
            db.ConexionesMicrosoft.Add(ConexionMicrosoft.Crear(beto, "beto@diger.gob.hn", "h", [1], DateTime.UtcNow));
            var v = ConexionMicrosoft.Crear(vencida, "v@diger.gob.hn", "h", [1], DateTime.UtcNow);
            v.MarcarVencida("invalid_grant");
            db.ConexionesMicrosoft.Add(v);
            await db.SaveChangesAsync();
        }

        var opciones = Options.Create(new MicrosoftGraphOptions { TenantId = "t", ClientId = "c", ClientSecret = "s" });
        var servicio = new SincronizacionCalendarioOutlookService(sp.GetRequiredService<IServiceScopeFactory>(), opciones,
            NullLogger<SincronizacionCalendarioOutlookService>.Instance);

        await servicio.CicloAsync(default);

        await sender.Received(1).Send(new SincronizarCalendarioOutlookCommand(ana), Arg.Any<CancellationToken>());
        await sender.Received(1).Send(new SincronizarCalendarioOutlookCommand(beto), Arg.Any<CancellationToken>());
        await sender.DidNotReceive().Send(new SincronizarCalendarioOutlookCommand(vencida), Arg.Any<CancellationToken>());
    }
}
