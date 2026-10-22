namespace Diger.TramitesEstado.Application.Notificaciones;

public sealed record InterruptoresModulo(bool Campana, bool Correo, bool Agrupar)
{
    /// <summary>Lo que rige para un módulo sin fila: campana sí, correo no, agrupar sí.</summary>
    public static readonly InterruptoresModulo PorDefecto = new(true, false, true);
}

public sealed record AjustesVigentes(
    int IntervaloMinutos,
    IReadOnlyDictionary<ModuloNotificacion, InterruptoresModulo> Modulos,
    DateTime? UltimaRevisionUtc)
{
    public InterruptoresModulo De(ModuloNotificacion modulo) =>
        Modulos.TryGetValue(modulo, out var i) ? i : InterruptoresModulo.PorDefecto;
}

/// <summary>El correo saliente que rige ahora: el del panel o, si nunca se guardó, el de appsettings.</summary>
/// <param name="PasswordIlegible">Hay una contraseña guardada pero este servidor no la puede descifrar.</param>
public sealed record SmtpVigente(
    string Host, int Puerto, bool Ssl, string Usuario, string Password,
    string Remitente, string NombreRemitente, int TimeoutSegundos,
    bool DesdePanel, bool HayPassword, bool PasswordIlegible)
{
    /// <summary>Mismo criterio que <c>SmtpSettings.EstaConfigurado</c>, más la contraseña ilegible.</summary>
    public bool EstaConfigurado =>
        !PasswordIlegible &&
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(Usuario) &&
        !Usuario.Contains("ejemplo", StringComparison.OrdinalIgnoreCase);

    /// <summary>Un record imprime todas sus propiedades; este no puede imprimir la contraseña.</summary>
    public override string ToString() => $"SmtpVigente {{ Host = {Host}, Usuario = {Usuario}, Password = *** }}";
}

/// <summary>Los ajustes de notificaciones vigentes, con caché corta. <see cref="Invalidar"/> se llama al guardar.</summary>
public interface IAjustesNotificaciones
{
    Task<AjustesVigentes> ObtenerAsync(CancellationToken ct = default);
    Task<SmtpVigente>     SmtpAsync(CancellationToken ct = default);
    void                  Invalidar();
}

/// <summary>Cifra y descifra secretos guardados en la base. <see cref="Descifrar"/> devuelve null
/// si no se puede (claves de Data Protection perdidas): nunca lanza.</summary>
public interface IProtectorSecretos
{
    string  Cifrar(string texto);
    string? Descifrar(string cifrado);
}

/// <summary>
/// Despierta al revisor automático. Guardar el intervalo la dispara: así un cambio de 4 h a 15 min
/// aplica ya, y no al terminar la espera de 4 h que estaba en curso.
/// </summary>
public sealed class SenalRevision
{
    private readonly SemaphoreSlim _senal = new(0, 1);

    public void Disparar()
    {
        try { if (_senal.CurrentCount == 0) _senal.Release(); }
        catch (SemaphoreFullException) { /* ya estaba disparada */ }
    }

    /// <returns>true si la despertaron; false si se cumplió el tiempo.</returns>
    public Task<bool> EsperarAsync(TimeSpan tiempo, CancellationToken ct) => _senal.WaitAsync(tiempo, ct);
}
