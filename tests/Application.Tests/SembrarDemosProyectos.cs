using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Domain.Enums;
using Diger.TramitesEstado.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests;

/// <summary>
/// Sembrador de proyectos de demostración: dos por cada unidad que tenga gente activa.
///
/// <para>NO es una prueba — escribe en la base local de desarrollo, y por eso está marcado Skip.
/// Se corre a mano quitando el Skip:
/// <c>dotnet test tests\Application.Tests --filter "FullyQualifiedName~SembrarDemosProyectos"</c>.
/// Es idempotente: borra lo anterior con código <c>PRY-DEMO%</c> antes de sembrar.</para>
///
/// <para>Usa el dominio y no INSERT sueltos a propósito: el avance del entregable lo promedian sus
/// actividades y el del proyecto sus entregables, así que sembrar por SQL dejaría porcentajes que
/// la pantalla contradice al recalcular.</para>
///
/// <para>Los interesados se reparten entre unidades distintas de la dueña, que es lo que ejercita
/// la rama del filtro de alcance por la que un interesado ve un proyecto fuera de su unidad.</para>
/// </summary>
public class SembrarDemosProyectos
{
    private const string Cn =
        "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=GestionGD;Integrated Security=True;Trust Server Certificate=True";

    private static AppDbContext Contexto()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(Cn).Options;
        var usuario  = Substitute.For<ICurrentUserService>();
        usuario.EsGlobal.Returns(true);          // el sembrador no debe toparse con el alcance
        usuario.Nombre.Returns("Sembrador demo");
        return new AppDbContext(opciones, usuario, Substitute.For<IPublisher>());
    }

    private sealed record Persona(Guid Id, string Nombre, string? Correo, string Area, string Unidad);

    /// <summary>Nombre de los dos proyectos de cada unidad. Se escriben a mano porque el punto de
    /// una demo es que se lea como el trabajo real de esa unidad, no «Proyecto 1 / Proyecto 2».</summary>
    private static readonly Dictionary<string, string[]> Temas = new()
    {
        ["CONEC"]  = ["Enlace de fibra a sedes departamentales",  "Wifi público en ventanillas de atención"],
        ["DAI"]    = ["Asistente de consultas para ventanilla",   "Clasificador automático de trámites"],
        ["DITRA"]  = ["Ventanilla única de trámites municipales", "Digitalización de constancias y certificaciones"],
        ["DPD"]    = ["Cartera de transformación 2027",           "Modelo de gestión por procesos"],
        ["PRYDIG"] = ["Expediente digital interinstitucional",    "Firma electrónica en trámites de licencia"],
        ["TALDIG"] = ["Programa de formación en analítica",       "Certificación de competencias digitales"],
        ["DGR"]    = ["Tablero del despacho",                     "Agenda de seguimiento institucional"],
        ["EID"]    = ["Modelo de evaluación de programas",        "Repositorio de datos abiertos"],
        ["LEM"]    = ["Guía metodológica de proyectos",           "Manual de indicadores de resultado"],
        ["MSD"]    = ["Tablero de indicadores institucional",     "Sistema de alertas de desempeño"],
    };

    /// <summary>Tres formas de estructura, con perfiles de avance distintos, para que los tableros
    /// no muestren veinte proyectos idénticos.</summary>
    private static (string Ent, int DiasPlan, (string Act, int Ini, int Fin, int Pct)[] Acts)[] Plantilla(int i) =>
        (i % 3) switch
        {
            0 =>
            [
                ("Diagnóstico y línea base", -5, [("Relevamiento de la situación actual", -42, -22, 100),
                                                  ("Entrevistas con las áreas",           -38, -18, 100),
                                                  ("Informe de línea base",               -16,  -4, 100)]),
                ("Diseño de la solución",    18, [("Propuesta técnica",                   -12,  14,  70),
                                                  ("Validación con los involucrados",       6,  22,  40),
                                                  ("Documento de diseño",                  18,  38,   0)]),
                ("Implementación",           62, [("Construcción",                         40,  58,   0),
                                                  ("Prueba piloto",                        55,  70,   0)]),
            ],
            1 =>
            [
                ("Acuerdo interinstitucional", 8, [("Mesa técnica de arranque",  -25,  -8, 100),
                                                   ("Convenio firmado",           -6,  10,  55)]),
                ("Puesta en operación",       50, [("Configuración del servicio",   5,  30,  25),
                                                   ("Capacitación a usuarios",     28,  45,   0),
                                                   ("Cierre y traspaso",           46,  58,   0)]),
            ],
            _ =>
            [
                ("Preparación", -2, [("Definición de alcance",    -30, -12, 100),
                                     ("Plan de trabajo aprobado", -14,  -2, 100)]),
                ("Ejecución",   30, [("Desarrollo de contenidos",  -8,  26,  60),
                                     ("Revisión de calidad",       20,  36,  15)]),
                ("Cierre",      55, [("Informe final",             45,  58,   0)]),
            ],
        };

    [Fact(Skip = "Sembrador de datos: quitar el Skip y correr a mano contra la base local")]
    public async Task Sembrar()
    {
        await using var ctx = Contexto();
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var gente = await ctx.Usuarios.AsNoTracking()
            .Where(u => !u.IsDeleted)
            .Join(ctx.AsignacionesUsuario.AsNoTracking(), u => u.Id, a => a.UsuarioId,
                  (u, a) => new Persona(u.Id, u.Nombre, u.Correo, a.AreaId ?? "", a.UnidadId ?? ""))
            .ToListAsync();

        var porUnidad = gente.Where(p => p.Unidad != "")
                             .GroupBy(p => p.Unidad)
                             .Where(g => Temas.ContainsKey(g.Key))
                             .OrderBy(g => g.Key)
                             .ToList();

        // La PMO entra de patrocinadora en la mitad de los proyectos DE SU PROPIA ÁREA: es el
        // caso de alguien que cruza unidades sin pertenecer a ninguna, pero sin salirse del área.
        var pmo = gente.FirstOrDefault(p => p.Nombre.StartsWith("Vanessa Paola"));

        await LimpiarAsync(ctx);

        var i = 0;
        foreach (var unidad in porUnidad)
        {
            var suGente = unidad.OrderBy(p => p.Nombre).ToList();
            var ajenos  = gente.Where(p => p.Unidad != "" && p.Unidad != unidad.Key)
                               .OrderBy(p => p.Nombre).ToList();

            for (var n = 0; n < 2; n++, i++)
            {
                var dueño = suGente[n % suGente.Count];

                // Dos interesados de otras unidades, tomados de forma escalonada para que no sean
                // siempre los mismos dos nombres en los veinte proyectos.
                var invitados = new List<(Persona, RolInteresado, NivelCualitativo)>
                {
                    (ajenos[(i * 3)     % ajenos.Count], RolInteresado.ContraparteTecnica, NivelCualitativo.Alta),
                    (ajenos[(i * 3 + 7) % ajenos.Count], RolInteresado.Beneficiario,       NivelCualitativo.Media),
                };
                // La PMO patrocina solo dentro de su propia área: está asignada a Gobierno Digital
                // y no debe aparecer en proyectos de otra. Sin este filtro terminaba viendo cuatro
                // proyectos de SIGER por la vía de interesado, más que la propia jefatura de su área.
                if (pmo is not null && i % 2 == 0 && dueño.Area == pmo.Area)
                    invitados.Add((pmo, RolInteresado.Patrocinador, NivelCualitativo.Alta));

                await SembrarUnoAsync(ctx, hoy,
                    codigo:   $"PRY-DEMO-{unidad.Key}-{n + 1:00}",
                    nombre:   Temas[unidad.Key][n],
                    objetivo: $"Proyecto de demostración de la unidad {unidad.Key}, para revisar la ficha y las vistas de interesados.",
                    dueño:    dueño,
                    interesados: invitados,
                    estructura:  Plantilla(i),
                    prioridad:   (PrioridadProyecto)((i % 3) + 1),
                    accion:      (AccionProyecto)((i % 4) + 1));
            }
        }
    }

    private static async Task LimpiarAsync(AppDbContext ctx)
    {
        var viejos = await ctx.Proyectos.IgnoreQueryFilters()
            .Where(p => p.Codigo.StartsWith("PRY-DEMO")).Select(p => p.Id).ToListAsync();
        if (viejos.Count == 0) return;

        // ExecuteDelete y no RemoveRange: Proyecto es ISoftDeletable, así que quitarlo por el
        // change tracker solo lo marca IsDeleted y cada corrida iría dejando su propia capa de
        // basura. Los hijos caen por FK en cascada.
        await ctx.ProyectoInteresados.Where(x => viejos.Contains(x.ProyectoId)).ExecuteDeleteAsync();
        await ctx.ProyectoAvances.Where(x => viejos.Contains(x.ProyectoId)).ExecuteDeleteAsync();
        await ctx.Proyectos.IgnoreQueryFilters().Where(p => viejos.Contains(p.Id)).ExecuteDeleteAsync();
    }

    private static async Task SembrarUnoAsync(
        AppDbContext ctx, DateOnly hoy,
        string codigo, string nombre, string objetivo,
        Persona dueño,
        List<(Persona P, RolInteresado Rol, NivelCualitativo Inf)> interesados,
        (string Ent, int DiasPlan, (string Act, int Ini, int Fin, int Pct)[] Acts)[] estructura,
        PrioridadProyecto prioridad,
        AccionProyecto accion)
    {
        var p = Proyecto.Crear(codigo, nombre, objetivo);
        p.InstitucionId   = "DIGER";
        p.AreaId          = dueño.Area;
        p.UnidadId        = dueño.Unidad;
        p.ResponsableId   = dueño.Id;
        p.Responsable     = dueño.Nombre;
        p.Prioridad       = prioridad;
        p.Accion          = accion;
        p.FechaInicioPlan = hoy.AddDays(-45);
        p.FechaFinPlan    = hoy.AddDays(72);
        p.CambiarEstado(EstadoProyecto.EnEjecucion, dueño.Nombre);

        var orden = 1;
        foreach (var (nomEnt, diasPlan, acts) in estructura)
        {
            var e = EntregableProyecto.Crear(nomEnt, orden++);
            e.Definir(nomEnt, null, hoy.AddDays(diasPlan), dueño.Id, dueño.Nombre);

            var o = 1;
            foreach (var (nomAct, ini, fin, pct) in acts)
            {
                var a = ActividadProyecto.Crear(nomAct, o++);
                a.Definir(nomAct, null, hoy.AddDays(ini), hoy.AddDays(fin), dueño.Id, dueño.Nombre);
                if (pct > 0) a.Reportar(pct, hoy);
                e.Agregar(a);
            }

            // El porcentaje del entregable es calculado, pero su estado sí se guarda: se deja
            // coherente con lo reportado para que la ficha no muestre «Pendiente» sobre un 100 %.
            if (e.ActividadesTerminadas())                    e.Completar(hoy);
            else if (e.Actividades.Any(a => a.AvancePct > 0)) e.CambiarEstado(EstadoEntregable.EnProceso);

            p.Agregar(e);
        }
        p.RecalcularAvance(p.Entregables);

        ctx.Proyectos.Add(p);
        await ctx.SaveChangesAsync();

        // Un usuario puede tener varias asignaciones —hay quien está en tres unidades—, así que
        // la misma persona podía caer dos veces en la lista y chocar contra el índice único
        // (ProyectoId, UsuarioId). Gana la primera aparición, y el dueño va primero.
        var unicos = interesados
            .Prepend((dueño, RolInteresado.Ejecutor, NivelCualitativo.Alta))
            .GroupBy(x => x.Item1.Id)
            .Select(g => g.First());

        foreach (var (per, rol, inf) in unicos)
            ctx.ProyectoInteresados.Add(InteresadoProyecto.Crear(
                p.Id, per.Id, per.Nombre, rol, dueño.Nombre, inf, per.Correo, "DIGER"));

        var n = 0;
        foreach (var e in p.Entregables)
            foreach (var a in e.Actividades.Where(x => x.AvancePct > 0))
                ctx.ProyectoAvances.Add(AvanceProyecto.Crear(
                    p.Id, e.Id, a.Id,
                    $"Se reporta {a.AvancePct}% en «{a.Nombre}».",
                    a.AvancePct, dueño.Nombre,
                    bloqueo: (++n == 2) ? "A la espera de la designación del enlace institucional." : null));

        ctx.ProyectoAvances.Add(AvanceProyecto.Crear(
            p.Id, null, null, "Reunión de arranque con las unidades participantes.", null, dueño.Nombre));
        ctx.ProyectoAvances.Add(AvanceProyecto.Crear(
            p.Id, null, null, "Se acuerda revisión quincenal de avance.", null, dueño.Nombre));

        await ctx.SaveChangesAsync();
    }
}
