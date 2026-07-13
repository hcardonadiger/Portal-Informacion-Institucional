-- ============================================================
--  Diger.TramitesEstado — Script de creación de base de datos
--  SQL Server 2019+
--  Actualizado para Soportar Escalabilidad, Jerarquía y Auditoría
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
--  1. TABLAS DE JERARQUÍA ORGANIZACIONAL
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Institucion')
BEGIN
    CREATE TABLE Institucion (
        Id VARCHAR(50) NOT NULL,
        Nombre NVARCHAR(200) NOT NULL,
        Descripcion NVARCHAR(500) NULL,
        NombreCorto NVARCHAR(50) NULL,
        LogoUrl NVARCHAR(500) NULL,
        InfoExtra NVARCHAR(MAX) NULL,
        
        -- Auditoría
        UsuarioCreo NVARCHAR(150) NOT NULL,
        UsuarioModifico NVARCHAR(150) NULL,
        FechaCreacion DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaModificacion DATETIME2 NULL,

        CONSTRAINT PK_Institucion PRIMARY KEY (Id)
    );
    PRINT 'Tabla Institucion creada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Area')
BEGIN
    CREATE TABLE Area (
        Id VARCHAR(50) NOT NULL,
        InstitucionId VARCHAR(50) NOT NULL,
        Nombre NVARCHAR(200) NOT NULL,
        Descripcion NVARCHAR(500) NULL,
        NombreCorto NVARCHAR(50) NULL,
        LogoUrl NVARCHAR(500) NULL,
        
        -- Auditoría
        UsuarioCreo NVARCHAR(150) NOT NULL,
        UsuarioModifico NVARCHAR(150) NULL,
        FechaCreacion DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaModificacion DATETIME2 NULL,

        CONSTRAINT PK_Area PRIMARY KEY (Id),
        CONSTRAINT FK_Area_Institucion FOREIGN KEY (InstitucionId) REFERENCES Institucion(Id)
    );
    PRINT 'Tabla Area creada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Unidad')
BEGIN
    CREATE TABLE Unidad (
        Id VARCHAR(50) NOT NULL,
        AreaId VARCHAR(50) NOT NULL,
        Nombre NVARCHAR(200) NOT NULL,
        Descripcion NVARCHAR(500) NULL,
        NombreCorto NVARCHAR(50) NULL,
        LogoUrl NVARCHAR(500) NULL,
        
        -- Auditoría
        UsuarioCreo NVARCHAR(150) NOT NULL,
        UsuarioModifico NVARCHAR(150) NULL,
        FechaCreacion DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaModificacion DATETIME2 NULL,

        CONSTRAINT PK_Unidad PRIMARY KEY (Id),
        CONSTRAINT FK_Unidad_Area FOREIGN KEY (AreaId) REFERENCES Area(Id)
    );
    PRINT 'Tabla Unidad creada.';
END
GO

-- ============================================================
--  2. TABLAS DE USUARIOS Y SEGURIDAD
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Usuarios')
BEGIN
    CREATE TABLE Usuarios (
        Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Nombre NVARCHAR(150) NOT NULL,
        Correo NVARCHAR(200) NOT NULL,
        Telefono NVARCHAR(50) NULL,
        ContrasenaHash NVARCHAR(500) NULL,
        
        -- Auditoría
        UsuarioCreo NVARCHAR(150) NOT NULL DEFAULT 'Sistema',
        UsuarioModifico NVARCHAR(150) NULL,
        FechaCreacion DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaModificacion DATETIME2 NULL,

        CONSTRAINT PK_Usuarios PRIMARY KEY (Id)
    );
    PRINT 'Tabla Usuarios creada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AsignacionesUsuario')
