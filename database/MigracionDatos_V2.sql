USE DigerTramitesEstado_Nueva;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

PRINT '=======================================================';
PRINT 'INICIANDO MIGRACION DE DATOS DESDE BACKUP LOCAL (TramitesEstado_Temp)';
PRINT '=======================================================';

-- Hacemos limpieza de las tablas en la BD nueva para evitar duplicados si se corre varias veces
DELETE FROM TicketComentarios;
DELETE FROM TicketAdjuntos;
DELETE FROM TicketTramites;
DELETE FROM Tickets;
DELETE FROM Asistentes;
DELETE FROM AcuerdosReunion;
DELETE FROM Reuniones;
DELETE FROM Contactos;

DELETE FROM ExpedienteEtapaAvances;
DELETE FROM ExpedienteSecciones;
DELETE FROM ExpedienteTramites;
DELETE FROM FlujoNodos;
DELETE FROM FundamentosLegales;
DELETE FROM InfraChecklist;
DELETE FROM InfraCondiciones;
DELETE FROM InfraPerfiles;
DELETE FROM TramiteRequisitos;
DELETE FROM DocumentosInternos;
DELETE FROM DocumentosSolicitados;
DELETE FROM Expedientes;
DELETE FROM TramitesDefinicion;
DELETE FROM Instituciones;
DELETE FROM TemasTicket;
DELETE FROM CategoriasTicket;

-- ==========================================
-- 0. TABLAS TEMPORALES Y MAPEOS
-- ==========================================
PRINT '>> Creando tabla de mapeo de Instituciones...';
IF OBJECT_ID('tempdb..#InstMapping') IS NOT NULL DROP TABLE #InstMapping;
CREATE TABLE #InstMapping (
    OldId INT PRIMARY KEY,
    NewId NVARCHAR(120),
    Nombre NVARCHAR(120)
);

-- Generar IDs semánticos. 
-- Ej: "CANATURH / IHT" -> "CANATURHIHT", "ARSA" -> "ARSA"
INSERT INTO #InstMapping (OldId, NewId, Nombre)
SELECT 
    Id, 
    REPLACE(REPLACE(REPLACE(Nombre, ' ', ''), '/', ''), '.', ''),
    Nombre
FROM TramitesEstado_Temp.dbo.Instituciones;

-- ==========================================
-- 1. CATALOGOS BASICOS
-- ==========================================
PRINT '>> Migrando Catálogos Básicos...';

