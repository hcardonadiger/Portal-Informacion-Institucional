namespace Diger.TramitesEstado.Application.Expedientes.Common;

// ── DTOs hijos ────────────────────────────────────────────────────────────
public sealed record TramiteInput(
    int     TramiteIndex,
    string  NombreTramite,
    string? NombreCorto,
    string? AreaResponsable,
    string? Modalidad,
    string? PlazoLegal,
    string? Tercero,
    string? TiempoReal,
    string? MetodoPago,
    string? PagoBanco,
    string? PagoCuenta,
    string? TgrInst,
    string? TgrRubro,
    string? TgrMonto,
    string? DocEntregado,
    string? Objetivo,
    string? Alcance,
    string? AlcanceObs,
    string? Descripcion,
    string? Dirigido,
    string? Horario,
    string? Telefono,
    string? EmailTramite,
    string? SitioWeb);

public sealed record RequisitoInput(
    int TramiteIndex, int Orden, string Requisito, string? Obs,
    AccionRequisito? Accion, string? Justificacion);

public sealed record FlujoNodoInput(
    int TramiteIndex, FaseFlujo Fase, int Orden, TipoNodoFlujo Tipo,
    string? Titulo, string? Area, string? Tiempo, string? DocEmitido,
    string? Obs, string? RetornoA);

public sealed record LegalInput(int Orden, string Instrumento, string? Articulos, string? Obs);

public sealed record DocSolicitadoInput(
    int Orden, string Nombre, string? Tipo, bool Recibido, DateOnly? Fecha, string? Url);

public sealed record DocInternoInput(int Orden, string Documento, string? Area, string? Obs);

public sealed record PerfilInput(string Perfil, string? Nombre, string? Correo);

public sealed record ChecklistInput(int Orden, string Grupo, string Requisito, InfraStatus Status, string? Obs);

public sealed record SeccionInput(int Seccion, EstadoSeccion Estado, string? Nota);

// ── DTO raíz de entrada ───────────────────────────────────────────────────
public sealed record ExpedienteInputDto(
    string   InstitucionId,
    string   Institucion,
    DateOnly? FechaApertura,
    string   Analista,
    string?  DirSede,
    int      NumTramitesProd,
    string?  ContactoNombre,
    string?  ContactoCargo,
    string?  ContactoCorreo,
    string?  ContactoTel,
    string?  ObsLegal,
    int?     NumFuncionarios,
    int?     VolumenAnual,
    string?  TiempoObservado,
    string?  TiempoNorma,
    string?  DescProceso,
    string?  DocsAdicionales,
    string?  ObsFlujo,
    int?     FuncionariosDig,
    string?  TiempoDig,
    string?  ObsModelo,
    string?  InfraPersonal,
    int?     InfraPersonalTI,
    string?  InfraRespSol,
    string?  InfraAcomp,
    string?  InfraDcModalidad,
    string?  InfraDcVirt,
    string?  InfraDcVirtOtro,
    string?  InfraDcDisp,
    string?  InfraDcObs,
    string?  InfraPlan,
    EstadoExpediente        EstadoExpediente,
    EstadoLevantamientoExp? EstadoLevantamiento,
    string?  ObsExpediente,
    string?  ObsLevantamiento,
    string?  ValidadoDiger,
    string?  ValidadoInst,
    DateOnly? FechaValidacion,
    string?  NumActa,
    List<TramiteInput>        Tramites,
    List<RequisitoInput>      Requisitos,
    List<FlujoNodoInput>      Flujos,
    List<LegalInput>          Legal,
    List<DocSolicitadoInput>  DocsSolicitados,
    List<DocInternoInput>     DocsInternos,
    List<PerfilInput>         Perfiles,
    List<string>              Condiciones,
    List<ChecklistInput>      ChecklistInfra,
    List<SeccionInput>        Secciones);
