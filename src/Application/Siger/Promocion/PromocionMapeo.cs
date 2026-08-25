using Diger.TramitesEstado.Application.Siger.Publico;

namespace Diger.TramitesEstado.Application.Siger.Promocion;

/// <summary>
/// Cómo un trámite de expediente se convierte en una ficha SIGER, y qué pasa cuando vuelve a
/// pasarse una ficha que ya existe.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que más importa acá no es lo que copia, sino lo que NO copia.</b> Una ficha promovida
/// nace sin publicar y sin <c>IdSiger</c>, y el reparto de propiedad impide que volver a pasarla
/// borre lo que se decidió del lado de SIGER.
/// </para>
/// <para>
/// <b>El reparto, que sale de D-17:</b>
/// </para>
/// <list type="table">
/// <item>
///   <term>Manda el expediente</term>
///   <description>Nombre, descripción, objetivo, dirigido a, dependencia, enlace principal,
///   categoría, modalidad, tiempo, costo, vigencia, temporalidad, observaciones DIGER, si está en
///   SOL, el tramo del enlace, y las tres colecciones de contenido.</description>
/// </item>
/// <item>
///   <term>Manda SIGER, y no se toca nunca desde acá</term>
///   <description><c>Codigo</c>, <c>IdSiger</c>, <c>EstadoSiger</c> y los pasos del proceso
///   (D-11).</description>
/// </item>
/// <item>
///   <term>Manda la curaduría, y tampoco se toca</term>
///   <description><c>Publicado</c> y <c>EsPopular</c>. Actualizar una ficha <b>no puede</b>
///   sacarla del portal del ciudadano ni meterla: eso lo decide una persona en la pantalla de
///   publicación (D-10).</description>
/// </item>
/// </list>
/// <para>
/// <b>Desviación del plan original:</b> allí <c>EstaEnSol</c> y el enlace a SOL figuraban del lado
/// de SIGER. Las fases 7 y 8 los movieron al expediente —D-17 los pone en el grupo de contenido y
/// la Fase 8 le dio al expediente dónde guardarlos—, así que el expediente los manda. La
/// <c>SolUrl</c> heredada sí se queda del lado de SIGER: es de antes de que las direcciones se
/// compusieran y no tiene equivalente en el expediente (D-14).
/// </para>
/// </remarks>
public static class PromocionMapeo
{
    /// <summary>
    /// Crea la ficha de un trámite que hasta ahora solo vivía en un expediente.
    /// </summary>
    /// <param name="codigo">El código ya generado. Se recibe hecho y no se calcula acá porque
    /// depende de qué códigos existan en la base, y este mapeo no toca la base.</param>
    public static TramiteSiger CrearFicha(ExpedienteTramite t, Expediente e, string codigo)
    {
        var ficha = new TramiteSiger
        {
            Codigo = codigo,

            // Sin IdSiger: es la marca de que esta ficha no existe en el inventario de SIGER.
            IdSiger = null,

            // Nace en Registrado y sin publicar. Promover y publicar son dos actos distintos
            // (D-10): que un trámite esté modelado no significa que alguien haya decidido
            // enseñárselo al ciudadano.
            EstadoSiger = ReglaPublicacion.Registrado,
            Publicado   = false,
            EsPopular   = false,

            FechaIngreso = DateTime.UtcNow
        };

        CamposDelExpediente(ficha, t, e);
        return ficha;
    }

    /// <summary>
    /// Reescribe en la ficha <b>solo</b> las columnas que el expediente manda. Lo usa tanto la
    /// promoción —sobre una ficha recién creada— como cada pase posterior, sobre una que ya
    /// existe y que puede llevar meses publicada.
    /// </summary>
    public static void CamposDelExpediente(TramiteSiger destino, ExpedienteTramite t, Expediente e)
    {
        destino.Nombre        = t.NombreTramite.Trim();
        destino.Institucion   = e.Institucion;
        destino.InstitucionId = e.InstitucionId;
        destino.Sigla         = e.InstitucionId;
        destino.Dependencia   = Limpio(t.AreaResponsable);

        destino.Descripcion = Limpio(t.Descripcion);
        destino.Objetivo    = Limpio(t.Objetivo);
        destino.DirigidoA   = Limpio(t.Dirigido);

        // El sitio web del trámite es el enlace principal de la ficha pública.
        destino.EnlacePrincipal = Limpio(t.SitioWeb);

        destino.CategoriaId = t.CategoriaId;

        // Se normaliza otra vez aunque el expediente ya lo guarde normalizado. Es barato y cubre
        // una ficha que venga de una importación o de una carga antigua; el normalizador deja
        // intacto lo que ya es del catálogo.
        destino.Modalidad = ModalidadNormalizador.Normalizar(t.Modalidad);

        destino.TiempoTexto = TiempoTexto(t);

        destino.CostoEsGratuito = t.EsGratuito;
        destino.CostoTexto      = CostoTexto(t);

        destino.VigenciaDocumento  = Limpio(t.VigenciaDocumento);
        destino.Temporalidad       = Limpio(t.Temporalidad);
        destino.ObservacionesDiger = Limpio(t.ObservacionesDiger);

        destino.EstaEnSol = t.EstaEnSol;
        destino.SolTramo  = DireccionSol.Normalizar(t.SolTramo);

        destino.UltimaModificacion = DateTime.UtcNow;
    }

