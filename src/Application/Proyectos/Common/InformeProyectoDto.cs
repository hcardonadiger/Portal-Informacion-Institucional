using Diger.TramitesEstado.Application.Proyectos.Queries; // TableroProyectoDto

namespace Diger.TramitesEstado.Application.Proyectos.Common;

/// <summary>
/// Datos consolidados de un proyecto para el <b>Informe de Estado</b> (formato PMI): reúne la ficha
/// (acta), los indicadores del tablero (avance vs. línea base, cronograma, ritmo, equipo), el
/// registro de riesgos, los interesados y los avances reportados. Lo arma
/// <c>GetInformeProyectoQuery</c> reutilizando las consultas que ya alimentan el panel, y lo consume
/// <see cref="IInformeProyectoPdfService"/>.
/// </summary>
public sealed record InformeProyectoDto(
    ProyectoDetailDto                     Ficha,
    TableroProyectoDto                    Tablero,
    IReadOnlyList<RiesgoProyectoDto>      Riesgos,
    IReadOnlyList<InteresadoProyectoDto>  Interesados,
    IReadOnlyList<AvanceProyectoDto>      Avances,
    string                                GeneradoPor,
    DateTime                              GeneradoEn);

/// <summary>Genera el Informe de Estado del proyecto en PDF (QuestPDF).</summary>
public interface IInformeProyectoPdfService
{
    byte[] Generar(InformeProyectoDto dto);
}
