using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Common.Models;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Expedientes;

/// <summary>Un expediente documenta los trámites de OTRA institución: la casa (DIGER) es quien hace
/// la racionalización, nunca quien la recibe, así que ningún expediente lleva su Id. Anclar el
/// filtro solo en <c>InstitucionId == _activeInst</c> dejaba a todo el personal de la casa sin ver
/// un solo expediente — no por falta de permisos, sino porque la condición no podía cumplirse.</summary>
public class AlcanceExpedienteCasaTests
{
    private sealed class Usuario(bool global, string? inst, string? area, string? unidad, NivelAlcance nivel)
        : ICurrentUserService
    {
        public Guid?       UserId               => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string?     Nombre               => "prueba";
        public string?     Correo               => "prueba@diger.gob.hn";
        public string?     Rol                  => "Empleado";
        public bool        IsAuthenticated       => true;
        public bool        EsGlobal             => global;
        public NivelAlcance NivelAlcance         => nivel;
        public bool        EsSoloLectura         => false;
        public bool        EsSupervisor          => false;
        public bool        EsTecnicoSoporte      => false;
        public bool        EsJefeDeArea          => false;
        public bool        EsPmo                 => false;
        public string?     ActiveInstitucionId   => inst;
        public string?     ActiveAreaId          => area;
        public string?     ActiveUnidadId        => unidad;
        public IReadOnlyCollection<string> InstitucionesAsignadas => inst is null ? [] : [inst];
        public bool        PuedeAccederInstitucion(string? institucionId) => global || institucionId == inst;
    }

    /// <summary>Contexto sobre una base en memoria compartida por nombre, para poder sembrar con un
    /// usuario global y consultar con otro sin volver a sembrar.</summary>
    private static AppDbContext Ctx(string db, ICurrentUserService usuario, string casa = "DIGER")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(db).Options;
        return new AppDbContext(opts, usuario, NSubstitute.Substitute.For<MediatR.IPublisher>(),
            Options.Create(new InstitucionOptions { Id = casa }));
    }

    private static readonly ICurrentUserService Global =
        new Usuario(true, null, null, null, NivelAlcance.Global);

    private static void Sembrar(string db)
    {
        using var ctx = Ctx(db, Global);
        ctx.Expedientes.Add(Expediente.Crear("EXP-1", "IHADFA", null, null, "IHADFA", "Ana"));
        ctx.Expedientes.Add(Expediente.Crear("EXP-2", "CONSUCOOP", null, null, "CONSUCOOP", "Ana"));
        ctx.Expedientes.Add(Expediente.Crear("EXP-3", "INPREMA", null, null, "INPREMA", "Ana"));
        ctx.SaveChanges();
    }

    [Fact]
    public async Task Un_empleado_de_la_casa_ve_los_expedientes_que_trabaja()
    {
        var db = Guid.NewGuid().ToString();
        Sembrar(db);

        // Exactamente el caso reportado: DIGER / GOBDIG / DITRA, rol Empleado (alcance Unidad).
        using var ctx = Ctx(db, new Usuario(false, "DIGER", "GOBDIG", "DITRA", NivelAlcance.Unidad));
        var vistos = await ctx.Expedientes.Select(e => e.Codigo).ToListAsync();

        vistos.Should().BeEquivalentTo(["EXP-1", "EXP-2", "EXP-3"],
            "ningun expediente lleva el Id de la casa, asi que anclar solo en InstitucionId le daba cero");
    }

    [Fact]
    public async Task Una_institucion_racionalizada_sigue_viendo_solo_lo_suyo()
    {
        var db = Guid.NewGuid().ToString();
        Sembrar(db);

        using var ctx = Ctx(db, new Usuario(false, "IHADFA", null, null, NivelAlcance.Institucion));
        var vistos = await ctx.Expedientes.Select(e => e.Codigo).ToListAsync();

        vistos.Should().BeEquivalentTo(["EXP-1"],
            "la excepcion es solo para la casa: entre instituciones racionalizadas no se abre nada");
    }

    [Fact]
    public async Task La_institucion_de_la_casa_es_configurable_no_esta_fija_en_DIGER()
    {
        var db = Guid.NewGuid().ToString();
        Sembrar(db);

        // Mismo usuario de DIGER, pero el despliegue declara que la casa es otra.
        using var ctx = Ctx(db, new Usuario(false, "DIGER", null, null, NivelAlcance.Institucion), casa: "SEFIN");
        var vistos = await ctx.Expedientes.Select(e => e.Codigo).ToListAsync();

        vistos.Should().BeEmpty("si la casa es SEFIN, un usuario de DIGER es una institucion mas");
    }

    [Fact]
    public async Task El_soft_delete_sigue_aplicando_para_la_casa()
    {
        var db = Guid.NewGuid().ToString();
        Sembrar(db);
        using (var siembra = Ctx(db, Global))
        {
            var e = await siembra.Expedientes.FirstAsync(x => x.Codigo == "EXP-2");
            siembra.Expedientes.Remove(e);   // soft-delete: SaveChanges lo pasa a IsDeleted
            await siembra.SaveChangesAsync();
        }

        using var ctx = Ctx(db, new Usuario(false, "DIGER", null, null, NivelAlcance.Unidad));
        var vistos = await ctx.Expedientes.Select(e => e.Codigo).ToListAsync();

        vistos.Should().BeEquivalentTo(["EXP-1", "EXP-3"],
            "ver todos los expedientes no es ver los borrados");
    }
}
