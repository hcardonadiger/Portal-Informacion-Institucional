using Microsoft.EntityFrameworkCore;

namespace Diger.TramitesEstado.Web.Common;

/// <summary>
/// Resuelve a qué módulos del portal puede acceder el usuario actual. Lo usa el navbar para
/// decidir qué enlaces mostrar.
///
/// Ya no consulta RolModuloAccesos: la fuente de verdad es la matriz rol×permiso, y "puede
/// entrar al módulo X" se traduce a "tiene la clave X.Ver" — la misma que exige
/// PermissionPageFilter en la página de destino. Antes eran dos mecanismos separados que
/// podían contradecirse: el navbar escondía un módulo al que la URL directa sí dejaba entrar,
/// o al revés. Ahora la respuesta es una sola.
///
/// Sigue siendo solo una ayuda de UI: el bloqueo real lo hace PermissionPageFilter en cada
/// handler, no este servicio.
/// </summary>
public sealed class AccesoModulosService(
    ICurrentUserService currentUser, IPermissionCache permisos, IApplicationDbContext ctx)
{
    private HashSet<string>? _claves;

    public bool EsAdministrador => currentUser.EsGlobal;

    private bool? _esSoporte;

    /// <summary>¿El usuario actual atiende el chat de soporte? Lo es quien tenga la capacidad
    /// EsTecnicoSoporte en su rol o al menos un tema asignado (<c>UsuarioTemas</c>). Solo
    /// controla la visibilidad del enlace del navbar. Se resuelve una vez por request.</summary>
    public async Task<bool> EsSoporteAsync(CancellationToken ct = default)
    {
        if (_esSoporte is bool cache) return cache;
        if (EsAdministrador || currentUser.EsTecnicoSoporte) return (_esSoporte = true).Value;
        var uid = currentUser.UserId;
        if (uid is null) return (_esSoporte = false).Value;
        _esSoporte = await ctx.UsuarioTemas.AnyAsync(ut => ut.UsuarioId == uid.Value, ct);
        return _esSoporte.Value;
    }

    /// <summary>¿Puede ver el módulo? Equivale a tener la clave "<paramref name="modulo"/>.Ver".</summary>
    public Task<bool> PuedeAsync(string modulo, CancellationToken ct = default) =>
        PuedeClaveAsync($"{modulo}.{AccionModulo.Ver}", ct);

    /// <summary>
    /// ¿Tiene esta clave concreta ("Tickets.Eliminar")? Es la misma pregunta que responde
    /// PermissionPageFilter antes de entrar al handler, pero disponible ANTES de pintar la
    /// pantalla: sirve para no mostrar un botón que al hacer clic va a dar Forbidden.
    ///
    /// Es solo una ayuda de UI — quien decide sigue siendo el filtro del servidor. Esconder
    /// un botón no protege nada por sí solo; lo que evita es prometerle al usuario una acción
    /// que no puede hacer.
    /// </summary>
    public async Task<bool> PuedeClaveAsync(string clave, CancellationToken ct = default)
    {
        if (EsAdministrador) return true;

        var rolId = currentUser.Rol;
        if (string.IsNullOrWhiteSpace(rolId)) return false;

        _claves ??= await permisos.ObtenerAsync(rolId, ct);
        return _claves.Contains(clave);
    }

    /// <summary>Atajo para los dos casos más comunes al pintar una lista.</summary>
    public Task<bool> PuedeEditarAsync(string modulo, CancellationToken ct = default) =>
        PuedeClaveAsync($"{modulo}.{AccionModulo.Editar}", ct);

    public Task<bool> PuedeEliminarAsync(string modulo, CancellationToken ct = default) =>
        PuedeClaveAsync($"{modulo}.{AccionModulo.Eliminar}", ct);
}
