using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Tests.Permisos;
using Diger.TramitesEstado.Application.Usuarios.Commands.EliminarUsuario;
using Diger.TramitesEstado.Application.Usuarios.Queries.GetUsuarios;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Usuarios;

/// <summary>
/// Borrado lógico de usuarios.
///
/// <para><b>Por qué lógico y no duro.</b> Quince columnas repartidas en once tablas guardan el
/// GUID de un usuario <b>sin clave foránea</b> —<c>Proyectos.ResponsableId</c>,
/// <c>Expedientes.AnalistaId</c>, <c>ProyectoInteresados.UsuarioId</c> y compañía—. Un DELETE las
/// dejaría apuntando a un fantasma, en silencio; y encima fallaría en seco contra
/// <c>Tickets.CreadoPorId</c>, que es NO ACTION. La fila se conserva y lo que cambia es que deja
/// de verse.</para>
///
/// <para><b>Ocultar sin cerrar el login sería un maquillaje.</b> Por eso el borrado se apoya en un
/// filtro global de <c>AppDbContext</c> y no en un <c>Where</c> puesto en la lista: así el usuario
/// desaparece también del login, de los selectores de responsable y de cualquier consulta futura
/// que nadie se acuerde de filtrar. La prueba del login es la que fija esa diferencia.</para>
/// </summary>
public class EliminarUsuarioTests : IDisposable
{
    private const string Admin    = "Administrador";
    private const string Empleado = "Empleado";

    private readonly AppDbContext _ctx;
    private readonly IRolCatalogo _catalogo = CatalogoFake.Con(
        CatalogoFake.Rol(Admin, esAdministrador: true),
        CatalogoFake.Rol(Empleado));

    /// <summary>Quien ejecuta: administrador (EsGlobal) y distinto de a quien se borra.</summary>
    private readonly ICurrentUserService _admin = Substitute.For<ICurrentUserService>();

    public EliminarUsuarioTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var contexto = Substitute.For<ICurrentUserService>();
        contexto.EsGlobal.Returns(true);
        _ctx = new AppDbContext(opts, contexto, Substitute.For<MediatR.IPublisher>());

