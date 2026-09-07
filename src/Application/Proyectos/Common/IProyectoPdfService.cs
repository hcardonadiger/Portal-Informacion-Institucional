using Diger.TramitesEstado.Application.Proyectos.Queries;

namespace Diger.TramitesEstado.Application.Proyectos.Common;

/// <summary>
/// Genera el documento PDF con la estructura completa del proyecto: la ficha, la EDT, el
/// cronograma dibujado, la bitácora de avances, el equipo, la documentación, los vínculos y la
/// auditoría. No es una impresión de la página web — es un documento maquetado aparte, con el
/// mismo criterio que <c>IActaPdfService</c>.
/// </summary>
public interface IProyectoPdfService
{
    byte[] Generar(ProyectoPdfDto dto);
}

/// <summary>
/// Lo que consume el generador. Reúne los DTO que la ficha ya carga en vez de redefinirlos: si
/// una consulta cambia de forma, el PDF se entera al compilar y no queda mostrando una copia
/// vieja del dato.
/// </summary>
public sealed record ProyectoPdfDto(
    ProyectoDetailDto                     Proyecto,
    CronogramaDto                         Cronograma,
    IReadOnlyList<AvanceProyectoDto>      Avances,
    IReadOnlyList<InteresadoProyectoDto>  Interesados,
    IReadOnlyList<RiesgoProyectoDto>      Riesgos,
    IReadOnlyList<DocumentoProyectoDto>   Documentos,
    VinculosProyectoDto                   Vinculos,
    IReadOnlyList<BitacoraProyectoDto>    Auditoria,

    /// <summary>Nombre legible de la institución, del área y de la unidad. La ficha guarda Ids y
    /// el PDF no debería obligar a quien lo lee a traducirlos.</summary>
    string? InstitucionNombre = null,
    string? AreaNombre        = null,
    string? UnidadNombre      = null);
