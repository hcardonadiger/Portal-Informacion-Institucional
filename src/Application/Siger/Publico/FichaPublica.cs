namespace Diger.TramitesEstado.Application.Siger.Publico;

/// <summary>Catálogo cerrado de modalidad para la ficha pública. "Digital" = Virtual o Hibrido —
/// ver <see cref="EsDigital"/>. Un enum C# no se usa a propósito (ver M-04 del plan: TramiteSiger
/// se mantiene anémico, y el catálogo cerrado ya está protegido por CHECK en la base).</summary>
public static class ModalidadPublica
{
    public const string Virtual    = "Virtual";
    public const string Presencial = "Presencial";
    public const string Hibrido    = "Hibrido";

    /// <summary>Un trámite Híbrido cuenta como "se puede hacer en línea" igual que uno Virtual —
    /// filtrar solo por Virtual subestima el conteo de trámites digitales.</summary>
    public static bool EsDigital(string? modalidad) => modalidad is Virtual or Hibrido;
}

/// <summary>
/// Qué le falta a una ficha para poder servirle de algo al ciudadano.
/// </summary>
/// <remarks>
/// <para>
/// <b>Esta regla es de PortalDigital, y desde la separación de la API lo es del todo.</b> Antes
/// tenía una copia en la consulta del catálogo público, dentro del proyecto de la API: agregar un
/// campo obligatorio acá obligaba a tocar y desplegar la API. Hoy la API no la conoce — lee una
/// columna.
/// </para>
/// <para>
/// <b>La gemela en SQL es la columna calculada <c>TramitesSiger.FichaCompleta</c></b>, definida en
/// la migración <c>ColumnaFichaCompleta</c>. Las dos tienen que decir lo mismo, y por eso conviven
/// en este repositorio y no repartidas en dos. Esta sigue viva porque responde algo que un
/// booleano no puede: <i>qué</i> falta, que es lo que el editor le enseña al técnico.
/// </para>
/// <para>
/// Que digan lo mismo se comprueba en dos sitios: <c>FichaPublicaCompletitudTests</c> fija la
/// tabla de verdad en C#, y <c>scripts/sql/16-verificar-ficha-completa.sql</c> corre esa misma
/// tabla de verdad contra la columna en la base real.
/// </para>
/// </remarks>
public static class FichaPublicaCompletitud
{
    /// <summary>Qué le falta a la ficha para poder publicarse. Lista vacía = ficha completa.</summary>
    /// <remarks>
    /// Los nombres son los que el técnico ve en el editor, no los de las columnas: quien lee la
    /// alerta va a buscar el campo en la pantalla, no en la base. El costo se decide por
    /// <paramref name="costoEsGratuito"/> y no por el texto del monto, porque "es gratuito" ya es
    /// una respuesta completa aunque no haya monto que escribir.
    /// <para>
    /// La comparación es contra <c>null</c> y no contra cadena vacía a propósito: la columna
    /// calculada de la base decide exactamente igual. Si acá se apretara el criterio, el catálogo
    /// público mostraría fichas que esta alerta declara incompletas.
    /// </para>
    /// <para>
    /// Desde la Fase 7 el enlace a SOL puede venir del tramo o de la URL heredada, así que la
    /// condición es «alguna de las dos». Ese caso, además, la base lo impide de raíz por
    /// <c>CK_TramitesSiger_Sol</c>: una ficha marcada en SOL sin ningún enlace no se puede
    /// guardar. Acá se sigue evaluando porque esta alerta corre <b>antes</b> de guardar, sobre lo
    /// que hay en el formulario, que es justo donde sirve avisar.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> CamposFaltantes(int? categoriaId, string? modalidad,
        string? tiempoTexto, bool? costoEsGratuito, bool estaEnSol, string? solUrl, string? solTramo)
    {
        var faltantes = new List<string>(5);

        if (categoriaId is null)         faltantes.Add("categoría");
        if (modalidad is null)           faltantes.Add("modalidad");
        if (tiempoTexto is null)         faltantes.Add("tiempo");
        if (costoEsGratuito is null)     faltantes.Add("costo");
        if (estaEnSol && solUrl is null && solTramo is null) faltantes.Add("enlace a SOL");

        return faltantes;
    }

    /// <summary>Una ficha está completa cuando no le falta nada. Definido sobre
    /// <see cref="CamposFaltantes"/> a propósito: el día que se agregue un campo obligatorio, la
    /// alerta que ve el técnico y el filtro que ve el ciudadano no pueden decir cosas distintas.</summary>
    public static bool Evaluar(int? categoriaId, string? modalidad, string? tiempoTexto,
        bool? costoEsGratuito, bool estaEnSol, string? solUrl, string? solTramo) =>
        CamposFaltantes(categoriaId, modalidad, tiempoTexto, costoEsGratuito, estaEnSol, solUrl, solTramo).Count == 0;

    /// <summary>Cómo se le dice al técnico qué falta. Vive junto a la regla y no en cada página
    /// para que el inventario, el detalle y el editor no acaben con tres redacciones distintas
    /// del mismo aviso.</summary>
    public static string Frase(IReadOnlyList<string> faltantes) =>
        faltantes.Count == 0
            ? "La ficha pública está completa."
            : $"Falta capturar: {string.Join(", ", faltantes)}.";
}
