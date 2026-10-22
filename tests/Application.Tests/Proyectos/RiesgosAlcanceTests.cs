using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Tests.Expedientes; // FakeCurrentUser
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Proyectos;

/// <summary>
/// S3 (2026-10-22): editar, cambiar de estado o eliminar un riesgo exige que quien lo pide alcance
/// el proyecto del propio riesgo y, si la pantalla manda el proyecto, que el riesgo sea de ese
/// proyecto. La tabla de riesgos no tiene filtro de alcance propio, así que antes bastaba con
/// cambiar el número del riesgo en la petición para tocar uno ajeno.
///
/// <para>Las de alcance usan un segundo contexto, con un usuario de institución, sobre la misma base
/// en memoria: el primero (global) solo siembra.</para>
/// </summary>
public sealed class RiesgosAlcanceTests : IDisposable
{
    private const string MiInstitucion   = "INST-A";
    private const string OtraInstitucion = "INST-B";

    private readonly string _base = Guid.NewGuid().ToString();
    private readonly AppDbContext _global;
    private readonly AppDbContext _acotado;
    private readonly ICurrentUserService _usuario;

    private readonly int _miProyecto;
    private readonly int _otroMio;
    private readonly int _deOtraInstitucion;

    public RiesgosAlcanceTests()
    {
        _global = Contexto(new FakeCurrentUser());

        _usuario = Substitute.For<ICurrentUserService>();
        _usuario.IsAuthenticated.Returns(true);
        _usuario.UserId.Returns(Guid.NewGuid());
        _usuario.Nombre.Returns("Usuario de institución");
        _usuario.EsGlobal.Returns(false);
        _usuario.NivelAlcance.Returns(NivelAlcance.Institucion);
        _usuario.ActiveInstitucionId.Returns(MiInstitucion);

        _miProyecto        = Sembrar("PRY-2026-801", MiInstitucion);
        _otroMio           = Sembrar("PRY-2026-802", MiInstitucion);
        _deOtraInstitucion = Sembrar("PRY-2026-803", OtraInstitucion);

        _acotado = Contexto(_usuario);
    }

