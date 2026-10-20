namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Un valor que el sistema propone para un campo vacío de una ficha SIGER, a la espera de que
/// una persona lo apruebe o lo rechace.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué existe.</b> De las 1 057 fichas del inventario, 1 032 no tienen categoría,
/// modalidad, tiempo ni costo. Llenarlas a mano es trabajo de meses; llenarlas en automático y
/// directo es meter datos inventados en el portal que ve el ciudadano. D-24 corta por en medio:
/// se propone en masa, se aprueba con criterio, y nada llega a la ficha sin que alguien lo haya
/// mirado.
/// </para>
/// <para>
/// <b>Esta tabla es el registro de procedencia, y por eso sobrevive a la aprobación.</b> Una fila
/// aprobada no se borra: queda diciendo qué propuso la máquina, con qué justificación y quién lo
/// aceptó. Es lo que hace auditable el llenado dentro de un año, cuando nadie recuerde de dónde
/// salió la categoría de un trámite.
/// </para>
/// <para>
/// <b>Por qué no hay una columna «Autollenado» en la ficha.</b> Era la forma literal de D-24 y se
/// descartó por una razón concreta: una bandera guardada en <c>TramitesSiger</c> se vuelve mentira
/// en cuanto alguien corrige el campo a mano —seguiría diciendo «esto lo puso una máquina» sobre
/// un valor que puso una persona—, y no hay forma barata de limpiarla sin enganchar todas las
/// rutas de edición. Acá la procedencia se responde comparando: el campo es de origen automático
/// si existe una propuesta aprobada para él <i>y</i> la ficha todavía tiene ese valor. Esa
/// respuesta no se desactualiza sola. Ver <c>PropuestasLlenado.SigueVigente</c>.
/// </para>
/// </remarks>
public sealed class PropuestaLlenado : BaseAuditableEntity
{
    public int TramiteSigerId { get; set; }

    public CampoFicha Campo { get; set; }

    /// <summary>
    /// El valor propuesto, siempre como texto. Cómo se interpreta depende de <see cref="Campo"/>
    /// y esa conversión vive en un solo lugar —<c>ValorLlenado</c>— para que proponer, mostrar y
    /// aplicar no puedan discrepar. Null es un valor propuesto legítimo solo si algún día se
    /// propone borrar algo; hoy nunca lo es.
    /// </summary>
    public string? ValorPropuesto { get; set; }

    public CertezaLlenado Certeza { get; set; }

    /// <summary>
    /// De dónde salió el valor, en lenguaje llano y con los datos que lo sostienen
    /// («3 pasos con tiempo declarado: 1 + 2 + 5 días»). Es obligatorio y es lo que hace
    /// defendible una aprobación por tandas: sin esto, aprobar 200 filas de un clic es firmar
    /// a ciegas.
    /// </summary>
    public string Justificacion { get; set; } = default!;

    public EstadoPropuesta Estado { get; set; } = EstadoPropuesta.Pendiente;

    public DateTime? DecididaEl { get; set; }

    /// <summary>Quién aprobó o rechazó. Null mientras esté pendiente.</summary>
    public string? DecididaPor { get; set; }
}

