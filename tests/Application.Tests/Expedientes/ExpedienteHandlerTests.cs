using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Expedientes.Commands.CrearExpediente;
using Diger.TramitesEstado.Application.Expedientes.Common;
using Diger.TramitesEstado.Application.Expedientes.Queries.GetExpedientes;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Expedientes;

internal sealed class FakeCurrentUser : ICurrentUserService
{
    public int?        UserId          => 1;
    public string?     Nombre          => "test";
    public string?     Correo          => "test@diger.gob.hn";
    public RolUsuario? Rol             => RolUsuario.Coordinador;
    public bool        IsAuthenticated => true;
    public bool        EsGlobal        => true;
    public IReadOnlyCollection<int> InstitucionesAsignadas => [];
    public bool        PuedeAccederInstitucion(int? institucionId) => true;
}

public class ExpedienteHandlerTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ExpedienteRepository _repo;

    public ExpedienteHandlerTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeCurrentUser());
        _repo = new ExpedienteRepository(_ctx);
    }

    private static ExpedienteInputDto BuildInput(string institucion = "CNBS", int institucionId = 7) => new(
        InstitucionId: institucionId, Institucion: institucion, FechaApertura: null, Analista: "Ana Analista",
        DirSede: null, NumTramitesProd: 0,
        ContactoNombre: null, ContactoCargo: null, ContactoCorreo: null, ContactoTel: null,
        ObsLegal: null, NumFuncionarios: 5, VolumenAnual: null, TiempoObservado: null, TiempoNorma: null,
        DescProceso: null, DocsAdicionales: null, ObsFlujo: null, FuncionariosDig: null, TiempoDig: null, ObsModelo: null,
        InfraPersonal: null, InfraPersonalTI: null, InfraRespSol: null, InfraAcomp: null, InfraDcModalidad: null,
        InfraDcVirt: null, InfraDcVirtOtro: null, InfraDcDisp: null, InfraDcObs: null, InfraPlan: null,
        EstadoExpediente: EstadoExpediente.EnExploracion, EstadoLevantamiento: null,
        ObsExpediente: null, ObsLevantamiento: null, ValidadoDiger: null, ValidadoInst: null,
        FechaValidacion: null, NumActa: null,
        Tramites: [new TramiteInput(0, "Constancia de solvencia", "Solvencia", "Registro", null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)],
        Requisitos: [new RequisitoInput(0, 0, "Cédula", null, AccionRequisito.Mantener, null)],
        Flujos: [new FlujoNodoInput(0, FaseFlujo.Actual, 0, TipoNodoFlujo.Inicio, "Recepción", null, null, null, null, null)],
        Legal: [], DocsSolicitados: [], DocsInternos: [], Perfiles: [], Condiciones: [],
        ChecklistInfra: [], Secciones: []);

    [Fact]
    public async Task Crear_GeneraCodigoYPersisteAgregado()
    {
        var handler = new CrearExpedienteCommandHandler(_repo, new FakeCurrentUser(), _ctx);

        var id = await handler.Handle(new CrearExpedienteCommand(BuildInput()), CancellationToken.None);

        var saved = await _ctx.Expedientes
            .Include(e => e.Tramites).Include(e => e.Requisitos).Include(e => e.Flujos)
            .FirstAsync(e => e.Id == id);

        saved.Codigo.Should().StartWith("EXP-CNBS-");
        saved.Institucion.Should().Be("CNBS");
        saved.Tramites.Should().HaveCount(1);
        saved.Requisitos.Should().HaveCount(1);
        saved.Flujos.Should().HaveCount(1);
    }

    [Fact]
    public async Task Crear_DosExpedientes_GeneraCodigosSecuenciales()
    {
        var handler = new CrearExpedienteCommandHandler(_repo, new FakeCurrentUser(), _ctx);
        await handler.Handle(new CrearExpedienteCommand(BuildInput()), CancellationToken.None);
        await handler.Handle(new CrearExpedienteCommand(BuildInput()), CancellationToken.None);

        var codigos = await _ctx.Expedientes.Select(e => e.Codigo).ToListAsync();
        codigos.Should().OnlyHaveUniqueItems();
        codigos.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetExpedientes_RetornaListaConTramites()
    {
        var handler = new CrearExpedienteCommandHandler(_repo, new FakeCurrentUser(), _ctx);
        await handler.Handle(new CrearExpedienteCommand(BuildInput("SAG", 14)), CancellationToken.None);

        var listHandler = new GetExpedientesQueryHandler(_ctx);
        var result = await listHandler.Handle(new GetExpedientesQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Institucion.Should().Be("SAG");
        result[0].NumTramites.Should().Be(1);
        result[0].TramiteNombres.Should().Contain("Constancia de solvencia");
    }

    public void Dispose() => _ctx.Dispose();
}
