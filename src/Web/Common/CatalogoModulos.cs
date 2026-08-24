namespace Diger.TramitesEstado.Web.Common;

/// <summary>Un módulo tal como se presenta en la administración de permisos.</summary>
public sealed record ModuloInfo(string Clave, string Etiqueta, string Area, int Orden, bool EsSubmodulo);

/// <summary>
/// Agrupa los módulos del catálogo de permisos para la pantalla de administración.
///
/// Las áreas y las etiquetas son **las mismas del menú de navegación** (_Layout.cshtml), a
/// propósito: quien configura un rol lo hace mirando lo que el usuario final va a ver. Una
/// primera versión de este mapa inventó su propia taxonomía ("Operación", "Catálogos") y el
/// resultado fue que Contactos —que en el menú está bajo Soporte— aparecía en otra sección y
/// con otro nombre, así que no se encontraba. Si cambia un grupo del navbar, cambia acá.
///
/// Vive en el Web y no en la base: el área es una decisión de presentación. Guardarla en la
/// tabla Permisos obligaría a una migración para reordenar el menú de administración, y el
/// catálogo se sincroniza solo por reflexión — la clave "Modulo.Accion" es lo único que tiene
/// que ser estable.
///
/// Un módulo que no esté en el mapa NO se pierde: cae en "Sin clasificar", que aparece al
/// final de la pantalla. Mismo criterio que las advertencias de PermissionCatalogSyncService:
/// que se vea, en vez de desaparecer cuando alguien agrega una página nueva.
/// </summary>
public static class CatalogoModulos
{
    public const string SinClasificar = "Sin clasificar";

    /// <summary>Áreas en el mismo orden en que aparecen los grupos del navbar.</summary>
    public static readonly IReadOnlyList<string> Areas =
        ["Expedientes", "SIGER", "Tableros", "Agenda", "Soporte", "Administración", SinClasificar];

    private static readonly Dictionary<string, (string Etiqueta, string Area, int Orden)> Mapa =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // ── Expedientes ──────────────────────────────────────────────
            ["Expedientes"]             = ("Expedientes",              "Expedientes", 10),
            ["PlanTrabajo"]             = ("Plan de trabajo",          "Expedientes", 20),
            ["Recursos"]                = ("Recursos y plantillas",    "Expedientes", 30),

            // ── SIGER ────────────────────────────────────────────────────
            ["Siger"]                   = ("Inventario",               "SIGER", 10),
            ["Siger.Conciliacion"]      = ("Conciliación",             "SIGER", 11),
            ["Siger.Publicacion"]       = ("Publicado en HondurasÁgil", "SIGER", 12),
            ["Siger.Llenado"]           = ("Llenado asistido",        "SIGER", 13),

            // ── Tableros ─────────────────────────────────────────────────
            ["Tableros"]                = ("Tableros",                 "Tableros", 10),
            ["Informes"]                = ("Informes",                 "Tableros", 20),
            ["Tramites"]                = ("Indicadores de trámites",  "Tableros", 30),

            // ── Agenda ───────────────────────────────────────────────────
            ["Calendario"]              = ("Calendario",               "Agenda", 10),
            ["Reuniones"]               = ("Reuniones y compromisos",  "Agenda", 20),

            // ── Soporte ──────────────────────────────────────────────────
            ["Tickets"]                 = ("Tickets",                  "Soporte", 10),
            ["Tickets.Temas"]           = ("Temas y categorías",       "Soporte", 11),
            ["Chat"]                    = ("Chat de soporte",          "Soporte", 20),
            ["Contactos"]               = ("Contactos",                "Soporte", 30),
            ["Contactos.Estado"]        = ("Dar de baja y reactivar",  "Soporte", 31),

            // ── Administración ───────────────────────────────────────────
            ["Instituciones"]           = ("Instituciones",            "Administración", 10),
            ["Areas"]                   = ("Áreas",                    "Administración", 20),
            ["Unidades"]                = ("Unidades",                 "Administración", 30),
            ["Usuarios"]                = ("Usuarios",                 "Administración", 40),
            ["Usuarios.Contrasenas"]    = ("Restablecer contraseñas",  "Administración", 41),
            ["Accesos.Roles"]           = ("Roles",                    "Administración", 50),
            ["Accesos.Permisos"]        = ("Permisos",                 "Administración", 60),
            ["Admin.PlantillasTramite"] = ("Plantillas de trámite",    "Administración", 70),
            ["Admin.Importaciones"]     = ("Importaciones",            "Administración", 80),
            ["Admin.Migraciones"]       = ("Migraciones",              "Administración", 90),
        };

    public static ModuloInfo Obtener(string clave)
    {
        // Un submódulo ("Tickets.Temas") se indenta bajo su padre siempre que el padre
        // también esté en el catálogo; si no, se muestra como módulo de primer nivel.
        var punto = clave.IndexOf('.');
        var esSub = punto > 0 && Mapa.ContainsKey(clave[..punto]);

        return Mapa.TryGetValue(clave, out var m)
            ? new ModuloInfo(clave, m.Etiqueta, m.Area, m.Orden, esSub)
            : new ModuloInfo(clave, clave, SinClasificar, 999, esSub);
    }
}
