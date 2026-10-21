using Diger.TramitesEstado.Application.MiDia.Common;

namespace Diger.TramitesEstado.Web.Models;

/// <summary>
/// Etiquetas y colores de la bandeja «Mi día». Sigue el mismo criterio que
/// <see cref="CompromisoUi"/>: el vocabulario visible vive en el Web, no en los DTO.
/// </summary>
public static class MiDiaUi
{
    public static string Label(OrigenPendiente o) => o switch
    {
        OrigenPendiente.Compromiso => "Compromiso",
        OrigenPendiente.Actividad  => "Actividad",
        OrigenPendiente.Meta       => "Meta",
        _                          => "Ticket"
    };

    /// <summary>Colores (fondo, texto) de la píldora de origen. Reutiliza la paleta de estados de
    /// compromiso para que el portal no estrene tonos nuevos en cada pantalla.</summary>
    public static (string bg, string fg) Color(OrigenPendiente o) => o switch
    {
        OrigenPendiente.Compromiso => ("#e6f1fb", "#0c447c"),
        OrigenPendiente.Actividad  => ("#eaf3de", "#27500a"),
        OrigenPendiente.Meta       => ("#ede9fe", "#5b21b6"),
        _                          => ("#faeeda", "#854f0b")
    };

    /// <summary>Qué se va a hacer al abrir la fila. El verbo cambia por origen a propósito: un
    /// compromiso se revisa, una actividad se reporta dentro de su proyecto, un ticket se atiende.</summary>
    public static string Accion(OrigenPendiente o) => o switch
    {
        OrigenPendiente.Compromiso => "Ver compromiso",
        OrigenPendiente.Actividad  => "Abrir proyecto",
        OrigenPendiente.Meta       => "Abrir plan",
        _                          => "Atender ticket"
    };

    public static string Titulo(FranjaDia f) => f switch
    {
        FranjaDia.Atrasado    => "Atrasado",
        FranjaDia.Hoy         => "Hoy",
        FranjaDia.Manana      => "Mañana",
        FranjaDia.EstaSemana  => "Esta semana",
        FranjaDia.MasAdelante => "Más adelante",
        _                     => "Sin fecha"
    };

    /// <summary>El texto de la píldora de fecha: «Venció hace 3 d», «Vence hoy», «En 4 d».</summary>
    public static string Vencimiento(PendienteDto p) => p.DiasParaVencer switch
    {
        null            => "Sin fecha",
        0               => "Vence hoy",
        1               => "Vence mañana",
        < 0 and var d   => $"Venció hace {-d} d",
        var d           => $"En {d} d"
    };
}
