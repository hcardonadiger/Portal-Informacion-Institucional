namespace Diger.TramitesEstado.Web.Models;

/// <summary>Etiquetas y colores para los estados de seguimiento de compromisos (acuerdos de reunión).</summary>
public static class CompromisoUi
{
    public static readonly EstadoCompromiso[] Estados =
    [
        EstadoCompromiso.Pendiente, EstadoCompromiso.EnProgreso, EstadoCompromiso.EnRevision,
        EstadoCompromiso.Cumplido, EstadoCompromiso.Reprogramado, EstadoCompromiso.Cancelado
    ];

    public static string Label(EstadoCompromiso e) => e switch
    {
        EstadoCompromiso.EnProgreso   => "En proceso",
        EstadoCompromiso.EnRevision   => "En revisión",
        EstadoCompromiso.Pendiente    => "Pendiente a revisar",
        EstadoCompromiso.Cumplido     => "Aceptado",
        EstadoCompromiso.Reprogramado => "Reprogramado",
        _                             => e.ToString()
    };

    /// <summary>Colores (fondo, texto) para la píldora de estado.</summary>
    public static (string bg, string fg) Color(EstadoCompromiso e) => e switch
    {
        EstadoCompromiso.Pendiente    => ("#e6f1fb", "#0c447c"),
        EstadoCompromiso.EnProgreso   => ("#faeeda", "#854f0b"),
        EstadoCompromiso.EnRevision   => ("#f3e8ff", "#6b21a8"),
        EstadoCompromiso.Cumplido     => ("#eaf3de", "#27500a"),
        EstadoCompromiso.Reprogramado => ("#ede9fe", "#5b21b6"),
        _                             => ("#f1efe8", "#5f5e5a"),
    };

    public static (string bg, string fg) Vencido => ("#fcebeb", "#a32d2d");
}
