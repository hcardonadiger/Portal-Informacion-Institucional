using System.ComponentModel;
using Diger.TramitesEstado.Application.Expedientes.Queries.GetExpedienteById;
using Diger.TramitesEstado.Application.Expedientes.Queries.GetExpedientes;
using Diger.TramitesEstado.Application.Proyectos.Queries;
using Diger.TramitesEstado.Application.Reuniones.Queries.GetCompromisos;
using Diger.TramitesEstado.Application.Reuniones.Queries.GetReunionById;
using Diger.TramitesEstado.Application.Reuniones.Queries.GetReuniones;
using Diger.TramitesEstado.Application.Tickets.Queries.GetTicketById;
using Diger.TramitesEstado.Application.Tickets.Queries.GetTickets;
using MediatR;
using ModelContextProtocol.Server;

namespace Diger.TramitesEstado.Web.Mcp;

/// <summary>
/// Consulta de los módulos que se vinculan a un proyecto: reuniones, expedientes y tickets.
///
/// Existen para que el agente entienda el contexto de un proyecto antes de tocarlo —de qué
/// reunión salió un compromiso, qué expediente lo respalda— sin tener que adivinarlo.
///
/// <b>Solo lectura, y a propósito.</b> Ninguno de los comandos de estos módulos se expone:
/// el corte de escritura de este servidor es Proyectos. Cada herramienta exige la clave de
/// SU módulo, así que tener acceso a proyectos no abre por la ventana el resto del portal.
/// </summary>
[McpServerToolType]
public sealed class VinculadosTools(ISender sender, McpPermisos permisos)
{
    [McpServerTool(Name = "vinculos_proyecto")]
    [Description("Reuniones, expedientes y tickets vinculados a un proyecto. Es el punto de " +
                 "partida para entender de dónde viene el trabajo de un proyecto.")]
    public async Task<object> VinculosProyecto(
        [Description("Id numérico del proyecto.")] int proyectoId)
    {
        await permisos.ExigirAsync("Proyectos.Ver");
        return await sender.Send(new GetVinculosProyectoQuery(proyectoId));
    }

    // ── Reuniones ─────────────────────────────────────────────────────────────

    [McpServerTool(Name = "listar_reuniones")]
    [Description("Lista las reuniones visibles para el usuario del token. Las reuniones " +
                 "privadas de otras personas no aparecen.")]
    public async Task<object> ListarReuniones(
        [Description("Texto libre.")] string? busqueda = null,
        [Description("Institución.")] string? institucion = null,
        [Description("Tipo de reunión.")] string? tipo = null,
        [Description("Reuniones desde esta fecha (aaaa-mm-dd).")] DateOnly? fechaDesde = null,
        [Description("Reuniones hasta esta fecha (aaaa-mm-dd).")] DateOnly? fechaHasta = null,
        [Description("Página, desde 1.")] int? pagina = null,
        [Description("Filas por página.")] int? tamano = null)
    {
        await permisos.ExigirAsync("Reuniones.Ver");
        return await sender.Send(new GetReunionesQuery(
            Q: busqueda, Institucion: institucion, Tipo: tipo,
            FechaDesde: fechaDesde, FechaHasta: fechaHasta,
            Page: pagina, Size: tamano));
    }

    [McpServerTool(Name = "obtener_reunion")]
    [Description("Detalle de una reunión: agenda, asistentes y acuerdos.")]
    public async Task<object> ObtenerReunion(
        [Description("Id numérico de la reunión.")] int id)
    {
        await permisos.ExigirAsync("Reuniones.Ver");
        return await sender.Send(new GetReunionByIdQuery(id));
    }

    [McpServerTool(Name = "listar_compromisos")]
    [Description("Compromisos acordados en reuniones, con su responsable, fecha y estado. " +
                 "Útil para cruzar lo comprometido en acta contra el avance de un proyecto.")]
    public async Task<object> ListarCompromisos(
        [Description("Texto libre.")] string? busqueda = null,
        [Description("Estado del compromiso.")] EstadoCompromiso? estado = null,
        [Description("Institución.")] string? institucionId = null,
        [Description("Nombre del responsable.")] string? responsable = null,
        [Description("Solo los que ya pasaron su fecha.")] bool soloVencidos = false,
        [Description("Página, desde 1.")] int? pagina = null,
        [Description("Filas por página.")] int? tamano = null)
    {
        await permisos.ExigirAsync("Reuniones.Ver");
        return await sender.Send(new GetCompromisosQuery(
            Q: busqueda, Estado: estado, InstitucionId: institucionId,
            Responsable: responsable, SoloVencidos: soloVencidos,
            Page: pagina, Size: tamano));
    }

    // ── Expedientes ───────────────────────────────────────────────────────────

    [McpServerTool(Name = "listar_expedientes")]
    [Description("Lista los expedientes visibles para el usuario del token.")]
    public async Task<object> ListarExpedientes(
        [Description("Texto libre: código, institución, analista o nombre del trámite.")] string? busqueda = null,
        [Description("Página, desde 1.")] int? pagina = null,
        [Description("Filas por página.")] int? tamano = null)
    {
        await permisos.ExigirAsync("Expedientes.Ver");
        return await sender.Send(new GetExpedientesQuery(Q: busqueda, Page: pagina, Size: tamano));
    }

    [McpServerTool(Name = "obtener_expediente")]
    [Description("Detalle completo de un expediente y sus secciones.")]
    public async Task<object> ObtenerExpediente(
        [Description("Id numérico del expediente.")] int id)
    {
        await permisos.ExigirAsync("Expedientes.Ver");
        return await sender.Send(new GetExpedienteByIdQuery(id));
    }

    // ── Tickets ───────────────────────────────────────────────────────────────

    [McpServerTool(Name = "listar_tickets")]
    [Description("Lista los tickets de soporte visibles para el usuario del token.")]
    public async Task<object> ListarTickets(
        [Description("Estado del ticket.")] EstadoTicket? estado = null,
        [Description("Institución.")] string? institucionId = null,
        [Description("Id del usuario al que está asignado.")] Guid? asignadoAId = null,
        [Description("Texto libre.")] string? busqueda = null,
        [Description("Solo los que ya pasaron su fecha compromiso.")] bool soloVencidos = false,
        [Description("Página, desde 1.")] int? pagina = null,
        [Description("Filas por página.")] int? tamano = null)
    {
        await permisos.ExigirAsync("Tickets.Ver");
        return await sender.Send(new GetTicketsQuery(
            Estado: estado, InstitucionId: institucionId, AsignadoAId: asignadoAId,
            Q: busqueda, SoloVencidos: soloVencidos, Page: pagina, Size: tamano));
    }

    [McpServerTool(Name = "obtener_ticket")]
    [Description("Detalle de un ticket con sus comentarios.")]
    public async Task<object> ObtenerTicket(
        [Description("Id numérico del ticket.")] int id)
    {
        await permisos.ExigirAsync("Tickets.Ver");
        return await sender.Send(new GetTicketByIdQuery(id));
    }
}
