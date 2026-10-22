using System.ComponentModel;
using Diger.TramitesEstado.Application.Proyectos.Commands;
using Diger.TramitesEstado.Application.Proyectos.Common;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using MediatR;
using ModelContextProtocol.Server;

namespace Diger.TramitesEstado.Web.Mcp;

/// <summary>
/// Las herramientas de Proyectos que el portal le ofrece a un agente.
///
/// Cada una hace dos cosas: exigir su clave de permiso y despachar al mismo handler de
/// MediatR que usa la página equivalente. No tocan repositorios ni el DbContext, así que
/// las reglas de negocio, las validaciones y la bitácora son exactamente las del portal.
///
/// El alcance institucional no aparece por ningún lado a propósito: lo aplican los query
/// filters del <c>AppDbContext</c>, porque la petición llega con los mismos claims que
/// tendría esa persona navegando el sitio.
/// </summary>
[McpServerToolType]
public sealed class ProyectosTools(ISender sender, McpPermisos permisos)
{
    // ── Lectura ───────────────────────────────────────────────────────────────

    [McpServerTool(Name = "listar_proyectos")]
    [Description("Lista los proyectos visibles para el usuario del token, con filtros opcionales. " +
                 "Devuelve código, nombre, estado, responsable, avance y fechas. " +
                 "Úselo para encontrar el Id de un proyecto antes de consultarlo o actualizarlo.")]
    public async Task<object> ListarProyectos(
        [Description("Estado: Planificado, EnEjecucion, EnPausa, Cerrado o Cancelado.")] EstadoProyecto? estado = null,
        [Description("Texto libre: busca en el nombre y el código.")] string? busqueda = null,
        [Description("Año de inicio planificado.")] int? anio = null,
        [Description("Id del área que acota el proyecto.")] string? areaId = null,
        [Description("Id de la unidad que acota el proyecto.")] string? unidadId = null)
    {
        await permisos.ExigirAsync("Proyectos.Ver");
        return await sender.Send(new GetProyectosQuery(
            Estado: estado, Q: busqueda, Anio: anio, AreaId: areaId, UnidadId: unidadId));
    }

    [McpServerTool(Name = "obtener_proyecto")]
    [Description("Ficha completa de un proyecto: datos generales, entregables, actividades, " +
                 "avance y estados a los que puede pasar.")]
    public async Task<object?> ObtenerProyecto(
        [Description("Id numérico del proyecto.")] int id)
    {
        await permisos.ExigirAsync("Proyectos.Ver");
        return await sender.Send(new GetProyectoQuery(id));
    }

    [McpServerTool(Name = "avances_proyecto")]
    [Description("Bitácora de avances reportados en un proyecto: qué se hizo, cuándo, quién y " +
                 "contra qué actividad se imputó.")]
    public async Task<object> AvancesProyecto(
        [Description("Id numérico del proyecto.")] int proyectoId)
    {
        await permisos.ExigirAsync("Proyectos.Ver");
        return await sender.Send(new GetAvancesProyectoQuery(proyectoId));
    }

    [McpServerTool(Name = "riesgos_proyecto")]
    [Description("Riesgos registrados en un proyecto, con su probabilidad, impacto y estado.")]
    public async Task<object> RiesgosProyecto(
        [Description("Id numérico del proyecto.")] int proyectoId)
    {
        await permisos.ExigirAsync("Proyectos.Ver");
        return await sender.Send(new GetRiesgosProyectoQuery(proyectoId));
    }

    [McpServerTool(Name = "interesados_proyecto")]
    [Description("Personas interesadas en un proyecto y su papel en él.")]
    public async Task<object> InteresadosProyecto(
        [Description("Id numérico del proyecto.")] int proyectoId)
    {
        await permisos.ExigirAsync("Proyectos.Ver");
        return await sender.Send(new GetInteresadosProyectoQuery(proyectoId));
    }

    [McpServerTool(Name = "bitacora_proyecto")]
    [Description("Historial de cambios del proyecto: quién modificó qué y cuándo.")]
    public async Task<object> BitacoraProyecto(
        [Description("Id numérico del proyecto.")] int proyectoId)
    {
        await permisos.ExigirAsync("Proyectos.Ver");
        return await sender.Send(new GetBitacoraProyectoQuery(proyectoId));
    }

    [McpServerTool(Name = "mis_actividades")]
    [Description("Las actividades asignadas al usuario del token, de todos sus proyectos.")]
    public async Task<object> MisActividades(
        [Description("Incluir también las ya terminadas.")] bool incluirTerminadas = false)
    {
        await permisos.ExigirAsync("Proyectos.Ver");
        return await sender.Send(new GetMisActividadesQuery(incluirTerminadas));
    }

