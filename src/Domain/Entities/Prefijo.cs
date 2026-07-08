namespace Diger.TramitesEstado.Domain.Entities;

public sealed class Prefijo
{
    public string PrefijoInstitucion { get; private set; } = default!;
    public string PrefijoMovimiento  { get; private set; } = default!;
    public int    UltimoValor        { get; private set; }
    public string? UltimoCodigo      { get; private set; }

    private Prefijo() { }

    public static Prefijo Crear(string prefijoInstitucion, string prefijoMovimiento)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefijoInstitucion);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefijoMovimiento);

        return new Prefijo
        {
            PrefijoInstitucion = prefijoInstitucion.Trim().ToUpper(),
            PrefijoMovimiento  = prefijoMovimiento.Trim().ToUpper(),
            UltimoValor        = 0
        };
    }
}
