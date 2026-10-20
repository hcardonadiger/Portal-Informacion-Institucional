using Diger.TramitesEstado.Application.Proyectos.Common;

// Hay dos EntregableInput: el de Proyectos y el de Expedientes, y GlobalUsings.cs trae el
// segundo a todo el Web. Acá se habla de la ficha de un proyecto, así que se nombra el de
// Proyectos a mano en vez de dejar que el compilador elija —no puede— o que gane el global.
using EntregableInput = Diger.TramitesEstado.Application.Proyectos.Common.EntregableInput;

namespace Diger.TramitesEstado.Web.Mcp;

/// <summary>
/// Traduce la estructura que devuelve <c>GetProyectoQuery</c> al formato que espera
/// <c>ActualizarProyectoCommand</c>, sin cambiar nada.
///
/// <para><b>Por qué existe y por qué está probado aparte.</b> Ese comando no parchea la
/// ficha: la reconcilia contra la lista de entregables que recibe. Lo que no venga en la
/// lista se borra —las actividades ausentes se eliminan, sus avances quedan sin imputar y
/// sus dependencias se descartan— y una fila sin Id no se actualiza: se borra y se recrea
/// con identidad nueva, lo que deja en NULL el <c>EntregableId</c> de cada avance que la
/// apuntaba.</para>
///
/// <para>Cuando quien arma el llamado es un modelo de lenguaje, que omita una actividad o
/// que le invente un Id deja de ser hipotético. Por eso la herramienta MCP no le pide la
/// estructura a nadie: la relee y la reenvía por acá, y esta función es el único lugar
/// donde eso puede salir mal.</para>
/// </summary>
public static class EstructuraProyectoMcp
{
    public static IReadOnlyList<EntregableInput> Conservar(ProyectoDetailDto proyecto) =>
        proyecto.Entregables
            .Select(e => new EntregableInput(
                e.Id,
                e.Nombre,
                e.Descripcion,
                e.FechaPlan,
                e.Estado,
                e.ResponsableId,
                e.Responsable,
                e.Actividades
                    .Select(a => new ActividadInput(
                        a.Id,
                        a.Nombre,
                        a.Descripcion,
                        a.FechaInicioPlan,
                        a.FechaFinPlan,
                        a.AvancePct,
                        a.Estado == EstadoActividad.Cancelada,
                        a.ResponsableId,
                        a.Responsable,
                        a.Predecesoras.Select(p => p.Id).ToList()))
                    .ToList()))
            .ToList();
}