    [McpServerTool(Name = "opciones_alcance")]
    [Description("Áreas y unidades disponibles para acotar un proyecto. Consúltelo antes de crear " +
                 "o actualizar uno, para usar identificadores válidos.")]
    public async Task<object> OpcionesAlcance()
    {
        await permisos.ExigirAsync("Proyectos.Ver");
        return await sender.Send(new GetAlcanceOpcionesQuery());
    }

    // ── Escritura ─────────────────────────────────────────────────────────────

    [McpServerTool(Name = "crear_proyecto")]
    [Description("Crea un proyecto con sus datos generales. No crea entregables ni actividades: " +
                 "esos se cargan desde el portal.")]
    public async Task<object> CrearProyecto(
        [Description("Nombre del proyecto.")] string nombre,
        [Description("Objetivo o descripción.")] string? objetivo = null,
        [Description("Id del área que lo acota.")] string? areaId = null,
        [Description("Id de la unidad que lo acota.")] string? unidadId = null,
        [Description("Id del usuario responsable. Si se omite, el responsable es quien crea el proyecto.")] Guid? responsableId = null,
        [Description("Id de la prioridad del catálogo.")] int? prioridadId = null,
        [Description("Id de la categoría del catálogo.")] int? categoriaId = null,
        [Description("Fecha de inicio planificada (aaaa-mm-dd).")] DateOnly? fechaInicioPlan = null,
        [Description("Fecha de cierre planificada (aaaa-mm-dd).")] DateOnly? fechaFinPlan = null)
    {
        await permisos.ExigirEscrituraAsync("Proyectos.Crear");

        var id = await sender.Send(new CrearProyectoCommand(
            Nombre:          nombre,
            Objetivo:        objetivo,
            AreaId:          areaId,
            UnidadId:        unidadId,
            ResponsableId:   responsableId,
            PrioridadId:     prioridadId,
            CategoriaId:     categoriaId,
            FechaInicioPlan: fechaInicioPlan,
            FechaFinPlan:    fechaFinPlan));

        return new { Id = id, Mensaje = "Proyecto creado." };
    }

    /// <summary>
    /// Actualiza SOLO los campos de ficha. La estructura del proyecto se relee y se reenvía
    /// tal como está.
    ///
    /// <para><b>Esto no es una precaución de más.</b> <c>ActualizarProyectoCommand</c> no
    /// parchea: reconcilia. Los entregables y actividades que no vengan en la lista se
    /// borran, sus avances quedan sin imputar y sus dependencias se eliminan. Si esta
    /// herramienta dejara que el modelo armara esa lista, bastaría con que omitiera una
    /// actividad —o que la inventara sin Id— para destrozar el cronograma y la bitácora de
    /// un proyecto. Por eso la estructura ni siquiera aparece entre los parámetros: para
    /// cambiarla está el portal, donde una persona ve lo que va a borrar.</para>
    /// </summary>
    [McpServerTool(Name = "actualizar_ficha_proyecto")]
    [Description("Cambia los datos generales de un proyecto (nombre, objetivo, responsable, " +
                 "prioridad, categoría, alcance y fechas planificadas). NO toca entregables ni " +
                 "actividades: esos se conservan intactos. Los parámetros que se omitan mantienen " +
                 "su valor actual.")]
    public async Task<object> ActualizarFichaProyecto(
        [Description("Id numérico del proyecto.")] int id,
        [Description("Nuevo nombre.")] string? nombre = null,
        [Description("Nuevo objetivo.")] string? objetivo = null,
        [Description("Nuevo Id de área.")] string? areaId = null,
        [Description("Nuevo Id de unidad.")] string? unidadId = null,
        [Description("Nuevo Id de usuario responsable.")] Guid? responsableId = null,
        [Description("Nuevo Id de prioridad.")] int? prioridadId = null,
        [Description("Nuevo Id de categoría.")] int? categoriaId = null,
        [Description("Nueva fecha de inicio planificada (aaaa-mm-dd).")] DateOnly? fechaInicioPlan = null,
        [Description("Nueva fecha de cierre planificada (aaaa-mm-dd).")] DateOnly? fechaFinPlan = null)
    {
        await permisos.ExigirEscrituraAsync("Proyectos.Editar");

        var actual = await sender.Send(new GetProyectoQuery(id))
            ?? throw new McpPermisoDenegadoException($"No se encontró el proyecto {id}, o su cuenta no lo alcanza.");

        // La estructura vigente, reenviada con sus Ids. Sin los Ids el comando borraría y
        // recrearía cada fila, y eso dejaría en NULL la imputación de todos los avances.
        var entregables = EstructuraProyectoMcp.Conservar(actual);

        await sender.Send(new ActualizarProyectoCommand(
            Id:              id,
            Nombre:          nombre          ?? actual.Nombre,
            Objetivo:        objetivo        ?? actual.Objetivo,
            AreaId:          areaId          ?? actual.AreaId,
            UnidadId:        unidadId        ?? actual.UnidadId,
            ResponsableId:   responsableId   ?? actual.ResponsableId,
            Responsable:     actual.Responsable,
            PrioridadId:     prioridadId     ?? actual.PrioridadId,
            CategoriaId:     categoriaId     ?? actual.CategoriaId,
            Accion:          actual.Accion,
            FechaInicioPlan: fechaInicioPlan ?? actual.FechaInicioPlan,
            FechaFinPlan:    fechaFinPlan    ?? actual.FechaFinPlan,
            Entregables:     entregables));

        return new { Id = id, Mensaje = "Ficha actualizada. La estructura del proyecto quedó intacta." };
    }

