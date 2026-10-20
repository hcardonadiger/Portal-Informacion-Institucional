using System.Globalization;
using Diger.TramitesEstado.Application.Siger.Publico;

namespace Diger.TramitesEstado.Application.Siger.Llenado;

/// <summary>
/// De dónde sale cada valor que el llenado asistido propone.
/// </summary>
/// <remarks>
/// <para>
/// <b>Estas reglas proponen; no llenan.</b> Nada de lo que devuelvan llega a una ficha sin que
/// alguien lo apruebe (D-24). Eso es lo que permite que una regla sea agresiva —proponer
/// «Presencial» para mil fichas que tienen oficinas de atención y ningún canal en línea— sin que
/// el ciudadano pague el error si la regla se equivoca.
/// </para>
/// <para>
/// <b>Ninguna regla propone sobre el silencio.</b> Que una ficha no mencione un pago no prueba
/// que sea gratuita, y por eso el costo se propone solo cuando el texto dice algo. Un hueco vacío
/// es un dato honesto: dice «no se sabe». Un hueco llenado a la ligera dice «es gratis» con la
/// misma cara con la que lo diría un dato verificado, y el ciudadano no puede distinguirlos.
/// </para>
/// <para>
/// <b>La certeza es la parte útil.</b> No está para adornar la pantalla: es el eje sobre el que se
/// aprueba por tandas. Alta significa que el dato ya estaba en la base y solo se transformó; Baja
/// significa que es un supuesto razonable. Quien revisa acepta lo primero en bloque y mira lo
/// segundo.
/// </para>
/// </remarks>
public static class ReglasLlenado
{
    /// <summary>Tope de la columna <c>Justificacion</c>. Se recorta acá y no en la base para que
    /// una ficha con treinta pasos no tumbe el guardado del lote entero.</summary>
    private const int TopeJustificacion = 400;

    /// <summary>
    /// Todo lo que se puede proponer para una ficha, limitado a <paramref name="camposVacios"/>.
    /// </summary>
    /// <param name="categoriasPorNombre">Catálogo real de categorías, nombre normalizado → id. Se
    /// recibe en vez de constantes en el código porque los ids salen de datos sembrados:
    /// cablearlos haría que la regla escribiera en la categoría equivocada en cualquier base donde
    /// la siembra hubiera corrido en otro orden.</param>
    public static IReadOnlyList<PropuestaCalculada> Proponer(
        DatosParaLlenado datos,
        IReadOnlySet<CampoFicha> camposVacios,
        IReadOnlyDictionary<string, int> categoriasPorNombre)
    {
        var propuestas = new List<PropuestaCalculada>(4);

        if (camposVacios.Contains(CampoFicha.Tiempo))
            Agregar(propuestas, Tiempo(datos));

        if (camposVacios.Contains(CampoFicha.Costo))
            Agregar(propuestas, Costo(datos));

        if (camposVacios.Contains(CampoFicha.Categoria))
            Agregar(propuestas, Categoria(datos, categoriasPorNombre));

        if (camposVacios.Contains(CampoFicha.Modalidad))
            Agregar(propuestas, Modalidad(datos));

        return propuestas;
    }

    private static void Agregar(List<PropuestaCalculada> destino, PropuestaCalculada? propuesta)
    {
        if (propuesta is not null)
            destino.Add(propuesta with { Justificacion = Recortar(propuesta.Justificacion) });
    }

    private static string Recortar(string texto) =>
        texto.Length <= TopeJustificacion ? texto : texto[..(TopeJustificacion - 1)] + "…";

    // ── Tiempo ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Suma los días declarados en los pasos. Es la única regla que sale de un dato numérico ya
    /// capturado, y por eso la única que puede llegar a certeza Alta.
    /// </summary>
    /// <remarks>
    /// <b>Suma, no promedia ni toma el máximo</b>, porque un trámite se recorre paso por paso: el
    /// ciudadano espera la suma. El supuesto —que los pasos son sucesivos y no simultáneos— se
    /// dice en la justificación para que quien revise pueda no estar de acuerdo.
    /// <para>
    /// Si algún paso no declaró tiempo la suma se queda corta, y prometer menos tiempo del real es
    /// el error que más molesta a quien hace el trámite. Por eso ahí la certeza baja a Media: el
    /// valor sigue sirviendo, pero merece que alguien lo mire.
    /// </para>
    /// </remarks>
    private static PropuestaCalculada? Tiempo(DatosParaLlenado datos)
    {
        if (datos.TiemposDePaso.Count == 0) return null;

        var dias = new List<decimal>(datos.TiemposDePaso.Count);
        foreach (var crudo in datos.TiemposDePaso)
            if (ADias(crudo) is { } d)
                dias.Add(d);

        if (dias.Count == 0) return null;

        var suma  = dias.Sum();
        var todos = dias.Count == datos.TiemposDePaso.Count;

        var detalle = string.Join(" + ", dias.Take(8).Select(Numero));
        if (dias.Count > 8) detalle += " + …";

        var justificacion = todos
            ? $"Suma de los {dias.Count} pasos, todos con tiempo declarado: {detalle} = {Numero(suma)} días. Se suman por ser pasos sucesivos."
            : $"Suma de los {dias.Count} de {datos.TiemposDePaso.Count} pasos que declaran tiempo: {detalle} = {Numero(suma)} días. Los otros {datos.TiemposDePaso.Count - dias.Count} no declaran, así que el total puede quedarse corto.";

        return new PropuestaCalculada(CampoFicha.Tiempo, EnPalabras(suma),
            todos ? CertezaLlenado.Alta : CertezaLlenado.Media, justificacion);
    }