    /// <summary>
    /// El tiempo que ve el ciudadano: el real observado si se midió, y si no el plazo de ley.
    /// En ese orden porque el real es lo que de verdad le va a pasar, y el legal es el techo.
    /// </summary>
    public static string? TiempoTexto(ExpedienteTramite t) =>
        Limpio(t.TiempoReal) ?? Limpio(t.PlazoLegal);

    /// <summary>
    /// El texto del costo, o null.
    /// </summary>
    /// <remarks>
    /// Tres reglas, y las tres importan:
    /// <list type="bullet">
    /// <item>Si es <b>gratuito</b>, no lleva texto: «es gratuito» ya es una respuesta completa y
    ///   un monto al lado solo confundiría.</item>
    /// <item>Si <b>no se capturó</b> la gratuidad, tampoco se arma texto. Que haya un método de
    ///   pago escrito no prueba que el trámite cueste: prueba que alguien llenó ese campo. La
    ///   ficha queda incompleta y una persona la revisa, que es lo correcto.</item>
    /// <item>Si <b>tiene costo</b>, se arma con lo que haya: monto, método, o los dos.</item>
    /// </list>
    /// </remarks>
    public static string? CostoTexto(ExpedienteTramite t)
    {
        if (t.EsGratuito is not false) return null;

        var monto  = Limpio(t.TgrMonto);
        var metodo = Limpio(t.MetodoPago);

        return (monto, metodo) switch
        {
            (not null, not null) => $"{monto} — {metodo}",
            (not null, null)     => monto,
            (null, not null)     => metodo,
            _                    => null
        };
    }

    /// <summary>
    /// Los requisitos, renumerados desde 1 en el orden del expediente.
    /// </summary>
    /// <remarks>
    /// Se renumera y no se copia el <c>Orden</c> porque en el expediente empieza en 0 y puede
    /// tener huecos si alguien quitó filas del medio; la ficha pública los enseña como una lista
    /// numerada y un «requisito 0» o un salto del 2 al 4 se ve como un error.
    /// </remarks>
    public static List<RequisitoSiger> Requisitos(IEnumerable<TramiteRequisito> reqs) =>
        reqs.Where(r => !string.IsNullOrWhiteSpace(r.Requisito))
            .OrderBy(r => r.Orden)
            .Select((r, i) => new RequisitoSiger { Numero = i + 1, Requisito = r.Requisito.Trim() })
            .ToList();

    public static List<EntregableSiger> Entregables(IEnumerable<ExpedienteTramiteEntregable> items) =>
        items.Where(g => !string.IsNullOrWhiteSpace(g.Entregable))
             .OrderBy(g => g.Orden)
             .Select((g, i) => new EntregableSiger
             {
                 Numero = i + 1,
                 Entregable = g.Entregable.Trim(),
                 Formato = Limpio(g.Formato),
                 Presentacion = Limpio(g.Presentacion)
             })
             .ToList();

    public static List<LugarAtencionSiger> Lugares(IEnumerable<ExpedienteTramiteLugar> items) =>
        items.Where(l => !string.IsNullOrWhiteSpace(l.Lugar))
             .OrderBy(l => l.Orden)
             .Select((l, i) => new LugarAtencionSiger
             {
                 Numero = i + 1,
                 Lugar = l.Lugar.Trim(),
                 Ciudad = Limpio(l.Ciudad),
                 Direccion = Limpio(l.Direccion),
                 Telefonos = Limpio(l.Telefonos)
             })
             .ToList();

    /// <summary>Cadena vacía y cadena con espacios son lo mismo que nada. Se unifican acá para
    /// que la ficha no distinga entre «no capturado» y «capturado en blanco», que al ciudadano
    /// le da exactamente igual y a la regla de completitud no.</summary>
    private static string? Limpio(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
