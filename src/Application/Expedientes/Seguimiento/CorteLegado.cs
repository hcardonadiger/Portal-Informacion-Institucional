namespace Diger.TramitesEstado.Application.Expedientes.Seguimiento;

/// <summary>
/// Corte que separa los expedientes "legado" (importados del portal anterior, sin
/// seguimiento confiable en la nueva metodología de digitalización) de los "nuevos",
/// que sí cuentan en los tableros. Un expediente sin <c>FechaApertura</c> se trata
/// como legado por defecto, ya que no hay forma de saber cuándo se abrió.
/// </summary>
public static class CorteLegado
{
    public static readonly DateOnly Fecha = new(2026, 4, 1);

    public static bool EsLegado(DateOnly? fechaApertura) => fechaApertura is null || fechaApertura < Fecha;
}
