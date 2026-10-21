namespace Diger.TramitesEstado.Application.PlanTrabajo.Common;

public sealed record MetaTramiteDto(
    int       Id,
    int       Orden,
    string    NombreTramite,
    DateOnly? FechaEstimadaInicio,
    DateOnly? FechaEstimadaFin,
    DateOnly? FechaRealFin,
    Guid?     ResponsableId,
    string?   Responsable,
    EstadoMeta Estado,
    string?   Observaciones,
    int?      ExpedienteId,
    string?   ExpedienteCodigo,
    EstadoExpediente? EstadoExpedienteVinculado,
    int?      ExpedienteTramiteIndex,
    string?   ExpedienteTramiteNombre)
{
    public bool EstaVencida =>
        FechaEstimadaFin.HasValue && FechaEstimadaFin < DateOnly.FromDateTime(DateTime.UtcNow)
        && Estado is not (EstadoMeta.Cumplida or EstadoMeta.Cancelada);
}

public sealed record PlanTrabajoListItemDto(
    int   Id,
    string InstitucionId,
    string Institucion,
    int   Anio,
    EstadoPlanTrabajo Estado,
    int   TotalMetas,
    int   MetasCumplidas,
    int   MetasVencidas,
    DateTime CreatedAt)
{
    public int CumplimientoPct => TotalMetas > 0 ? MetasCumplidas * 100 / TotalMetas : 0;
}

public sealed record PlanTrabajoDetailDto(
    int   Id,
    string InstitucionId,
    string Institucion,
    int   Anio,
    EstadoPlanTrabajo Estado,
    string? Observaciones,
    Guid?   AprobadoPorId,
    DateOnly? FechaAprobacion,
    DateTime CreatedAt,
    IReadOnlyList<MetaTramiteDto> Metas)
{
    public int TotalMetas      => Metas.Count;
    public int MetasCumplidas  => Metas.Count(m => m.Estado == EstadoMeta.Cumplida);
    public int MetasEnProgreso => Metas.Count(m => m.Estado == EstadoMeta.EnProgreso);
    public int MetasVencidas   => Metas.Count(m => m.EstaVencida);
    public int CumplimientoPct => TotalMetas > 0 ? MetasCumplidas * 100 / TotalMetas : 0;
}
