using System.Text.RegularExpressions;
using Diger.TramitesEstado.Domain.Common;

namespace Diger.TramitesEstado.Domain.Entities;

public sealed class Institucion : BaseAuditableEntity<string>
{
    public string Nombre      { get; private set; } = default!;
    public string? Descripcion { get; private set; }
    public string? NombreCorto { get; private set; }
    public string? LogoUrl     { get; private set; }
    public string? Color       { get; private set; }
    public string? InfoExtra   { get; private set; }
    public bool   Activo      { get; private set; } = true;

    // ── Contacto institucional (ficha pública) ─────────────────────────────
    public string? Telefono  { get; private set; }
    public string? SitioWeb  { get; private set; }
    public string? Direccion { get; private set; }
    public string? Horario   { get; private set; }
    public string? Tipo      { get; private set; }

    // ── Dirección en SOL (D-04, D-20) ──────────────────────────────────────

    /// <summary>
    /// La ruta de esta institución dentro de SOL, cuando la real difiere de su llave.
    ///
    /// <b>Nula significa «nadie la ha corregido»</b>, y entonces vale la llave primaria. No se
    /// copia el Id acá al crear la institución, aunque sería más simple de leer, por la misma
    /// razón que en la Fase 5 no se guardó una bandera de autollenado: un valor copiado se
    /// convierte en mentira en cuanto cambia el original, y además borra la diferencia entre
    /// «nadie lo tocó» y «alguien decidió que fuera igual a la llave». Resolverlo por
    /// <see cref="RutaSolEfectiva"/> no se desactualiza nunca.
    /// </summary>
    public string? RutaSol { get; private set; }

    /// <summary>La ruta que de verdad se usa para componer direcciones: la corregida si existe,
    /// la llave si no. Nunca vacía, porque la llave nunca lo está.</summary>
    public string RutaSolEfectiva => RutaSol ?? Id;

    private readonly List<TramiteDefinicion> _tramites = [];
    public IReadOnlyCollection<TramiteDefinicion> Tramites => _tramites.AsReadOnly();

    private Institucion() { }

    public static Institucion Crear(string id, string nombre, string? descripcion = null, string? nombreCorto = null, string? logoUrl = null, string? infoExtra = null)
    {
        ValidarId(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        
        return new Institucion
        {
            Id = id.Trim().ToUpper(),
            Nombre = nombre.Trim(),
            Descripcion = descripcion?.Trim(),
            NombreCorto = nombreCorto?.Trim().ToUpper(),
            LogoUrl = logoUrl?.Trim(),
            InfoExtra = infoExtra?.Trim(),
            Activo = true 
        };
    }

    private static void ValidarId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Regex.IsMatch(id.Trim(), @"^[A-Z0-9\-_]+$"))
        {
            throw new DomainException("El Id de la Institución solo puede contener letras mayúsculas, números, guiones (-) y guiones bajos (_), sin espacios.");
        }
    }

    public void Renombrar(string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        Nombre = nombre.Trim();
    }

    public void ActualizarDetalles(string? descripcion, string? nombreCorto, string? logoUrl, string? infoExtra, string? color = null)
    {
        Descripcion = descripcion?.Trim();
        NombreCorto = nombreCorto?.Trim().ToUpper();
        LogoUrl = logoUrl?.Trim();
        InfoExtra = infoExtra?.Trim();
        Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
    }

    /// <summary>Datos de contacto para la ficha pública. Rechaza un sitio web que no sea
    /// una URL absoluta http(s) — misma regla que valida el trámite individual.</summary>
    public void RegistrarContacto(string? telefono, string? sitioWeb, string? direccion, string? horario, string? tipo)
    {
        var sitioWebLimpio = string.IsNullOrWhiteSpace(sitioWeb) ? null : sitioWeb.Trim();
        if (sitioWebLimpio is not null && !sitioWebLimpio.StartsWith("http://") && !sitioWebLimpio.StartsWith("https://"))
            throw new DomainException("El sitio web debe ser una URL absoluta (http:// o https://).");

        Telefono  = string.IsNullOrWhiteSpace(telefono)  ? null : telefono.Trim();
        SitioWeb  = sitioWebLimpio;
        Direccion = string.IsNullOrWhiteSpace(direccion) ? null : direccion.Trim();
        Horario   = string.IsNullOrWhiteSpace(horario)   ? null : horario.Trim();
        Tipo      = string.IsNullOrWhiteSpace(tipo)      ? null : tipo.Trim();
    }

    /// <summary>
    /// Corrige la ruta de esta institución en SOL. Vacío la devuelve a su valor por defecto —la
    /// llave— en vez de dejarla en blanco, para que borrar el campo en el formulario signifique
    /// «vuelve a lo de siempre» y no «esta institución se queda sin ruta».
    /// </summary>
    /// <exception cref="DomainException">Si la ruta no puede ir dentro de una dirección. Se
    /// rechaza en vez de escaparse: un espacio o un acento acá es un descuido de captura, y
    /// escaparlo produciría enlaces que se ven bien y llevan a un 404 que nadie nota.</exception>
    public void FijarRutaSol(string? ruta)
    {
        var limpia = DireccionSol.Normalizar(ruta);

        if (limpia is not null && !DireccionSol.EsSegmentoValido(limpia))
            throw new DomainException(
                "La ruta de SOL solo puede llevar letras, números, guiones (-) y guiones bajos (_), " +
                "separando tramos con barra (/). Sin espacios, tildes ni la dirección completa.");

        // Igual a la llave es igual a no haberla corregido. Guardarlo como valor propio haría
        // que una institución renombrada arrastrara la ruta vieja para siempre.
        RutaSol = string.Equals(limpia, Id, StringComparison.OrdinalIgnoreCase) ? null : limpia;
    }

    public void Activar()    => Activo = true;
    public void Desactivar() => Activo = false;

    public void AgregarTramite(TramiteDefinicion t)
    {
        ArgumentNullException.ThrowIfNull(t);
        _tramites.Add(t);
    }

    public void LimpiarTramites() => _tramites.Clear();
}

public sealed class TramiteDefinicion : BaseEntity
{
    public string InstitucionId { get; private set; } = default!;
    public string Nombre        { get; private set; } = default!;
    public int    Orden         { get; private set; }

    private TramiteDefinicion() { }

    public static TramiteDefinicion Crear(string institucionId, string nombre, int orden) =>
        new() { InstitucionId = institucionId, Nombre = nombre.Trim(), Orden = orden };
}