    [McpServerTool(Name = "cambiar_estado_proyecto")]
    [Description("Mueve el proyecto a otro estado. Consulte antes la ficha: trae la lista de " +
                 "estados a los que puede pasar desde el actual.")]
    public async Task<object> CambiarEstadoProyecto(
        [Description("Id numérico del proyecto.")] int id,
        [Description("Estado destino: Planificado, EnEjecucion, EnPausa, Cerrado o Cancelado.")] EstadoProyecto nuevo)
    {
        await permisos.ExigirEscrituraAsync("Proyectos.Editar");
        await sender.Send(new CambiarEstadoProyectoCommand(id, nuevo));
        return new { Id = id, Estado = nuevo.ToString(), Mensaje = "Estado actualizado." };
    }

    [McpServerTool(Name = "registrar_avance")]
    [Description("Reporta un avance en un proyecto. Si se imputa a una actividad y se da un " +
                 "porcentaje, el número sube por el árbol hasta el proyecto. Sin actividad es " +
                 "solo una nota de ejecución y no mueve indicadores.")]
    public async Task<object> RegistrarAvance(
        [Description("Id numérico del proyecto.")] int proyectoId,
        [Description("Qué se hizo.")] string descripcion,
        [Description("Id del entregable al que se imputa.")] int? entregableId = null,
        [Description("Id de la actividad a la que se imputa.")] int? actividadId = null,
        [Description("Nuevo porcentaje de la actividad (0 a 100).")] int? porcentajeActividad = null,
        [Description("Descripción del bloqueo, si lo hay.")] string? bloqueo = null,
        [Description("Id del riesgo que este bloqueo confirma.")] int? riesgoId = null)
    {
        await permisos.ExigirEscrituraAsync("Proyectos.Avance.Crear");

        var id = await sender.Send(new RegistrarAvanceCommand(
            ProyectoId:          proyectoId,
            Descripcion:         descripcion,
            EntregableId:        entregableId,
            ActividadId:         actividadId,
            PorcentajeActividad: porcentajeActividad,
            Bloqueo:             bloqueo,
            RiesgoId:            riesgoId));

        return new { Id = id, Mensaje = "Avance registrado." };
    }

    [McpServerTool(Name = "registrar_riesgo")]
    [Description("Registra un riesgo en un proyecto. Un proyecto cerrado o cancelado ya no admite riesgos nuevos.")]
    public async Task<object> RegistrarRiesgo(
        [Description("Id numérico del proyecto.")] int proyectoId,
        [Description("Descripción del riesgo.")] string descripcion,
        [Description("Categoría del riesgo.")] CategoriaRiesgo categoria,
        [Description("Probabilidad de que ocurra.")] NivelCualitativo probabilidad,
        [Description("Impacto si ocurre.")] NivelCualitativo impacto,
        [Description("Estrategia de respuesta prevista.")] EstrategiaRiesgo estrategia,
        [Description("Medida de mitigación.")] string? mitigacion = null,
        [Description("Id del usuario responsable del riesgo.")] Guid? responsableId = null,
        [Description("Fecha de la próxima revisión (aaaa-mm-dd).")] DateOnly? fechaRevision = null)
    {
        await permisos.ExigirEscrituraAsync("Proyectos.Editar");

        var id = await sender.Send(new RegistrarRiesgoCommand(
            ProyectoId:    proyectoId,
            Descripcion:   descripcion,
            Categoria:     categoria,
            Probabilidad:  probabilidad,
            Impacto:       impacto,
            Estrategia:    estrategia,
            Mitigacion:    mitigacion,
            ResponsableId: responsableId,
            FechaRevision: fechaRevision));

        return new { Id = id, Mensaje = "Riesgo registrado." };
    }

    [McpServerTool(Name = "cambiar_estado_riesgo")]
    [Description("Cambia el estado de un riesgo del proyecto.")]
    public async Task<object> CambiarEstadoRiesgo(
        [Description("Id numérico del riesgo.")] int riesgoId,
        [Description("Estado destino: Identificado, Materializado, Mitigado o Cerrado.")] EstadoRiesgo nuevo)
    {
        await permisos.ExigirEscrituraAsync("Proyectos.Editar");
        await sender.Send(new CambiarEstadoRiesgoCommand(riesgoId, nuevo));
        return new { Id = riesgoId, Estado = nuevo.ToString(), Mensaje = "Estado del riesgo actualizado." };
    }
}
