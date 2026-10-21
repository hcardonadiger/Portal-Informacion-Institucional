using Diger.TramitesEstado.Domain.Common;

namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Rol administrable desde /Accesos/Roles. El Id es el código del rol ("JefeArea"),
/// el mismo string que viaja en el claim "diger:rol" y que se guarda en
/// AsignacionUsuario.Rol y RolPermiso.RolId — por eso no se puede cambiar después
/// de creado (renombrarlo dejaría huérfanas las asignaciones y las concesiones).
///
/// NivelAlcance sustituye las ramas por nombre de rol que tenían los filtros RLS;
/// las cuatro capacidades booleanas sustituyen los chequeos hardcodeados que había
/// repartidos por el código (bypass de Administrador, bloqueo de escritura del
/// Consultor, "es jefe" y "es técnico de soporte").
/// </summary>
public sealed class Rol : BaseAuditableEntity<string>
{
    public string       Nombre           { get; private set; } = default!;
    public string?      Descripcion      { get; private set; }
    public string?      Color            { get; private set; }
    public NivelAlcance NivelAlcance     { get; private set; }
    public bool         EsAdministrador  { get; private set; }
    public bool         EsSoloLectura    { get; private set; }
    public bool         EsSupervisor     { get; private set; }
    public bool         EsTecnicoSoporte { get; private set; }
    public bool         EsJefeDeArea     { get; private set; }
    public bool         EsPmo            { get; private set; }
    public bool         Activo           { get; private set; } = true;

    /// <summary>Rol base del sistema: no se puede eliminar (sí ajustar capacidades).</summary>
    public bool EsSistema { get; private set; }

    private Rol() { }

    public static Rol Crear(
        string codigo,
        string nombre,
        NivelAlcance nivelAlcance,
        string? descripcion = null,
        string? color = null,
        bool esAdministrador = false,
        bool esSoloLectura = false,
        bool esSupervisor = false,
        bool esTecnicoSoporte = false,
        bool esSistema = false,
        bool esJefeDeArea = false,
        bool esPmo = false)
    {
        ValidarCodigo(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        if (esAdministrador && esSoloLectura)
            throw new DomainException("Un rol administrador no puede ser de solo lectura.");

        return new Rol
        {
            Id = codigo.Trim(),
            Nombre = nombre.Trim(),
            Descripcion = descripcion?.Trim(),
            Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim(),
            NivelAlcance = nivelAlcance,
            EsAdministrador = esAdministrador,
            EsSoloLectura = esSoloLectura,
            EsSupervisor = esSupervisor,
            EsTecnicoSoporte = esTecnicoSoporte,
            EsJefeDeArea = esJefeDeArea,
            EsPmo = esPmo,
            EsSistema = esSistema,
            Activo = true
        };
    }

    private static void ValidarCodigo(string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        var limpio = codigo.Trim();

        if (limpio.Length > 60)
            throw new DomainException("El código del rol no puede exceder 60 caracteres.");

        // Viaja como claim y como valor de columna en varias tablas: se restringe a
        // caracteres seguros para evitar sorpresas al compararlo o al serializarlo.
        if (!limpio.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
            throw new DomainException("El código del rol solo puede contener letras, números, guiones (-) y guiones bajos (_), sin espacios.");
    }

    public void Actualizar(
        string nombre,
        NivelAlcance nivelAlcance,
        string? descripcion,
        string? color,
        bool esAdministrador,
        bool esSoloLectura,
        bool esSupervisor,
        bool esTecnicoSoporte,
        bool esJefeDeArea,
        bool esPmo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        if (esAdministrador && esSoloLectura)
            throw new DomainException("Un rol administrador no puede ser de solo lectura.");

        Nombre = nombre.Trim();
        NivelAlcance = nivelAlcance;
        Descripcion = descripcion?.Trim();
        Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
        EsAdministrador = esAdministrador;
        EsSoloLectura = esSoloLectura;
        EsSupervisor = esSupervisor;
        EsTecnicoSoporte = esTecnicoSoporte;
        EsJefeDeArea = esJefeDeArea;
        EsPmo = esPmo;
    }

    public void Activar()    => Activo = true;
    public void Desactivar() => Activo = false;
}
