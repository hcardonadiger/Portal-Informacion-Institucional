using Diger.TramitesEstado.Application.Common.Interfaces;
using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Application.Tests.Siger;

/// <summary>
/// Un usuario con alcance global, para las pruebas que necesitan ver todo el inventario.
/// </summary>
/// <remarks>
/// Vivía dentro de las pruebas de la API pública. Cuando la API se separó a su propio proyecto,
/// esas pruebas se fueron con ella —y esta clase no, porque quienes la usan son las pruebas de
/// importación y promoción, que se quedan en PortalDigital.
/// </remarks>
internal sealed class FakeGlobalCurrentUser : ICurrentUserService
{
    public Guid?       UserId               => Guid.NewGuid();
    public string?     Nombre               => "test";
    public string?     Correo               => "test@diger.gob.hn";
    public string?     Rol                  => "Administrador";
    public bool        IsAuthenticated      => true;
    public bool        EsGlobal             => true;
    public NivelAlcance NivelAlcance        => NivelAlcance.Global;
    public bool        EsSoloLectura        => false;
    public bool        EsSupervisor         => true;
    public bool        EsTecnicoSoporte     => true;
    public string?     ActiveInstitucionId  => null;
    public string?     ActiveAreaId         => null;
    public string?     ActiveUnidadId       => null;
    public IReadOnlyCollection<string> InstitucionesAsignadas => [];
    public bool        PuedeAccederInstitucion(string? institucionId) => true;
}
