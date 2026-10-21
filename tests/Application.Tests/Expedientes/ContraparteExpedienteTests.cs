using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Application.Expedientes.Commands.ActualizarExpediente;
using Diger.TramitesEstado.Application.Expedientes.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Expedientes;

internal class FakeContraparteCurrentUser(Guid userId) : ICurrentUserService
{
    public Guid?       UserId               => userId;
    public string?     Nombre               => "Contraparte Test";
    public string?     Correo               => "contraparte@institucion.gob.hn";
    public string?     Rol                  => "Empleado";
    public bool        IsAuthenticated       => true;
    public bool        EsGlobal             => false;
    // Empleado: el alcance de unidad reproduce la rama de RLS que antes se elegía por nombre.
    public NivelAlcance NivelAlcance         => NivelAlcance.Unidad;
    public bool        EsSoloLectura         => false;
    public bool        EsSupervisor          => false;
    public bool        EsTecnicoSoporte      => true;
    public bool        EsJefeDeArea          => false;
    public bool        EsPmo                 => false;
    public string?     ActiveInstitucionId   => "CNBS";
    public string?     ActiveAreaId          => null;
    public string?     ActiveUnidadId        => null;
    public IReadOnlyCollection<string> InstitucionesAsignadas => ["CNBS"];
    public bool        PuedeAccederInstitucion(string? institucionId) => institucionId == "CNBS";
}

public class ContraparteExpedienteTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ExpedienteRepository _repo;
    private readonly Guid _contraparteId = Guid.NewGuid();

    public ContraparteExpedienteTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var fakeUser = new FakeContraparteCurrentUser(_contraparteId);
        _ctx = new AppDbContext(opts, fakeUser, NSubstitute.Substitute.For<MediatR.IPublisher>());
        _repo = new ExpedienteRepository(_ctx);
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task ActualizarExpediente_ContraparteConPlazoVencido_LanzaDomainException()
    {
        // Arrange
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var exp = Expediente.Crear("EXP-100", "CNBS", null, null, "CNBS", "Analista DIGER");
        exp.ContraparteUsuarioId = _contraparteId;
        exp.FechaLimiteEntrega = hoy.AddDays(-1); // Vencido ayer
        _ctx.Expedientes.Add(exp);
        await _ctx.SaveChangesAsync();

        var input = ExpedienteMapper.ToInputDto(exp);
        var handler = new ActualizarExpedienteCommandHandler(_repo, _ctx, new FakeContraparteCurrentUser(_contraparteId), _ctx);

        // Act
        var act = async () => await handler.Handle(new ActualizarExpedienteCommand(exp.Id, input), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*plazo de entrega*vencido*");
    }
}
