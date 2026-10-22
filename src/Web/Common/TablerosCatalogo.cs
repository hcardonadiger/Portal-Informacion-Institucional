namespace Diger.TramitesEstado.Web.Common;

/// <summary>En qué quedó cada tablero tras la auditoría (<c>docs/tableros/auditoria-tableros.md</c>).
/// Solo marca: en esta entrega ningún tablero se fusiona, rediseña ni retira.</summary>
public enum EstadoTablero { Vigente, Nuevo, Repetido, EnRediseno, PropuestoRetirar }

/// <summary>Una entrada del catálogo de tableros.</summary>
/// <param name="Claves">Claves de permiso que hacen falta para <b>llegar</b> al destino, todas. Se
/// gatea con la clave del destino, no con la de la página donde vive el enlace (DESIGN.md). Vacía =
/// autoservicio, la ve cualquier sesión.</param>
/// <param name="Relacionado">Ruta del tablero con el que se repite o al que se propone migrar.</param>
/// <param name="Visible">Condición adicional sobre el rol (capacidades, nunca el nombre).</param>
public sealed record TableroCatalogoItem(
    string Ruta, string Titulo, string Descripcion, string Icono,
    IReadOnlyList<string> Claves,
    EstadoTablero Estado, string? Relacionado = null, string? Nota = null,
    Func<ICurrentUserService, bool>? Visible = null);

/// <summary>
/// La lista de tableros del portal, en un solo lugar: la lee el Inicio para la rejilla «Todos los
/// tableros» y cada tablero marcado para su línea de insignia bajo el título. Retirar una insignia
/// es cambiar una línea aquí.
///
/// <para><c>/Tableros/Proyecto/{id}</c> no se lista porque necesita un id; <c>/Informes</c> tampoco,
/// porque no es un tablero.</para>
/// </summary>
public static class TablerosCatalogo
{
    private static readonly string[] ProyectosVer   = ["Proyectos.Ver"];
    private static readonly string[] TablerosVer    = ["Tableros.Ver"];
    // Las tres de digitalización piden Tableros.Ver en la página pero son de un solo dominio: el
    // Inicio solo las ofrecía a quien trabaja expedientes, y eso se conserva.
    private static readonly string[] Digitalizacion = ["Tableros.Ver", "Expedientes.Ver"];

    public static readonly IReadOnlyList<TableroCatalogoItem> Items =
    [
        new("/Tableros/Ejecutivo", "Tablero ejecutivo de proyectos",
            "Avance, salud y decisiones de la cartera", "i-plan", ProyectosVer, EstadoTablero.Nuevo),

        new("/Tableros/Proyectos", "Proyectos — seguimiento operativo",
            "Doce indicadores, semáforo y tablas de atención; lleva al alcance del rol y a la ficha de cada proyecto",
            "i-plan", ProyectosVer, EstadoTablero.EnRediseno,
            // Quien no alcanza el nivel de institución es redirigido desde esa página a su tablero de
            // área o de unidad: ofrecérsela sería un enlace que no lleva adonde dice. Misma regla
            // que las pestañas de _TabsProyectos.
            Visible: u => u.EsGlobal || u.NivelAlcance is NivelAlcance.Institucion or NivelAlcance.Global),

        new("/Tableros/ProyectosArea", "Proyectos de mi área",
            "Semáforo de la cartera del área, por unidad", "i-area", ProyectosVer, EstadoTablero.Repetido,
            Relacionado: "/Tableros/Proyectos", Visible: u => u.EsJefeDeArea),

        new("/Tableros/ProyectosUnidad", "Mis proyectos",
            "Proyectos donde usted es responsable o interesado", "i-unidad", ProyectosVer, EstadoTablero.Repetido,
            // Con el operativo y no con «mi área»: el operativo recorta por DeUsuarioId con el mismo
            // predicado, y es visible para todo el que tiene Proyectos.Ver; «mi área» solo para
            // jefes de área (Revisión 1, R6).
            Relacionado: "/Tableros/Proyectos"),

        new("/Tableros/Reuniones", "Reuniones y acuerdos",
            "Compromisos, cumplimiento y capacitación", "i-reunion", TablerosVer, EstadoTablero.EnRediseno),

        new("/Tableros/Tickets", "Tickets de soporte",
            "Volumen, tiempos de resolución y temas", "i-ticket", TablerosVer, EstadoTablero.EnRediseno),

        new("/Tableros/Digitalizacion", "Digitalización",
            "Avance por institución, etapas y analistas", "i-grafica", Digitalizacion, EstadoTablero.EnRediseno),

        new("/Tableros/Tramites", "Avance de trámites",
            "Semáforo de seguimiento por trámite", "i-grafica", Digitalizacion, EstadoTablero.Repetido,
            Relacionado: "/Tableros/Digitalizacion"),

        new("/Tableros/Expedientes", "Expedientes",
            "Avance de digitalización por expediente", "i-expediente", Digitalizacion, EstadoTablero.PropuestoRetirar,
            Relacionado: "/Tableros/Digitalizacion"),

        new("/Siger/Tablero", "Observatorio SIGER",
            "Inventario de trámites SIGER; vive en el módulo SIGER", "i-institucion", ["Siger.Ver"], EstadoTablero.Vigente),

        new("/Tableros/MiTablero", "Mi tablero",
            "Su trabajo asignado: tickets, acuerdos y proyectos", "i-tablero", [], EstadoTablero.Vigente),
    ];

