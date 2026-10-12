using System.Reflection;
using QuestPDF.Drawing;

namespace Diger.TramitesEstado.Infrastructure.Reports;

/// <summary>
/// Registra Poppins —la tipografía del Manual de Marca— en QuestPDF.
/// Los PDF salían en Arial, que no es tipografía de marca, y el acta y los
/// informes son documentos oficiales que salen de la institución.
/// Si el registro falla, QuestPDF cae a su fuente por defecto: el peor caso
/// es un PDF con otra letra, nunca un error en producción.
/// </summary>
public static class FuentesMarca
{
    private static bool _registradas;
    private static readonly object _candado = new();

    /// <summary>Nombre de familia con el que QuestPDF la resuelve.</summary>
    public const string Familia = "Poppins";

    public static void Registrar()
    {
        if (_registradas) return;
        lock (_candado)
        {
            if (_registradas) return;
            var ensamblado = Assembly.GetExecutingAssembly();
            foreach (var recurso in ensamblado.GetManifestResourceNames())
            {
                if (!recurso.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)) continue;
                using var flujo = ensamblado.GetManifestResourceStream(recurso);
                if (flujo is null) continue;
                try { FontManager.RegisterFont(flujo); }
                catch { /* se queda con la fuente por defecto */ }
            }
            // Se marca aunque algún registro haya fallado: reintentarlo en cada
            // PDF solo repetiría el fallo y costaría reflexión por documento.
            _registradas = true;
        }
    }
}
