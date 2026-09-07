namespace Diger.TramitesEstado.Application.Common.Models;

/// <summary>
/// Política de asignación del módulo de tickets de soporte, configurable vía appsettings
/// (sección "Soporte:Asignacion"). Los dos modos son independientes y pueden coexistir con el
/// "Tomar" del técnico; se leen tanto en la capa Web (mostrar controles) como en los handlers
/// (hacer valer la política en servidor, no solo esconderla en la UI).
/// </summary>
public sealed class SoporteOptions
{
    public AsignacionOptions Asignacion { get; init; } = new();

    public sealed class AsignacionOptions
    {
        /// <summary>Feature A: el usuario que crea el ticket elige categoría/operador de soporte.</summary>
        public bool ManualEnCreacion { get; init; }

        /// <summary>Con <see cref="ManualEnCreacion"/> activo, exige que el creador elija operador.
        /// Si es <c>false</c>, elegirlo es opcional (queda sin asignar cuando no lo elige).</summary>
        public bool OperadorObligatorio { get; init; }

        /// <summary>Feature B: habilita la asignación/reasignación por un administrador central
        /// (quien tenga el permiso <c>Tickets.Asignacion</c>).</summary>
        public bool AdministradorCentral { get; init; }

        /// <summary>Mantiene disponible el "Tomar" (auto-asignación del técnico). Por defecto sí:
        /// el admin central y la auto-asignación coexisten salvo que se apague explícitamente.</summary>
        public bool PermitirAutoAsignacion { get; init; } = true;
    }
}
