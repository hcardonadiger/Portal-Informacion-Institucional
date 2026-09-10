using Diger.TramitesEstado.Application.Informes.Common;

namespace Diger.TramitesEstado.Application.Informes;

public interface IInformeService
{
    byte[] GenerarPdf(InformeInstitucionDto dto);
    byte[] GenerarExcel(InformeInstitucionDto dto);
}
