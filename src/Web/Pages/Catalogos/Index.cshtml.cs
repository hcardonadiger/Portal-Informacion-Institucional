namespace Diger.TramitesEstado.Web.Pages.Catalogos;

/// <summary>
/// Índice de los catálogos administrables del portal. Reemplaza los enlaces sueltos que antes
/// colgaban del menú «Administración»: ahí convivían cosas de distinta naturaleza —usuarios,
/// roles y permisos, que son control de acceso, junto a instituciones, áreas y unidades, que
/// son datos maestros—. Al separarlos, agregar un catálogo nuevo ya no alarga el menú.
///
/// No lleva [Permission] propio a propósito. Esta pantalla no muestra ningún dato: es una
/// portada que enlaza a otras páginas, y cada una exige su permiso al entrar. Inventarle una
/// clave propia obligaría a otorgarla en cada rol que ya puede administrar algún catálogo,
/// solo para dejarlo pasar por la portada que lleva a donde ya podía ir. En cambio arma la
/// lista preguntando por el permiso de cada destino: quien solo administra áreas y unidades
/// ve esas dos tarjetas, no seis.
/// </summary>
[PermisoNoRequerido("Portada de navegación: no expone datos y cada catálogo enlazado exige su propio permiso.")]
public sealed class IndexModel(AccesoModulosService acceso) : PageModel
{
    /// <param name="Pagina">Ruta Razor del catálogo, tal como la recibe asp-page.</param>
    /// <param name="Icono">Identificador dentro del sprite de iconos (_IconSprite).</param>
    public sealed record Tarjeta(string Titulo, string Descripcion, string Pagina, string Icono);

    public IReadOnlyList<Tarjeta> Tarjetas { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Cada entrada se condiciona a la MISMA clave que exige su página de destino, para que
        // la portada no prometa una tarjeta que al hacer clic responda Forbidden.
        var candidatas = new (string Clave, Tarjeta Tarjeta)[]
        {
            ("Instituciones.Ver", new(
                "Instituciones",
                "Entidades del Estado con las que trabaja DIGER.",
                "/Instituciones/Index", "i-institucion")),

            ("Areas.Ver", new(
                "Áreas",
                "Divisiones internas de cada institución.",
                "/Areas/Index", "i-area")),

            ("Unidades.Ver", new(
                "Unidades",
                "Dependencias que cuelgan de cada área.",
                "/Unidades/Index", "i-unidad")),

            ("Prioridades.Proyectos.Editar", new(
                "Prioridades de proyectos",
                "Las opciones de prioridad que ofrece la ficha de un proyecto.",
                "/Catalogos/Prioridades", "i-alerta")),

            ("Tickets.Temas.Editar", new(
                "Temas de tickets",
                "Categorías de soporte y su tiempo máximo de atención.",
                "/Tickets/Temas", "i-ticket")),
        };

        var tarjetas = new List<Tarjeta>(candidatas.Length);
        foreach (var (clave, tarjeta) in candidatas)
            if (await acceso.PuedeClaveAsync(clave, ct))
                tarjetas.Add(tarjeta);

        Tarjetas = tarjetas;
    }
}