    private AppDbContext Contexto(ICurrentUserService quien) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_base).Options,
            quien, Substitute.For<MediatR.IPublisher>());

    private int Sembrar(string codigo, string institucion)
    {
        var p = Proyecto.Crear(codigo, $"Proyecto {codigo}");
        p.PrioridadId   = PrioridadesDePrueba.Media(_global);
        p.InstitucionId = institucion;
        _global.Proyectos.Add(p);
        _global.SaveChanges();
        return p.Id;
    }

    private int Riesgo(int proyectoId)
    {
        var r = RiesgoProyecto.Crear(proyectoId, "Falta designar contraparte", CategoriaRiesgo.Institucional,
            NivelCualitativo.Media, NivelCualitativo.Alta, EstrategiaRiesgo.Mitigar, "siembra");
        _global.ProyectoRiesgos.Add(r);
        _global.SaveChanges();
        return r.Id;
    }

    private static ActualizarRiesgoCommand Edicion(int riesgoId, int? proyectoId, string texto = "Texto corregido") =>
        new(riesgoId, texto, CategoriaRiesgo.Institucional, NivelCualitativo.Alta, NivelCualitativo.Alta,
            EstrategiaRiesgo.Mitigar, null, null, null, null, ProyectoId: proyectoId);

    private Task<RiesgoProyecto> LeerAsync(int riesgoId) =>
        _global.ProyectoRiesgos.AsNoTracking().SingleAsync(r => r.Id == riesgoId);

    // ── El riesgo tiene que ser del proyecto que manda la pantalla ────────

    [Fact]
    public async Task No_se_edita_un_riesgo_de_otro_proyecto_cambiando_su_numero()
    {
        var deOtro = Riesgo(_otroMio);

        var act = () => new ActualizarRiesgoCommandHandler(_acotado, _usuario)
            .Handle(Edicion(deOtro, _miProyecto, "Pisado"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        (await LeerAsync(deOtro)).Descripcion.Should().NotBe("Pisado");
    }

    [Fact]
    public async Task No_se_cambia_el_estado_de_un_riesgo_de_otro_proyecto()
    {
        var deOtro = Riesgo(_otroMio);

        var act = () => new CambiarEstadoRiesgoCommandHandler(_acotado, _usuario)
            .Handle(new CambiarEstadoRiesgoCommand(deOtro, EstadoRiesgo.Cerrado, _miProyecto), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        (await LeerAsync(deOtro)).Estado.Should().Be(EstadoRiesgo.Abierto);
    }

    [Fact]
    public async Task No_se_elimina_un_riesgo_de_otro_proyecto()
    {
        var deOtro = Riesgo(_otroMio);

        var act = () => new EliminarRiesgoCommandHandler(_acotado, _usuario)
            .Handle(new EliminarRiesgoCommand(deOtro, _miProyecto), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        (await _global.ProyectoRiesgos.AnyAsync(r => r.Id == deOtro)).Should().BeTrue();
    }

    // ── Y quien lo pide tiene que alcanzar el proyecto del riesgo ─────────

    [Fact]
    public async Task Sin_alcance_sobre_el_proyecto_no_se_edita_cambia_ni_elimina_su_riesgo()
    {
        var ajeno = Riesgo(_deOtraInstitucion);

        var editar = () => new ActualizarRiesgoCommandHandler(_acotado, _usuario)
            .Handle(Edicion(ajeno, _deOtraInstitucion, "Pisado"), CancellationToken.None);
        var estado = () => new CambiarEstadoRiesgoCommandHandler(_acotado, _usuario)
            .Handle(new CambiarEstadoRiesgoCommand(ajeno, EstadoRiesgo.Cerrado, _deOtraInstitucion), CancellationToken.None);
        var borrar = () => new EliminarRiesgoCommandHandler(_acotado, _usuario)
            .Handle(new EliminarRiesgoCommand(ajeno, _deOtraInstitucion), CancellationToken.None);

        await editar.Should().ThrowAsync<NotFoundException>();
        await estado.Should().ThrowAsync<NotFoundException>();
        await borrar.Should().ThrowAsync<NotFoundException>();

        var r = await LeerAsync(ajeno);
        r.Descripcion.Should().NotBe("Pisado");
        r.Estado.Should().Be(EstadoRiesgo.Abierto);
    }

    [Fact]
    public async Task Sin_proyecto_indicado_se_sigue_exigiendo_el_alcance()
    {
        // Es la forma en que llama la herramienta MCP de cambio de estado: solo el número del
        // riesgo. Sin proyecto que comparar, el alcance se exige igual, por el proyecto del riesgo.
        var ajeno = Riesgo(_deOtraInstitucion);

        var estado = () => new CambiarEstadoRiesgoCommandHandler(_acotado, _usuario)
            .Handle(new CambiarEstadoRiesgoCommand(ajeno, EstadoRiesgo.Cerrado), CancellationToken.None);
        var editar = () => new ActualizarRiesgoCommandHandler(_acotado, _usuario)
            .Handle(Edicion(ajeno, null, "Pisado"), CancellationToken.None);
        var borrar = () => new EliminarRiesgoCommandHandler(_acotado, _usuario)
            .Handle(new EliminarRiesgoCommand(ajeno), CancellationToken.None);

        await estado.Should().ThrowAsync<NotFoundException>();
        await editar.Should().ThrowAsync<NotFoundException>();
        await borrar.Should().ThrowAsync<NotFoundException>();
        (await LeerAsync(ajeno)).Estado.Should().Be(EstadoRiesgo.Abierto);
    }

    // ── Regresión: con alcance todo sigue igual que antes ─────────────────

    [Fact]
    public async Task Con_alcance_se_edita_cambia_de_estado_y_elimina_igual_que_antes()
    {
        var rid = Riesgo(_miProyecto);

        await new ActualizarRiesgoCommandHandler(_acotado, _usuario)
            .Handle(Edicion(rid, _miProyecto), CancellationToken.None);
        (await LeerAsync(rid)).Descripcion.Should().Be("Texto corregido");

        await new CambiarEstadoRiesgoCommandHandler(_acotado, _usuario)
            .Handle(new CambiarEstadoRiesgoCommand(rid, EstadoRiesgo.EnTratamiento, _miProyecto), CancellationToken.None);
        (await LeerAsync(rid)).Estado.Should().Be(EstadoRiesgo.EnTratamiento);

        await new EliminarRiesgoCommandHandler(_acotado, _usuario)
            .Handle(new EliminarRiesgoCommand(rid, _miProyecto), CancellationToken.None);
        (await _global.ProyectoRiesgos.AnyAsync(r => r.Id == rid)).Should().BeFalse();

        var bitacora = await _global.BitacorasProyecto.AsNoTracking()
            .Where(b => b.ProyectoId == _miProyecto && b.Tipo == TipoEventoProyecto.Riesgo)
            .Select(b => b.Detalle).ToListAsync();
        bitacora.Should().Contain(d => d.StartsWith("Severidad del riesgo"))
            .And.Contain(d => d.StartsWith("Riesgo eliminado"));
    }

    [Fact]
    public async Task Con_alcance_y_sin_proyecto_indicado_se_cambia_el_estado_como_antes()
    {
        var mio = Riesgo(_miProyecto);

        await new CambiarEstadoRiesgoCommandHandler(_acotado, _usuario)
            .Handle(new CambiarEstadoRiesgoCommand(mio, EstadoRiesgo.Cerrado), CancellationToken.None);

        (await LeerAsync(mio)).Estado.Should().Be(EstadoRiesgo.Cerrado);
    }

    public void Dispose()
    {
        _acotado.Dispose();
        _global.Dispose();
    }
}