    private static string Numero(decimal d) => d.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Los pasos guardan días como número suelto («1», «0.5», «30»). Se acepta la coma
    /// decimal porque el dato se capturó desde varias configuraciones regionales.</summary>
    private static decimal? ADias(string? crudo)
    {
        if (string.IsNullOrWhiteSpace(crudo)) return null;

        var limpio = crudo.Trim().Replace(',', '.');
        return decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d >= 0
            ? d
            : null;
    }

    /// <summary>
    /// El número en la frase que va a leer el ciudadano. Se redondea <b>hacia arriba</b>: entre
    /// prometer 3 días y que tarde 4, o prometer 4 y que tarde 4, el segundo error no existe.
    /// </summary>
    internal static string EnPalabras(decimal dias)
    {
        if (dias == 0) return "El mismo día";
        if (dias < 1)  return "Menos de 1 día hábil";
        if (dias <= 1) return "1 día hábil";
        return $"{Math.Ceiling(dias)} días hábiles";
    }

    // ── Costo ─────────────────────────────────────────────────────────────────

    private static readonly string[] SenalesDePago =
    [
        "recibo de pago", "comprobante de pago", "boleta de pago", "recibo tgr", "pago de",
        "pagar", "cancelar el valor", "cancelar la tarifa", "arancel", "lempiras",
        "deposito bancario", "timbre"
    ];

    private static readonly string[] SenalesDeGratuidad =
    [
        "sin costo alguno", "sin ningun costo", "no tiene costo", "libre de costo",
        "exento de pago", "es gratuito", "es gratuita", "gratuito", "gratuita", "gratis"
    ];

    /// <summary>
    /// Decide si el trámite tiene costo mirando lo que dicen sus pasos y requisitos.
    /// </summary>
    /// <remarks>
    /// <b>El silencio no se interpreta.</b> Una ficha que no menciona ningún pago puede ser
    /// gratuita o puede ser una ficha mal capturada, y no hay forma de distinguirlas desde acá.
    /// Proponer «gratuito» sobre esa duda pondría en el portal público un dato inventado con la
    /// misma apariencia que uno verificado. Se devuelve null y el campo se queda vacío, que es la
    /// respuesta honesta.
    /// </remarks>
    private static PropuestaCalculada? Costo(DatosParaLlenado datos)
    {
        var (fragmentoPago,   citaPago)   = Buscar(datos, SenalesDePago);
        var (fragmentoGratis, citaGratis) = Buscar(datos, SenalesDeGratuidad);

        // Ambas señales a la vez suele ser «el trámite es gratuito pero adjunte el recibo de pago
        // del timbre». Gana el costo —cobrar algo, aunque sea poco, no es ser gratuito— pero baja
        // la certeza, porque también puede ser al revés.
        if (fragmentoPago is not null && fragmentoGratis is not null)
            return new PropuestaCalculada(CampoFicha.Costo, ValorLlenado.DeCosto(false), CertezaLlenado.Baja,
                $"El texto menciona pago y gratuidad a la vez: «{citaPago}» y «{citaGratis}». Se propone que tiene costo, pero conviene revisarlo.");

        if (fragmentoPago is not null)
            return new PropuestaCalculada(CampoFicha.Costo, ValorLlenado.DeCosto(false), CertezaLlenado.Media,
                $"El texto del trámite menciona un pago: «{citaPago}».");

        if (fragmentoGratis is not null)
            return new PropuestaCalculada(CampoFicha.Costo, ValorLlenado.DeCosto(true), CertezaLlenado.Media,
                $"El texto del trámite lo declara sin costo: «{citaGratis}».");

        return null;
    }

