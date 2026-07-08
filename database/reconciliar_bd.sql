-- ============================================================
--  Diger.TramitesEstado — Script de RECONCILIACIÓN
--  Propósito: Poner la BD existente al día con el modelo EF Core nuevo
--  (Fase 1: Jerarquía Institución/Área/Unidad)
--  Ejecutar UNA SOLA VEZ en DigerTramitesEstado_Dev
-- ============================================================

USE DigerTramitesEstado_Dev;
GO

PRINT '=== Iniciando reconciliación de BD con modelo EF Core nuevo ===';

-- ============================================================
--  PASO 1: Crear las tablas nuevas que EF Core necesita y que
--          la BD antigua no tiene: Areas, Unidades,
--          AsignacionesUsuario, Movimientos, Prefijos
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Areas')
BEGIN
    CREATE TABLE Areas (
        Id            NVARCHAR(120) NOT NULL,
        InstitucionId NVARCHAR(120) NOT NULL,
        Nombre        NVARCHAR(120) NOT NULL,
        Descripcion   NVARCHAR(MAX) NULL,
        NombreCorto   NVARCHAR(MAX) NULL,
        LogoUrl       NVARCHAR(MAX) NULL,
        CreatedAt     DATETIME2     NOT NULL DEFAULT '2026-01-01T00:00:00.000Z',
        CreatedBy     NVARCHAR(MAX) NULL,
        UpdatedAt     DATETIME2     NULL,
        UpdatedBy     NVARCHAR(MAX) NULL,
        CONSTRAINT PK_Areas PRIMARY KEY (Id),
        CONSTRAINT FK_Areas_Instituciones
            FOREIGN KEY (InstitucionId) REFERENCES Instituciones(Id) ON DELETE CASCADE
    );
    PRINT 'Tabla Areas creada.';
END
ELSE
    PRINT 'Tabla Areas ya existe.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Unidades')
BEGIN
    CREATE TABLE Unidades (
        Id          NVARCHAR(120) NOT NULL,
        AreaId      NVARCHAR(120) NOT NULL,
        Nombre      NVARCHAR(120) NOT NULL,
        Descripcion NVARCHAR(MAX) NULL,
        NombreCorto NVARCHAR(MAX) NULL,
        LogoUrl     NVARCHAR(MAX) NULL,
        CreatedAt   DATETIME2     NOT NULL DEFAULT '2026-01-01T00:00:00.000Z',
        CreatedBy   NVARCHAR(MAX) NULL,
        UpdatedAt   DATETIME2     NULL,
        UpdatedBy   NVARCHAR(MAX) NULL,
        CONSTRAINT PK_Unidades PRIMARY KEY (Id),
        CONSTRAINT FK_Unidades_Areas
            FOREIGN KEY (AreaId) REFERENCES Areas(Id) ON DELETE CASCADE
    );
    PRINT 'Tabla Unidades creada.';
END
ELSE
    PRINT 'Tabla Unidades ya existe.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AsignacionesUsuario')
BEGIN
    CREATE TABLE AsignacionesUsuario (
        Id            UNIQUEIDENTIFIER NOT NULL,
        UsuarioId     UNIQUEIDENTIFIER NOT NULL,
        InstitucionId NVARCHAR(120)    NOT NULL,
        AreaId        NVARCHAR(120)    NULL,
        UnidadId      NVARCHAR(120)    NULL,
        Rol           NVARCHAR(60)     NOT NULL,
        CreatedAt     DATETIME2        NOT NULL DEFAULT '2026-01-01T00:00:00.000Z',
        CreatedBy     NVARCHAR(MAX)    NULL,
        UpdatedAt     DATETIME2        NULL,
        UpdatedBy     NVARCHAR(MAX)    NULL,
        CONSTRAINT PK_AsignacionesUsuario PRIMARY KEY (Id),
        CONSTRAINT FK_AsignacionesUsuario_Usuarios
            FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id) ON DELETE CASCADE,
        CONSTRAINT FK_AsignacionesUsuario_Instituciones
            FOREIGN KEY (InstitucionId) REFERENCES Instituciones(Id) ON DELETE CASCADE
    );
    PRINT 'Tabla AsignacionesUsuario creada.';
END
ELSE
    PRINT 'Tabla AsignacionesUsuario ya existe.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Movimientos')
BEGIN
    CREATE TABLE Movimientos (
        Id          NVARCHAR(120) NOT NULL,
        Nombre      NVARCHAR(120) NOT NULL,
        Descripcion NVARCHAR(MAX) NULL,
        CreatedAt   DATETIME2     NOT NULL DEFAULT '2026-01-01T00:00:00.000Z',
        CreatedBy   NVARCHAR(MAX) NULL,
        UpdatedAt   DATETIME2     NULL,
        UpdatedBy   NVARCHAR(MAX) NULL,
        CONSTRAINT PK_Movimientos PRIMARY KEY (Id)
    );
    PRINT 'Tabla Movimientos creada.';
END
ELSE
    PRINT 'Tabla Movimientos ya existe.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Prefijos')
BEGIN
    CREATE TABLE Prefijos (
        PrefijoInstitucion NVARCHAR(120) NOT NULL,
        PrefijoMovimiento  NVARCHAR(120) NOT NULL,
        UltimoValor        INT           NOT NULL,
        UltimoCodigo       NVARCHAR(MAX) NULL,
        CONSTRAINT PK_Prefijos PRIMARY KEY (PrefijoInstitucion, PrefijoMovimiento)
    );
    PRINT 'Tabla Prefijos creada.';
END
ELSE
    PRINT 'Tabla Prefijos ya existe.';
GO

-- ============================================================
--  PASO 2: Agregar columnas faltantes a tablas existentes
--          (AreaId, UnidadId que el nuevo modelo requiere)
-- ============================================================

