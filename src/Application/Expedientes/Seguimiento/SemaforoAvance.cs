namespace Diger.TramitesEstado.Application.Expedientes.Seguimiento;

public enum BandaAvance
{
    Rezagado,   // rojo
    EnProceso,  // naranja
    Avanzado,   // verde
}

/// <summary>
/// Semáforo de avance de digitalización. Fuente única de los cortes y de los
/// colores: lo consumen el tablero de trámites, el informe en pantalla y el
/// Excel. Antes cada superficie tenía sus propios umbrales y el mismo trámite
/// podía caer en bandas distintas según dónde se mirara.
/// </summary>
/// <remarks>
/// No aplica al Plan de Trabajo: ahí el porcentaje es cumplimiento de metas,
/// otra métrica con su propia escala.
/// </remarks>
public static class SemaforoAvance
{
    /// <summary>Desde este porcentaje (inclusive) el avance es verde. Configurable vía
    /// appsettings ("Expedientes:Semaforo:UmbralAvanzado"), asignado en Program.cs al arrancar.</summary>
    public static int UmbralAvanzado { get; set; } = 70;

    /// <summary>Desde este porcentaje (inclusive) el avance es naranja. Configurable vía
    /// appsettings ("Expedientes:Semaforo:UmbralEnProceso"), asignado en Program.cs al arrancar.</summary>
    public static int UmbralEnProceso { get; set; } = 20;

    public static BandaAvance Banda(int avancePct) =>
        avancePct >= UmbralAvanzado  ? BandaAvance.Avanzado  :
        avancePct >= UmbralEnProceso ? BandaAvance.EnProceso :
                                       BandaAvance.Rezagado;

    public static string Etiqueta(BandaAvance b) => b switch
    {
        BandaAvance.Avanzado  => $"Avanzado ({UmbralAvanzado}–100%)",
        BandaAvance.EnProceso => $"En proceso ({UmbralEnProceso}–{UmbralAvanzado - 1}%)",
        _                     => $"Rezagado (0–{UmbralEnProceso - 1}%)",
    };

    /// <summary>Nombre corto, sin el rango — para chips y columnas angostas.</summary>
    public static string EtiquetaCorta(BandaAvance b) => b switch
    {
        BandaAvance.Avanzado  => "Avanzado",
        BandaAvance.EnProceso => "En proceso",
        _                     => "Rezagado",
    };

    /// <summary>Fondo tenue y texto oscuro de la misma familia (pares ya usados en el portal).</summary>
    public static (string Fondo, string Texto, string Solido) Colores(BandaAvance b) => b switch
    {
        BandaAvance.Avanzado  => ("#EAF3DE", "#27500A", "#639922"),
        BandaAvance.EnProceso => ("#FAEEDA", "#854F0B", "#EF9F27"),
        _                     => ("#FCEBEB", "#A32D2D", "#E24B4A"),
    };
}
