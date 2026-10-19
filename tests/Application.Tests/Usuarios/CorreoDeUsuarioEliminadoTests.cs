using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Usuarios.Commands.CrearUsuario;
using Diger.TramitesEstado.Domain.Common;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Usuarios;

/// <summary>
/// El correo de un usuario dado de baja sigue ocupado.
///
/// <para><c>IX_Usuarios_Correo</c> es ÚNICO y no lleva filtro, así que cubre también a los
/// eliminados —que se conservan a propósito, para poder restaurarlos—. Pero
/// <c>UsuarioRepository.ExisteCorreoAsync</c> consulta a través del filtro global
/// <c>!u.IsDeleted</c>, o sea que no los ve: la validación da el correo por libre y el choque
/// aparece recién en el INSERT, como violación de índice en vez de un mensaje entendible. En la
/// base hay 15 usuarios eliminados.</para>
///
/// <para>El resto del módulo ya resuelve esto bien —EliminarUsuario, el listado con «mostrar
/// eliminados» y la identificación por token piden <c>IgnoreQueryFilters()</c> explícitamente—, así
/// que lo que falta acá es la misma excepción.</para>
/// </summary>
public class CorreoDeUsuarioEliminadoTests
{
    private sealed class Admin : ICurrentUserService
    {
        public Guid?        UserId              => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string?      Nombre              => "admin";
        public string?      Correo              => "admin@diger.gob.hn";
        public string?      Rol                 => "Administrador";
        public bool         IsAuthenticated     => true;
        public bool         EsGlobal            => true;
        public NivelAlcance NivelAlcance        => NivelAlcance.Global;
        public bool         EsSoloLectura       => false;
        public bool         EsSupervisor        => true;
        public bool         EsTecnicoSoporte    => false;
        public bool         EsJefeDeArea        => false;
        public bool         EsPmo               => false;
        public string?      ActiveInstitucionId => null;
        public string?      ActiveAreaId        => null;
        public string?      ActiveUnidadId      => null;
        public IReadOnlyCollection<string> InstitucionesAsignadas => [];
        public bool PuedeAccederInstitucion(string? institucionId) => true;
    }

    private sealed class HasherFalso : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string hash) => hash == "hash:" + password;
    }

    [Fact]
    public async Task No_se_puede_reusar_el_correo_de_un_usuario_eliminado()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

        await using var ctx = new AppDbContext(opts, new Admin(), NSubstitute.Substitute.For<MediatR.IPublisher>());

        var baja = Usuario.Crear("Carlos Mejía", "carlos.mejia@diger.gob.hn", "hash:x");
        baja.IsDeleted = true;
        await ctx.Usuarios.AddAsync(baja);
        await ctx.SaveChangesAsync();

        var crear = () => new CrearUsuarioCommandHandler(new UsuarioRepository(ctx), new HasherFalso(), ctx)
            .Handle(new CrearUsuarioCommand("Carlos Mejia", "carlos.mejia@diger.gob.hn", "Clave#2026"), default);

        await crear.Should().ThrowAsync<DomainException>(
            "el correo sigue ocupado por el eliminado; sin este aviso el fallo sale como violación de IX_Usuarios_Correo");
    }
}
