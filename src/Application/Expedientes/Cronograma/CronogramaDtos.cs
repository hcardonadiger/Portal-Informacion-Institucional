using Diger.TramitesEstado.Application.Expedientes.Seguimiento;

namespace Diger.TramitesEstado.Application.Expedientes.Cronograma;

public sealed record EtapaCronogramaDto(
    string    EtapaNum,
    string    Label,
    double    Peso,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    DateOnly? FechaRealFin,
    string?   Responsable,
    string?   Observacion,
    double    AvancePct,
    // Solo en vista centralizada: true cuando los trámites tienen valores distintos
    // en esta etapa (los campos muestran únicamente los valores comunes a todos).
    bool      Variaciones = false
);

public sealed record CronogramaExpedienteDto(
    int    ExpedienteId,
    string Codigo,
    string Institucion,
    IReadOnlyList<TramiteItem> Tramites,
    int    TramiteActual,          // -1 en vista centralizada
    IReadOnlyList<EtapaCronogramaDto> Etapas,
    bool   EsCentralizado = false
);
