using Diger.TramitesEstado.Application.Common.Exceptions;
using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.TokensMcp;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.TokensMcp;

/// <summary>
/// Emisión y revocación de tokens MCP. Lo que se protege: que el token nunca se persista en
/// claro, que nadie emita un token sobre una asignación ajena y que la revocación corte de
/// verdad.
/// </summary>
public class TokenMcpCommandsTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _uow;
    private readonly TokenMcpHasher _hasher = new();
    private readonly Guid _usuarioId = Guid.NewGuid();

    public TokenMcpCommandsTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _currentUser.EsGlobal.Returns(true);
        _currentUser.UserId.Returns(_usuarioId);

        _ctx = new AppDbContext(opts, _currentUser, Substitute.For<MediatR.IPublisher>());
        _uow = _ctx;
    }

    private async Task<AsignacionUsuario> SembrarAsignacionAsync(
        Guid? usuarioId = null, string institucion = "DIGER", string rol = "Empleado")
    {
        var asignacion = AsignacionUsuario.Crear(usuarioId ?? _usuarioId, institucion, null, null, rol);
        _ctx.AsignacionesUsuario.Add(asignacion);
        await _ctx.SaveChangesAsync();
        return asignacion;
    }

    private GenerarTokenMcpCommandHandler Generador() => new(_ctx, _currentUser, _hasher, _uow);

    // ── Emisión ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Con_una_sola_asignacion_no_hace_falta_elegir()
    {
        var asignacion = await SembrarAsignacionAsync();

        var generado = await Generador().Handle(
            new GenerarTokenMcpCommand("Claude Code", null, 90), CancellationToken.None);

        var fila = await _ctx.TokensAccesoMcp.SingleAsync();
        fila.AsignacionId.Should().Be(asignacion.Id);
        fila.UsuarioId.Should().Be(_usuarioId);
        generado.Token.Should().StartWith("pdt.");
    }

    /// <summary>El corazón del asunto: en la base queda el hash, no el token.</summary>
    [Fact]
    public async Task Lo_que_se_persiste_es_el_hash_y_no_el_token()
    {
        await SembrarAsignacionAsync();

        var generado = await Generador().Handle(
            new GenerarTokenMcpCommand("Claude Code", null, null), CancellationToken.None);

        var fila = await _ctx.TokensAccesoMcp.SingleAsync();

        fila.HashSha256.Should().NotBe(generado.Token);
        fila.HashSha256.Should().NotContain(generado.Token.Split('.')[2]);
        fila.Prefijo.Should().Be(generado.Prefijo);
        _hasher.Verificar(fila.HashSha256, generado.Token).Should().BeTrue(
            "el token entregado tiene que seguir validando contra lo guardado");
    }

    [Fact]
    public async Task Con_varias_asignaciones_exige_elegir_una()
    {
        await SembrarAsignacionAsync(institucion: "DIGER");
        await SembrarAsignacionAsync(institucion: "SEFIN");

        var accion = async () => await Generador().Handle(
            new GenerarTokenMcpCommand("Claude Code", null, null), CancellationToken.None);

        await accion.Should().ThrowAsync<DomainException>()
            .WithMessage("*elija*");
    }

    [Fact]
    public async Task Con_varias_asignaciones_respeta_la_elegida()
    {
        await SembrarAsignacionAsync(institucion: "DIGER");
        var sefin = await SembrarAsignacionAsync(institucion: "SEFIN");

        await Generador().Handle(
            new GenerarTokenMcpCommand("Claude Code", sefin.Id, null), CancellationToken.None);

        (await _ctx.TokensAccesoMcp.SingleAsync()).AsignacionId.Should().Be(sefin.Id);
    }

    /// <summary>Emitir un token sobre la asignación de otra persona sería escalar privilegios:
    /// el token hereda la institución y el rol de esa asignación.</summary>
    [Fact]
    public async Task No_se_puede_emitir_sobre_una_asignacion_ajena()
    {
        await SembrarAsignacionAsync();
        var ajena = await SembrarAsignacionAsync(usuarioId: Guid.NewGuid(), institucion: "SEFIN", rol: "Administrador");

        var accion = async () => await Generador().Handle(
            new GenerarTokenMcpCommand("Robado", ajena.Id, null), CancellationToken.None);

        await accion.Should().ThrowAsync<DomainException>().WithMessage("*no es suya*");
        (await _ctx.TokensAccesoMcp.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_ninguna_asignacion_no_se_emite_nada()
    {
        var accion = async () => await Generador().Handle(
            new GenerarTokenMcpCommand("Claude Code", null, null), CancellationToken.None);

        await accion.Should().ThrowAsync<DomainException>().WithMessage("*asignación*");
    }

    [Fact]
    public async Task La_vigencia_en_dias_se_traduce_a_una_fecha_de_vencimiento()
    {
        await SembrarAsignacionAsync();

        await Generador().Handle(
            new GenerarTokenMcpCommand("Claude Code", null, 30), CancellationToken.None);

        var fila = await _ctx.TokensAccesoMcp.SingleAsync();
        fila.ExpiraUtc.Should().NotBeNull();
        fila.ExpiraUtc!.Value.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Sin_dias_de_vigencia_el_token_no_vence()
    {
        await SembrarAsignacionAsync();

        await Generador().Handle(
            new GenerarTokenMcpCommand("Claude Code", null, null), CancellationToken.None);

        (await _ctx.TokensAccesoMcp.SingleAsync()).ExpiraUtc.Should().BeNull();
    }

    /// <summary>La auditoría solo sellaba las entidades con Id entero: el token (Id Guid)
    /// salía con CreatedAt en 01/01/0001 y así lo mostraba la pantalla.</summary>
    [Fact]
    public async Task El_token_emitido_lleva_su_fecha_de_creacion()
    {
        await SembrarAsignacionAsync();

        await Generador().Handle(
            new GenerarTokenMcpCommand("Claude Code", null, null), CancellationToken.None);

        (await _ctx.TokensAccesoMcp.SingleAsync()).CreatedAt
            .Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    /// <summary>Mismo defecto, otra entidad con Id Guid: el sello no puede depender del tipo
    /// de la llave.</summary>
    [Fact]
    public async Task La_auditoria_sella_entidades_con_llave_guid()
    {
        var asignacion = await SembrarAsignacionAsync();

        asignacion.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    // ── Revocación ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Revocar_un_token_propio_lo_inutiliza()
    {
        await SembrarAsignacionAsync();
        var generado = await Generador().Handle(
            new GenerarTokenMcpCommand("Claude Code", null, null), CancellationToken.None);

        await new RevocarTokenMcpCommandHandler(_ctx, _currentUser, _uow)
            .Handle(new RevocarTokenMcpCommand(generado.Id), CancellationToken.None);

        var fila = await _ctx.TokensAccesoMcp.SingleAsync();
        fila.EstaRevocado.Should().BeTrue();
        fila.EsUtilizable(DateTime.UtcNow).Should().BeFalse();
    }

    /// <summary>Revocar no borra: un token que se filtró es justo lo que hay que poder mirar
    /// después.</summary>
    [Fact]
    public async Task Revocar_conserva_la_fila()
    {
        await SembrarAsignacionAsync();
        var generado = await Generador().Handle(
            new GenerarTokenMcpCommand("Claude Code", null, null), CancellationToken.None);

        await new RevocarTokenMcpCommandHandler(_ctx, _currentUser, _uow)
            .Handle(new RevocarTokenMcpCommand(generado.Id), CancellationToken.None);

        (await _ctx.TokensAccesoMcp.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task No_se_puede_revocar_el_token_de_otro()
    {
        // Un token de otra persona, sembrado a mano.
        var otro = TokenAccesoMcp.Crear(Guid.NewGuid(), Guid.NewGuid(), "Ajeno", "xyz789", "hash", null);
        _ctx.TokensAccesoMcp.Add(otro);
        await _ctx.SaveChangesAsync();

        var accion = async () => await new RevocarTokenMcpCommandHandler(_ctx, _currentUser, _uow)
            .Handle(new RevocarTokenMcpCommand(otro.Id), CancellationToken.None);

        // Mismo error que si no existiera: a quien prueba con Ids ajenos no se le confirma cuáles hay.
        await accion.Should().ThrowAsync<NotFoundException>();
        (await _ctx.TokensAccesoMcp.SingleAsync()).EstaRevocado.Should().BeFalse();
    }

    /// <summary>La vista de administración sí puede: ante un token filtrado, alguien tiene que
    /// poder cortarlo sin la sesión del dueño.</summary>
    [Fact]
    public async Task Un_administrador_si_puede_revocar_el_token_de_otro()
    {
        var otro = TokenAccesoMcp.Crear(Guid.NewGuid(), Guid.NewGuid(), "Ajeno", "xyz789", "hash", null);
        _ctx.TokensAccesoMcp.Add(otro);
        await _ctx.SaveChangesAsync();

        await new RevocarTokenMcpComoAdminCommandHandler(_ctx, _uow)
            .Handle(new RevocarTokenMcpComoAdminCommand(otro.Id), CancellationToken.None);

        (await _ctx.TokensAccesoMcp.SingleAsync()).EstaRevocado.Should().BeTrue();
    }

    // ── Consulta ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task La_lista_propia_no_muestra_los_tokens_de_otros()
    {
        await SembrarAsignacionAsync();
        await Generador().Handle(new GenerarTokenMcpCommand("Mío", null, null), CancellationToken.None);

        _ctx.TokensAccesoMcp.Add(
            TokenAccesoMcp.Crear(Guid.NewGuid(), Guid.NewGuid(), "Ajeno", "xyz789", "hash", null));
        await _ctx.SaveChangesAsync();

        var mios = await new GetMisTokensMcpQueryHandler(_ctx, _currentUser)
            .Handle(new GetMisTokensMcpQuery(), CancellationToken.None);

        mios.Should().ContainSingle().Which.Nombre.Should().Be("Mío");
    }

    /// <summary>Ningún DTO puede llevar el hash: la pantalla lo mostraría y el navegador lo
    /// guardaría en caché.</summary>
    [Fact]
    public async Task La_lista_expone_el_prefijo_y_nunca_el_hash()
    {
        await SembrarAsignacionAsync();
        var generado = await Generador().Handle(
            new GenerarTokenMcpCommand("Mío", null, null), CancellationToken.None);

        var mios = await new GetMisTokensMcpQueryHandler(_ctx, _currentUser)
            .Handle(new GetMisTokensMcpQuery(), CancellationToken.None);

        var dto = mios.Single();
        dto.Prefijo.Should().Be(generado.Prefijo);

        typeof(TokenMcpDto).GetProperties().Select(p => p.Name)
            .Should().NotContain(n => n.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task El_estado_refleja_revocado_y_vencido()
    {
        await SembrarAsignacionAsync();
        var generado = await Generador().Handle(
            new GenerarTokenMcpCommand("Mío", null, null), CancellationToken.None);

        var antes = await new GetMisTokensMcpQueryHandler(_ctx, _currentUser)
            .Handle(new GetMisTokensMcpQuery(), CancellationToken.None);
        antes.Single().Estado.Should().Be("Activo");

        await new RevocarTokenMcpCommandHandler(_ctx, _currentUser, _uow)
            .Handle(new RevocarTokenMcpCommand(generado.Id), CancellationToken.None);

        var despues = await new GetMisTokensMcpQueryHandler(_ctx, _currentUser)
            .Handle(new GetMisTokensMcpQuery(), CancellationToken.None);
        despues.Single().Estado.Should().Be("Revocado");
        despues.Single().EstaVigente.Should().BeFalse();
    }

    /// <summary>Los tokens emitidos antes del arreglo quedaron sin fecha en la base. No hay de
    /// dónde reconstruirla, así que la lista dice que no la sabe en vez de mostrar 01/01/0001.</summary>
    [Fact]
    public async Task Un_token_sin_fecha_registrada_no_inventa_una()
    {
        await SembrarAsignacionAsync();
        await Generador().Handle(new GenerarTokenMcpCommand("Viejo", null, null), CancellationToken.None);

        var fila = await _ctx.TokensAccesoMcp.SingleAsync();
        fila.CreatedAt = default;
        await _ctx.SaveChangesAsync();

        var mios = await new GetMisTokensMcpQueryHandler(_ctx, _currentUser)
            .Handle(new GetMisTokensMcpQuery(), CancellationToken.None);

        mios.Single().CreadoUtc.Should().BeNull();
    }

    public void Dispose() => _ctx.Dispose();
}
