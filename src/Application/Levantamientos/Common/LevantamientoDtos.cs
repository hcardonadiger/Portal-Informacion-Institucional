namespace Diger.TramitesEstado.Application.Levantamientos.Common;

public sealed record TramiteChecklistDto(
    int     Id,
    string  NombreTramite,
    int     Orden,
    bool    ActaFirmada,
    bool    RequiereMejoras,
    bool    TieneInstructivo,
    bool    Socializado,
    string? Observaciones
);

public sealed record MiembroEquipoDto(
    int     Id,
    string  Funcion,
    string  Nombre,
    string? Contacto,
    int     Orden
);

public sealed record DocumentoAdjuntoDto(
    int       Id,
    string    Nombre,
    string?   Tipo,
    string    Url,
    DateOnly? FechaDocumento,
    DateTime  FechaRegistro
);

public sealed record LevantamientoDetailDto(
    int                          Id,
    string                       Institucion,
    string                       Encargado,
    string?                      Correo,
    string?                      Celular,
    EstadoLevantamientoExp       Estado,
    string?                      ObsEstado,
    bool                         MigradaSOL,
    bool                         Limitante,
    string?                      LimitanteObs,
    bool                         Personal,
    string?                      PersonalObs,
    bool                         RequiereAcompanamiento,
    bool                         Habilidad,
    string?                      HabilidadObs,
    string?                      ObsGenerales,
    DateTime                     CreatedAt,
    DateTime?                    UpdatedAt,
    IReadOnlyList<TramiteChecklistDto> Tramites,
    IReadOnlyList<MiembroEquipoDto>    Equipo,
    IReadOnlyList<DocumentoAdjuntoDto> Documentos
);

public sealed record LevantamientoListItemDto(
    int                    Id,
    string                 Institucion,
    string                 Encargado,
    EstadoLevantamientoExp Estado,
    DateTime               CreatedAt,
    DateTime?              UpdatedAt
);
