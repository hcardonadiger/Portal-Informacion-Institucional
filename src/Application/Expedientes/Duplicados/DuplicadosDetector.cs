using Diger.TramitesEstado.Application.Common;

namespace Diger.TramitesEstado.Application.Expedientes.Duplicados;

/// <summary>Datos mínimos de un expediente necesarios para compararlo contra otros de la misma institución.</summary>
public sealed record ExpedienteDuplicadoCandidato(int Id, string InstitucionId, string Codigo, IReadOnlyList<string> TramiteNombres);

/// <summary>
/// Detecta posibles expedientes duplicados dentro de una misma institución comparando los
/// nombres de sus trámites (no el código, que se genera automáticamente). No descarta nada:
/// solo señala pares para revisión manual.
/// </summary>
public static class DuplicadosDetector
{
    private const double UmbralNombreTramite = 0.82;
    private const double UmbralSolapamiento  = 0.6;

    /// <summary>Para cada expediente marcado como posible duplicado, los códigos de los expedientes con los que hace match.</summary>
    public static IReadOnlyDictionary<int, IReadOnlyList<string>> Detectar(IReadOnlyList<ExpedienteDuplicadoCandidato> candidatos)
    {
        var resultado = new Dictionary<int, List<string>>();
        foreach (var grupo in candidatos.GroupBy(c => c.InstitucionId))
        {
            var lista = grupo.ToList();
            for (var i = 0; i < lista.Count; i++)
            for (var j = i + 1; j < lista.Count; j++)
            {
                if (!SonSimilares(lista[i], lista[j])) continue;
                Agregar(resultado, lista[i].Id, lista[j].Codigo);
                Agregar(resultado, lista[j].Id, lista[i].Codigo);
            }
        }
        return resultado.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value);
    }

    private static void Agregar(Dictionary<int, List<string>> mapa, int id, string codigo)
    {
        if (!mapa.TryGetValue(id, out var lista)) mapa[id] = lista = [];
        if (!lista.Contains(codigo)) lista.Add(codigo);
    }

    private static bool SonSimilares(ExpedienteDuplicadoCandidato a, ExpedienteDuplicadoCandidato b)
    {
        if (a.TramiteNombres.Count == 0 || b.TramiteNombres.Count == 0) return false;

        var usados = new bool[b.TramiteNombres.Count];
        var coincidencias = 0;
        foreach (var nombreA in a.TramiteNombres)
        {
            for (var k = 0; k < b.TramiteNombres.Count; k++)
            {
                if (usados[k]) continue;
                if (TextoSimilitud.Similitud(nombreA, b.TramiteNombres[k]) < UmbralNombreTramite) continue;
                usados[k] = true;
                coincidencias++;
                break;
            }
        }

        var minCount = Math.Min(a.TramiteNombres.Count, b.TramiteNombres.Count);
        return (double)coincidencias / minCount >= UmbralSolapamiento;
    }
}
