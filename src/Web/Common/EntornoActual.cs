namespace Diger.TramitesEstado.Web.Common;

/// <summary>Qué base de datos está sirviendo la aplicación en este momento.</summary>
/// <remarks>
/// <para>
/// Existe porque en el mismo servidor conviven <c>TramitesEstado_Ensayo</c> y
/// <c>TramitesEstado_Prod</c>, y las dos se ven idénticas desde la pantalla. Sin un cartel
/// visible, la única manera de saber dónde está uno parado es abrir un archivo de configuración
/// —y para cuando uno lo abre, ya pudo haber escrito.
/// </para>
/// <para>
/// Devuelve solo el nombre de la base. La cadena de conexión lleva credenciales y nunca sale de
/// acá completa.
/// </para>
/// </remarks>
public static class EntornoActual
{
    private const string NombreProduccion = "Prod";

    public static string BaseDeDatos(IConfiguration config)
    {
        var cadena = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cadena)) return "sin configurar";

        foreach (var parte in cadena.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = parte.Split('=', 2);
            if (kv.Length != 2) continue;

            var clave = kv[0].Trim();
            if (clave.Equals("Database", StringComparison.OrdinalIgnoreCase) ||
                clave.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase))
                return kv[1].Trim();
        }

        return "desconocida";
    }

    /// <summary>Cierto cuando la base apunta a producción. Es lo que decide si el cartel avisa
    /// en rojo: da igual el nombre del entorno de ASP.NET si la cadena apunta a los datos reales.</summary>
    public static bool EsProduccion(IConfiguration config) =>
        BaseDeDatos(config).Contains(NombreProduccion, StringComparison.OrdinalIgnoreCase);
}
