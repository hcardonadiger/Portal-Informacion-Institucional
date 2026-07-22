namespace Diger.TramitesEstado.Application.Informes.Common;

public sealed record InformeTramiteDto(
    int    TramiteIndex,
    string NombreTramite,
    int    TotalPasos,
    int    PasosCompletados)
{
    public int AvancePct => TotalPasos > 0 ? PasosCompletados * 100 / TotalPasos : 0;
}

public sealed record InformeExpedienteDto(
    int              Id,
    string           Codigo,
    EstadoExpediente Estado,
    string           Analista,
    DateOnly?        FechaApertura,
    DateTime         CreatedAt,
    IReadOnlyList<InformeTramiteDto> Tramites)
{
    public int AvancePct => Tramites.Count > 0
        ? Tramites.Sum(t => t.AvancePct) / Tramites.Count
        : 0;
}

public sealed record InformeInstitucionDto(
    string   InstitucionId,
    string   InstitucionNombre,
    DateOnly? Desde,
    DateOnly? Hasta,
    IReadOnlyList<InformeExpedienteDto> Expedientes)
{
    public int TotalTramites   => Expedientes.Sum(e => e.Tramites.Count);
    public int AvancePromedio  => Expedientes.Count > 0 ? Expedientes.Sum(e => e.AvancePct) / Expedientes.Count : 0;
    public int Cerrados        => Expedientes.Count(e => e.Estado == EstadoExpediente.Cerrado);
    public int EnValidacion    => Expedientes.Count(e => e.Estado == EstadoExpediente.EnValidacion);
    public int EnModelado      => Expedientes.Count(e => e.Estado == EstadoExpediente.EnModelado);
    public int EnLevantamiento => Expedientes.Count(e => e.Estado == EstadoExpediente.EnLevantamiento);
    public int EnExploracion   => Expedientes.Count(e => e.Estado == EstadoExpediente.EnExploracion);
}
