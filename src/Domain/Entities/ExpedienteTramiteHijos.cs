namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Lo que el trámite le entrega al ciudadano cuando termina.
///
/// Es hijo del <b>trámite dentro del expediente</b>, no del expediente: se identifica por
/// <see cref="ExpedienteId"/> + <see cref="TramiteIndex"/>, igual que
/// <see cref="TramiteRequisito"/>. Se reemplaza en bloque en cada guardado, como las otras diez
/// colecciones hijas.
/// </summary>
public sealed class ExpedienteTramiteEntregable : BaseEntity
{
    public int    ExpedienteId { get; set; }
    public int    TramiteIndex { get; set; }
    public int    Orden        { get; set; }
    public string Entregable   { get; set; } = default!;
    /// <summary>Físico, digital, ambos… tal como lo escriba el analista.</summary>
    public string? Formato      { get; set; }
    /// <summary>Cómo se entrega: en ventanilla, por correo, descarga…</summary>
    public string? Presentacion { get; set; }
}

/// <summary>
/// Dónde se atiende el trámite. Mismo esquema de identidad y de reemplazo que
/// <see cref="ExpedienteTramiteEntregable"/>.
/// </summary>
public sealed class ExpedienteTramiteLugar : BaseEntity
{
    public int    ExpedienteId { get; set; }
    public int    TramiteIndex { get; set; }
    public int    Orden        { get; set; }
    public string Lugar        { get; set; } = default!;
    public string? Ciudad     { get; set; }
    public string? Direccion  { get; set; }
    public string? Telefonos  { get; set; }
}