    /// <summary>La entrada de una ruta, o null si no está en el catálogo.</summary>
    public static TableroCatalogoItem? Buscar(string ruta) =>
        Items.FirstOrDefault(i => string.Equals(i.Ruta, ruta, StringComparison.OrdinalIgnoreCase));

    /// <summary>Título de la ruta relacionada, para el texto de la insignia.</summary>
    public static string TituloDe(string? ruta) => ruta is null ? "" : Buscar(ruta)?.Titulo ?? ruta;

    /// <summary>Texto de la insignia. Vigente no lleva.</summary>
    public static string? TextoInsignia(TableroCatalogoItem item) => item.Estado switch
    {
        EstadoTablero.Nuevo            => "Nuevo",
        EstadoTablero.Repetido         => $"Repetido con {TituloDe(item.Relacionado)}",
        EstadoTablero.EnRediseno       => "Rediseño pendiente",
        EstadoTablero.PropuestoRetirar => $"Propuesto retirar · usa {TituloDe(item.Relacionado)}",
        _                              => null
    };

    public static string IconoInsignia(EstadoTablero e) => e switch
    {
        EstadoTablero.Nuevo            => "i-plan",
        EstadoTablero.Repetido         => "i-enlace",
        EstadoTablero.EnRediseno       => "i-grafica",
        EstadoTablero.PropuestoRetirar => "i-vencido",
        _                              => ""
    };

    public static string ClaseInsignia(EstadoTablero e) => e switch
    {
        EstadoTablero.Nuevo            => "tablero-badge nuevo",
        EstadoTablero.Repetido         => "tablero-badge repetido",
        EstadoTablero.EnRediseno       => "tablero-badge rediseno",
        EstadoTablero.PropuestoRetirar => "tablero-badge retirar",
        _                              => ""
    };

    /// <summary>Las entradas que el usuario puede abrir: todas sus claves concedidas y la condición
    /// de rol, si la hay.</summary>
    public static async Task<IReadOnlyList<TableroCatalogoItem>> VisiblesAsync(
        AccesoModulosService acceso, ICurrentUserService usuario, CancellationToken ct = default)
    {
        var visibles = new List<TableroCatalogoItem>();
        foreach (var item in Items)
            if (await PuedeAbrirAsync(item, acceso, usuario, ct))
                visibles.Add(item);
        return visibles;
    }

    /// <summary>El usuario llega de verdad a ese tablero: tiene todas sus claves y cumple la
    /// condición de rol. Lo usa también la línea de insignia para no enlazar un destino ajeno.</summary>
    public static async Task<bool> PuedeAbrirAsync(
        TableroCatalogoItem item, AccesoModulosService acceso, ICurrentUserService usuario, CancellationToken ct = default)
    {
        if (item.Visible is { } cond && !cond(usuario)) return false;

        foreach (var clave in item.Claves)
            if (!await acceso.PuedeClaveAsync(clave, ct)) return false;

        return true;
    }
}
