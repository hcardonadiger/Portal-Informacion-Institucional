using System.ComponentModel;
using Diger.TramitesEstado.Application.Reuniones.Memoria;
using Diger.TramitesEstado.Application.Reuniones.Queries.GetReunionById;
using MediatR;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Diger.TramitesEstado.Web.Mcp;

/// <summary>
/// La ayuda memoria de una reunión a partir de una transcripción, por el MCP.
///
/// <para>Las reglas —qué se le pide a la IA y qué se lee de lo que devuelve— viven en
/// <see cref="MemoriaDesdeTranscripcion"/>, la misma copia que usa el editor. Aquí no se
/// reescribe ninguna: una herramienta entrega las instrucciones que corresponden al tipo de la
/// reunión y la otra despacha <see cref="LlenarMemoriaReunionCommand"/>.</para>
///
/// <para><b>No sincroniza Outlook, a propósito.</b> La memoria se llena después de la reunión, y
/// una cita que ya terminó no se actualiza (avisaría «reunión actualizada» a cada invitado). Si
/// se llenara antes, la cita se pone al día la próxima vez que alguien guarde desde el editor.</para>
/// </summary>
[McpServerToolType]
[Description("Ayuda memoria de una reunión a partir de su transcripción, con las reglas del portal.")]
public sealed class ReunionesMemoriaTools(ISender sender, McpPermisos permisos)
{
    [McpServerTool(Name = "instrucciones_memoria_reunion")]
    [Description("Las instrucciones para redactar la ayuda memoria de una reunión: el prompt, el " +
                 "formato JSON exacto y los campos que se llenan. Dependen del tipo de la reunión " +
                 "(una capacitación pide además convocados, asistentes y compromisos) y de si la " +
                 "encuesta está activa (pide la firma del validador de la institución). Pídalas " +
                 "siempre antes de llenar_memoria_reunion.")]
    public async Task<object> InstruccionesMemoriaReunion(
        [Description("Id numérico de la reunión.")] int id)
    {
        await permisos.ExigirAsync("Reuniones.Ver");

        var datos = (await LeerAsync(id)).Datos;
        var esCapacitacion = MemoriaDesdeTranscripcion.EsCapacitacion(datos.Tipo);
        var instrucciones = MemoriaDesdeTranscripcion.Instrucciones(esCapacitacion, datos.EncuestaActiva);

        return new
        {
            ReunionId      = id,
            Titulo         = datos.Titulo,
            Tipo           = datos.Tipo,
            EsCapacitacion = esCapacitacion,
            EncuestaActiva = datos.EncuestaActiva,
            instrucciones.Prompt,
            instrucciones.Formato,
            instrucciones.Campos,
        };
    }

    [McpServerTool(Name = "llenar_memoria_reunion")]
    [Description("Escribe en la reunión la ayuda memoria redactada como JSON. Primero pida " +
                 "instrucciones_memoria_reunion y redacte el JSON siguiendo esas reglas a partir de " +
                 "la transcripción que le dio la persona. «Generales» (título, fecha, tipo, lugar…) " +
                 "nunca se escribe desde aquí. Si un campo ya tiene otro valor no se escribe nada y " +
                 "se lo dice: pregúntele a la persona; no use «sobreescribir» salvo que ella lo pida. " +
                 "Los acuerdos que ya tienen seguimiento no se reemplazan nunca.")]
    public async Task<object> LlenarMemoriaReunion(
        [Description("Id numérico de la reunión.")] int id,
        [Description("El objeto JSON con la ayuda memoria, con el formato de instrucciones_memoria_reunion.")] string json,
        [Description("Reemplazar los campos que ya tienen información. Solo si la persona lo pidió.")] bool sobreescribir = false)
    {
        await permisos.ExigirEscrituraAsync("Reuniones.Editar");

        var datos = (await LeerAsync(id)).Datos;

        try
        {
            await sender.Send(new LlenarMemoriaReunionCommand(id, json, sobreescribir));
        }
        catch (DomainException de)
        {
            // McpException es lo que el protocolo transmite: con una excepción común el SDK
            // responde «An error occurred invoking…» y la IA no sabe qué preguntarle a la persona.
            throw new McpException(de.Message);
        }
        catch (NotFoundException)
        {
            throw NoEncontrada(id);
        }

        var mensaje = MemoriaDesdeTranscripcion.Interpretar(
            json, MemoriaDesdeTranscripcion.EsCapacitacion(datos.Tipo), datos.EncuestaActiva).Mensaje;

        return new { Id = id, Mensaje = mensaje };
    }

    /// <summary>La reunión, si la cuenta la alcanza. Fuera de alcance y no existente responden lo
    /// mismo, como en <see cref="ProyectosTools"/>: no se confirma que exista lo que no se ve.</summary>
    private async Task<ReunionDetailDto> LeerAsync(int id)
    {
        try
        {
            return await sender.Send(new GetReunionByIdQuery(id));
        }
        catch (NotFoundException)
        {
            throw NoEncontrada(id);
        }
    }

    private static McpPermisoDenegadoException NoEncontrada(int id) =>
        new($"No se encontró la reunión {id}, o su cuenta no la alcanza.");
}
