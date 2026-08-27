namespace Diger.TramitesEstado.Presentation.Security;

/// <summary>
/// Carga el catálogo de roles en memoria al arrancar, antes de atender peticiones.
/// CurrentUserService (Infrastructure) lo necesita para construirse — sin esto, el host
/// Presentation ni siquiera arranca. Espejo minimalista de Web/Security/RolCatalogoLoader.cs;
/// Presentation no puede referenciar Web (son dos hosts independientes), así que se duplica
/// a propósito en vez de mover el original a un proyecto compartido.
/// </summary>
public sealed class RolCatalogoLoader(IRolCatalogo catalogo) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => catalogo.RecargarAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
