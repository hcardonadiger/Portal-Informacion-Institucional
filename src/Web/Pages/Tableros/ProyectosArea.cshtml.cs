using Diger.TramitesEstado.Application.Dashboards.Queries;

namespace Diger.TramitesEstado.Web.Pages.Tableros;

/// <summary>Qué conjunto de proyectos mira el jefe de área.</summary>
public enum AlcanceTableroArea
{
    /// <summary>Todos los proyectos del área activa, sea o no interesado de cada uno. Es lo que
    /// un jefe necesita para dirigir: los que nadie le asignó también son suyos.</summary>
    Area = 0,

    /// <summary>Solo donde figura como responsable o interesado. El conjunto que ve el nivel
    /// Unidad, útil para separar «lo que dirijo» de «lo que me toca a mí».</summary>
    Mios = 1
}

/// <summary>
/// Tablero de nivel Área: el portafolio del área con el mismo detalle que el de institución,
/// desglosado por unidad y conmutable entre «toda mi área» y «solo lo mío».
///
/// <para><b>Por qué ya no reutiliza <c>GetMisProyectosDashboardQuery</c>.</b> Hasta el 2026-09-07
/// esta página leía «mis proyectos» y su comentario advertía que leer por <c>AreaId</c> abriría un
/// segundo camino de acceso sin auditar. Esa advertencia quedó obsoleta el 2026-08-23, cuando
/// <c>Proyecto</c> estrenó filtro global en <c>AppDbContext</c> con una rama propia para
/// <c>NivelAlcance.Area</c> anclada en la institución activa. El camino ya existe, ya está
/// auditado y ya tiene pruebas: acotar por área es hoy un filtro de presentación sobre un
/// <c>IQueryable</c> que el filtro global ya recortó, no una segunda puerta.</para>
///
/// <para><b>El área nunca viaja en la petición.</b> Lo único que se bindea es
/// <see cref="Alcance"/>, un enum de dos valores. El área sale siempre de
/// <c>ICurrentUserService.ActiveAreaId</c>, así que un <c>?AreaIds=OTRA</c> tecleado a mano no se
/// bindea a nada. Y aunque se colara, la intersección ocurre sobre lo ya recortado: un área ajena
/// no puede ampliar nada.</para>
/// </summary>
[Authorize]
// Misma clave que sus hermanos /Tableros/Proyectos y /Tableros/ProyectosUnidad: quién ve los
// proyectos lo decide "Proyectos.Ver" y no "Tableros.Ver". Gatear este tablero con la clave de
// los tableros abriría por la ventana lo que el módulo cierra por la puerta.
[Permission("Proyectos", AccionModulo.Ver, "Ver proyectos")]
public sealed class ProyectosAreaModel(ISender sender, ICurrentUserService currentUser) : PageModel
{
    /// <summary>Recuerda el último alcance elegido. Es una preferencia de vista, así que vive en
    /// una cookie y no en la base — esta página no escribe nada.</summary>
    public const string CookieAlcance = "tablero-area-alcance";

    public ProyectosDashboardDto Data { get; private set; } = default!;

    /// <summary>Nullable a propósito: un valor basura en la URL no bindea, se trata como ausente y
    /// la página responde con el alcance por defecto en vez de reventar.</summary>
    [BindProperty(SupportsGet = true)] public AlcanceTableroArea? Alcance { get; set; }

    /// <summary>El alcance que efectivamente se aplicó, que no siempre es el pedido: a quien no es
    /// jefe de área se le fuerza a «mios».</summary>
    public AlcanceTableroArea AlcanceAplicado { get; private set; }

    /// <summary>Si se ofrece el conmutador. Se pregunta por la capacidad del rol y por tener área
    /// activa, nunca por el nombre del rol — misma regla que <c>_TabsProyectos</c>.</summary>
    public bool PuedeVerToda { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Sin capacidad de jefatura o sin área resuelta no hay «toda mi área» que mostrar: la
        // página conserva el comportamiento que tenía para todos antes de este cambio.
        PuedeVerToda = currentUser.EsJefeDeArea && !string.IsNullOrWhiteSpace(currentUser.ActiveAreaId);

        // Explícito en la URL manda; si no, lo recordado; si tampoco, «toda mi área», que es de lo
        // que trata la página.
        var pedido = Alcance ?? LeerRecordado();
        AlcanceAplicado = PuedeVerToda ? pedido ?? AlcanceTableroArea.Area : AlcanceTableroArea.Mios;

        // Solo se recuerda una elección explícita, y solo si podía tomarla.
        if (Alcance is { } elegido && PuedeVerToda) Recordar(elegido);

        // Que la vista pinte marcado lo que realmente se aplicó y no lo que se pidió.
        Alcance = AlcanceAplicado;

        Data = AlcanceAplicado == AlcanceTableroArea.Area
            ? await sender.Send(new GetProyectosDashboardQuery(AreaIds: [currentUser.ActiveAreaId!]), ct)
            // Guid.Empty y no null si no hay usuario: null significa «sin acotar» para la consulta,
            // que acá sería lo contrario de lo que se pide.
            : await sender.Send(new GetProyectosDashboardQuery(DeUsuarioId: currentUser.UserId ?? Guid.Empty), ct);
    }

    private AlcanceTableroArea? LeerRecordado() =>
        Request.Cookies.TryGetValue(CookieAlcance, out var v)
        && Enum.TryParse<AlcanceTableroArea>(v, out var a)
            ? a
            : null;

    private void Recordar(AlcanceTableroArea a) =>
        Response.Cookies.Append(CookieAlcance, a.ToString(), new CookieOptions
        {
            HttpOnly    = true,
            IsEssential = true,
            SameSite    = SameSiteMode.Lax,
            Expires     = DateTimeOffset.UtcNow.AddDays(30)
        });
}
