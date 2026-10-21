namespace Diger.TramitesEstado.Application.Reuniones.Common;

/// <summary>
/// Cómo se escribe una duración en minutos. Vive en un solo lugar porque la muestran tres
/// consumidores —la ficha de la reunión, el acta en pantalla y el acta en PDF— y verla escrita
/// distinto en el PDF y en la pantalla haría dudar de cuál es la buena.
/// </summary>
public static class DuracionTexto
{
    /// <summary>«45 min», «1 h», «1 h 30 min». Null cuando no hay duración declarada: se devuelve
    /// null y no «1 h» para que quien la muestre pueda decidir si omite el dato, en vez de exhibir
    /// el valor supuesto como si la reunión lo hubiera declarado.</summary>
    public static string? Formatear(int? minutos)
    {
        if (minutos is not > 0) return null;

        var (h, m) = (minutos.Value / 60, minutos.Value % 60);

        return (h, m) switch
        {
            (0, _) => $"{m} min",
            (_, 0) => $"{h} h",
            _      => $"{h} h {m} min"
        };
    }

    /// <summary>Opciones del selector del editor. Cubren la reunión de trabajo típica; la lista es
    /// corta a propósito —un campo libre fue justamente lo que dejó el dato inservible—.</summary>
    public static readonly (int Minutos, string Texto)[] Opciones =
    [
        (30,  "30 min"),
        (45,  "45 min"),
        (60,  "1 h"),
        (90,  "1 h 30 min"),
        (120, "2 h"),
        (180, "3 h"),
        (240, "4 h"),
        (480, "Jornada completa (8 h)")
    ];
}
