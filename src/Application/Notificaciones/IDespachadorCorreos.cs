namespace Diger.TramitesEstado.Application.Notificaciones;

/// <summary>Manda lo que espera en la bandeja de salida: un correo por persona.</summary>
public interface IDespachadorCorreos
{
    /// <returns>Cuántos correos salieron.</returns>
    Task<int> DespacharAsync(CancellationToken ct = default);
}
