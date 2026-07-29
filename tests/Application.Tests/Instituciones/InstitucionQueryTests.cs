using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Instituciones.Queries.GetInstitucionById;
using Diger.TramitesEstado.Application.Instituciones.Queries.GetInstituciones;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Instituciones;

internal sealed class FakeGlobalCurrentUser : ICurrentUserService
{
    public Guid?       UserId               => Guid.NewGuid();
    public string?     Nombre               => "test";
    public string?     Correo               => "test@diger.gob.hn";
    public string?     Rol                  => "Administrador";
    public bool        IsAuthenticated       => true;
    public bool        EsGlobal             => true;
    public string?     ActiveInstitucionId   => null;
    public string?     ActiveAreaId          => null;
    public string?     ActiveUnidadId        => null;
    public IReadOnlyCollection<string> InstitucionesAsignadas => [];
    public bool        PuedeAccederInstitucion(string? institucionId) => true;
}

public class InstitucionQueryTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly InstitucionRepository _repo;

    public InstitucionQueryTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeGlobalCurrentUser());
        _repo = new InstitucionRepository(_ctx);
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task GetInstituciones_CalculaNumTramitesDesdeExpedientes()
    {
        // Arrange
        var inst = Institucion.Crear("DIGER", "Dirección General de Regulación");
        _ctx.Instituciones.Add(inst);

        var exp1 = Expediente.Crear("EXP-001", "DIGER", null, null, "DIGER", "Analista");
        exp1.Agregar(new ExpedienteTramite { NombreTramite = "Tramite A", TramiteIndex = 0 });
        exp1.Agregar(new ExpedienteTramite { NombreTramite = "Tramite B", TramiteIndex = 1 });
        _ctx.Expedientes.Add(exp1);

        var exp2 = Expediente.Crear("EXP-002", "DIGER", null, null, "DIGER", "Analista");
        exp2.Agregar(new ExpedienteTramite { NombreTramite = "Tramite C", TramiteIndex = 0 });
        _ctx.Expedientes.Add(exp2);

        await _ctx.SaveChangesAsync();

        var handler = new GetInstitucionesQueryHandler(_ctx);

        // Act
        var result = await handler.Handle(new GetInstitucionesQuery(), CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be("DIGER");
        result.Items[0].NumTramites.Should().Be(3);
    }

    [Fact]
    public async Task GetInstitucionById_CalculaNumTramitesDesdeExpedientes()
    {
        // Arrange
        var inst = Institucion.Crear("CNBS", "Comisión Nacional de Bancos y Seguros");
        _ctx.Instituciones.Add(inst);

        var exp = Expediente.Crear("EXP-003", "CNBS", null, null, "CNBS", "Analista");
        exp.Agregar(new ExpedienteTramite { NombreTramite = "Licencia Bancaria", TramiteIndex = 0 });
        exp.Agregar(new ExpedienteTramite { NombreTramite = "Registro Financiero", TramiteIndex = 1 });
        _ctx.Expedientes.Add(exp);

        await _ctx.SaveChangesAsync();

        var handler = new GetInstitucionByIdQueryHandler(_repo, _ctx);

        // Act
        var result = await handler.Handle(new GetInstitucionByIdQuery("CNBS"), CancellationToken.None);

        // Assert
        result.Id.Should().Be("CNBS");
        result.NumTramites.Should().Be(2);
    }
}
