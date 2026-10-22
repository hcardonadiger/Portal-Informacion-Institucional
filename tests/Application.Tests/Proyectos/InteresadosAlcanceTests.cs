using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Services;
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
/// S4 (2026-10-22): editar o quitar un interesado exige que quien lo pide alcance el proyecto del
/// interesado y, si la pantalla manda el proyecto, que el interesado sea de ese proyecto. Es el
/// mismo defecto que tenían los riesgos: la tabla no tiene filtro de alcance propio, así que bastaba
/// con cambiar el número en la petición. Quitar a alguien, además, le quita el acceso al proyecto.
/// </summary>
public sealed class InteresadosAlcanceTests : IDisposable
{
    private const string MiInstitucion   = "INST-A";
    private const string OtraInstitucion = "INST-B";

    private readonly string _base = Guid.NewGuid().ToString();
    private readonly AppDbContext _global;
    private readonly AppDbContext _acotado;
    private readonly ICurrentUserService _usuario;
    private readonly IRolCatalogo _catalogo = Substitute.For<IRolCatalogo>();

    private readonly int _miProyecto;
    private readonly int _otroMio;
    private readonly int _deOtraInstitucion;

    public InteresadosAlcanceTests()
    {
        _global = Contexto(new FakeCurrentUser());

        _usuario = Substitute.For<ICurrentUserService>();
        _usuario.IsAuthenticated.Returns(true);
        _usuario.UserId.Returns(Guid.NewGuid());
        _usuario.Nombre.Returns("Usuario de institución");
        _usuario.EsGlobal.Returns(false);
        _usuario.NivelAlcance.Returns(NivelAlcance.Institucion);
        _usuario.ActiveInstitucionId.Returns(MiInstitucion);

        _miProyecto        = Sembrar("PRY-2026-811", MiInstitucion);
        _otroMio           = Sembrar("PRY-2026-812", MiInstitucion);
        _deOtraInstitucion = Sembrar("PRY-2026-813", OtraInstitucion);

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

    private int Interesado(int proyectoId)
    {
        var i = InteresadoProyecto.Crear(proyectoId, Guid.NewGuid(), "Persona interesada",
            RolInteresado.Beneficiario, "siembra");
        _global.ProyectoInteresados.Add(i);
        _global.SaveChanges();
        return i.Id;
    }

    private static ActualizarInteresadoCommand Edicion(int interesadoId, int? proyectoId) =>
        new(interesadoId, RolInteresado.ContraparteTecnica, NivelCualitativo.Alta, null, "Cargo nuevo", null,
            ProyectoId: proyectoId);

    private ActualizarInteresadoCommandHandler Editar() => new(_acotado);
    private QuitarInteresadoCommandHandler Quitar() =>
        new(_acotado, _usuario, new InteresadosAutomaticosSyncService(_acotado, _catalogo));

    private Task<InteresadoProyecto?> LeerAsync(int id) =>
        _global.ProyectoInteresados.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id);

    [Fact]
    public async Task No_se_edita_ni_quita_un_interesado_de_otro_proyecto_cambiando_su_numero()
    {
        var deOtro = Interesado(_otroMio);

        var editar = () => Editar().Handle(Edicion(deOtro, _miProyecto), CancellationToken.None);
        var quitar = () => Quitar().Handle(new QuitarInteresadoCommand(deOtro, _miProyecto), CancellationToken.None);

        await editar.Should().ThrowAsync<NotFoundException>();
        await quitar.Should().ThrowAsync<NotFoundException>();

        var i = await LeerAsync(deOtro);
        i.Should().NotBeNull("no se quitó");
        i!.Rol.Should().Be(RolInteresado.Beneficiario);
    }

    [Fact]
    public async Task Sin_alcance_sobre_el_proyecto_no_se_edita_ni_quita_su_interesado()
    {
        var ajeno = Interesado(_deOtraInstitucion);

        var editar = () => Editar().Handle(Edicion(ajeno, _deOtraInstitucion), CancellationToken.None);
        var quitar = () => Quitar().Handle(new QuitarInteresadoCommand(ajeno, _deOtraInstitucion), CancellationToken.None);
        var editarSinProyecto = () => Editar().Handle(Edicion(ajeno, null), CancellationToken.None);
        var quitarSinProyecto = () => Quitar().Handle(new QuitarInteresadoCommand(ajeno), CancellationToken.None);

        await editar.Should().ThrowAsync<NotFoundException>();
        await quitar.Should().ThrowAsync<NotFoundException>();
        await editarSinProyecto.Should().ThrowAsync<NotFoundException>();
        await quitarSinProyecto.Should().ThrowAsync<NotFoundException>();

        var i = await LeerAsync(ajeno);
        i.Should().NotBeNull();
        i!.Rol.Should().Be(RolInteresado.Beneficiario);
    }

    [Fact]
    public async Task Con_alcance_se_edita_y_quita_igual_que_antes()
    {
        var mio = Interesado(_miProyecto);

        await Editar().Handle(Edicion(mio, _miProyecto), CancellationToken.None);
        (await LeerAsync(mio))!.Rol.Should().Be(RolInteresado.ContraparteTecnica);

        await Quitar().Handle(new QuitarInteresadoCommand(mio, _miProyecto), CancellationToken.None);
        (await LeerAsync(mio)).Should().BeNull();

        var bitacora = await _global.BitacorasProyecto.AsNoTracking()
            .Where(b => b.ProyectoId == _miProyecto && b.Tipo == TipoEventoProyecto.Interesado)
            .Select(b => b.Detalle).ToListAsync();
        bitacora.Should().Contain(d => d.StartsWith("Interesado quitado"));
    }

    [Fact]
    public async Task Con_alcance_y_sin_proyecto_indicado_se_edita_y_quita_como_antes()
    {
        var mio = Interesado(_miProyecto);

        await Editar().Handle(Edicion(mio, null), CancellationToken.None);
        (await LeerAsync(mio))!.Rol.Should().Be(RolInteresado.ContraparteTecnica);

        await Quitar().Handle(new QuitarInteresadoCommand(mio), CancellationToken.None);
        (await LeerAsync(mio)).Should().BeNull();
    }

    public void Dispose()
    {
        _acotado.Dispose();
        _global.Dispose();
    }
}
