namespace Diger.TramitesEstado.Application.Siger.Promocion;

/// <summary>Un campo que cambiaría al pasar el trámite a SIGER.</summary>
public sealed record CambioCampo(string Campo, string? Antes, string? Despues);

/// <summary>Cuántas filas tiene una colección antes y después del pase.</summary>
public sealed record CambioColeccion(string Coleccion, int Antes, int Despues);

/// <summary>Lo que pasaría si se confirma el pase. No escribe nada.</summary>
/// <param name="EsNueva">La ficha todavía no existe: el pase la va a crear.</param>
/// <param name="Codigo">Código de la ficha destino, o el que se le asignaría si es nueva.</param>
/// <param name="Cambios">Campos que cambiarían. Vacío significa que no hay nada que pasar.</param>
/// <param name="Colecciones">Requisitos, entregables y lugares, antes y después.</param>
public sealed record VistaPreviaPase(
    bool EsNueva,
    string Codigo,
    string NombreTramite,
    IReadOnlyList<CambioCampo> Cambios,
    IReadOnlyList<CambioColeccion> Colecciones)
{
    public bool HayAlgoQuePasar =>
        Cambios.Count > 0 || Colecciones.Any(c => c.Antes != c.Despues);
}

/// <summary>
/// Qué cambiaría en una ficha si se le pasara el trámite del expediente.
/// </summary>
/// <remarks>
/// <para>
/// <b>El diff se calcula aplicando el mismo mapeo que escribe.</b> No hay una lista de campos
/// para comparar y otra para copiar: se crea una ficha de mentira, se le aplica
/// <see cref="PromocionMapeo.CamposDelExpediente"/> y se compara contra la real. Es la única
/// forma de que la vista previa no pueda mentir — dos listas paralelas discreparían el día que
/// alguien agregue un campo a una y olvide la otra, y entonces el diálogo diría «no cambia nada»
/// mientras el pase sobrescribe algo.
/// </para>
/// <para>
/// Solo se comparan los campos que <b>manda el expediente</b>. El código, el estado de SIGER, la
/// publicación y lo popular no aparecen porque el pase no los toca; enseñarlos como «sin cambio»
/// sugeriría que podrían cambiar.
/// </para>
/// </remarks>
public static class DiffPase
{
    public static VistaPreviaPase Calcular(
        TramiteSiger? actual,
        ExpedienteTramite t,
        Expediente e,
        string codigoSiEsNueva,
        IReadOnlyList<TramiteRequisito> requisitos,
        IReadOnlyList<ExpedienteTramiteEntregable> entregables,
        IReadOnlyList<ExpedienteTramiteLugar> lugares)
    {
        // La ficha de mentira: se le aplica exactamente el mismo mapeo que va a correr al
        // confirmar, así que lo que muestre el diálogo es lo que va a pasar.
        var comoQuedaria = new TramiteSiger();
        PromocionMapeo.CamposDelExpediente(comoQuedaria, t, e);

        var antes   = actual is null ? null : Comparables(actual);
        var despues = Comparables(comoQuedaria);

        var cambios = new List<CambioCampo>();
        foreach (var (campo, valorDespues) in despues)
        {
            var valorAntes = antes is null ? null : antes[campo];
            if (!string.Equals(valorAntes, valorDespues, StringComparison.Ordinal))
                cambios.Add(new CambioCampo(campo, valorAntes, valorDespues));
        }

        var colecciones = new List<CambioColeccion>
        {
            new("Requisitos",  actual?.Requisitos.Count      ?? 0, PromocionMapeo.Requisitos(requisitos).Count),
            new("Entregables", actual?.Entregables.Count     ?? 0, PromocionMapeo.Entregables(entregables).Count),
            new("Lugares de atención", actual?.LugaresAtencion.Count ?? 0, PromocionMapeo.Lugares(lugares).Count)
        };

        return new VistaPreviaPase(
            EsNueva: actual is null,
            Codigo: actual?.Codigo ?? codigoSiEsNueva,
            NombreTramite: t.NombreTramite,
            Cambios: cambios,
            Colecciones: colecciones);
    }

    /// <summary>
    /// Los campos que el expediente manda, con el nombre que la persona ve en pantalla y no el de
    /// la columna: quien lee el diálogo va a buscar el campo en el formulario, no en la base.
    /// </summary>
    private static Dictionary<string, string?> Comparables(TramiteSiger f) => new()
    {
        ["Nombre"]              = f.Nombre,
        ["Institución"]         = f.Institucion,
        ["Dependencia"]         = f.Dependencia,
        ["Descripción"]         = f.Descripcion,
        ["Objetivo"]            = f.Objetivo,
        ["Dirigido a"]          = f.DirigidoA,
        ["Enlace principal"]    = f.EnlacePrincipal,
        ["Categoría"]           = f.CategoriaId?.ToString(),
        ["Modalidad"]           = f.Modalidad,
        ["Tiempo"]              = f.TiempoTexto,
        ["¿Es gratuito?"]       = Tres(f.CostoEsGratuito),
        ["Costo"]               = f.CostoTexto,
        ["Vigencia"]            = f.VigenciaDocumento,
        ["Temporalidad"]        = f.Temporalidad,
        ["Observaciones DIGER"] = f.ObservacionesDiger,
        ["¿Está en SOL?"]       = f.EstaEnSol ? "Sí" : "No",
        ["Tramo del enlace SOL"] = f.SolTramo
    };

    /// <summary>El costo tiene tres estados y el diálogo tiene que distinguirlos: pasar de «no
    /// capturado» a «tiene costo» es un cambio, y leerlo como «de No a No» lo escondería.</summary>
    private static string? Tres(bool? v) => v switch
    {
        true  => "Sí",
        false => "No",
        null  => null
    };
}
