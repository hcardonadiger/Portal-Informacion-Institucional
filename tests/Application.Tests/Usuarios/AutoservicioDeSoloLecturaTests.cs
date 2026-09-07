using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Usuarios;

/// <summary>
/// La red de última línea de <c>AppDbContext.SaveChangesAsync</c> y el autoservicio.
///
/// <para>El bloqueo de mutaciones para roles de solo lectura no admitía excepciones, de modo que
/// un Consultor no podía cambiar su propia contraseña ni editar su perfil aunque el filtro de
/// páginas lo dejara pasar: moría al guardar. Eso convertía una decisión de negocio —«la
/// contraseña propia no depende de un permiso otorgable», que es lo que dice
/// <c>[PermisoNoRequerido]</c>— en algo imposible de cumplir.</para>
///
/// <para>La excepción se acota a la fila del propio usuario. Es la más estrecha que resuelve el
/// caso: no hace falta una bandera por unidad de trabajo que cualquier página pueda levantar.</para>
/// </summary>
public sealed class AutoservicioDeSoloLecturaTests
{
    private readonly DbContextOptions<AppDbContext> _opts = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    /// <summary>Contexto de un rol de solo lectura. El alcance es global a propósito: acá se mide
    /// el bloqueo de escritura, no el filtro institucional.</summary>
    private AppDbContext ComoSoloLectura(Guid usuarioId)
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(usuarioId);
        currentUser.EsSoloLectura.Returns(true);
        currentUser.EsGlobal.Returns(true);

        return new AppDbContext(_opts, currentUser, Substitute.For<MediatR.IPublisher>());
    }

    /// <summary>Un contexto sin la restricción, para sembrar sin chocar con la propia guarda.</summary>
    private AppDbContext Libre()
    {
        var sinRestriccion = Substitute.For<ICurrentUserService>();
        sinRestriccion.EsGlobal.Returns(true);
        sinRestriccion.EsSoloLectura.Returns(false);

        return new AppDbContext(_opts, sinRestriccion, Substitute.For<MediatR.IPublisher>());
    }

    private async Task<Guid> SembrarAsync(string correo)
    {
        await using var libre = Libre();

        var u = Usuario.Crear("Usuario de prueba", correo, "hash-viejo");
        libre.Usuarios.Add(u);
        await libre.SaveChangesAsync();

        return u.Id;
    }

    [Fact]
    public async Task Puede_cambiar_la_contrasena_de_su_propia_fila()
    {
        var propio = await SembrarAsync("propio@pruebas.gob.hn");

        await using var ctx = ComoSoloLectura(propio);
        var yo = await ctx.Usuarios.SingleAsync(u => u.Id == propio);
        yo.CambiarPassword("hash-nuevo");

        var guardadas = await ctx.SaveChangesAsync();

        guardadas.Should().Be(1);
    }

    [Fact]
    public async Task No_puede_tocar_la_fila_de_otro()
    {
        var propio  = await SembrarAsync("propio@pruebas.gob.hn");
        var ajenoId = await SembrarAsync("ajeno@pruebas.gob.hn");

        await using var ctx = ComoSoloLectura(propio);
        var fila = await ctx.Usuarios.SingleAsync(u => u.Id == ajenoId);
        fila.CambiarPassword("hash-de-otro");

        var acto = async () => await ctx.SaveChangesAsync();

        await acto.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task No_puede_mutar_nada_que_no_sea_su_propia_cuenta()
    {
        // La excepción es por guardado completo, no por fila suelta: aunque la única fila de
        // Usuario tocada sea la propia, cualquier otra entidad en la misma unidad de trabajo
        // vuelve a cerrar la puerta. Así no sirve de vehículo para colar otra mutación.
        var propio = await SembrarAsync("propio@pruebas.gob.hn");

        await using var ctx = ComoSoloLectura(propio);
        var yo = await ctx.Usuarios.SingleAsync(u => u.Id == propio);
        yo.CambiarPassword("hash-nuevo");

        var reunion = Reunion.Crear("Una reunión que este rol no puede crear");
        reunion.InstitucionId = "DIGER";
        ctx.Reuniones.Add(reunion);

        var acto = async () => await ctx.SaveChangesAsync();

        await acto.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
