using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

public class VisibilidadReunionTests
{
    // Usuario configurable para probar el filtro global por alcance + privacidad.
    private sealed class Usuario(Guid? id, bool global, string[] inst) : ICurrentUserService
    {
        public Guid?       UserId               => id;
        public string?     Nombre               => "u" + id;
        public string?     Correo               => $"u{id}@x.com";
        public string?     Rol                  => "Empleado";
        public bool        IsAuthenticated       => true;
        public bool        EsGlobal             => global;
        public string?     ActiveInstitucionId   => inst.Length > 0 ? inst[0] : null;
        public string?     ActiveAreaId          => null;
        public string?     ActiveUnidadId        => null;
        public IReadOnlyCollection<string> InstitucionesAsignadas => inst;
        public bool        PuedeAccederInstitucion(string? institucionId) => global || (institucionId is string i && inst.Contains(i));
    }

    private static DbContextOptions<AppDbContext> Opts(string db) =>
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(db).Options;

    [Fact]
    public async Task Privada_SoloVisibleParaSuCreador()
    {
        var db = Guid.NewGuid().ToString();

        // El dueño (usuario 1, alcance a institución 7) crea una privada y una pública.
        var uid1 = Guid.NewGuid();
        await using (var ctx = new AppDbContext(Opts(db), new Usuario(uid1, false, ["7"])))
        {
            var priv = Reunion.Crear("Privada"); priv.InstitucionId = "7"; priv.Institucion = "SENASA";
            priv.Visibilidad = VisibilidadReunion.Privada; priv.CreadoPorId = uid1;
            var pub = Reunion.Crear("Pública"); pub.InstitucionId = "7"; pub.Institucion = "SENASA";
            await ctx.Reuniones.AddRangeAsync(priv, pub);
            await ctx.SaveChangesAsync();
        }

        // El dueño ve ambas.
        await using (var ctx = new AppDbContext(Opts(db), new Usuario(uid1, false, ["7"])))
            (await ctx.Reuniones.Select(r => r.Titulo).ToListAsync())
                .Should().BeEquivalentTo(["Privada", "Pública"]);

        // Otro usuario con alcance a la misma institución NO ve la privada.
        await using (var ctx = new AppDbContext(Opts(db), new Usuario(Guid.NewGuid(), false, ["7"])))
            (await ctx.Reuniones.Select(r => r.Titulo).ToListAsync())
                .Should().BeEquivalentTo(["Pública"]);

        // Un administrador global tampoco ve la privada de otro.
        await using (var ctx = new AppDbContext(Opts(db), new Usuario(Guid.NewGuid(), true, [])))
            (await ctx.Reuniones.Select(r => r.Titulo).ToListAsync())
                .Should().BeEquivalentTo(["Pública"]);
    }
}
