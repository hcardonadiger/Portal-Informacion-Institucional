using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.Reuniones.Queries.GetReuniones;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Reuniones;

/// <summary>
/// El listado de reuniones se desglosa por <b>última creación</b>: lo último que se
/// registra encabeza la lista, aunque la reunión en sí sea de una fecha anterior.
/// Antes se ordenaba por <c>Fecha</c> de la reunión, así que un acta cargada hoy para
/// una sesión de meses atrás se hundía varias páginas abajo y parecía no haberse guardado.
/// </summary>
public class OrdenListadoReunionesTests
{
    private sealed class UsuarioGlobal : ICurrentUserService
    {
        public Guid?        UserId               => Guid.NewGuid();
        public string?      Nombre               => "test";
        public string?      Correo               => "test@diger.gob.hn";
        public string?      Rol                  => "Administrador";
        public bool         IsAuthenticated      => true;
        public bool         EsGlobal             => true;
        public NivelAlcance NivelAlcance         => NivelAlcance.Global;
        public bool         EsSoloLectura        => false;
        public bool         EsSupervisor         => true;
        public bool         EsTecnicoSoporte     => true;
        public bool         EsJefeDeArea         => false;
        public bool         EsPmo                => false;
        public string?      ActiveInstitucionId  => null;
        public string?      ActiveAreaId         => null;
        public string?      ActiveUnidadId       => null;
        public IReadOnlyCollection<string> InstitucionesAsignadas => [];
        public bool PuedeAccederInstitucion(string? institucionId) => true;
    }

    private static AppDbContext NuevoCtx() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new UsuarioGlobal(), NSubstitute.Substitute.For<MediatR.IPublisher>());

    /// <summary>Crea la reunión y le fija el CreatedAt (la auditoría lo pisa al insertar).</summary>
    private static async Task SembrarAsync(AppDbContext ctx, string titulo, DateOnly fecha, DateTime creadaEl,
                                           string institucion = "CNBS", string tipo = "Taller")
    {
        var r = Reunion.Crear(titulo);
        r.Fecha = fecha;
        r.InstitucionId = "7";
        r.Institucion = institucion;
        r.Tipo = tipo;
        ctx.Reuniones.Add(r);
        await ctx.SaveChangesAsync();
        r.CreatedAt = creadaEl;
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Listado_EncabezaLoUltimoCreado_AunqueSuFechaSeaAnterior()
    {
        await using var ctx = NuevoCtx();

        // La reunión vieja se registró de último: es la que debe encabezar.
        await SembrarAsync(ctx, "Taller de junio", new DateOnly(2026, 6, 10), new DateTime(2026, 9, 24, 10, 0, 0));
        await SembrarAsync(ctx, "Reunión de agosto", new DateOnly(2026, 8, 20), new DateTime(2026, 9, 10, 10, 0, 0));
        await SembrarAsync(ctx, "Sesión de septiembre", new DateOnly(2026, 9, 5), new DateTime(2026, 9, 5, 10, 0, 0));

        var res = await new GetReunionesQueryHandler(ctx).Handle(new GetReunionesQuery(), CancellationToken.None);

        res.Items.Select(i => i.Titulo).Should().ContainInOrder(
            "Taller de junio", "Reunión de agosto", "Sesión de septiembre");
    }

    [Fact]
    public async Task Busqueda_RespetaElMismoOrdenPorUltimaCreacion()
    {
        await using var ctx = NuevoCtx();

        await SembrarAsync(ctx, "Capacitación SIGER enero", new DateOnly(2026, 1, 15), new DateTime(2026, 9, 24, 9, 0, 0));
        await SembrarAsync(ctx, "Capacitación SIGER agosto", new DateOnly(2026, 8, 15), new DateTime(2026, 8, 16, 9, 0, 0));
        await SembrarAsync(ctx, "Otra cosa", new DateOnly(2026, 9, 20), new DateTime(2026, 9, 20, 9, 0, 0));

        var res = await new GetReunionesQueryHandler(ctx).Handle(new GetReunionesQuery("Capacitación SIGER"), CancellationToken.None);

        res.Total.Should().Be(2);
        res.Items.Select(i => i.Titulo).Should().ContainInOrder(
            "Capacitación SIGER enero", "Capacitación SIGER agosto");
    }

    [Fact]
    public async Task MismaCreacion_DesempataPorFechaDeReunionDescendente()
    {
        await using var ctx = NuevoCtx();

        // Caso típico de importación masiva: todas entran con el mismo CreatedAt.
        var lote = new DateTime(2026, 9, 2, 21, 13, 0);
        await SembrarAsync(ctx, "Sesión del 12", new DateOnly(2026, 8, 12), lote);
        await SembrarAsync(ctx, "Sesión del 28", new DateOnly(2026, 8, 28), lote);
        await SembrarAsync(ctx, "Sesión del 19", new DateOnly(2026, 8, 19), lote);

        var res = await new GetReunionesQueryHandler(ctx).Handle(new GetReunionesQuery(), CancellationToken.None);

        res.Items.Select(i => i.Titulo).Should().ContainInOrder(
            "Sesión del 28", "Sesión del 19", "Sesión del 12");
    }
}