BEGIN
    CREATE TABLE AsignacionesUsuario (
        Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        UsuarioId UNIQUEIDENTIFIER NOT NULL,
        InstitucionId VARCHAR(50) NOT NULL,
        AreaId VARCHAR(50) NULL,
        UnidadId VARCHAR(50) NULL,
        Rol VARCHAR(50) NOT NULL, -- Valores: 'JefeInstitucion', 'JefeArea', 'JefeUnidad', 'Empleado', 'Consultor'
        
        -- Auditoría
        UsuarioCreo NVARCHAR(150) NOT NULL DEFAULT 'Sistema',
        UsuarioModifico NVARCHAR(150) NULL,
        FechaCreacion DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaModificacion DATETIME2 NULL,

        CONSTRAINT PK_AsignacionesUsuario PRIMARY KEY (Id),
        CONSTRAINT FK_AsignacionesUsuario_Usuarios FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id),
        CONSTRAINT FK_AsignacionesUsuario_Institucion FOREIGN KEY (InstitucionId) REFERENCES Institucion(Id),
        CONSTRAINT FK_AsignacionesUsuario_Area FOREIGN KEY (AreaId) REFERENCES Area(Id),
        CONSTRAINT FK_AsignacionesUsuario_Unidad FOREIGN KEY (UnidadId) REFERENCES Unidad(Id)
    );
    PRINT 'Tabla AsignacionesUsuario creada.';
END
GO

-- ============================================================
--  3. TABLA DE MOVIMIENTOS Y PREFIJOS
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Movimientos')
BEGIN
    CREATE TABLE Movimientos (
        Id VARCHAR(50) NOT NULL,
        Nombre NVARCHAR(150) NOT NULL,
        Descripcion NVARCHAR(500) NULL,
        
        -- Auditoría
        UsuarioCreo NVARCHAR(150) NOT NULL,
        UsuarioModifico NVARCHAR(150) NULL,
        FechaCreacion DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaModificacion DATETIME2 NULL,

        CONSTRAINT PK_Movimientos PRIMARY KEY (Id)
    );
    PRINT 'Tabla Movimientos creada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Prefijos')
BEGIN
    CREATE TABLE Prefijos (
        PrefijoInstitucion VARCHAR(50) NOT NULL,
        PrefijoMovimiento VARCHAR(50) NOT NULL,
        UltimoValor INT NOT NULL DEFAULT 0,
        UltimoCodigo VARCHAR(100) NULL,

        CONSTRAINT PK_Prefijos PRIMARY KEY (PrefijoInstitucion, PrefijoMovimiento)
    );
    PRINT 'Tabla Prefijos creada.';
END
GO

