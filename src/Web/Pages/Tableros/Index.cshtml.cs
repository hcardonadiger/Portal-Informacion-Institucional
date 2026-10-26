using Diger.TramitesEstado.Application.Dashboards.Queries.GetEjecutivoProyectos;
using Microsoft.Extensions.Options;

namespace Diger.TramitesEstado.Web.Pages.Tableros;

/// <summary>
/// Inicio del portal y, desde el tablero ejecutivo, índice de todos los tableros.
///
/// <para>De arriba abajo: el resumen del tablero ejecutivo de proyectos (solo con
/// <c>Proyectos.Ver</c>), la tira «Atención hoy» con los indicadores comunes a todas las unidades,
/// la rejilla «Todos los tableros» con las insignias de la auditoría y, al final, los indicadores
/// operativos que ya estaban (tendencia de tickets y conteos), sin cambios.</para>
///
/// <para>Los filtros por período y por estado de trámite que vivían acá se fueron con el bloque de
/// digitalización: no tocaban tickets, acuerdos ni capacitación.</para>
/// </summary>
[Authorize]
[Permission("Tableros", AccionModulo.Ver, "Ver tableros")]
public sealed class IndexModel(
    ISender sender, IUsuarioRepository usuarioRepo, ICurrentUserService currentUser,
    AccesoModulosService acceso, IConfiguration config, IOptions<InstitucionOptions> institucion) : PageModel
{
    public ResumenDto Data { get; private set; } = default!;

    /// <summary>Resumen del ejecutivo. Null si el usuario no tiene <c>Proyectos.Ver</c>: el Inicio
    /// es el destino del login de todos los roles, y no se calcula lo que no se va a mostrar.</summary>
    public EjecutivoProyectosDto? Ejecutivo { get; private set; }
    public string? EjecutivoTitulo { get; private set; }

    /// <summary>Los tableros que este usuario puede abrir, con su insignia.</summary>
    public IReadOnlyList<TableroCatalogoItem> Tableros { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        // El técnico ve los KPIs de tickets limitados a sus temas o sus tickets asignados.
        // "No es jefatura" lo decide la capacidad EsSupervisor del rol (tabla Roles).
        var esTecnico = !currentUser.EsGlobal && !currentUser.EsSupervisor;

        IReadOnlyList<int>? temaIds = null;
        Guid? tecnicoId = null;
        if (esTecnico && currentUser.UserId is Guid uid)
        {
            temaIds = await usuarioRepo.GetTemaIdsAsync(uid, ct);
            tecnicoId = uid;
        }

        Data = await sender.Send(new GetResumenQuery(temaIds, tecnicoId), ct);

        // Misma cartera por defecto que /Tableros/Ejecutivo: el resumen no puede decir otros números
        // que el tablero al que enlaza.
        if (await acceso.PuedeClaveAsync("Proyectos.Ver", ct))
        {
            var contexto = await EjecutivoProyectosContexto.ResolverAsync(
                sender, currentUser, config, institucion.Value,
                areaPedida: null, areaExplicita: false, categoriaPedida: null,
                conOpciones: false, ct);

            Ejecutivo       = await sender.Send(new GetEjecutivoProyectosQuery(contexto.AreaId), ct);
            EjecutivoTitulo = contexto.Titulo;
        }

        Tableros = await TablerosCatalogo.VisiblesAsync(acceso, currentUser, ct);
    }
}
