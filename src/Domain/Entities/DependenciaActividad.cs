namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Dependencia Fin → Comienzo entre dos actividades del mismo proyecto: la
/// <see cref="SucesoraId">sucesora</see> no debería arrancar hasta que la
/// <see cref="PredecesoraId">predecesora</see> esté completada.
///
/// <para><b>Avisa, no bloquea.</b> El portal marca la actividad como bloqueada y la lleva al
/// tablero, pero no impide reportar avance sobre ella. Es deliberado: el portafolio se cargó desde
/// actas y arrastra estructura incompleta, así que una regla dura convertiría el dato faltante de
/// otro en un error del que reporta — y el efecto sería que se deje de reportar, que es
/// exactamente el problema que el módulo intenta resolver.</para>
///
/// <para><b>La posee la sucesora</b> y viaja con ella: quitar la actividad se lleva sus
/// dependencias en cascada. La FK del lado de la predecesora va en <c>NoAction</c> —sería la
/// segunda ruta de borrado desde <c>Proyectos</c> hacia esta misma tabla y SQL Server rechaza el
/// modelo con el error 1785, igual que pasó con <see cref="AvanceProyecto"/>—, así que las filas
/// que apuntan a una actividad borrada las limpia a mano la reconciliación del editor.</para>
/// </summary>
public sealed class DependenciaActividad : BaseEntity
{
    /// <summary>La actividad que espera. La fija EF al agregarla a la colección de la sucesora.</summary>
    public int SucesoraId { get; set; }

    /// <summary>La actividad que tiene que terminar primero.</summary>
    public int PredecesoraId { get; private set; }

    /// <summary>Hoy siempre <see cref="TipoDependencia.FinComienzo"/>. Ver la nota del enum.</summary>
    public TipoDependencia Tipo { get; private set; } = TipoDependencia.FinComienzo;

    private DependenciaActividad() { }   // EF

    public static DependenciaActividad Crear(int predecesoraId)
    {
        if (predecesoraId <= 0)
            throw new DomainException("Solo se puede depender de una actividad ya guardada.");

        return new DependenciaActividad { PredecesoraId = predecesoraId };
    }
}

/// <summary>
/// El grafo de dependencias de un proyecto, visto entero.
///
/// <para>Existe porque la regla no cabe en la actividad: una actividad conoce a sus predecesoras
/// por Id, pero no puede decir si el conjunto tiene un ciclo ni si la predecesora pertenece al
/// proyecto. Eso solo se sabe con el árbol cargado, y quien lo tiene cargado es el
/// <see cref="Proyecto"/>.</para>
/// </summary>
public static class DependenciasProyecto
{
    /// <summary>Todas las actividades del proyecto, sin importar de qué entregable cuelgan: una
    /// dependencia puede cruzar entregables dentro del mismo proyecto.</summary>
    public static IReadOnlyList<ActividadProyecto> Aplanar(Proyecto proyecto) =>
        proyecto.Entregables.SelectMany(e => e.Actividades).ToList();

    /// <summary>
    /// Valida el grafo completo después de reconciliar la estructura. Dos cosas: que ninguna
    /// dependencia apunte fuera del proyecto y que no haya ciclos.
    ///
    /// <para>Necesita el árbol cargado (<c>ProyectoConEstructura</c>). Con las colecciones vacías
    /// no encontraría nada y daría por bueno cualquier grafo.</para>
    /// </summary>
    public static void Validar(Proyecto proyecto)
    {
        var actividades = Aplanar(proyecto);
        var porId = actividades.Where(a => a.Id > 0).ToDictionary(a => a.Id);

        foreach (var actividad in actividades)
        foreach (var id in actividad.PredecesoraIds)
        {
            if (porId.ContainsKey(id)) continue;

            throw new DomainException(
                $"«{actividad.Nombre}» depende de una actividad que ya no está en la estructura de " +
                "este proyecto. Recargue la página y vuelva a intentarlo.");
        }

        DetectarCiclo(porId);
    }

    /// <summary>
    /// DFS con tres colores sobre las actividades guardadas.
    ///
    /// <para>Las actividades nuevas quedan fuera a propósito y no se pierde nada: para cerrar un
    /// ciclo hay que ser predecesora de alguien, y solo se puede depender de una actividad que ya
    /// tiene Id. Una fila recién agregada en el editor no puede ser parte de un círculo.</para>
    /// </summary>
    private static void DetectarCiclo(Dictionary<int, ActividadProyecto> porId)
    {
        const int EnLaPila = 1, Terminada = 2;

        var estado = new Dictionary<int, int>();
        var camino = new List<ActividadProyecto>();

        bool Visitar(ActividadProyecto actividad)
        {
            estado[actividad.Id] = EnLaPila;
            camino.Add(actividad);

            foreach (var id in actividad.PredecesoraIds)
            {
                if (!porId.TryGetValue(id, out var predecesora)) continue;

                var color = estado.GetValueOrDefault(predecesora.Id);
                if (color == EnLaPila)
                {
                    camino.Add(predecesora);   // cierra el círculo: queda repetida al final
                    return true;
                }
                if (color != Terminada && Visitar(predecesora)) return true;
            }

            camino.RemoveAt(camino.Count - 1);
            estado[actividad.Id] = Terminada;
            return false;
        }

        foreach (var actividad in porId.Values)
        {
            if (estado.GetValueOrDefault(actividad.Id) != 0) continue;
            if (!Visitar(actividad)) continue;

            // El camino trae la cola que llega al ciclo; el ciclo empieza donde reaparece el nodo
            // que lo cerró. Se recorta para que el mensaje nombre solo a los implicados.
            var cierre = camino[^1];
            var desde  = camino.FindIndex(a => a.Id == cierre.Id);
            var ciclo  = camino.Skip(desde).Select(a => $"«{a.Nombre}»");

            throw new DomainException(
                "Las dependencias forman un círculo y ninguna de esas actividades podría arrancar: " +
                string.Join(" espera a ", ciclo) + ". Quite una de esas dependencias.");
        }
    }
}
