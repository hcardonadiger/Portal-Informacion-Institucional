using System.Globalization;
using System.Text;

namespace Diger.TramitesEstado.Application.Reuniones.Memoria;

/// <summary>Un asistente tal como lo ve la regla: venga de la entidad o del formulario.</summary>
public sealed record AsistenteLista(string Nombre, string? Correo, string? Institucion, bool EnLaLista);

/// <summary>Un responsable que la memoria acepta: una persona de la lista o una institución.</summary>
public sealed record ResponsableValido(string Nombre, string? Correo, bool EsInstitucion);

/// <summary>
/// Quién puede ser responsable de un acuerdo de la memoria (decisión del 2026-10-30): las personas
/// que están en la lista de asistencia —registradas por QR, agregadas a mano o pre-registradas que
/// confirmaron— y las instituciones con que se registraron, más la contraparte de la reunión. Un
/// invitado de Outlook que no llegó no cuenta: estar invitado no es haber asistido.
///
/// <para>La IA escribe nombres como los oye («Wendy Oviedo (Gobernación)»); <see cref="Resolver"/>
/// los lleva al nombre exacto de la lista, que es lo que el editor ofrece en su desplegable. Lo que
/// no casa con nadie se descarta en vez de guardarse como texto suelto.</para>
/// </summary>
public static class ResponsablesMemoria
{
    public static IReadOnlyList<ResponsableValido> De(Reunion r) =>
        De(r.Asistentes.Select(a => new AsistenteLista(a.Nombre, a.Correo, a.Institucion, a.EnLaLista)), r.Institucion);

    /// <summary>Lo mismo desde el formulario de la reunión (el detalle que lee el MCP).</summary>
    public static IReadOnlyList<ResponsableValido> DeFormulario(IEnumerable<Common.AsistenteInput> asistentes, string? institucionReunion) =>
        De(asistentes.Select(a => new AsistenteLista(a.Nombre, a.Correo, a.Institucion,
            !a.EsPreregistro || a.AutoRegistro || a.Confirmado == true)), institucionReunion);

    /// <summary>La regla para la IA, con la lista concreta de esta reunión.</summary>
    public static string Regla(IReadOnlyList<ResponsableValido> validos) => validos.Count == 0
        ? "Responsables: nadie ha llenado todavía la lista de asistencia de esta reunión. Omite \"responsable\" en todos los acuerdos."
        : "Responsables: \"responsable\" debe ser EXACTAMENTE uno de estos nombres (personas de la lista de asistencia o " +
          "instituciones registradas); si quien se compromete no está aquí, omite \"responsable\":\n" +
          string.Join('\n', validos.Select(v => "- " + v.Nombre + (v.EsInstitucion ? " (institución)" : "")));

    public static IReadOnlyList<ResponsableValido> De(IEnumerable<AsistenteLista> asistentes, string? institucionReunion)
    {
        var personas = asistentes.Where(a => a.EnLaLista && !string.IsNullOrWhiteSpace(a.Nombre)).ToList();
        var validos = new List<ResponsableValido>();
        var vistos = new HashSet<string>();

        void Agregar(string? nombre, string? correo, bool esInstitucion)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return;
            var n = nombre.Trim();
            if (vistos.Add(Normalizar(n))) validos.Add(new ResponsableValido(n, correo?.Trim(), esInstitucion));
        }

        foreach (var p in personas) Agregar(p.Nombre, p.Correo, false);
        foreach (var p in personas) Agregar(p.Institucion, null, true);
        Agregar(institucionReunion, null, true);
        return validos;
    }

    /// <summary>
    /// El nombre exacto de la lista que corresponde a lo escrito, o null. Casa, en este orden: el
    /// nombre o el correo completos (sin tildes, mayúsculas ni lo que vaya entre paréntesis), y luego
    /// por palabras: si todas las palabras escritas (dos al menos) están en el nombre de una sola
    /// persona o institución. «Wendy Oviedo» casa con «Wendy Elizabeth Oviedo Martinez»; «Henry»
    /// solo, no.
    /// </summary>
    public static string? Resolver(string? escrito, IReadOnlyList<ResponsableValido> validos)
    {
        if (string.IsNullOrWhiteSpace(escrito)) return null;
        var texto = Normalizar(SinParentesis(escrito));
        if (texto.Length == 0) return null;

        var exacto = validos.FirstOrDefault(v => Normalizar(v.Nombre) == texto
                                                 || (v.Correo is not null && Normalizar(v.Correo) == texto));
        if (exacto is not null) return exacto.Nombre;

        var palabras = Palabras(texto);
        if (palabras.Count < 2) return null;

        var candidatos = validos.Where(v => palabras.IsSubsetOf(Palabras(Normalizar(v.Nombre)))).ToList();
        return candidatos.Count == 1 ? candidatos[0].Nombre : null;
    }

    private static string SinParentesis(string s)
    {
        var i = s.IndexOf('(');
        return i < 0 ? s : s[..i];
    }

    private static HashSet<string> Palabras(string normalizado) =>
        normalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => p.Length > 1).ToHashSet();

    /// <summary>Minúsculas, sin tildes y con un solo espacio entre palabras.</summary>
    private static string Normalizar(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.Trim().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