SET IDENTITY_INSERT CategoriasTicket ON;
INSERT INTO CategoriasTicket (Id, Nombre, Activo, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
SELECT Id, Nombre, Activo, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy 
FROM TramitesEstado_Temp.dbo.CategoriasTicket;
SET IDENTITY_INSERT CategoriasTicket OFF;

SET IDENTITY_INSERT TemasTicket ON;
INSERT INTO TemasTicket (Id, Nombre, HorasResolucion, Activo, CategoriaId, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
SELECT Id, Nombre, HorasResolucion, Activo, CategoriaId, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy 
FROM TramitesEstado_Temp.dbo.TemasTicket;
SET IDENTITY_INSERT TemasTicket OFF;

PRINT '>> Migrando Instituciones...';
-- Instituciones: Id usando el semántico
INSERT INTO Instituciones (Id, Nombre, Descripcion, NombreCorto, LogoUrl, InfoExtra, Activo, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
SELECT 
    m.NewId, 
    m.Nombre, 
    NULL, -- Descripcion
    NULL, -- NombreCorto
    NULL, -- LogoUrl
    NULL, -- InfoExtra
    i.Activo, 
    GETDATE(), -- CreatedAt
    NULL, 
    NULL, 
    NULL
FROM TramitesEstado_Temp.dbo.Instituciones i
JOIN #InstMapping m ON i.Id = m.OldId;

-- ==========================================
-- 2. USUARIOS Y AUTENTICACIÓN (SALTADO)
-- ==========================================
PRINT '>> Saltando migración de Usuarios...';

-- ==========================================
-- 3. EXPEDIENTES Y DATOS RELACIONADOS
-- ==========================================
PRINT '>> Migrando Expedientes...';

SET IDENTITY_INSERT TramitesDefinicion ON;
INSERT INTO TramitesDefinicion (Id, InstitucionId, Nombre, Orden)
SELECT 
    t.Id, 
    m.NewId, 
    t.Nombre, 
    t.Orden
FROM TramitesEstado_Temp.dbo.TramitesDefinicion t
JOIN #InstMapping m ON t.InstitucionId = m.OldId;
SET IDENTITY_INSERT TramitesDefinicion OFF;

SET IDENTITY_INSERT Expedientes ON;
INSERT INTO Expedientes (
    Id, Codigo, InstitucionId, AreaId, UnidadId, Institucion, OrigenExternoId, FechaApertura, Analista, 
    DirSede, NumTramitesProd, ContactoNombre, ContactoCargo, ContactoCorreo, ContactoTel, 
    ObsLegal, NumFuncionarios, VolumenAnual, TiempoObservado, TiempoNorma, DescProceso, 
    DocsAdicionales, ObsFlujo, FuncionariosDig, TiempoDig, ObsModelo, InfraPersonal, 
    InfraPersonalTI, InfraRespSol, InfraAcomp, InfraDcModalidad, InfraDcVirt, InfraDcVirtOtro, 
    InfraDcDisp, InfraDcObs, InfraPlan, EstadoExpediente, EstadoLevantamiento, ObsExpediente, 
    ObsLevantamiento, ValidadoDiger, ValidadoInst, FechaValidacion, NumActa, IsDeleted, CreatedAt, 
    CreatedBy, UpdatedAt, UpdatedBy
)
SELECT 
    e.Id, e.Codigo, m.NewId, NULL, NULL, e.Institucion, e.OrigenExternoId, e.FechaApertura, e.Analista, 
    e.DirSede, e.NumTramitesProd, e.ContactoNombre, e.ContactoCargo, e.ContactoCorreo, e.ContactoTel, 
    e.ObsLegal, e.NumFuncionarios, e.VolumenAnual, e.TiempoObservado, e.TiempoNorma, e.DescProceso, 
    e.DocsAdicionales, e.ObsFlujo, e.FuncionariosDig, e.TiempoDig, e.ObsModelo, e.InfraPersonal, 
    e.InfraPersonalTI, e.InfraRespSol, e.InfraAcomp, e.InfraDcModalidad, e.InfraDcVirt, e.InfraDcVirtOtro, 
    e.InfraDcDisp, e.InfraDcObs, e.InfraPlan, e.EstadoExpediente, e.EstadoLevantamiento, e.ObsExpediente, 
    e.ObsLevantamiento, e.ValidadoDiger, e.ValidadoInst, e.FechaValidacion, e.NumActa, 0, e.CreatedAt, 
    e.CreatedBy, e.UpdatedAt, e.UpdatedBy
FROM TramitesEstado_Temp.dbo.Expedientes e
JOIN #InstMapping m ON e.InstitucionId = m.OldId;
SET IDENTITY_INSERT Expedientes OFF;

-- Tablas dependientes de Expedientes
SET IDENTITY_INSERT DocumentosInternos ON;
INSERT INTO DocumentosInternos (Id, ExpedienteId, Orden, Documento, Area, Obs)
SELECT Id, ExpedienteId, Orden, Documento, Area, Obs FROM TramitesEstado_Temp.dbo.DocumentosInternos;
SET IDENTITY_INSERT DocumentosInternos OFF;

SET IDENTITY_INSERT DocumentosSolicitados ON;
INSERT INTO DocumentosSolicitados (Id, ExpedienteId, Orden, Nombre, Tipo, Recibido, Fecha, Url)
SELECT Id, ExpedienteId, Orden, Nombre, Tipo, Recibido, Fecha, Url FROM TramitesEstado_Temp.dbo.DocumentosSolicitados;
SET IDENTITY_INSERT DocumentosSolicitados OFF;

SET IDENTITY_INSERT ExpedienteEtapaAvances ON;
INSERT INTO ExpedienteEtapaAvances (Id, ExpedienteId, TramiteIndex, SubId, Estado)
SELECT Id, ExpedienteId, TramiteIndex, SubId, Estado FROM TramitesEstado_Temp.dbo.ExpedienteEtapaAvances;
SET IDENTITY_INSERT ExpedienteEtapaAvances OFF;

SET IDENTITY_INSERT ExpedienteSecciones ON;
INSERT INTO ExpedienteSecciones (Id, ExpedienteId, Seccion, Estado, Nota)
SELECT Id, ExpedienteId, Seccion, Estado, Nota FROM TramitesEstado_Temp.dbo.ExpedienteSecciones;
SET IDENTITY_INSERT ExpedienteSecciones OFF;

SET IDENTITY_INSERT ExpedienteTramites ON;
INSERT INTO ExpedienteTramites (Id, ExpedienteId, TramiteIndex, NombreTramite, NombreCorto, AreaResponsable, Modalidad, PlazoLegal, Tercero, TiempoReal, MetodoPago, PagoBanco, PagoCuenta, TgrInst, TgrRubro, TgrMonto, DocEntregado, Objetivo, Alcance, AlcanceObs, Descripcion, Dirigido, Horario, Telefono, EmailTramite, SitioWeb)
SELECT Id, ExpedienteId, TramiteIndex, NombreTramite, NombreCorto, AreaResponsable, Modalidad, PlazoLegal, Tercero, TiempoReal, MetodoPago, PagoBanco, PagoCuenta, TgrInst, TgrRubro, TgrMonto, DocEntregado, Objetivo, Alcance, AlcanceObs, Descripcion, Dirigido, Horario, Telefono, EmailTramite, SitioWeb FROM TramitesEstado_Temp.dbo.ExpedienteTramites;
SET IDENTITY_INSERT ExpedienteTramites OFF;

SET IDENTITY_INSERT FlujoNodos ON;
INSERT INTO FlujoNodos (Id, ExpedienteId, TramiteIndex, Fase, Orden, Tipo, Titulo, Area, Tiempo, DocEmitido, Obs, RetornoA)
SELECT Id, ExpedienteId, TramiteIndex, Fase, Orden, Tipo, Titulo, Area, Tiempo, DocEmitido, Obs, RetornoA FROM TramitesEstado_Temp.dbo.FlujoNodos;
SET IDENTITY_INSERT FlujoNodos OFF;

SET IDENTITY_INSERT FundamentosLegales ON;
INSERT INTO FundamentosLegales (Id, ExpedienteId, Orden, Instrumento, Articulos, Obs)
SELECT Id, ExpedienteId, Orden, Instrumento, Articulos, Obs FROM TramitesEstado_Temp.dbo.FundamentosLegales;
SET IDENTITY_INSERT FundamentosLegales OFF;

SET IDENTITY_INSERT InfraChecklist ON;
INSERT INTO InfraChecklist (Id, ExpedienteId, Orden, Grupo, Requisito, Status, Obs)
SELECT Id, ExpedienteId, Orden, Grupo, Requisito, Status, Obs FROM TramitesEstado_Temp.dbo.InfraChecklist;
SET IDENTITY_INSERT InfraChecklist OFF;

SET IDENTITY_INSERT InfraCondiciones ON;
INSERT INTO InfraCondiciones (Id, ExpedienteId, Condicion)
SELECT Id, ExpedienteId, Condicion FROM TramitesEstado_Temp.dbo.InfraCondiciones;
SET IDENTITY_INSERT InfraCondiciones OFF;

SET IDENTITY_INSERT InfraPerfiles ON;
INSERT INTO InfraPerfiles (Id, ExpedienteId, Perfil, Nombre, Correo)
SELECT Id, ExpedienteId, Perfil, Nombre, Correo FROM TramitesEstado_Temp.dbo.InfraPerfiles;
SET IDENTITY_INSERT InfraPerfiles OFF;

SET IDENTITY_INSERT TramiteRequisitos ON;
INSERT INTO TramiteRequisitos (Id, ExpedienteId, TramiteIndex, Orden, Requisito, Obs, Accion, Justificacion)
SELECT Id, ExpedienteId, TramiteIndex, Orden, Requisito, Obs, Accion, Justificacion FROM TramitesEstado_Temp.dbo.TramiteRequisitos;
SET IDENTITY_INSERT TramiteRequisitos OFF;

-- ==========================================
-- 4. EVENTOS Y SOPORTE (Reuniones, Tickets)
-- ==========================================
PRINT '>> Migrando Reuniones y Tickets...';

SET IDENTITY_INSERT Reuniones ON;
INSERT INTO Reuniones (
    Id, Titulo, OrigenExternoId, Visibilidad, CreadoPorId, RegistroToken, RegistroAbierto, Fecha, Hora, Duracion, Modalidad, Lugar, InstitucionId, AreaId, UnidadId, Institucion, Tipo, EsCapacitacionPlataforma, ObjetivoAgenda, Desarrollo, Tema, ObjetivoCap, Contenido, EpNombre, EpCargo, EpCorreo, EpTel, FacNombre, FacCargo, FacCorreo, Convocados, NumAsistentes, PctAsistencia, Satisfaccion, Compromisos, ValDiger, ValInst, DocsRecursos, Foto1Url, Foto1Desc, Foto2Url, Foto2Desc, IsDeleted, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
)
SELECT 
    r.Id, r.Titulo, r.OrigenExternoId, r.Visibilidad, NULL, r.RegistroToken, r.RegistroAbierto, r.Fecha, r.Hora, r.Duracion, r.Modalidad, r.Lugar, m.NewId, NULL, NULL, r.Institucion, r.Tipo, r.EsCapacitacionPlataforma, r.ObjetivoAgenda, r.Desarrollo, r.Tema, r.ObjetivoCap, r.Contenido, r.EpNombre, r.EpCargo, r.EpCorreo, r.EpTel, r.FacNombre, r.FacCargo, r.FacCorreo, r.Convocados, r.NumAsistentes, r.PctAsistencia, r.Satisfaccion, r.Compromisos, r.ValDiger, r.ValInst, r.DocsRecursos, r.Foto1Url, r.Foto1Desc, r.Foto2Url, r.Foto2Desc, 0, r.CreatedAt, r.CreatedBy, r.UpdatedAt, r.UpdatedBy
FROM TramitesEstado_Temp.dbo.Reuniones r
LEFT JOIN #InstMapping m ON r.InstitucionId = m.OldId;
SET IDENTITY_INSERT Reuniones OFF;

SET IDENTITY_INSERT AcuerdosReunion ON;
INSERT INTO AcuerdosReunion (Id, ReunionId, Orden, Compromiso, Responsable, Plazo, Estado, FechaCumplimiento, NotaSeguimiento, SeguimientoActualizadoEl, SeguimientoActualizadoPor)
SELECT Id, ReunionId, Orden, Compromiso, Responsable, Plazo, Estado, FechaCumplimiento, NotaSeguimiento, SeguimientoActualizadoEl, SeguimientoActualizadoPor FROM TramitesEstado_Temp.dbo.AcuerdosReunion;
SET IDENTITY_INSERT AcuerdosReunion OFF;

SET IDENTITY_INSERT Asistentes ON;
INSERT INTO Asistentes (Id, ReunionId, Nombre, Cargo, Institucion, Departamento, Correo, Telefono, AutoRegistro, RegistradoEl)
SELECT Id, ReunionId, Nombre, Cargo, Institucion, Departamento, Correo, Telefono, AutoRegistro, RegistradoEl FROM TramitesEstado_Temp.dbo.Asistentes;
SET IDENTITY_INSERT Asistentes OFF;

SET IDENTITY_INSERT Contactos ON;
INSERT INTO Contactos (Id, InstitucionId, AreaId, UnidadId, Institucion, Nombre, Cargo, Correo, Telefono, Notas, Origen, IsDeleted, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
SELECT c.Id, m.NewId, NULL, NULL, c.Institucion, c.Nombre, c.Cargo, c.Correo, c.Telefono, c.Notas, c.Origen, 0, c.CreatedAt, c.CreatedBy, c.UpdatedAt, c.UpdatedBy 
FROM TramitesEstado_Temp.dbo.Contactos c
JOIN #InstMapping m ON c.InstitucionId = m.OldId;
SET IDENTITY_INSERT Contactos OFF;

SET IDENTITY_INSERT Tickets ON;
INSERT INTO Tickets (
    Id, Numero, Titulo, Descripcion, TemaId, Prioridad, Estado, InstitucionId, AreaId, UnidadId, Institucion, ExpedienteId, ExpedienteCodigo, ReportanteNombre, ReportanteCorreo, ReportanteTelefono, CreadoPorId, CreadoPor, AsignadoAId, AsignadoA, FechaResolucion, NotaResolucion, IsDeleted, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
)
SELECT 
    t.Id, t.Numero, t.Titulo, t.Descripcion, t.TemaId, t.Prioridad, t.Estado, m.NewId, NULL, NULL, t.Institucion, t.ExpedienteId, t.ExpedienteCodigo, t.ReportanteNombre, t.ReportanteCorreo, t.ReportanteTelefono, NULL, t.CreadoPor, NULL, t.AsignadoA, t.FechaResolucion, t.NotaResolucion, 0, t.CreatedAt, t.CreatedBy, t.UpdatedAt, t.UpdatedBy
FROM TramitesEstado_Temp.dbo.Tickets t
LEFT JOIN #InstMapping m ON t.InstitucionId = m.OldId;
SET IDENTITY_INSERT Tickets OFF;

SET IDENTITY_INSERT TicketAdjuntos ON;
INSERT INTO TicketAdjuntos (Id, TicketId, ComentarioId, NombreArchivo, Url, Tamano)
SELECT Id, TicketId, ComentarioId, NombreArchivo, Url, Tamano FROM TramitesEstado_Temp.dbo.TicketAdjuntos;
SET IDENTITY_INSERT TicketAdjuntos OFF;

SET IDENTITY_INSERT TicketComentarios ON;
INSERT INTO TicketComentarios (Id, TicketId, Tipo, Autor, Texto, Fecha)
SELECT Id, TicketId, Tipo, Autor, Texto, Fecha FROM TramitesEstado_Temp.dbo.TicketComentarios;
SET IDENTITY_INSERT TicketComentarios OFF;

SET IDENTITY_INSERT TicketTramites ON;
INSERT INTO TicketTramites (Id, TicketId, TramiteDefinicionId, Tramite)
SELECT Id, TicketId, TramiteDefinicionId, Tramite FROM TramitesEstado_Temp.dbo.TicketTramites;
SET IDENTITY_INSERT TicketTramites OFF;

-- Limpieza
DROP TABLE #InstMapping;

PRINT '=======================================================';
PRINT 'MIGRACION COMPLETADA CORRECTAMENTE.';
PRINT '=======================================================';
