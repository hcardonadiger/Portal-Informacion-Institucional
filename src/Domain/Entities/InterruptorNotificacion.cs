namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Los tres interruptores de un módulo en el Panel de control. Tabla <c>ModulosNotificacion</c>.</summary>
public sealed class InterruptorNotificacion
{
    public ModuloNotificacion Modulo        { get; private set; }
    public bool               CampanaActiva { get; private set; }
    public bool               CorreoActivo  { get; private set; }
    public bool               Agrupar       { get; private set; }

    private InterruptorNotificacion() { }

    public static InterruptorNotificacion Crear(ModuloNotificacion modulo, bool campana = true, bool correo = false, bool agrupar = true)
        => new() { Modulo = modulo, CampanaActiva = campana, CorreoActivo = correo, Agrupar = agrupar };

    /// <returns>Si algo cambió: un guardado sin cambios no ensucia la bitácora.</returns>
    public bool Cambiar(bool campana, bool correo, bool agrupar)
    {
        var cambio = CampanaActiva != campana || CorreoActivo != correo || Agrupar != agrupar;
        CampanaActiva = campana;
        CorreoActivo  = correo;
        Agrupar       = agrupar;
        return cambio;
    }
}
