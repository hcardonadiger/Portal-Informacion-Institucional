using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Application.MiDia.Common;
using Diger.TramitesEstado.Application.MiDia.Queries.GetMiDia;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.MiDia;

/// <summary>
/// Usuario de prueba con Id <b>estable</b>.
///
/// <para>No se reutiliza el <c>FakeCurrentUser</c> compartido a propósito: aquél devuelve un
/// <c>Guid.NewGuid()</c> en cada lectura de <c>UserId</c>, lo cual da igual cuando solo se ejercita
/// el alcance, pero hace imposible probar cualquier consulta que filtre por «asignado a mí» —el id
/// con el que se siembra nunca vuelve a ser el mismo que el de la consulta—.</para>
/// </summary>
internal sealed class UsuarioFijo : ICurrentUserService
{
    public static readonly Guid IdUsuario = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public Guid?        UserId                 => IdUsuario;
    public string?      Nombre                 => "Ana Ortez";
    public string?      Correo                 => "ana.ortez@diger.gob.hn";
    public string?      Rol                    => "Coordinador";
    public bool         IsAuthenticated        => true;
    public bool         EsGlobal               => true;
    public NivelAlcance NivelAlcance           => NivelAlcance.Global;
    public bool         EsSoloLectura          => false;
    public bool         EsSupervisor           => true;
    public bool         EsTecnicoSoporte       => true;
    public string?      ActiveInstitucionId    => null;
    public string?      ActiveAreaId           => null;
    public string?      ActiveUnidadId         => null;
    public IReadOnlyCollection<string> InstitucionesAsignadas => [];
    public bool PuedeAccederInstitucion(string? institucionId) => true;
}

public class MiDiaTests : IDisposable
{
    private readonly AppDbContext _ctx;
    private readonly UsuarioFijo  _yo = new();
    private readonly GetMiDiaQueryHandler _handler;