-- ============================================================
--  STORED PROCEDURE: SP_GenerarCodigoMovimiento
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'SP_GenerarCodigoMovimiento')
BEGIN
    EXEC('
    CREATE PROCEDURE SP_GenerarCodigoMovimiento
        @Institucion VARCHAR(50),
        @Movimiento VARCHAR(50),
        @NuevoCodigo VARCHAR(100) OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;
        
        BEGIN TRANSACTION;
        
        DECLARE @ActualValor INT = 0;
        
        -- UPDLOCK para evitar concurrencia
        SELECT @ActualValor = UltimoValor
        FROM Prefijos WITH (UPDLOCK, HOLDLOCK)
        WHERE PrefijoInstitucion = @Institucion AND PrefijoMovimiento = @Movimiento;
        
        IF @@ROWCOUNT = 0
        BEGIN
            SET @ActualValor = 1;
            SET @NuevoCodigo = @Institucion + ''-'' + @Movimiento + ''-1'';
            
            INSERT INTO Prefijos (PrefijoInstitucion, PrefijoMovimiento, UltimoValor, UltimoCodigo)
            VALUES (@Institucion, @Movimiento, @ActualValor, @NuevoCodigo);
        END
        ELSE
        BEGIN
            SET @ActualValor = @ActualValor + 1;
            SET @NuevoCodigo = @Institucion + ''-'' + @Movimiento + ''-'' + CAST(@ActualValor AS VARCHAR(20));
            
            UPDATE Prefijos
            SET UltimoValor = @ActualValor,
                UltimoCodigo = @NuevoCodigo
            WHERE PrefijoInstitucion = @Institucion AND PrefijoMovimiento = @Movimiento;
        END
        
        COMMIT TRANSACTION;
    END
    ');
    PRINT 'Stored Procedure SP_GenerarCodigoMovimiento creado.';
END
GO

-- ============================================================
--  4. TABLAS TRANSACCIONALES (Con Campos de Jerarquía)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Levantamientos')
BEGIN
    CREATE TABLE Levantamientos (
        Id                     UNIQUEIDENTIFIER    NOT NULL  DEFAULT NEWSEQUENTIALID(),
        
        -- Jerarquía y Acceso
        InstitucionId          VARCHAR(50)         NULL,
        AreaId                 VARCHAR(50)         NULL,
        UnidadId               VARCHAR(50)         NULL,

        Institucion            NVARCHAR(100)       NOT NULL, 
        Encargado              NVARCHAR(150)       NOT NULL,
        Correo                 NVARCHAR(200)       NOT NULL,
        Celular                NVARCHAR(20)        NULL,

        Estado                 NVARCHAR(60)        NOT NULL,
        ObsEstado              NVARCHAR(1000)      NULL,

        MigradaSOL             NVARCHAR(30)        NOT NULL,
        Limitante              NVARCHAR(60)        NULL,
        LimitanteObs           NVARCHAR(500)       NULL,

        Personal               NVARCHAR(30)        NOT NULL,
        PersonalObs            NVARCHAR(500)       NULL,

        RequiereAcompanamiento BIT                 NOT NULL  DEFAULT 0,
        Habilidad              NVARCHAR(60)        NULL,
        HabilidadObs           NVARCHAR(500)       NULL,

        ObsGenerales           NVARCHAR(2000)      NULL,

        -- Auditoría
        UsuarioCreo            NVARCHAR(150)       NOT NULL DEFAULT 'Sistema',
        UsuarioModifico        NVARCHAR(150)       NULL,
        FechaCreacion          DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaModificacion      DATETIME2           NULL,
        CreatedAt              DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt              DATETIME2           NULL,

        CONSTRAINT PK_Levantamientos PRIMARY KEY (Id),
        CONSTRAINT FK_Levantamientos_Institucion FOREIGN KEY (InstitucionId) REFERENCES Institucion(Id),
        CONSTRAINT FK_Levantamientos_Area FOREIGN KEY (AreaId) REFERENCES Area(Id),
        CONSTRAINT FK_Levantamientos_Unidad FOREIGN KEY (UnidadId) REFERENCES Unidad(Id)
    );
    CREATE UNIQUE INDEX UIX_Levantamientos_Institucion ON Levantamientos (Institucion);
    CREATE INDEX IX_Levantamientos_FechaCreacion ON Levantamientos (FechaCreacion);
    PRINT 'Tabla Levantamientos creada.';
END
GO

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
        
        -- Jerarquía
        InstitucionId          VARCHAR(50)         NULL,
        AreaId                 VARCHAR(50)         NULL,
        UnidadId               VARCHAR(50)         NULL,

        -- Auditoría
        UsuarioCreo            NVARCHAR(150)       NOT NULL DEFAULT 'Sistema',
        UsuarioModifico        NVARCHAR(150)       NULL,
        FechaCreacion          DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaModificacion      DATETIME2           NULL,

        CONSTRAINT PK_TramitesChecklist PRIMARY KEY (Id),
        CONSTRAINT FK_TramitesChecklist_Levantamiento FOREIGN KEY (LevantamientoId) REFERENCES Levantamientos (Id) ON DELETE CASCADE,
        CONSTRAINT FK_TramitesChecklist_Institucion FOREIGN KEY (InstitucionId) REFERENCES Institucion(Id),
        CONSTRAINT FK_TramitesChecklist_Area FOREIGN KEY (AreaId) REFERENCES Area(Id),
        CONSTRAINT FK_TramitesChecklist_Unidad FOREIGN KEY (UnidadId) REFERENCES Unidad(Id)
    );
    CREATE INDEX IX_TramitesChecklist_LevantamientoId_Orden ON TramitesChecklist (LevantamientoId, Orden);
    PRINT 'Tabla TramitesChecklist creada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MiembrosEquipo')
