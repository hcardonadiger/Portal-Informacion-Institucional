using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Tickets.Commands.CrearTicket;
using Diger.TramitesEstado.Application.Tickets.Common;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using Diger.TramitesEstado.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Tickets;

/// <summary>
/// Alcance de los tickets de soporte.
///
/// <para>El filtro RLS de <c>Ticket</c> exige coincidencia EXACTA de área y unidad —a diferencia de
/// los de Expediente, Contacto y Proyecto, que aceptan además <c>== null</c>—, y
/// <c>CrearTicketCommand</c> nunca estampa esas dos columnas. Las dos cosas juntas dejan a cada
/// ticket con el ancla en blanco y a casi todo el padrón sin poder verlo: 44 de las 51 asignaciones
/// reales tienen área o unidad asignada. Incluye a quien lo acaba de reportar.</para>
/// </summary>
public class AlcanceTicketTests
{
    private sealed class Usuario(Guid? id, NivelAlcance nivel, string? inst, string? area, string? unidad)
        : ICurrentUserService
    {
        public Guid?        UserId              => id;
        public string?      Nombre              => "u" + id;
        public string?      Correo              => $"u{id}@diger.gob.hn";
        public string?      Rol                 => nivel.ToString();
        public bool         IsAuthenticated     => true;
        public bool         EsGlobal            => nivel == NivelAlcance.Global;
        public NivelAlcance NivelAlcance        => nivel;
        public bool         EsSoloLectura       => false;
        public bool         EsSupervisor        => false;
        public bool         EsTecnicoSoporte    => false;
        public bool         EsJefeDeArea        => false;
        public bool         EsPmo               => false;
        public string?      ActiveInstitucionId => inst;
        public string?      ActiveAreaId        => area;
        public string?      ActiveUnidadId      => unidad;
        public IReadOnlyCollection<string> InstitucionesAsignadas => inst is null ? [] : [inst];
        public bool PuedeAccederInstitucion(string? institucionId) =>
            EsGlobal || (institucionId is not null && institucionId == inst);
    }

    private static DbContextOptions<AppDbContext> Opts(string db) =>
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(db).Options;

    private static AppDbContext Ctx(string db, ICurrentUserService u) =>
        new(Opts(db), u, NSubstitute.Substitute.For<MediatR.IPublisher>());

    private static async Task SembrarAsync(string db)
    {
        await using var ctx = Ctx(db, new Usuario(null, NivelAlcance.Global, null, null, null));
        await ctx.Instituciones.AddAsync(Institucion.Crear("DIGER", "DIRECCION PRESIDENCIAL DE GESTION POR RESULTADOS"));
        await ctx.PrioridadesTicket.AddAsync(PrioridadTicket.Crear("Media", 1, ColorEtiqueta.Azul));
        await ctx.SaveChangesAsync();
    }

    private static CrearTicketCommandHandler Crear(AppDbContext ctx, ICurrentUserService u) =>
        new(new TicketRepository(ctx), new InstitucionRepository(ctx), new ExpedienteRepository(ctx), u, ctx, ctx);

    /// <summary>Un empleado con unidad asignada —40 de las 51 asignaciones de la base— reporta un
    /// ticket y no vuelve a verlo: el filtro compara <c>UnidadId == su unidad</c> contra la columna
    /// nula que dejó el comando.</summary>
    [Fact]
    public async Task Quien_reporta_un_ticket_lo_sigue_viendo()
    {
        var db = Guid.NewGuid().ToString();
        await SembrarAsync(db);
        var yo = new Usuario(Guid.NewGuid(), NivelAlcance.Unidad, "DIGER", "GOBDIG", "TALDIG");

        await using (var ctx = Ctx(db, yo))
            await Crear(ctx, yo).Handle(
                new CrearTicketCommand(new TicketFormDto { Titulo = "No carga el acta", InstitucionId = "DIGER" }), default);

        await using (var ctx = Ctx(db, yo))
            (await ctx.Tickets.CountAsync()).Should().Be(1,
                "quien reporta un ticket tiene que poder seguirlo; si no, lo reporta otra vez");
    }

    /// <summary>Lo mismo para un jefe de área: su rol es de nivel Área y su asignación trae área,
    /// así que la rama exige <c>AreaId == su área</c> contra una columna nula.</summary>
    [Fact]
    public async Task Un_jefe_de_area_ve_los_tickets_de_su_institucion()
    {
        var db = Guid.NewGuid().ToString();
        await SembrarAsync(db);
        var reporta = new Usuario(Guid.NewGuid(), NivelAlcance.Unidad, "DIGER", null, null);
        var jefe    = new Usuario(Guid.NewGuid(), NivelAlcance.Area,   "DIGER", "GOBDIG", null);

        await using (var ctx = Ctx(db, reporta))
            await Crear(ctx, reporta).Handle(
                new CrearTicketCommand(new TicketFormDto { Titulo = "Error al exportar", InstitucionId = "DIGER" }), default);

        await using (var ctx = Ctx(db, jefe))
            (await ctx.Tickets.CountAsync()).Should().Be(1,
                "el ticket es de su institución y no tiene área propia: esconderlo deja el módulo sin jefatura");
    }
}
