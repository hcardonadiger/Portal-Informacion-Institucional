using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Dashboards.Queries.GetMisProyectosDashboard;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Services;
using Diger.TramitesEstado.Application.Roles;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

/// <summary>
/// El ciclo completo del interesado automático, de punta a punta y <b>sin ningún mock del
/// sync</b>: el servicio real, el catálogo de roles real y los handlers reales.
///
/// <para>Las suites que ya existían cubren cada costura por separado —los disparos con un mock,
/// el servicio invocado a mano, el tablero con las filas sembradas a mano— y ninguna la unión.
/// Esta es la unión: se crea un proyecto con área y, <b>sin sembrar ninguna fila de interesado</b>,
/// el jefe de área tiene que verlo en su tablero. Y las dos variantes de revocación, que son las
/// que defienden para siempre el disparador que faltaba en <c>ActualizarRolCommandHandler</c>:
/// quitarle la capacidad al rol, y desactivar el rol.</para>
///
/// <para>El catálogo es el <see cref="RolCatalogo"/> de verdad, no un doble: guarda una foto
/// inmutable que solo cambia en <c>RecargarAsync</c>. Es lo que hace que estas pruebas detecten
/// también el orden equivocado —sincronizar antes de recargar el catálogo leería las capacidades
/// viejas y no revocaría nada—, cosa que un doble que consultara la base en cada llamada dejaría
/// pasar.</para>
/// </summary>
public class InteresadosAutomaticosCicloTests : IDisposable
{
    private const string Area = "AREA-X";
    private const string RolJefe = "JefeArea";

    private readonly AppDbContext _ctx;
    private readonly ServiceProvider _sp;
    private readonly IRolCatalogo _catalogo;
    private readonly InteresadosAutomaticosSyncService _sync;
    private readonly UsuarioActualFalso _actor = new() { ActiveInstitucionId = "DIGER", Nombre = "Administrador" };

    public InteresadosAutomaticosCicloTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, _actor, NSubstitute.Substitute.For<MediatR.IPublisher>());

        // El catálogo real necesita un IServiceScopeFactory del que sacar un IApplicationDbContext.
        _sp = new ServiceCollection()
            .AddSingleton<IApplicationDbContext>(_ctx)
            .BuildServiceProvider();
        _catalogo = new RolCatalogo(
            _sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<RolCatalogo>.Instance);

        _sync = new InteresadosAutomaticosSyncService(_ctx, _catalogo);
    }

    /// <summary>Rol con la capacidad, usuario asignado al área, catálogo cargado y un proyecto de
    /// esa área creado por el comando real. Devuelve al jefe y el id del proyecto.</summary>
    private async Task<(Usuario Jefe, int ProyectoId)> EscenarioAsync()
    {
        _ctx.Roles.Add(Rol.Crear(RolJefe, "Jefe de Área", NivelAlcance.Area, esJefeDeArea: true));
        var jefe = Usuario.Crear("Jefa del área", $"{Guid.NewGuid():N}@diger.gob.hn", "hash");
        _ctx.Usuarios.Add(jefe);
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefe.Id, "DIGER", Area, null, RolJefe));
        await _ctx.SaveChangesAsync();
        await _catalogo.RecargarAsync();

        var proyectoId = await new CrearProyectoCommandHandler(_ctx, _actor, _sync)
            .Handle(new CrearProyectoCommand("Expediente digital", AreaId: Area), CancellationToken.None);

        return (jefe, proyectoId);
    }

    private async Task<MisProyectosDashboardDto> TableroDelJefeAsync(Usuario jefe) =>
        await new GetMisProyectosDashboardQueryHandler(_ctx, new UsuarioActualFalso { UserId = jefe.Id })
            .Handle(new GetMisProyectosDashboardQuery(), CancellationToken.None);

    private Task ActualizarRolAsync(bool esJefeDeArea, bool activo) =>
        new ActualizarRolCommandHandler(_ctx, _catalogo, _sync).Handle(
            new ActualizarRolCommand(
                RolJefe, "Jefe de Área", NivelAlcance.Area, null, null,
                EsAdministrador: false, EsSoloLectura: false, EsSupervisor: false, EsTecnicoSoporte: false,
                Activo: activo, EsJefeDeArea: esJefeDeArea, EsPmo: false),
            CancellationToken.None);

    [Fact]
    public async Task CrearProyectoConArea_ElJefeDeEsaAreaLoVeSinSembrarNingunaFila()
    {
        var (jefe, proyectoId) = await EscenarioAsync();

        var tablero = await TableroDelJefeAsync(jefe);

        tablero.Proyectos.Should().ContainSingle(p => p.ProyectoId == proyectoId,
            "la sincronización automática lo dejó como interesado sin que nadie sembrara la fila");
    }

    [Fact]
    public async Task QuitarleLaCapacidadAlRol_LeQuitaElProyectoDelTablero()
    {
        var (jefe, proyectoId) = await EscenarioAsync();
        (await TableroDelJefeAsync(jefe)).Proyectos.Should().ContainSingle(p => p.ProyectoId == proyectoId);

        await ActualizarRolAsync(esJefeDeArea: false, activo: true);

        (await TableroDelJefeAsync(jefe)).Proyectos.Should().BeEmpty(
            "destildar «Jefe de área» tiene que revocar el acceso que esa casilla concedía");
        (await _ctx.ProyectoInteresados.AnyAsync(i => i.UsuarioId == jefe.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task DesactivarElRol_LeQuitaElProyectoDelTablero()
    {
        var (jefe, proyectoId) = await EscenarioAsync();
        (await TableroDelJefeAsync(jefe)).Proyectos.Should().ContainSingle(p => p.ProyectoId == proyectoId);

        await ActualizarRolAsync(esJefeDeArea: true, activo: false);

        (await TableroDelJefeAsync(jefe)).Proyectos.Should().BeEmpty(
            "un rol inactivo sale del catálogo y deja de conceder capacidades");
    }

    /// <summary>La otra dirección del mismo defecto: encender la casilla tiene que conceder el
    /// acceso sin que nadie vuelva a guardar el proyecto ni la jerarquía del usuario.</summary>
    [Fact]
    public async Task DarleLaCapacidadAlRol_LeSumaLosProyectosDeSuArea()
    {
        _ctx.Roles.Add(Rol.Crear(RolJefe, "Jefe de Área", NivelAlcance.Area, esJefeDeArea: false));
        var jefe = Usuario.Crear("Jefa del área", $"{Guid.NewGuid():N}@diger.gob.hn", "hash");
        _ctx.Usuarios.Add(jefe);
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(jefe.Id, "DIGER", Area, null, RolJefe));
        await _ctx.SaveChangesAsync();
        await _catalogo.RecargarAsync();

        var proyectoId = await new CrearProyectoCommandHandler(_ctx, _actor, _sync)
            .Handle(new CrearProyectoCommand("Expediente digital", AreaId: Area), CancellationToken.None);
        (await TableroDelJefeAsync(jefe)).Proyectos.Should().BeEmpty("todavía no es jefe de área");

        await ActualizarRolAsync(esJefeDeArea: true, activo: true);

        (await TableroDelJefeAsync(jefe)).Proyectos.Should().ContainSingle(p => p.ProyectoId == proyectoId,
            "encender la casilla concede el acceso sin tener que reguardar cada proyecto");
    }

    public void Dispose()
    {
        _ctx.Dispose();
        _sp.Dispose();
    }
}

/// <summary>
/// ICurrentUserService de prueba con valores explícitos. No es un Substitute a propósito: NSubstitute
/// devuelve cadena vacía —no null— en las propiedades string sin configurar, y AppDbContext decide
/// con IsNullOrEmpty, así que un mock pelado estampa AreaId = "" en las entidades nuevas y arruina
/// en silencio los casos «sin área».
/// </summary>
internal sealed class UsuarioActualFalso : ICurrentUserService
{
    public Guid?        UserId              { get; init; }
    public string?      Nombre              { get; init; }
    public string?      Correo              => null;
    public string?      Rol                 => null;
    public bool         IsAuthenticated     => true;
    public bool         EsGlobal            => true;
    public NivelAlcance NivelAlcance        => NivelAlcance.Global;
    public bool         EsSoloLectura       => false;
    public bool         EsSupervisor        => true;
    public bool         EsTecnicoSoporte    => false;
    public bool         EsJefeDeArea        => false;
    public bool         EsPmo               => false;
    public string?      ActiveInstitucionId { get; init; }
    public string?      ActiveAreaId        => null;
    public string?      ActiveUnidadId      => null;
    public IReadOnlyCollection<string> InstitucionesAsignadas => [];
    public bool PuedeAccederInstitucion(string? institucionId) => true;
}
