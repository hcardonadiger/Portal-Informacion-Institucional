namespace Diger.TramitesEstado.Web.Common;

/// <summary>Una entrada del catálogo de tableros.</summary>
/// <param name="Claves">Claves de permiso que hacen falta para <b>llegar</b> al destino, todas. Se
/// gatea con la clave del destino, no con la de la página donde vive el enlace (DESIGN.md). Vacía =
/// autoservicio, la ve cualquier sesión.</param>
/// <param name="Visible">Condición adicional sobre el rol (capacidades, nunca el nombre).</param>
public sealed record TableroCatalogoItem(
    string Ruta, string Titulo, string Descripcion, string Icono,
    IReadOnlyList<string> Claves,
    Func<ICurrentUserService, bool>? Visible = null);

/// <summary>
/// La lista de tableros del portal, en un solo lugar: la lee el Inicio para la rejilla «Todos los
/// tableros». Las insignias de la auditoría (nuevo, repetido, rediseño pendiente, propuesto
/// retirar) se quitaron el 2026-10-27: eran notas de trabajo, no algo que el usuario deba leer.
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
            "Avance, salud y decisiones de la cartera", "i-plan", ProyectosVer),

        new("/Tableros/Proyectos", "Proyectos — seguimiento operativo",
            "Doce indicadores, semáforo y tablas de atención; lleva al alcance del rol y a la ficha de cada proyecto",
            "i-plan", ProyectosVer,
            // Quien no alcanza el nivel de institución es redirigido desde esa página a su tablero de
            // área o de unidad: ofrecérsela sería un enlace que no lleva adonde dice. Misma regla
            // que las pestañas de _TabsProyectos.
            Visible: u => u.EsGlobal || u.NivelAlcance is NivelAlcance.Institucion or NivelAlcance.Global),

        new("/Tableros/ProyectosArea", "Proyectos de mi área",
            "Semáforo de la cartera del área, por unidad", "i-area", ProyectosVer,
            Visible: u => u.EsJefeDeArea),

        new("/Tableros/ProyectosUnidad", "Mis proyectos",
            "Proyectos donde usted es responsable o interesado", "i-unidad", ProyectosVer),

        new("/Tableros/Reuniones", "Reuniones y acuerdos",
            "Compromisos, cumplimiento y capacitación", "i-reunion", TablerosVer),

        new("/Tableros/Tickets", "Tickets de soporte",
            "Volumen, tiempos de resolución y temas", "i-ticket", TablerosVer),

        new("/Tableros/Digitalizacion", "Digitalización",
            "Avance por institución, etapas y analistas", "i-grafica", Digitalizacion),

        new("/Tableros/Tramites", "Avance de trámites",
            "Semáforo de seguimiento por trámite", "i-grafica", Digitalizacion),

        new("/Tableros/Expedientes", "Expedientes",
            "Avance de digitalización por expediente", "i-expediente", Digitalizacion),

        new("/Siger/Tablero", "Observatorio SIGER",
            "Inventario de trámites SIGER; vive en el módulo SIGER", "i-institucion", ["Siger.Ver"]),

        new("/Tableros/MiTablero", "Mi tablero",
            "Su trabajo asignado: tickets, acuerdos y proyectos", "i-tablero", []),
    ];

    /// <summary>La entrada de una ruta, o null si no está en el catálogo.</summary>
    public static TableroCatalogoItem? Buscar(string ruta) =>
        Items.FirstOrDefault(i => string.Equals(i.Ruta, ruta, StringComparison.OrdinalIgnoreCase));

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
    /// condición de rol.</summary>
    public static async Task<bool> PuedeAbrirAsync(
        TableroCatalogoItem item, AccesoModulosService acceso, ICurrentUserService usuario, CancellationToken ct = default)
    {
        if (item.Visible is { } cond && !cond(usuario)) return false;

        foreach (var clave in item.Claves)
            if (!await acceso.PuedeClaveAsync(clave, ct)) return false;

        return true;
    }
}
