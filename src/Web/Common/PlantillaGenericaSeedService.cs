using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Siembra la plantilla «Proyecto genérico»: el ciclo de vida de cualquier proyecto —inicio,
/// planificación, ejecución, seguimiento y cierre— para que un proyecto nuevo no arranque con la
/// estructura en blanco aunque nadie haya armado todavía una plantilla propia.
///
/// <para>No es una migración por lo mismo que <c>PermisosSeedService</c>: en producción el esquema
/// llega por los scripts de despliegue, no por <c>MigrateAsync</c>, y un dato sembrado en una
/// migración no llegaría. Acá se siembra al arrancar en cualquier ambiente.</para>
///
/// <para>Una sola vez: si existe una plantilla con ese nombre en la institución de la casa
/// —activa, desactivada o eliminada— no se toca. Así, si alguien la edita, la desactiva o la
/// borra, el arranque siguiente respeta esa decisión en vez de volver a crearla.</para>
/// </summary>
public sealed class PlantillaGenericaSeedService(
    IServiceScopeFactory scopeFactory,
    IOptions<InstitucionOptions> institucion,
    ILogger<PlantillaGenericaSeedService> logger) : IHostedService
{
    public const string Nombre = "Proyecto genérico";

    /// <summary>Entregables y sus actividades, en orden. Solo el esqueleto: fechas, responsables y
    /// avances son de cada proyecto.</summary>
    private static readonly (string Entregable, string Descripcion, string[] Actividades)[] Estructura =
    [
        ("Acta de constitución", "Inicio: qué se va a lograr, para quién y con qué alcance.",
            ["Definir objetivo y alcance", "Identificar a los interesados", "Aprobar el acta de constitución"]),
        ("Plan del proyecto", "Planificación: cómo y cuándo se va a hacer.",
            ["Desglosar entregables y actividades", "Estimar el cronograma", "Identificar los riesgos", "Aprobar el plan"]),
        ("Producto del proyecto", "Ejecución: lo que el proyecto entrega.",
            ["Desarrollar el producto o servicio", "Revisar el avance con los interesados", "Atender los cambios aprobados"]),
        ("Informes de seguimiento", "Seguimiento y control del avance y de los riesgos.",
            ["Reportar el avance periódico", "Dar seguimiento a los riesgos"]),
        ("Acta de cierre", "Cierre: entrega formal y aprendizaje.",
            ["Entregar y obtener la aceptación", "Registrar las lecciones aprendidas", "Cerrar administrativamente"])
    ];

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            if (await SembrarAsync(ctx, institucion.Value.Id, ct))
                logger.LogInformation("Plantilla «{Nombre}» sembrada para {Institucion}.", Nombre, institucion.Value.Id);
        }
        catch (Exception ex)
        {
            // Sin tumbar el arranque: sin la plantilla el portal funciona igual, solo que el alta
            // no la ofrece hasta que alguien cree una en /Proyectos/Plantillas.
            logger.LogError(ex, "No se pudo sembrar la plantilla «{Nombre}».", Nombre);
        }
    }

    /// <summary>Crea la plantilla si la casa no tiene ninguna con ese nombre. Devuelve si la creó.
    /// Pública para las pruebas, que crean el esquema después de que arrancan los servicios.</summary>
    public static async Task<bool> SembrarAsync(IApplicationDbContext ctx, string institucionId, CancellationToken ct)
    {
        var casa = institucionId.Trim().ToUpperInvariant();

        // Sin usuario no hay institución activa: el filtro global no alcanzaría a ver nada.
        if (await ctx.PlantillasProyecto.IgnoreQueryFilters()
                .AnyAsync(p => p.Nombre == Nombre && p.InstitucionId == casa, ct))
            return false;

        var plantilla = PlantillaProyecto.Crear(Nombre, casa, NivelAlcance.Institucion,
            descripcion: "Ciclo de vida de cualquier proyecto: inicio, planificación, ejecución, seguimiento y cierre. Ajústela después a lo que el proyecto necesite.");

        var ordenE = 1;
        foreach (var (entregable, descripcion, actividades) in Estructura)
        {
            var e = new PlantillaEntregable { Orden = ordenE++, Nombre = entregable, Descripcion = descripcion };
            var ordenA = 1;
            foreach (var a in actividades)
                e.Agregar(new PlantillaActividad { Orden = ordenA++, Nombre = a });
            plantilla.Agregar(e);
        }

        ctx.PlantillasProyecto.Add(plantilla);
        await ctx.SaveChangesAsync(ct);
        return true;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