BEGIN
    CREATE TABLE MiembrosEquipo (
        Id               UNIQUEIDENTIFIER    NOT NULL  DEFAULT NEWSEQUENTIALID(),
        LevantamientoId  UNIQUEIDENTIFIER    NOT NULL,
        Funcion          NVARCHAR(100)       NOT NULL,
        Nombre           NVARCHAR(150)       NOT NULL,
        Contacto         NVARCHAR(200)       NULL,
        Orden            INT                 NOT NULL  DEFAULT 0,
        
        -- Jerarquía
        InstitucionId          VARCHAR(50)         NULL,
        AreaId                 VARCHAR(50)         NULL,
        UnidadId               VARCHAR(50)         NULL,
        
        -- Auditoría
        UsuarioCreo            NVARCHAR(150)       NOT NULL DEFAULT 'Sistema',
        UsuarioModifico        NVARCHAR(150)       NULL,
        FechaCreacion          DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaModificacion      DATETIME2           NULL,

        CONSTRAINT PK_MiembrosEquipo PRIMARY KEY (Id),
        CONSTRAINT FK_MiembrosEquipo_Levantamiento FOREIGN KEY (LevantamientoId) REFERENCES Levantamientos (Id) ON DELETE CASCADE,
        CONSTRAINT FK_MiembrosEquipo_Institucion FOREIGN KEY (InstitucionId) REFERENCES Institucion(Id),
        CONSTRAINT FK_MiembrosEquipo_Area FOREIGN KEY (AreaId) REFERENCES Area(Id),
        CONSTRAINT FK_MiembrosEquipo_Unidad FOREIGN KEY (UnidadId) REFERENCES Unidad(Id)
    );
    CREATE INDEX IX_MiembrosEquipo_LevantamientoId ON MiembrosEquipo (LevantamientoId);
    PRINT 'Tabla MiembrosEquipo creada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DocumentosAdjuntos')
BEGIN
    CREATE TABLE DocumentosAdjuntos (
        Id               UNIQUEIDENTIFIER    NOT NULL  DEFAULT NEWSEQUENTIALID(),
        LevantamientoId  UNIQUEIDENTIFIER    NOT NULL,
        Nombre           NVARCHAR(200)       NOT NULL,
        Tipo             NVARCHAR(30)        NOT NULL,
        Url              NVARCHAR(500)       NULL,
        FechaDocumento   DATE                NULL,
        
        -- Jerarquía
        InstitucionId          VARCHAR(50)         NULL,
        AreaId                 VARCHAR(50)         NULL,
        UnidadId               VARCHAR(50)         NULL,
        
        -- Auditoría
        UsuarioCreo            NVARCHAR(150)       NOT NULL DEFAULT 'Sistema',
        UsuarioModifico        NVARCHAR(150)       NULL,
        FechaCreacion          DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
        FechaModificacion      DATETIME2           NULL,
        FechaRegistro          DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_DocumentosAdjuntos PRIMARY KEY (Id),
        CONSTRAINT FK_DocumentosAdjuntos_Levantamiento FOREIGN KEY (LevantamientoId) REFERENCES Levantamientos (Id) ON DELETE CASCADE,
        CONSTRAINT FK_DocumentosAdjuntos_Institucion FOREIGN KEY (InstitucionId) REFERENCES Institucion(Id),
        CONSTRAINT FK_DocumentosAdjuntos_Area FOREIGN KEY (AreaId) REFERENCES Area(Id),
        CONSTRAINT FK_DocumentosAdjuntos_Unidad FOREIGN KEY (UnidadId) REFERENCES Unidad(Id)
    );
    CREATE INDEX IX_DocumentosAdjuntos_LevantamientoId ON DocumentosAdjuntos (LevantamientoId);
    PRINT 'Tabla DocumentosAdjuntos creada.';
END
GO

-- ============================================================
--  Fin del script
-- ============================================================
PRINT '✔ Base de datos DigerTramitesEstado lista con estructura escalable.';
GO

-- ============================================================
--  SOFT-DELETE: Agregar columna IsDeleted a tablas transaccionales
--  (Idempotente: seguro ejecutar en base de datos existente)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Expedientes') AND name = 'IsDeleted')
BEGIN
    ALTER TABLE dbo.Expedientes ADD IsDeleted BIT NOT NULL DEFAULT 0;
    PRINT 'Columna IsDeleted agregada a Expedientes.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Contactos') AND name = 'IsDeleted')
BEGIN
    ALTER TABLE dbo.Contactos ADD IsDeleted BIT NOT NULL DEFAULT 0;
    PRINT 'Columna IsDeleted agregada a Contactos.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reuniones') AND name = 'IsDeleted')
BEGIN
    ALTER TABLE dbo.Reuniones ADD IsDeleted BIT NOT NULL DEFAULT 0;
    PRINT 'Columna IsDeleted agregada a Reuniones.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Tickets') AND name = 'IsDeleted')
BEGIN
    ALTER TABLE dbo.Tickets ADD IsDeleted BIT NOT NULL DEFAULT 0;
    PRINT 'Columna IsDeleted agregada a Tickets.';
END
GO

PRINT '✔ Soft-Delete aplicado correctamente en todas las tablas transaccionales.';
GO
