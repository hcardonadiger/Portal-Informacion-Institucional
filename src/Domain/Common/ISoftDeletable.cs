namespace Diger.TramitesEstado.Domain.Common;

/// <summary>
/// Marca una entidad de dominio como "borrables de forma lógica".
/// El AppDbContext intercepta EntityState.Deleted y en lugar de eliminar el registro,
/// pone IsDeleted = true. Un Global Query Filter en OnModelCreating lo excluye de todas
/// las consultas automáticamente.
/// COMPATIBILIDAD CON ESCALABILIDAD: El filtro de RLS (InstitucionId / AreaId / UnidadId)
/// se fusionará con !IsDeleted en AppDbContext; no definir filtros redundantes aquí.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