    /// <summary>Busca las frases en pasos y requisitos, y devuelve además el fragmento real donde
    /// apareció —con sus tildes y mayúsculas— para poder citarlo.</summary>
    private static (string? Frase, string? Cita) Buscar(DatosParaLlenado datos, string[] frases)
    {
        foreach (var texto in datos.TextosDePaso.Concat(datos.TextosDeRequisito))
        {
            var normalizado = TextoNormalizado.De(texto);
            var frase = TextoNormalizado.PrimeraQueAparece(normalizado, frases);
            if (frase is not null)
                return (frase, Citar(texto));
        }

        return (null, null);
    }

    private static string Citar(string texto)
    {
        var limpio = texto.Trim().Replace('\n', ' ').Replace('\r', ' ');
        return limpio.Length <= 110 ? limpio : limpio[..109] + "…";
    }

    // ── Categoría ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Palabras que delatan el tema de un trámite, por categoría del catálogo. Se indexa por
    /// nombre normalizado y no por id a propósito: los ids salen de datos sembrados y no son
    /// estables entre bases.
    /// </summary>
    private static readonly (string Categoria, string[] Palabras)[] PalabrasPorCategoria =
    [
        ("salud y seguridad social", ["salud", "sanitari", "medicament", "hospital", "medic", "enfermer",
            "farmac", "epidemi", "vacun", "seguridad social", "pension", "jubilac", "cotizante", "afiliad",
            "alimentos y bebidas", "psicotropic"]),

        ("educacion y cultura", ["educa", "escolar", "universi", "titulo", "diploma", "docent", "cultura",
            "patrimonio", "biblioteca", "beca", "academ", "equivalencia de estudios"]),

        ("impuestos y finanzas", ["impuest", "tribut", "aduan", "arancel", "fiscal", "declaracion jurada",
            "rtn", "exoner", "banc", "seguro", "financier", "bolsa de valores", "casa de cambio",
            "credito", "tesoreria"]),

        ("identidad y ciudadania", ["identidad", "pasaporte", "cedula", "nacionalidad", "migra", "residenc",
            "naturaliza", "visa", "antecedentes", "partida de nacimiento", "matrimonio", "defuncion",
            "notari", "apostilla", "legaliza", "extranjer"]),

        ("empresas y negocios", ["empresa", "comerc", "sociedad mercantil", "marca", "patente",
            "registro mercantil", "inversion", "franquic", "licencia de operacion", "cooperativa",
            "pyme", "importac", "exportac", "propiedad industrial", "derecho de autor", "concesion"]),

        ("vivienda y propiedad", ["propiedad", "inmueble", "catastro", "hipotec", "dominio pleno",
            "lotific", "construccion", "terreno", "vivienda", "urbaniz", "bien raiz", "servidumbre"]),

        ("transporte y vehiculos", ["vehicul", "licencia de conducir", "placa", "transporte", "automotor",
            "marina mercante", "naveg", "buque", "embarcac", "aeronave", "transito", "matricula de nave",
            "gente de mar", "maritim", "aeropuerto", "puerto"]),

        ("medio ambiente", ["ambient", "forestal", "madera", "bosque", "contaminac", "recurso natural",
            "fauna", "flora", "pesca", "mineri", "aprovechamiento", "vida silvestre", "cuenca",
            "residuos", "aguas residuales"])
    ];

    /// <summary>
    /// Clasifica el trámite en una de las ocho categorías por las palabras de su nombre y, si el
    /// nombre no dice nada, de su descripción.
    /// </summary>
    /// <remarks>
    /// <b>El nombre pesa más que la descripción</b> porque es lo que alguien escribió para
    /// identificar el trámite, mientras que la descripción arrastra contexto de la institución que
    /// despista: la de un instituto forestal menciona bosques aunque el trámite sea una constancia
    /// de identidad.
    /// <para>
    /// <b>Un empate no se resuelve, se abandona.</b> Si dos categorías aciertan con la misma
    /// fuerza no hay forma de elegir sin inventar, y elegir mal es peor que no proponer: una
    /// categoría equivocada manda el trámite a la sección que no es y nadie lo encuentra, mientras
    /// que una vacía al menos se ve vacía.
    /// </para>
    /// </remarks>
    private static PropuestaCalculada? Categoria(
        DatosParaLlenado datos, IReadOnlyDictionary<string, int> categoriasPorNombre)
    {
        var porNombre = Clasificar(TextoNormalizado.De(datos.Nombre));
        if (porNombre is { } n && categoriasPorNombre.TryGetValue(n.Categoria, out var idNombre))
            return new PropuestaCalculada(CampoFicha.Categoria, ValorLlenado.DeCategoria(idNombre),
                CertezaLlenado.Media,
                $"El nombre del trámite dice «{n.Palabra}», que corresponde a {n.Categoria}.");

        var porTexto = Clasificar(TextoNormalizado.De($"{datos.Descripcion} {datos.Objetivo}"));
        if (porTexto is { } t && categoriasPorNombre.TryGetValue(t.Categoria, out var idTexto))
            return new PropuestaCalculada(CampoFicha.Categoria, ValorLlenado.DeCategoria(idTexto),
                CertezaLlenado.Baja,
                $"El nombre no lo dice, pero la descripción menciona «{t.Palabra}», que apunta a {t.Categoria}. Conviene confirmarlo.");

        return null;
    }

