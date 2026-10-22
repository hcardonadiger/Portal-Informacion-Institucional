using System.Globalization;
using Diger.TramitesEstado.Application.Siger.Publico;

namespace Diger.TramitesEstado.Application.Siger.Llenado;

/// <summary>
/// La traducción entre el texto que guarda una <see cref="PropuestaLlenado"/> y el campo real de
/// la ficha.
/// </summary>
/// <remarks>
/// <para>
/// Una propuesta guarda su valor como texto porque los cuatro campos que puede llenar son de
/// cuatro tipos distintos —un id, una cadena de catálogo cerrado, una cadena libre y un booleano
/// de tres estados— y una tabla con cuatro columnas nullable, de las cuales siempre tres están
/// vacías, miente sobre su propio contenido.
/// </para>
/// <para>
/// El precio de esa decisión es que hay una convención que respetar, y por eso vive entera acá.
/// Proponer, mostrar en pantalla y escribir en la ficha son tres momentos separados por semanas;
/// si cada uno interpretara el texto por su cuenta, el día que discrepen la pantalla enseñaría
/// una cosa y la aprobación escribiría otra, y nadie lo notaría hasta verlo en el portal público.
/// </para>
/// </remarks>
public static class ValorLlenado
{
    // ── Escribir la propuesta ─────────────────────────────────────────────────

    public static string DeCategoria(int categoriaId) => categoriaId.ToString(CultureInfo.InvariantCulture);
    public static string DeModalidad(string modalidad) => modalidad;
    public static string DeTiempo(string tiempoTexto)  => tiempoTexto;
    public static string DeCosto(bool esGratuito)      => esGratuito ? "true" : "false";

    // ── Leer la ficha ─────────────────────────────────────────────────────────

    /// <summary>Qué tiene hoy la ficha en ese campo, en el mismo formato en que se guardaría una
    /// propuesta. Sirve para dos cosas: no proponer sobre lo que ya está lleno, y responder si un
    /// valor sigue siendo el que propuso la máquina.</summary>
    public static string? ActualDe(TramiteSiger ficha, CampoFicha campo) => campo switch
    {
        CampoFicha.Categoria => ficha.CategoriaId?.ToString(CultureInfo.InvariantCulture),
        CampoFicha.Modalidad => ficha.Modalidad,
        CampoFicha.Tiempo    => ficha.TiempoTexto,
        CampoFicha.Costo     => ficha.CostoEsGratuito is null ? null : (ficha.CostoEsGratuito.Value ? "true" : "false"),
        _                    => null
    };

    /// <summary>Si el campo está vacío y por tanto admite propuesta.</summary>
    public static bool EstaVacio(TramiteSiger ficha, CampoFicha campo) => ActualDe(ficha, campo) is null;

    /// <summary>
    /// Si el valor que hoy tiene la ficha sigue siendo el que se aprobó. Esto es lo que sustituye
    /// a una bandera «Autollenado» guardada en la ficha: no se desactualiza cuando alguien corrige
    /// el campo a mano, porque no es un dato guardado sino una comparación.
    /// </summary>
    public static bool SigueVigente(TramiteSiger ficha, PropuestaLlenado propuesta) =>
        propuesta.Estado == EstadoPropuesta.Aprobada &&
        ActualDe(ficha, propuesta.Campo) == propuesta.ValorPropuesto;

    // ── Escribir en la ficha ──────────────────────────────────────────────────

    /// <summary>
    /// Aplica el valor propuesto a la ficha. Devuelve <c>false</c> —sin tocar nada— si el texto no
    /// se puede interpretar para ese campo.
    /// </summary>
    /// <remarks>
    /// Devuelve un booleano en vez de lanzar a propósito: esto se ejecuta dentro de aprobaciones
    /// por tandas de cientos de filas, y una fila corrupta no puede tumbar la tanda entera. Quien
    /// llama cuenta los fallos y los reporta.
    /// </remarks>
    public static bool Aplicar(TramiteSiger ficha, CampoFicha campo, string? valor)
    {
        switch (campo)
        {
            case CampoFicha.Categoria:
                if (!int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0)
                    return false;
                ficha.CategoriaId = id;
                return true;

            case CampoFicha.Modalidad:
                // Catálogo cerrado con CHECK en la base: escribir algo fuera de la lista haría
                // fallar el SaveChanges de la tanda completa, no solo de esta fila.
                if (valor is not (ModalidadPublica.Virtual or ModalidadPublica.Presencial or ModalidadPublica.Hibrido))
                    return false;
                ficha.Modalidad = valor;
                return true;

            case CampoFicha.Tiempo:
                if (string.IsNullOrWhiteSpace(valor)) return false;
                ficha.TiempoTexto = valor.Trim();
                return true;

            case CampoFicha.Costo:
                if (!bool.TryParse(valor, out var gratuito)) return false;
                ficha.CostoEsGratuito = gratuito;
                return true;

            default:
                return false;
        }
    }

    // ── Mostrar ───────────────────────────────────────────────────────────────

    /// <summary>Cómo se enseña el valor a quien revisa. La categoría necesita el catálogo porque
    /// un «3» en pantalla no le dice nada a nadie.</summary>
    public static string ParaMostrar(CampoFicha campo, string? valor, IReadOnlyDictionary<int, string> categorias)
    {
        if (campo != CampoFicha.Categoria)
            return campo == CampoFicha.Costo
                ? valor == "true" ? "Gratuito" : valor == "false" ? "Tiene costo" : "—"
                : valor ?? "—";

        return int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            && categorias.TryGetValue(id, out var nombre)
                ? nombre
                : $"categoría {valor} (ya no existe)";
    }

    public static string Etiqueta(CampoFicha campo) => campo switch
    {
        CampoFicha.Categoria => "categoría",
        CampoFicha.Modalidad => "modalidad",
        CampoFicha.Tiempo    => "tiempo",
        CampoFicha.Costo     => "costo",
        _                    => campo.ToString()
    };
}
