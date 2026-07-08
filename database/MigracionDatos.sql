USE DigerTramitesEstado_Dev;
GO

PRINT '=======================================================';
PRINT 'INICIANDO MIGRACION DE DATOS DESDE ESQUEMA BACKUP';
PRINT '=======================================================';

-- Deshabilitar restricciones de clave foránea temporalmente (opcional si mantenemos el orden)
-- EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

-- ==========================================
-- 0. TABLAS TEMPORALES Y MAPEOS
-- ==========================================
PRINT '>> Creando tabla de mapeo de Usuarios...';
IF OBJECT_ID('tempdb..#UsuarioMapping') IS NOT NULL DROP TABLE #UsuarioMapping;
CREATE TABLE #UsuarioMapping (
    OldId INT PRIMARY KEY,
    NewId UNIQUEIDENTIFIER
);

-- Populating the mapping with NEWID()
INSERT INTO #UsuarioMapping (OldId, NewId)
SELECT Id, NEWID()
FROM backup.dbo.Usuarios;


-- ==========================================
-- 1. CATALOGOS BASICOS
-- ==========================================
PRINT '>> Migrando Catálogos Básicos...';

SET IDENTITY_INSERT CategoriasTicket ON;
INSERT INTO CategoriasTicket (Id, Nombre, Activo, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
SELECT Id, Nombre, Activo, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy 
FROM backup.dbo.CategoriasTicket;
SET IDENTITY_INSERT CategoriasTicket OFF;

SET IDENTITY_INSERT TemasTicket ON;
INSERT INTO TemasTicket (Id, Nombre, HorasResolucion, Activo, CategoriaId, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
SELECT Id, Nombre, HorasResolucion, Activo, CategoriaId, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy 
FROM backup.dbo.TemasTicket;
SET IDENTITY_INSERT TemasTicket OFF;

SET IDENTITY_INSERT RolModuloAccesos ON;
INSERT INTO RolModuloAccesos (Id, Rol, Modulo)
SELECT Id, Rol, Modulo 
FROM backup.dbo.RolModuloAccesos;
SET IDENTITY_INSERT RolModuloAccesos OFF;

PRINT '>> Migrando Instituciones...';
-- Instituciones: Id ya no es IDENTITY, se migra de INT a NVARCHAR(120)
INSERT INTO Instituciones (Id, Nombre, Descripcion, NombreCorto, LogoUrl, InfoExtra, Activo, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
SELECT 
    CAST(Id AS NVARCHAR(120)), 
    Nombre, 
    NULL, -- Descripcion
    NULL, -- NombreCorto
    NULL, -- LogoUrl
    NULL, -- InfoExtra
    Activo, 
    GETDATE(), -- CreatedAt no existía antes
    NULL, 
    NULL, 
    NULL
FROM backup.dbo.Instituciones;


-- ==========================================
-- 2. USUARIOS Y AUTENTICACIÓN
-- ==========================================
PRINT '>> Migrando Usuarios...';
INSERT INTO Usuarios (Id, Nombre, Correo, PasswordHash, Telefono, Activo, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
SELECT 
    m.NewId, 
    u.Nombre, 
    u.Correo, 
    u.PasswordHash, 
    NULL, -- Telefono no existía en el esquema anterior
    u.Activo, 
    u.CreatedAt, 
    u.CreatedBy, 
    u.UpdatedAt, 
    u.UpdatedBy
FROM backup.dbo.Usuarios u
JOIN #UsuarioMapping m ON u.Id = m.OldId;

PRINT '>> Migrando Asignaciones de Usuarios...';
-- AsignacionesUsuario reemplaza a UsuarioInstituciones
INSERT INTO AsignacionesUsuario (Id, UsuarioId, InstitucionId, AreaId, UnidadId, Rol, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
SELECT 
    NEWID(), 
    m.NewId, 
    CAST(ui.InstitucionId AS NVARCHAR(120)), 
    NULL, -- AreaId (Null por ahora)
    NULL, -- UnidadId (Null por ahora)
    u.Rol, -- El Rol se extrae de la tabla Usuarios antigua
    GETDATE(), 
    NULL, 
    NULL, 
    NULL
FROM backup.dbo.UsuarioInstituciones ui
JOIN backup.dbo.Usuarios u ON ui.UsuarioId = u.Id
JOIN #UsuarioMapping m ON ui.UsuarioId = m.OldId;

SET IDENTITY_INSERT UsuarioTemas ON;
INSERT INTO UsuarioTemas (Id, UsuarioId, TemaId)
SELECT 
    ut.Id, 
    m.NewId, 
    ut.TemaId
FROM backup.dbo.UsuarioTemas ut
JOIN #UsuarioMapping m ON ut.UsuarioId = m.OldId;
SET IDENTITY_INSERT UsuarioTemas OFF;


-- ==========================================
-- 3. EXPEDIENTES Y DATOS RELACIONADOS
-- ==========================================
PRINT '>> Migrando Expedientes...';

SET IDENTITY_INSERT TramitesDefinicion ON;
INSERT INTO TramitesDefinicion (Id, InstitucionId, Nombre, Orden)
SELECT 
    Id, 
    CAST(InstitucionId AS NVARCHAR(120)), 
    Nombre, 
    Orden
FROM backup.dbo.TramitesDefinicion;
SET IDENTITY_INSERT TramitesDefinicion OFF;

SET IDENTITY_INSERT Expedientes ON;
INSERT INTO Expedientes (
    Id, Codigo, InstitucionId, AreaId, UnidadId, Institucion, OrigenExternoId, FechaApertura, Analista, 
    DirSede, NumTramitesProd, ContactoNombre, ContactoCargo, ContactoCorreo, ContactoTel, 
    ObsLegal, NumFuncionarios, VolumenAnual, TiempoObservado, TiempoNorma, DescProceso, 
    DocsAdicionales, ObsFlujo, FuncionariosDig, TiempoDig, ObsModelo, InfraPersonal, 
    InfraPersonalTI, InfraRespSol, InfraAcomp, InfraDcModalidad, InfraDcVirt, InfraDcVirtOtro, 
    InfraDcDisp, InfraDcObs, InfraPlan, EstadoExpediente, EstadoLevantamiento, ObsExpediente, 
    ObsLevantamiento, ValidadoDiger, ValidadoInst, FechaValidacion, NumActa, CreatedAt, 
    CreatedBy, UpdatedAt, UpdatedBy
)
SELECT 
    Id, Codigo, CAST(InstitucionId AS NVARCHAR(120)), NULL, NULL, Institucion, OrigenExternoId, FechaApertura, Analista, 
    DirSede, NumTramitesProd, ContactoNombre, ContactoCargo, ContactoCorreo, ContactoTel, 
    ObsLegal, NumFuncionarios, VolumenAnual, TiempoObservado, TiempoNorma, DescProceso, 
    DocsAdicionales, ObsFlujo, FuncionariosDig, TiempoDig, ObsModelo, InfraPersonal, 
    InfraPersonalTI, InfraRespSol, InfraAcomp, InfraDcModalidad, InfraDcVirt, InfraDcVirtOtro, 
    InfraDcDisp, InfraDcObs, InfraPlan, EstadoExpediente, EstadoLevantamiento, ObsExpediente, 
    ObsLevantamiento, ValidadoDiger, ValidadoInst, FechaValidacion, NumActa, CreatedAt, 
    CreatedBy, UpdatedAt, UpdatedBy
FROM backup.dbo.Expedientes;
SET IDENTITY_INSERT Expedientes OFF;

-- Tablas dependientes de Expedientes
SET IDENTITY_INSERT DocumentosInternos ON;
INSERT INTO DocumentosInternos (Id, ExpedienteId, Orden, Documento, Area, Obs)
SELECT Id, ExpedienteId, Orden, Documento, Area, Obs FROM backup.dbo.DocumentosInternos;
SET IDENTITY_INSERT DocumentosInternos OFF;

SET IDENTITY_INSERT DocumentosSolicitados ON;
INSERT INTO DocumentosSolicitados (Id, ExpedienteId, Orden, Nombre, Tipo, Recibido, Fecha, Url)
SELECT Id, ExpedienteId, Orden, Nombre, Tipo, Recibido, Fecha, Url FROM backup.dbo.DocumentosSolicitados;
SET IDENTITY_INSERT DocumentosSolicitados OFF;

SET IDENTITY_INSERT ExpedienteEtapaAvances ON;
INSERT INTO ExpedienteEtapaAvances (Id, ExpedienteId, TramiteIndex, SubId, Estado)
SELECT Id, ExpedienteId, TramiteIndex, SubId, Estado FROM backup.dbo.ExpedienteEtapaAvances;
SET IDENTITY_INSERT ExpedienteEtapaAvances OFF;

SET IDENTITY_INSERT ExpedienteSecciones ON;
INSERT INTO ExpedienteSecciones (Id, ExpedienteId, Seccion, Estado, Nota)
SELECT Id, ExpedienteId, Seccion, Estado, Nota FROM backup.dbo.ExpedienteSecciones;
SET IDENTITY_INSERT ExpedienteSecciones OFF;

SET IDENTITY_INSERT ExpedienteTramites ON;
INSERT INTO ExpedienteTramites (Id, ExpedienteId, TramiteIndex, NombreTramite, NombreCorto, AreaResponsable, Modalidad, PlazoLegal, Tercero, TiempoReal, MetodoPago, PagoBanco, PagoCuenta, TgrInst, TgrRubro, TgrMonto, DocEntregado, Objetivo, Alcance, AlcanceObs, Descripcion, Dirigido, Horario, Telefono, EmailTramite, SitioWeb)
SELECT Id, ExpedienteId, TramiteIndex, NombreTramite, NombreCorto, AreaResponsable, Modalidad, PlazoLegal, Tercero, TiempoReal, MetodoPago, PagoBanco, PagoCuenta, TgrInst, TgrRubro, TgrMonto, DocEntregado, Objetivo, Alcance, AlcanceObs, Descripcion, Dirigido, Horario, Telefono, EmailTramite, SitioWeb FROM backup.dbo.ExpedienteTramites;
SET IDENTITY_INSERT ExpedienteTramites OFF;

SET IDENTITY_INSERT FlujoNodos ON;
INSERT INTO FlujoNodos (Id, ExpedienteId, TramiteIndex, Fase, Orden, Tipo, Titulo, Area, Tiempo, DocEmitido, Obs, RetornoA)
SELECT Id, ExpedienteId, TramiteIndex, Fase, Orden, Tipo, Titulo, Area, Tiempo, DocEmitido, Obs, RetornoA FROM backup.dbo.FlujoNodos;
SET IDENTITY_INSERT FlujoNodos OFF;

SET IDENTITY_INSERT FundamentosLegales ON;
INSERT INTO FundamentosLegales (Id, ExpedienteId, Orden, Instrumento, Articulos, Obs)
SELECT Id, ExpedienteId, Orden, Instrumento, Articulos, Obs FROM backup.dbo.FundamentosLegales;
SET IDENTITY_INSERT FundamentosLegales OFF;

SET IDENTITY_INSERT InfraChecklist ON;
INSERT INTO InfraChecklist (Id, ExpedienteId, Orden, Grupo, Requisito, Status, Obs)
SELECT Id, ExpedienteId, Orden, Grupo, Requisito, Status, Obs FROM backup.dbo.InfraChecklist;
SET IDENTITY_INSERT InfraChecklist OFF;

SET IDENTITY_INSERT InfraCondiciones ON;
INSERT INTO InfraCondiciones (Id, ExpedienteId, Condicion)
SELECT Id, ExpedienteId, Condicion FROM backup.dbo.InfraCondiciones;
SET IDENTITY_INSERT InfraCondiciones OFF;

SET IDENTITY_INSERT InfraPerfiles ON;
INSERT INTO InfraPerfiles (Id, ExpedienteId, Perfil, Nombre, Correo)
SELECT Id, ExpedienteId, Perfil, Nombre, Correo FROM backup.dbo.InfraPerfiles;
SET IDENTITY_INSERT InfraPerfiles OFF;

SET IDENTITY_INSERT TramiteRequisitos ON;
INSERT INTO TramiteRequisitos (Id, ExpedienteId, TramiteIndex, Orden, Requisito, Obs, Accion, Justificacion)
SELECT Id, ExpedienteId, TramiteIndex, Orden, Requisito, Obs, Accion, Justificacion FROM backup.dbo.TramiteRequisitos;
SET IDENTITY_INSERT TramiteRequisitos OFF;


-- ==========================================
-- 4. EVENTOS Y SOPORTE (Reuniones, Tickets)
-- ==========================================
PRINT '>> Migrando Reuniones y Tickets...';

SET IDENTITY_INSERT Reuniones ON;
INSERT INTO Reuniones (
    Id, Titulo, OrigenExternoId, Visibilidad, CreadoPorId, RegistroToken, RegistroAbierto, Fecha, Hora, Duracion, Modalidad, Lugar, InstitucionId, AreaId, UnidadId, Institucion, Tipo, EsCapacitacionPlataforma, ObjetivoAgenda, Desarrollo, Tema, ObjetivoCap, Contenido, EpNombre, EpCargo, EpCorreo, EpTel, FacNombre, FacCargo, FacCorreo, Convocados, NumAsistentes, PctAsistencia, Satisfaccion, Compromisos, ValDiger, ValInst, DocsRecursos, Foto1Url, Foto1Desc, Foto2Url, Foto2Desc, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
)
SELECT 
    r.Id, r.Titulo, r.OrigenExternoId, r.Visibilidad, m.NewId, r.RegistroToken, r.RegistroAbierto, r.Fecha, r.Hora, r.Duracion, r.Modalidad, r.Lugar, CAST(r.InstitucionId AS NVARCHAR(120)), NULL, NULL, r.Institucion, r.Tipo, r.EsCapacitacionPlataforma, r.ObjetivoAgenda, r.Desarrollo, r.Tema, r.ObjetivoCap, r.Contenido, r.EpNombre, r.EpCargo, r.EpCorreo, r.EpTel, r.FacNombre, r.FacCargo, r.FacCorreo, r.Convocados, r.NumAsistentes, r.PctAsistencia, r.Satisfaccion, r.Compromisos, r.ValDiger, r.ValInst, r.DocsRecursos, r.Foto1Url, r.Foto1Desc, r.Foto2Url, r.Foto2Desc, r.CreatedAt, r.CreatedBy, r.UpdatedAt, r.UpdatedBy
FROM backup.dbo.Reuniones r
LEFT JOIN #UsuarioMapping m ON r.CreadoPorId = m.OldId;
SET IDENTITY_INSERT Reuniones OFF;

SET IDENTITY_INSERT AcuerdosReunion ON;
INSERT INTO AcuerdosReunion (Id, ReunionId, Orden, Compromiso, Responsable, Plazo, Estado, FechaCumplimiento, NotaSeguimiento, SeguimientoActualizadoEl, SeguimientoActualizadoPor)
SELECT Id, ReunionId, Orden, Compromiso, Responsable, Plazo, Estado, FechaCumplimiento, NotaSeguimiento, SeguimientoActualizadoEl, SeguimientoActualizadoPor FROM backup.dbo.AcuerdosReunion;
SET IDENTITY_INSERT AcuerdosReunion OFF;

SET IDENTITY_INSERT Asistentes ON;
INSERT INTO Asistentes (Id, ReunionId, Nombre, Cargo, Institucion, Departamento, Correo, Telefono, AutoRegistro, RegistradoEl)
SELECT Id, ReunionId, Nombre, Cargo, Institucion, Departamento, Correo, Telefono, AutoRegistro, RegistradoEl FROM backup.dbo.Asistentes;
SET IDENTITY_INSERT Asistentes OFF;

SET IDENTITY_INSERT Contactos ON;
INSERT INTO Contactos (Id, InstitucionId, AreaId, UnidadId, Institucion, Nombre, Cargo, Correo, Telefono, Notas, Origen, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
SELECT Id, CAST(InstitucionId AS NVARCHAR(120)), NULL, NULL, Institucion, Nombre, Cargo, Correo, Telefono, Notas, Origen, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy FROM backup.dbo.Contactos;
SET IDENTITY_INSERT Contactos OFF;

SET IDENTITY_INSERT Tickets ON;
INSERT INTO Tickets (
    Id, Numero, Titulo, Descripcion, TemaId, Prioridad, Estado, InstitucionId, AreaId, UnidadId, Institucion, ExpedienteId, ExpedienteCodigo, ReportanteNombre, ReportanteCorreo, ReportanteTelefono, CreadoPorId, CreadoPor, AsignadoAId, AsignadoA, FechaResolucion, NotaResolucion, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
)
SELECT 
    t.Id, t.Numero, t.Titulo, t.Descripcion, t.TemaId, t.Prioridad, t.Estado, CAST(t.InstitucionId AS NVARCHAR(120)), NULL, NULL, t.Institucion, t.ExpedienteId, t.ExpedienteCodigo, t.ReportanteNombre, t.ReportanteCorreo, t.ReportanteTelefono, m1.NewId, t.CreadoPor, m2.NewId, t.AsignadoA, t.FechaResolucion, t.NotaResolucion, t.CreatedAt, t.CreatedBy, t.UpdatedAt, t.UpdatedBy
FROM backup.dbo.Tickets t
LEFT JOIN #UsuarioMapping m1 ON t.CreadoPorId = m1.OldId
LEFT JOIN #UsuarioMapping m2 ON t.AsignadoAId = m2.OldId;
SET IDENTITY_INSERT Tickets OFF;

SET IDENTITY_INSERT TicketAdjuntos ON;
INSERT INTO TicketAdjuntos (Id, TicketId, ComentarioId, NombreArchivo, Url, Tamano)
SELECT Id, TicketId, ComentarioId, NombreArchivo, Url, Tamano FROM backup.dbo.TicketAdjuntos;
SET IDENTITY_INSERT TicketAdjuntos OFF;

SET IDENTITY_INSERT TicketComentarios ON;
INSERT INTO TicketComentarios (Id, TicketId, Tipo, Autor, Texto, Fecha)
SELECT Id, TicketId, Tipo, Autor, Texto, Fecha FROM backup.dbo.TicketComentarios;
SET IDENTITY_INSERT TicketComentarios OFF;

SET IDENTITY_INSERT TicketTramites ON;
INSERT INTO TicketTramites (Id, TicketId, TramiteDefinicionId, Tramite)
SELECT Id, TicketId, TramiteDefinicionId, Tramite FROM backup.dbo.TicketTramites;
SET IDENTITY_INSERT TicketTramites OFF;

-- Limpieza
DROP TABLE #UsuarioMapping;

-- Habilitar restricciones
-- EXEC sp_msforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';

PRINT '=======================================================';
PRINT 'MIGRACION COMPLETADA CORRECTAMENTE.';
PRINT '=======================================================';
