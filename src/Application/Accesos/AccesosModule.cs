using Diger.TramitesEstado.Application.Common.Exceptions;

namespace Diger.TramitesEstado.Application.Accesos;

/// <summary>
/// Catálogo (fijo) de módulos/opciones del portal para el control de accesos por rol.
/// Los módulos "configurables" se administran en la matriz rol × módulo; los "solo-admin"
/// son de infraestructura y siempre requieren Administrador.
/// </summary>
public static class ModulosPortal
{
    // Configurables (matriz)
    public const string Tableros    = "Tableros";
    public const string Calendario  = "Calendario";
    public const string Expedientes = "Expedientes";
    public const string Reuniones   = "Reuniones";
    public const string Tickets     = "Tickets";
    public const string Contactos   = "Contactos";
    // Solo-admin (infraestructura)
    public const string Instituciones = "Instituciones";
    public const string Usuarios      = "Usuarios";
    public const string Accesos       = "Accesos";

    public static readonly IReadOnlyList<(string Clave, string Nombre)> Configurables =
    [
        (Tableros,    "Tableros"),
        (Calendario,  "Calendario"),
        (Expedientes, "Expedientes"),
        (Reuniones,   "Reuniones y compromisos"),
        (Tickets,     "Tickets"),
        (Contactos,   "Contactos"),
    ];

    public static readonly IReadOnlyList<string> SoloAdmin = [Instituciones, Usuarios, Accesos];

    public static bool EsConfigurable(string clave) => Configurables.Any(m => m.Clave == clave);
    public static bool EsSoloAdmin(string clave) => SoloAdmin.Contains(clave);
    public static string Nombre(string clave) =>
        Configurables.FirstOrDefault(m => m.Clave == clave).Nombre ?? clave;

    /// <summary>Roles cuyo acceso es configurable (Administrador siempre tiene todo).</summary>
    public static readonly IReadOnlyList<RolUsuario> RolesConfigurables = [RolUsuario.Coordinador, RolUsuario.Tecnico];
}

public sealed record RolAccesoDto(RolUsuario Rol, IReadOnlyList<string> Modulos);

// ── Query: matriz de accesos configurables ────────────────────────────────
public sealed record GetAccesosQuery : IRequest<IReadOnlyList<RolAccesoDto>>;

public sealed class GetAccesosQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetAccesosQuery, IReadOnlyList<RolAccesoDto>>
{
    public async Task<IReadOnlyList<RolAccesoDto>> Handle(GetAccesosQuery q, CancellationToken ct)
    {
        var grants = await ctx.RolModuloAccesos.ToListAsync(ct);
        return ModulosPortal.RolesConfigurables
            .Select(r => new RolAccesoDto(r,
                grants.Where(g => g.Rol == r && ModulosPortal.EsConfigurable(g.Modulo))
                      .Select(g => g.Modulo).ToList()))
            .ToList();
    }
}

// ── Command: reemplaza los accesos de un rol (solo configurables) ──────────
public sealed record GuardarAccesosCommand(RolUsuario Rol, IReadOnlyList<string> Modulos) : IRequest<Unit>;

public sealed class GuardarAccesosCommandHandler(IApplicationDbContext ctx)
    : IRequestHandler<GuardarAccesosCommand, Unit>
{
    public async Task<Unit> Handle(GuardarAccesosCommand cmd, CancellationToken ct)
    {
        if (!ModulosPortal.RolesConfigurables.Contains(cmd.Rol))
            throw new DomainException("El acceso del Administrador no es configurable.");

        var validos = (cmd.Modulos ?? [])
            .Where(ModulosPortal.EsConfigurable)
            .Distinct()
            .ToList();

        var actuales = await ctx.RolModuloAccesos.Where(g => g.Rol == cmd.Rol).ToListAsync(ct);
        ctx.RolModuloAccesos.RemoveRange(actuales);
        foreach (var m in validos)
            ctx.RolModuloAccesos.Add(RolModuloAcceso.Crear(cmd.Rol, m));

        await ctx.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