    private static (string Categoria, string Palabra)? Clasificar(string textoNormalizado)
    {
        if (textoNormalizado.Length == 0) return null;

        (string Categoria, string Palabra, int Aciertos)? mejor = null;
        var hayEmpate = false;

        foreach (var (categoria, palabras) in PalabrasPorCategoria)
        {
            var aciertos = 0;
            string? primera = null;

            foreach (var palabra in palabras)
                if (textoNormalizado.Contains(palabra, StringComparison.Ordinal))
                {
                    aciertos++;
                    primera ??= palabra;
                }

            if (aciertos == 0) continue;

            if (mejor is null || aciertos > mejor.Value.Aciertos)
            {
                mejor = (categoria, primera!, aciertos);
                hayEmpate = false;
            }
            else if (aciertos == mejor.Value.Aciertos)
            {
                hayEmpate = true;
            }
        }

        if (hayEmpate || mejor is null) return null;
        return (mejor.Value.Categoria, mejor.Value.Palabra);
    }

    // ── Modalidad ─────────────────────────────────────────────────────────────

    private static readonly string[] SenalesEnLinea =
    [
        "en linea", "sitio web", "pagina web", "plataforma", "correo electronico", "portal web",
        "www.", "http", "sistema informatico", "descargar el formulario", "via electronica"
    ];

    private static readonly string[] SenalesPresenciales =
    [
        "presentarse", "acudir", "ventanilla", "presencial", "apersonarse", "comparecer",
        "entregar en la oficina", "oficinas de", "en las instalaciones"
    ];

    /// <summary>
    /// Propone cómo se hace el trámite. Es la regla más débil de las cuatro y ninguna de sus
    /// respuestas pasa de certeza Baja.
    /// </summary>
    /// <remarks>
    /// En el inventario de hoy <b>ninguna ficha</b> tiene marcado <c>EstaEnSol</c> ni
    /// <c>DisponibleEnLinea</c>, y ningún paso declara su propia modalidad: no queda más señal que
    /// el texto y la existencia de oficinas de atención. Con eso «Presencial» acierta casi
    /// siempre, y esa es justamente la razón para desconfiar: una regla que responde lo mismo para
    /// mil fichas no está clasificando, está poniendo un valor por defecto. Se propone igual
    /// —un valor por defecto correcto y revisable sirve más que el vacío— pero se propone diciendo
    /// lo que es, y por eso todas las justificaciones terminan en «supuesto, no dato».
    /// </remarks>
    private static PropuestaCalculada? Modalidad(DatosParaLlenado datos)
    {
        var (enLinea,    citaLinea)  = Buscar(datos, SenalesEnLinea);
        var (presencial, citaPresen) = Buscar(datos, SenalesPresenciales);

        if (enLinea is not null && (presencial is not null || datos.LugaresDeAtencion > 0))
            return new PropuestaCalculada(CampoFicha.Modalidad, ValorLlenado.DeModalidad(ModalidadPublica.Hibrido),
                CertezaLlenado.Baja,
                $"El texto menciona un canal en línea («{citaLinea}») y también atención presencial. Supuesto, no dato.");

        if (enLinea is not null)
            return new PropuestaCalculada(CampoFicha.Modalidad, ValorLlenado.DeModalidad(ModalidadPublica.Virtual),
                CertezaLlenado.Baja,
                $"El texto solo menciona canales en línea («{citaLinea}») y la ficha no registra lugares de atención. Supuesto, no dato.");

        if (presencial is not null)
            return new PropuestaCalculada(CampoFicha.Modalidad, ValorLlenado.DeModalidad(ModalidadPublica.Presencial),
                CertezaLlenado.Baja,
                $"El texto describe atención presencial («{citaPresen}») y no menciona ningún canal en línea. Supuesto, no dato.");

        if (datos.LugaresDeAtencion > 0)
            return new PropuestaCalculada(CampoFicha.Modalidad, ValorLlenado.DeModalidad(ModalidadPublica.Presencial),
                CertezaLlenado.Baja,
                $"La ficha registra {datos.LugaresDeAtencion} lugar(es) de atención y ningún canal en línea. Supuesto por descarte, no dato.");

        return null;
    }
}