-- Expedientes: agregar AreaId y UnidadId
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Expedientes') AND name = 'AreaId')
BEGIN
    ALTER TABLE Expedientes ADD AreaId NVARCHAR(120) NULL;
    PRINT 'Columna AreaId agregada a Expedientes.';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Expedientes') AND name = 'UnidadId')
BEGIN
    ALTER TABLE Expedientes ADD UnidadId NVARCHAR(120) NULL;
    PRINT 'Columna UnidadId agregada a Expedientes.';
END
GO

-- Contactos: agregar AreaId y UnidadId
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contactos') AND name = 'AreaId')
BEGIN
    ALTER TABLE Contactos ADD AreaId NVARCHAR(120) NULL;
    PRINT 'Columna AreaId agregada a Contactos.';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contactos') AND name = 'UnidadId')
BEGIN
    ALTER TABLE Contactos ADD UnidadId NVARCHAR(120) NULL;
    PRINT 'Columna UnidadId agregada a Contactos.';
END
GO

-- Reuniones: agregar AreaId y UnidadId
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Reuniones') AND name = 'AreaId')
BEGIN
    ALTER TABLE Reuniones ADD AreaId NVARCHAR(120) NULL;
    PRINT 'Columna AreaId agregada a Reuniones.';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Reuniones') AND name = 'UnidadId')
BEGIN
    ALTER TABLE Reuniones ADD UnidadId NVARCHAR(120) NULL;
    PRINT 'Columna UnidadId agregada a Reuniones.';
END
GO

-- Tickets: agregar AreaId y UnidadId
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tickets') AND name = 'AreaId')
BEGIN
    ALTER TABLE Tickets ADD AreaId NVARCHAR(120) NULL;
    PRINT 'Columna AreaId agregada a Tickets.';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tickets') AND name = 'UnidadId')
BEGIN
    ALTER TABLE Tickets ADD UnidadId NVARCHAR(120) NULL;
    PRINT 'Columna UnidadId agregada a Tickets.';
END
GO

-- ============================================================
--  PASO 3: Agregar FKs hacia Areas y Unidades en las tablas
--          transaccionales (solo si no existen ya)
-- ============================================================

-- Expedientes → Areas
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Expedientes_Areas_AreaId')
BEGIN
    ALTER TABLE Expedientes
        ADD CONSTRAINT FK_Expedientes_Areas_AreaId
        FOREIGN KEY (AreaId) REFERENCES Areas(Id);
    PRINT 'FK Expedientes → Areas agregada.';
END

-- Expedientes → Unidades
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Expedientes_Unidades_UnidadId')
BEGIN
    ALTER TABLE Expedientes
        ADD CONSTRAINT FK_Expedientes_Unidades_UnidadId
        FOREIGN KEY (UnidadId) REFERENCES Unidades(Id);
    PRINT 'FK Expedientes → Unidades agregada.';
END

-- Contactos → Areas
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Contactos_Areas_AreaId')
BEGIN
    ALTER TABLE Contactos
        ADD CONSTRAINT FK_Contactos_Areas_AreaId
        FOREIGN KEY (AreaId) REFERENCES Areas(Id);
    PRINT 'FK Contactos → Areas agregada.';
END

-- Contactos → Unidades
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Contactos_Unidades_UnidadId')
BEGIN
    ALTER TABLE Contactos
        ADD CONSTRAINT FK_Contactos_Unidades_UnidadId
        FOREIGN KEY (UnidadId) REFERENCES Unidades(Id);
    PRINT 'FK Contactos → Unidades agregada.';
END

-- Reuniones → Areas
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Reuniones_Areas_AreaId')
BEGIN
    ALTER TABLE Reuniones
        ADD CONSTRAINT FK_Reuniones_Areas_AreaId
        FOREIGN KEY (AreaId) REFERENCES Areas(Id);
    PRINT 'FK Reuniones → Areas agregada.';
END

-- Reuniones → Unidades
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Reuniones_Unidades_UnidadId')
BEGIN
    ALTER TABLE Reuniones
        ADD CONSTRAINT FK_Reuniones_Unidades_UnidadId
        FOREIGN KEY (UnidadId) REFERENCES Unidades(Id);
    PRINT 'FK Reuniones → Unidades agregada.';
END

-- Tickets → Areas
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Tickets_Areas_AreaId')
BEGIN
    ALTER TABLE Tickets
        ADD CONSTRAINT FK_Tickets_Areas_AreaId
        FOREIGN KEY (AreaId) REFERENCES Areas(Id);
    PRINT 'FK Tickets → Areas agregada.';
END

-- Tickets → Unidades
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Tickets_Unidades_UnidadId')
BEGIN
    ALTER TABLE Tickets
        ADD CONSTRAINT FK_Tickets_Unidades_UnidadId
        FOREIGN KEY (UnidadId) REFERENCES Unidades(Id);
    PRINT 'FK Tickets → Unidades agregada.';
END
GO

-- ============================================================
--  PASO 4: Limpiar el historial de migraciones antiguas y
--          registrar la única migración del nuevo proyecto
--          para que MigrateAsync() no intente recrear todo.
-- ============================================================

-- Borramos los registros de migraciones del proyecto anterior
-- (el nuevo proyecto tiene una sola migración: InitialCreate)
DELETE FROM __EFMigrationsHistory;
PRINT 'Historial de migraciones antiguas limpiado.';

-- Registramos la nueva migración como ya aplicada
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
VALUES ('20260706200739_InitialCreate', '9.0.0');
PRINT 'Migración 20260706200739_InitialCreate registrada como aplicada.';
GO

PRINT '=== Reconciliación completada exitosamente. ===';
PRINT 'La aplicación debería funcionar correctamente ahora.';
GO
