-- ============================================================
--  Diger.TramitesEstado — Script de creación de base de datos
--  SQL Server 2019+
--  Generado: 2026-06-09
-- ============================================================

USE master;
GO

-- Crear la base de datos si no existe
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'DigerTramitesEstado')
BEGIN
    CREATE DATABASE DigerTramitesEstado
        COLLATE Modern_Spanish_CI_AI;
END
GO

USE DigerTramitesEstado;
GO

-- ============================================================
--  Tabla: Levantamientos  (Aggregate Root)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Levantamientos')
BEGIN
    CREATE TABLE Levantamientos (
        -- Identificador
        Id                     UNIQUEIDENTIFIER    NOT NULL  DEFAULT NEWSEQUENTIALID(),

        -- Paso 1: Información general
        Institucion            NVARCHAR(100)       NOT NULL,
        Encargado              NVARCHAR(150)       NOT NULL,
        Correo                 NVARCHAR(200)       NOT NULL,
        Celular                NVARCHAR(20)        NULL,

        -- Estado de los trámites
        -- Valores: 'DigitalizadoEnProduccion' | 'EnProcesoDeDigitalizacion' | 'NoIniciado' | 'Otro'
        Estado                 NVARCHAR(60)        NOT NULL,
        ObsEstado              NVARCHAR(1000)      NULL,

        -- Migración SOL
        -- Valores: 'Si' | 'No' | 'EnProceso'
        MigradaSOL             NVARCHAR(30)        NOT NULL,
        -- Valores: 'FaltaPersonalTecnico' | 'FaltaInfraestructura' | 'FaltaPresupuesto'
        --          | 'RestriccionesAdministrativas' | 'Otro'
        Limitante              NVARCHAR(60)        NULL,
        LimitanteObs           NVARCHAR(500)       NULL,

        -- Personal técnico
        -- Valores: 'Si' | 'No' | 'Parcialmente'
        Personal               NVARCHAR(30)        NOT NULL,
        PersonalObs            NVARCHAR(500)       NULL,

        -- Acompañamiento
        RequiereAcompanamiento BIT                 NOT NULL  DEFAULT 0,
        -- Valores: 'AdministracionPlataforma' | 'SoporteTecnico' | 'DesarrolloConfiguracion'
        --          | 'GestionUsuarios' | 'Otro'
        Habilidad              NVARCHAR(60)        NULL,
        HabilidadObs           NVARCHAR(500)       NULL,

        -- Paso 2: Observaciones generales
        ObsGenerales           NVARCHAR(2000)      NULL,

        -- Auditoría
        CreatedAt              DATETIME2           NOT NULL  DEFAULT SYSUTCDATETIME(),
        UpdatedAt              DATETIME2           NULL,

        CONSTRAINT PK_Levantamientos PRIMARY KEY (Id)
    );

    -- Institución única por levantamiento
    CREATE UNIQUE INDEX UIX_Levantamientos_Institucion
        ON Levantamientos (Institucion);

    -- Índice para consultas por fecha de creación
    CREATE INDEX IX_Levantamientos_CreatedAt
        ON Levantamientos (CreatedAt);

    PRINT 'Tabla Levantamientos creada.';
END
ELSE
    PRINT 'Tabla Levantamientos ya existe — omitida.';
GO

-- ============================================================
--  Tabla: TramitesChecklist  (Paso 2 — grid de trámites)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TramitesChecklist')
BEGIN
    CREATE TABLE TramitesChecklist (
        Id               UNIQUEIDENTIFIER    NOT NULL  DEFAULT NEWSEQUENTIALID(),
        LevantamientoId  UNIQUEIDENTIFIER    NOT NULL,
        NombreTramite    NVARCHAR(300)       NOT NULL,
        Orden            INT                 NOT NULL  DEFAULT 0,
        ActaFirmada      BIT                 NOT NULL  DEFAULT 0,
        RequiereMejoras  BIT                 NOT NULL  DEFAULT 0,
        TieneInstructivo BIT                 NOT NULL  DEFAULT 0,
        Socializado      BIT                 NOT NULL  DEFAULT 0,
        Observaciones    NVARCHAR(1000)      NULL,

        CONSTRAINT PK_TramitesChecklist PRIMARY KEY (Id),
        CONSTRAINT FK_TramitesChecklist_Levantamiento
            FOREIGN KEY (LevantamientoId)
            REFERENCES Levantamientos (Id)
            ON DELETE CASCADE
    );

    -- Consultas por levantamiento ordenadas
    CREATE INDEX IX_TramitesChecklist_LevantamientoId_Orden
        ON TramitesChecklist (LevantamientoId, Orden);

    PRINT 'Tabla TramitesChecklist creada.';
END
ELSE
    PRINT 'Tabla TramitesChecklist ya existe — omitida.';
GO

-- ============================================================
--  Tabla: MiembrosEquipo  (Paso 1 — equipo institucional)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MiembrosEquipo')
BEGIN
    CREATE TABLE MiembrosEquipo (
        Id               UNIQUEIDENTIFIER    NOT NULL  DEFAULT NEWSEQUENTIALID(),
        LevantamientoId  UNIQUEIDENTIFIER    NOT NULL,
        Funcion          NVARCHAR(100)       NOT NULL,
        Nombre           NVARCHAR(150)       NOT NULL,
        Contacto         NVARCHAR(200)       NULL,
        Orden            INT                 NOT NULL  DEFAULT 0,

        CONSTRAINT PK_MiembrosEquipo PRIMARY KEY (Id),
        CONSTRAINT FK_MiembrosEquipo_Levantamiento
            FOREIGN KEY (LevantamientoId)
            REFERENCES Levantamientos (Id)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_MiembrosEquipo_LevantamientoId
        ON MiembrosEquipo (LevantamientoId);

    PRINT 'Tabla MiembrosEquipo creada.';
END
ELSE
    PRINT 'Tabla MiembrosEquipo ya existe — omitida.';
GO

-- ============================================================
--  Tabla: DocumentosAdjuntos  (Paso 2 — archivos adjuntos)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DocumentosAdjuntos')
BEGIN
    CREATE TABLE DocumentosAdjuntos (
        Id               UNIQUEIDENTIFIER    NOT NULL  DEFAULT NEWSEQUENTIALID(),
        LevantamientoId  UNIQUEIDENTIFIER    NOT NULL,
        Nombre           NVARCHAR(200)       NOT NULL,
        -- Valores: 'Acta' | 'Informe' | 'Instructivo' | 'Presentacion' | 'Memorando'
        --          | 'VideoTutorial' | 'Resolucion' | 'Otro'
        Tipo             NVARCHAR(30)        NOT NULL,
        Url              NVARCHAR(500)       NULL,
        FechaDocumento   DATE                NULL,
        FechaRegistro    DATETIME2           NOT NULL  DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_DocumentosAdjuntos PRIMARY KEY (Id),
        CONSTRAINT FK_DocumentosAdjuntos_Levantamiento
            FOREIGN KEY (LevantamientoId)
            REFERENCES Levantamientos (Id)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_DocumentosAdjuntos_LevantamientoId
        ON DocumentosAdjuntos (LevantamientoId);

    PRINT 'Tabla DocumentosAdjuntos creada.';
END
ELSE
    PRINT 'Tabla DocumentosAdjuntos ya existe — omitida.';
GO

-- ============================================================
--  Fin del script
-- ============================================================
PRINT '✔ Base de datos DigerTramitesEstado lista.';
GO
