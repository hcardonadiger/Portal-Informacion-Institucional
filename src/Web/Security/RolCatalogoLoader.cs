namespace Diger.TramitesEstado.Web.Security;

/// <summary>
/// Carga el catálogo de roles en memoria al arrancar, antes de atender peticiones.
/// Debe registrarse ANTES de PermissionCatalogSyncService: los filtros RLS y el bypass
/// de administrador dependen de este catálogo desde el primer request.
/// </summary>
public sealed class RolCatalogoLoader(IRolCatalogo catalogo) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => catalogo.RecargarAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