    /// <summary>La bandeja razona en día local, igual que el DTO.</summary>
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);

    public MiDiaTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _ctx = new AppDbContext(opts, new UsuarioFijo(), NSubstitute.Substitute.For<MediatR.IPublisher>());
        _handler = new GetMiDiaQueryHandler(_ctx, _yo);
    }

    // ── Siembra ───────────────────────────────────────────────────────────────

    private async Task<int> SembrarCompromisoAsync(DateOnly? plazo, string? responsable = null)
    {
        var r = Reunion.Crear("Mesa técnica con SEFIN");
        r.Agregar(new AcuerdoReunion
        {
            Compromiso  = "Enviar el borrador de la ficha",
            Responsable = responsable ?? _yo.Nombre,
            Plazo       = plazo,
            Estado      = EstadoCompromiso.Pendiente
        });
        _ctx.Reuniones.Add(r);
        await _ctx.SaveChangesAsync();
        return r.Id;
    }

    private async Task SembrarActividadAsync(DateOnly? finPlan, Guid? responsableId = null)
    {
        var p = Proyecto.Crear("PRY-001", "Portal de trámites");
        var e = EntregableProyecto.Crear("Diseño funcional", 1);
        var a = ActividadProyecto.Crear("Redactar los TDR", 1);
        a.Definir("Redactar los TDR", null, null, finPlan, responsableId ?? UsuarioFijo.IdUsuario, "Ana Ortez");
        e.Agregar(a);
        p.Agregar(e);
        _ctx.Proyectos.Add(p);
        await _ctx.SaveChangesAsync();
    }

    private async Task SembrarMetaAsync(DateOnly? fin, EstadoPlanTrabajo estadoPlan = EstadoPlanTrabajo.Activo)
    {
        // Nombre completo: «PlanTrabajo» a secas resuelve al espacio de nombres
        // Application.PlanTrabajo, no a la entidad —el mismo choque que obliga a calificarla en
        // IApplicationDbContext—.
        var plan = Domain.Entities.PlanTrabajo.Crear("DIGER", "Dirección de Gestión por Resultados", Hoy.Year);
        plan.Estado = estadoPlan;
        _ctx.PlanTrabajos.Add(plan);
        await _ctx.SaveChangesAsync();

        _ctx.MetasTrabajo.Add(new MetaTramite
        {
            PlanTrabajoId    = plan.Id,
            Orden            = 1,
            NombreTramite    = "Licencia sanitaria",
            ResponsableId    = UsuarioFijo.IdUsuario,
            FechaEstimadaFin = fin,
            Estado           = EstadoMeta.Pendiente
        });
        await _ctx.SaveChangesAsync();
    }

    private async Task SembrarTicketAsync(int horasSla, DateTime? creadoUtc = null)
    {
        var tema = TemaTicket.Crear("Correo institucional", horasSla);
        _ctx.TemasTicket.Add(tema);
        await _ctx.SaveChangesAsync();

        var t = Ticket.Crear("TCK-2026-0001", "No llegan los correos de notificación");
        t.TemaId = tema.Id;
        t.Asignar(UsuarioFijo.IdUsuario, "Ana Ortez", "sistema");
        _ctx.Tickets.Add(t);
        await _ctx.SaveChangesAsync();

        // CreatedAt lo pone SaveChanges; para fijar el vencimiento del SLA se reescribe después.
        if (creadoUtc is { } c)
        {
            t.CreatedAt = c;
            await _ctx.SaveChangesAsync();
        }
    }

    // ── Pruebas ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bandeja_reune_los_cuatro_origenes_en_una_sola_lista()
    {
        await SembrarCompromisoAsync(Hoy);
        await SembrarActividadAsync(Hoy.AddDays(1));
        await SembrarMetaAsync(Hoy.AddDays(2));
        await SembrarTicketAsync(horasSla: 48);

        var r = await _handler.Handle(new GetMiDiaQuery(), CancellationToken.None);

        r.TotalAbiertos.Should().Be(4);
        r.Pendientes.Select(p => p.Origen).Should().BeEquivalentTo(new[]
        {
            OrigenPendiente.Compromiso, OrigenPendiente.Actividad,
            OrigenPendiente.Meta,       OrigenPendiente.Ticket
        });
    }

    [Fact]
    public async Task Las_franjas_separan_lo_atrasado_de_lo_de_hoy_y_lo_de_la_semana()
    {
        await SembrarCompromisoAsync(Hoy.AddDays(-3));   // atrasado
        await SembrarActividadAsync(Hoy);                // hoy
        await SembrarMetaAsync(Hoy.AddDays(4));          // esta semana

        var r = await _handler.Handle(new GetMiDiaQuery(), CancellationToken.None);

        r.Atrasados.Should().Be(1);
        r.ParaHoy.Should().Be(1);
        r.EstaSemana.Should().Be(1);

        // Lo atrasado encabeza la lista: es el orden con el que se atiende, no el de inserción.
        r.Pendientes.First().Franja.Should().Be(FranjaDia.Atrasado);
        r.Pendientes.First().DiasParaVencer.Should().Be(-3);
    }

    [Fact]
    public async Task No_trae_el_trabajo_de_otra_persona()
    {
        await SembrarActividadAsync(Hoy, responsableId: Guid.Parse("22222222-2222-2222-2222-222222222222"));
        await SembrarCompromisoAsync(Hoy, responsable: "Carlos Fuentes");

        var r = await _handler.Handle(new GetMiDiaQuery(), CancellationToken.None);

        r.TotalAbiertos.Should().Be(0);
        r.Pendientes.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_tema_sin_SLA_deja_el_ticket_sin_fecha_en_vez_de_inventarle_una()
    {
        await SembrarTicketAsync(horasSla: 0);

        var r = await _handler.Handle(new GetMiDiaQuery(), CancellationToken.None);

        r.SinFecha.Should().Be(1);
        r.Atrasados.Should().Be(0);
        r.Pendientes.Single().Fecha.Should().BeNull();
        r.Pendientes.Single().Franja.Should().Be(FranjaDia.SinFecha);
    }

    [Fact]
    public async Task El_SLA_vencido_deja_el_ticket_atrasado()
    {
        // Creado hace cinco días con SLA de 24 h: venció hace cuatro.
        await SembrarTicketAsync(horasSla: 24, creadoUtc: DateTime.UtcNow.AddDays(-5));

        var r = await _handler.Handle(new GetMiDiaQuery(), CancellationToken.None);

        r.Atrasados.Should().Be(1);
        r.Pendientes.Single().Origen.Should().Be(OrigenPendiente.Ticket);
    }

    [Fact]
    public async Task Las_metas_de_un_plan_en_borrador_no_son_un_compromiso_todavia()
    {
        await SembrarMetaAsync(Hoy, estadoPlan: EstadoPlanTrabajo.Borrador);

        var r = await _handler.Handle(new GetMiDiaQuery(), CancellationToken.None);

        r.TotalAbiertos.Should().Be(0);
    }

    [Fact]
    public async Task Lo_que_vence_mas_alla_del_horizonte_se_cuenta_pero_no_se_lista()
    {
        await SembrarMetaAsync(Hoy.AddDays(45));

        var soloSemana = await _handler.Handle(new GetMiDiaQuery(), CancellationToken.None);
        soloSemana.Pendientes.Should().BeEmpty();
        soloSemana.MasAdelante.Should().Be(1);
        soloSemana.Ocultos.Should().Be(1);
        soloSemana.TotalAbiertos.Should().Be(1);

        var todo = await _handler.Handle(new GetMiDiaQuery(IncluirMasAdelante: true), CancellationToken.None);
        todo.Pendientes.Should().HaveCount(1);
        todo.Ocultos.Should().Be(0);
    }

    [Fact]
    public async Task La_agenda_va_aparte_y_no_engrosa_los_pendientes()
    {
        var r0 = Reunion.Crear("Comité de gobierno digital");
        r0.Fecha = Hoy;
        r0.RegistrarAsistente("Ana Ortez", "Coordinadora", "DIGER", null, _yo.Correo, null);
        _ctx.Reuniones.Add(r0);
        await _ctx.SaveChangesAsync();

        var r = await _handler.Handle(new GetMiDiaQuery(), CancellationToken.None);

        r.Agenda.Should().HaveCount(1);
        r.Agenda.Single().EsHoy.Should().BeTrue();
        r.TotalAbiertos.Should().Be(0);
    }

    public void Dispose() => _ctx.Dispose();
}
