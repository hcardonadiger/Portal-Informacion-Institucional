namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Bitácora de solo agregar del Panel de control: qué cambió, quién y cuándo. Mismo patrón que
/// <see cref="PermisoAuditoria"/>, que no sirve acá porque sus columnas son las de la matriz de
/// permisos. <b>El valor de una contraseña nunca se escribe</b>: solo «contraseña cambiada».
/// </summary>
public sealed class PanelControlBitacora : BaseEntity
{
    public string   Seccion { get; private set; } = "";
    public string   Detalle { get; private set; } = "";
    public string   Actor   { get; private set; } = "";
    public DateTime Fecha   { get; private set; }

    private PanelControlBitacora() { }

    public static PanelControlBitacora Crear(string seccion, string detalle, string actor, DateTime fechaUtc) => new()
    {
        Seccion = Notificacion.Recortar(seccion, 100),
        Detalle = Notificacion.Recortar(detalle, 1000),
        Actor   = Notificacion.Recortar(actor, 200),
        Fecha   = fechaUtc,
    };
}
