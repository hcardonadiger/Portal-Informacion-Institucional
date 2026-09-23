using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Instituciones;

/// <summary>Un empleado anclado a DIGER: el caso que rompía el selector de instituciones
/// convocadas.</summary>
internal sealed class FakeEmpleadoDiger : ICurrentUserService
{
    public Guid?       UserId               => Guid.NewGuid();
    public string?     Nombre               => "Empleado";
    public string?     Correo               => "empleado@diger.gob.hn";
    public string?     Rol                  => "Empleado";
    public bool        IsAuthenticated       => true;
    public bool        EsGlobal             => false;
    public NivelAlcance NivelAlcance         => NivelAlcance.Unidad;
    public bool        EsSoloLectura         => false;
    public bool        EsSupervisor          => false;
    public bool        EsTecnicoSoporte      => false;
    public bool        EsJefeDeArea          => false;
    public bool        EsPmo                 => false;
    public string?     ActiveInstitucionId   => "DIGER";
    public string?     ActiveAreaId          => null;
    public string?     ActiveUnidadId        => null;
    public IReadOnlyCollection<string> InstitucionesAsignadas => ["DIGER"];
    public bool        PuedeAccederInstitucion(string? institucionId) => institucionId == "DIGER";
}

/// <summary>Quien escanea el QR de auto-registro no tiene sesión: sin institución activa, el filtro
/// de alcance resuelve `i.Id == null` y no devuelve nada.</summary>
internal sealed class FakeAnonimo : ICurrentUserService
{
    public Guid?       UserId               => null;
    public string?     Nombre               => null;
    public string?     Correo               => null;
    public string?     Rol                  => null;
    public bool        IsAuthenticated       => false;
    public bool        EsGlobal             => false;
    public NivelAlcance NivelAlcance         => NivelAlcance.Unidad;
    public bool        EsSoloLectura         => false;
    public bool        EsSupervisor          => false;
    public bool        EsTecnicoSoporte      => false;
    public bool        EsJefeDeArea          => false;
    public bool        EsPmo                 => false;
    public string?     ActiveInstitucionId   => null;
    public string?     ActiveAreaId          => null;
    public string?     ActiveUnidadId        => null;
    public IReadOnlyCollection<string> InstitucionesAsignadas => [];
    public bool        PuedeAccederInstitucion(string? institucionId) => false;
}

/// <summary>El catálogo de instituciones tiene dos lecturas distintas y no se pueden confundir:
/// como dato con alcance (solo la mía) y como catálogo del que elegir a quién convocar (todas).
/// Antes solo existía la primera, y por eso el selector de «Instituciones convocadas» mostraba una
/// sola opción a un empleado y ninguna en el auto-registro anónimo.</summary>
public class CatalogoParaConvocarTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly InstitucionRepository _repo;

    public CatalogoParaConvocarTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeEmpleadoDiger(), NSubstitute.Substitute.For<MediatR.IPublisher>());
        _repo = new InstitucionRepository(_ctx);
    }

    public void Dispose() => _ctx.Dispose();

    private async Task SembrarTresAsync()
    {
        _ctx.Instituciones.Add(Institucion.Crear("DIGER", "Direccion General de Regulacion"));
        _ctx.Instituciones.Add(Institucion.Crear("CNBS", "Comision Nacional de Bancos y Seguros"));
        _ctx.Instituciones.Add(Institucion.Crear("SEFIN", "Secretaria de Finanzas"));
        await _ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task El_alcance_recorta_el_catalogo_a_la_institucion_del_usuario()
    {
        // Documenta el comportamiento que SI queremos conservar: para los datos con alcance, un
        // empleado de DIGER ve DIGER. Es el filtro global de EF, no la pagina, quien lo recorta:
        // por eso quitar el .Where() del PageModel no arreglaba nada.
        await SembrarTresAsync();

        var conAlcance = await _repo.GetAllActivasAsync();

        conAlcance.Select(i => i.Id).Should().BeEquivalentTo(["DIGER"]);
    }

    [Fact]
    public async Task Para_convocar_un_empleado_ve_todas_las_instituciones()
    {
        await SembrarTresAsync();

        var catalogo = await _repo.GetActivasParaConvocarAsync();

        catalogo.Select(i => i.Id).Should().BeEquivalentTo(["CNBS", "DIGER", "SEFIN"],
            "convocar no es ver: hay que poder invitar a cualquier institucion, no solo a la propia");
    }

    [Fact]
    public async Task El_catalogo_para_convocar_sigue_ocultando_las_inactivas()
    {
        await SembrarTresAsync();
        var sefin = await _ctx.Instituciones.IgnoreQueryFilters().FirstAsync(i => i.Id == "SEFIN");
        sefin.Desactivar();
        await _ctx.SaveChangesAsync();

        var catalogo = await _repo.GetActivasParaConvocarAsync();

        catalogo.Select(i => i.Id).Should().BeEquivalentTo(["CNBS", "DIGER"],
            "saltarse el alcance no es saltarse el Activo");
    }
}

/// <summary>El auto-registro por QR lo abre gente sin sesion. Va en su propia clase porque el
/// usuario se fija al construir el DbContext.</summary>
public class CatalogoParaConvocarAnonimoTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly InstitucionRepository _repo;

    public CatalogoParaConvocarAnonimoTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new FakeAnonimo(), NSubstitute.Substitute.For<MediatR.IPublisher>());
        _repo = new InstitucionRepository(_ctx);
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task Sin_sesion_el_catalogo_con_alcance_sale_vacio_y_el_de_convocar_no()
    {
        _ctx.Instituciones.Add(Institucion.Crear("DIGER", "Direccion General de Regulacion"));
        _ctx.Instituciones.Add(Institucion.Crear("CNBS", "Comision Nacional de Bancos y Seguros"));
        await _ctx.SaveChangesAsync();

        var conAlcance = await _repo.GetAllActivasAsync();
        var catalogo   = await _repo.GetActivasParaConvocarAsync();

        conAlcance.Should().BeEmpty("sin institucion activa el filtro global resuelve i.Id == null");
        catalogo.Select(i => i.Id).Should().BeEquivalentTo(["CNBS", "DIGER"],
            "si no, el formulario de auto-registro se queda sin una sola institucion que elegir");
    }
}