        _admin.EsGlobal.Returns(true);
        _admin.UserId.Returns(Guid.NewGuid());
        _admin.Nombre.Returns("Henry Cardona");
    }

    public void Dispose() => _ctx.Dispose();

    private async Task<Usuario> SembrarAsync(string rol = Empleado, string? correo = null)
    {
        var u = Usuario.Crear("Usuario de prueba", correo ?? $"{Guid.NewGuid():N}@diger.gob.hn", "hash");
        _ctx.Usuarios.Add(u);
        _ctx.AsignacionesUsuario.Add(AsignacionUsuario.Crear(u.Id, "DIGER", null, null, rol));
        await _ctx.SaveChangesAsync(CancellationToken.None);
        return u;
    }

    private EliminarUsuarioCommandHandler Handler(ICurrentUserService? quien = null) =>
        new(_ctx, _ctx, quien ?? _admin, _catalogo);

    private Task<int> EnLaListaAsync() =>
        new GetUsuariosQueryHandler(_ctx)
            .Handle(new GetUsuariosQuery(), CancellationToken.None)
            .ContinueWith(t => t.Result.Total);

    [Fact]
    public async Task Eliminar_lo_saca_de_la_lista_de_usuarios()
    {
        var u = await SembrarAsync();
        (await EnLaListaAsync()).Should().Be(1);

        await Handler().Handle(new EliminarUsuarioCommand(u.Id), CancellationToken.None);

        (await EnLaListaAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Eliminar_conserva_la_fila_para_que_nada_quede_apuntando_al_vacio()
    {
        var u = await SembrarAsync();

        await Handler().Handle(new EliminarUsuarioCommand(u.Id), CancellationToken.None);

        var fila = await _ctx.Usuarios.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == u.Id);
        fila.Should().NotBeNull("es un borrado lógico: la fila sobrevive");
        fila!.IsDeleted.Should().BeTrue();
        fila.Activo.Should().BeFalse("además se desactiva, para cerrar también los caminos que solo miran Activo");
    }

    [Fact]
    public async Task Eliminar_le_cierra_el_login()
    {
        // La prueba que separa un borrado de un maquillaje: si esta falla, el «eliminado» sigue
        // entrando al portal con su contraseña de siempre.
        var u = await SembrarAsync(correo: "saliente@diger.gob.hn");
        var repo = new UsuarioRepository(_ctx);

        (await repo.GetByCorreoAsync("saliente@diger.gob.hn")).Should().NotBeNull();

        await Handler().Handle(new EliminarUsuarioCommand(u.Id), CancellationToken.None);

        (await repo.GetByCorreoAsync("saliente@diger.gob.hn")).Should().BeNull();
    }

    [Fact]
    public async Task Eliminar_lo_rechaza_a_quien_no_es_administrador()
    {
        var u = await SembrarAsync();

        var jefe = Substitute.For<ICurrentUserService>();
        jefe.EsGlobal.Returns(false);
        jefe.UserId.Returns(Guid.NewGuid());

        var act = () => Handler(jefe).Handle(new EliminarUsuarioCommand(u.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*administrador*");
        (await EnLaListaAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Eliminar_no_deja_que_uno_se_borre_a_si_mismo()
    {
        var u = await SembrarAsync(Admin);

        var el = Substitute.For<ICurrentUserService>();
        el.EsGlobal.Returns(true);
        el.UserId.Returns(u.Id);

        var act = () => Handler(el).Handle(new EliminarUsuarioCommand(u.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*sí mismo*");
    }

    [Fact]
    public async Task Eliminar_no_deja_borrar_al_ultimo_administrador()
    {
        // Mismo invariante que ya protege a la desactivación: el portal no puede quedarse sin
        // nadie capaz de administrarlo. Sin esto, la única salida sería tocar la base a mano.
        var unico = await SembrarAsync(Admin);

        var act = () => Handler().Handle(new EliminarUsuarioCommand(unico.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        (await EnLaListaAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Eliminar_a_un_administrador_se_permite_si_queda_otro()
    {
        await SembrarAsync(Admin);
        var segundo = await SembrarAsync(Admin);

        await Handler().Handle(new EliminarUsuarioCommand(segundo.Id), CancellationToken.None);

        (await EnLaListaAsync()).Should().Be(1);
    }

    // ── Restaurar ─────────────────────────────────────────────────
    [Fact]
    public async Task La_lista_con_eliminados_los_muestra_marcados()
    {
        var u = await SembrarAsync();
        await Handler().Handle(new EliminarUsuarioCommand(u.Id), CancellationToken.None);

        var conEliminados = await new GetUsuariosQueryHandler(_ctx)
            .Handle(new GetUsuariosQuery(IncluirEliminados: true), CancellationToken.None);

        conEliminados.Items.Should().ContainSingle().Which.Eliminado.Should().BeTrue();
    }

    [Fact]
    public async Task Restaurar_lo_devuelve_a_la_lista_y_le_reabre_el_login()
    {
        var u = await SembrarAsync(correo: "vuelve@diger.gob.hn");
        await Handler().Handle(new EliminarUsuarioCommand(u.Id), CancellationToken.None);

        await new RestaurarUsuarioCommandHandler(_ctx, _ctx, _admin)
            .Handle(new RestaurarUsuarioCommand(u.Id), CancellationToken.None);

        (await EnLaListaAsync()).Should().Be(1);
        (await new UsuarioRepository(_ctx).GetByCorreoAsync("vuelve@diger.gob.hn")).Should().NotBeNull();
    }

    [Fact]
    public async Task Restaurar_tambien_es_solo_para_administradores()
    {
        var u = await SembrarAsync();
        await Handler().Handle(new EliminarUsuarioCommand(u.Id), CancellationToken.None);

        var jefe = Substitute.For<ICurrentUserService>();
        jefe.EsGlobal.Returns(false);

        var act = () => new RestaurarUsuarioCommandHandler(_ctx, _ctx, jefe)
            .Handle(new RestaurarUsuarioCommand(u.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*administrador*");
        (await EnLaListaAsync()).Should().Be(0);
    }
}
