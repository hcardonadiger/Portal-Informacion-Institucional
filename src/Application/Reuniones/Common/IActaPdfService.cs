namespace Diger.TramitesEstado.Application.Reuniones.Common;

/// <summary>Genera el PDF con formato del acta/registro de reunión (no es un print de la página web,
/// sino un documento maquetado que jala los datos de la reunión).</summary>
public interface IActaPdfService
{
    byte[] Generar(ActaPdfDto dto);
}

/// <summary>Datos que consume el generador de PDF del acta.</summary>
public sealed record ActaPdfDto(
    ReunionFormDto Datos,
    IReadOnlyList<AsistenteInput> Asistentes,
    IReadOnlyList<AcuerdoInput> Acuerdos,
    string? InstitucionNombre);
