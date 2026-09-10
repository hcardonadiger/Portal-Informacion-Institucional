# Historial de Scripts y Cambios en la Base de Datos

Este archivo contiene todos los scripts SQL y migraciones ejecutados en la base de datos del proyecto **Diger.TramitesEstado**. Está pensado para que los compañeros del equipo puedan copiar y ejecutar estos scripts directamente en sus bases de datos locales o de desarrollo.

---

## [2026-07-21] Inactivación de Catálogos (Agregar campo `Activo` a `Areas` y `Unidades`)

### Descripción
Se agregó el campo lógico `Activo` (`bit NOT NULL DEFAULT 1`) a las tablas `Areas` y `Unidades` para permitir inactivar registros en lugar de realizar eliminaciones físicas.

### Migración EF Core Asociada
- **Nombre de Migración:** `20260721152706_AddActivoToAreaAndUnidad`
- **Comando EF:** `dotnet ef database update`

### Script SQL Directo para Ejecutar en SQL Server:

```sql
-- Agregar campo Activo a la tabla Unidades
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Unidades]') AND name = 'Activo')
BEGIN
    ALTER TABLE [Unidades] ADD [Activo] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

-- Agregar campo Activo a la tabla Areas
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Areas]') AND name = 'Activo')
BEGIN
    ALTER TABLE [Areas] ADD [Activo] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO
```

---

## [2026-07-21] Comentarios y Evidencias en Compromisos (`ComentariosCompromisos`)

### Descripción
Se creó la tabla `ComentariosCompromisos` para almacenar los comentarios y archivos de avance/evidencia adjuntos por los usuarios en cada acuerdo/compromiso de reunión.

### Migración EF Core Asociada
- **Nombre de Migración:** `AddComentariosCompromisos`

### Script SQL Directo para Ejecutar en SQL Server:

```sql
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ComentariosCompromisos')
BEGIN
    CREATE TABLE [ComentariosCompromisos] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [AcuerdoReunionId] int NOT NULL,
        [Comentario] nvarchar(4000) NULL,
        [ArchivoNombre] nvarchar(255) NULL,
        [ArchivoUrl] nvarchar(1000) NULL,
        [ArchivoTamano] bigint NULL,
        [CreadoPor] nvarchar(200) NOT NULL,
        [CreadoPorRol] nvarchar(100) NULL,
        [CreadoEl] datetime2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_ComentariosCompromisos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ComentariosCompromisos_AcuerdosReunion_AcuerdoReunionId] FOREIGN KEY ([AcuerdoReunionId]) REFERENCES [AcuerdosReunion] ([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_ComentariosCompromisos_AcuerdoReunionId] ON [ComentariosCompromisos] ([AcuerdoReunionId]);
END;
GO
```

---

## [2026-07-23] Recuperación de Contraseña (Campos `PasswordResetToken` y `PasswordResetTokenExpiration` en `Usuarios`)

### Descripción
Se agregaron los campos `PasswordResetToken` (`nvarchar(256) NULL`) y `PasswordResetTokenExpiration` (`datetime2 NULL`) a la tabla `Usuarios` para permitir el proceso de recuperación de contraseña olvidada mediante tokens temporales por correo electrónico.

### Migración EF Core Asociada
- **Nombre de Migración:** `20260723100000_AddPasswordResetTokenToUsuario` (o `dotnet ef migrations add AddPasswordResetTokenToUsuario`)
- **Comando EF:** `dotnet ef database update`

### Script SQL Directo para Ejecutar en SQL Server:

```sql
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Usuarios]') AND name = 'PasswordResetToken')
BEGIN
    ALTER TABLE [Usuarios] ADD [PasswordResetToken] nvarchar(256) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Usuarios]') AND name = 'PasswordResetTokenExpiration')
BEGIN
    ALTER TABLE [Usuarios] ADD [PasswordResetTokenExpiration] datetime2 NULL;
END;
GO
```

---

## [2026-07-23] Agregar Campo `Area` a la Tabla `Contactos`

### Descripción
Se agregó la columna `Area` (`nvarchar(150) NULL`) a la tabla `Contactos` para almacenar el nombre del área o departamento de la institución (seleccionada del catálogo o ingresada manualmente como "Otros") tanto desde la gestión de contactos como en la captura de asistencia de reuniones.

### Migración EF Core Asociada
- **Nombre de Migración:** `AddAreaToContacto`
- **Comando EF:** `dotnet ef database update`

### Script SQL Directo para Ejecutar en SQL Server:

```sql
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Contactos]') AND name = 'Area')
BEGIN
    ALTER TABLE [Contactos] ADD [Area] nvarchar(150) NULL;
END;
GO
```

---

## [2026-07-24] Creación de Tabla `Recursos` para Repositorio de Archivos y Plantillas

### Descripción
Se creó la tabla `Recursos` para gestionar el repositorio centralizado de recursos, plantillas y archivos descargables del portal. Almacena título, descripción, categoría, metadatos del archivo adjunto (nombre original, ruta local, tamaño en bytes), contador de descargas y auditoría estándar con soft-delete (`IsDeleted`).

### Migración EF Core Asociada
- **Nombre de Migración:** `20260724153537_AddRecursosTable`
- **Comando EF:** `dotnet ef database update`

### Script SQL Directo para Ejecutar en SQL Server:

```sql
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Recursos')
BEGIN
    CREATE TABLE [Recursos] (
        [Id] int NOT NULL IDENTITY(1,1),
        [IsDeleted] bit NOT NULL DEFAULT 0,
        [Titulo] nvarchar(max) NOT NULL,
        [Descripcion] nvarchar(max) NULL,
        [Categoria] nvarchar(max) NOT NULL,
        [ArchivoNombre] nvarchar(max) NOT NULL,
        [ArchivoUrl] nvarchar(max) NOT NULL,
        [ArchivoTamano] bigint NOT NULL,
        [DescargasCount] int NOT NULL DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Recursos] PRIMARY KEY ([Id])
    );
END;
GO
```

---

## [2026-07-27] Vinculación Reunión-Expediente-Trámites y Contraparte Institucional en Expedientes

### Descripción
1. Se agregaron los campos `ExpedienteId` e `ExpedienteCodigo` a la tabla `Reuniones` para vincular una reunión a un expediente.
2. Se agregaron los campos `ExpedienteId`, `TramiteIndex` y `TramiteNombre` a la tabla `AcuerdosReunion` (compromisos) para relacionar un compromiso con un trámite específico.
3. Se agregaron los campos `ContraparteUsuarioId`, `ContraparteUsuarioNombre` y `FechaLimiteEntrega` a la tabla `Expedientes` para asignar la contraparte institucional y su fecha límite de llenado de ficha.

### Migración EF Core Asociada
- **Nombre de Migración:** `20260727195213_AddReunionExpedienteYContraparte`
- **Comando EF:** `dotnet ef database update`

### Script SQL Directo para Ejecutar en SQL Server:

```sql
-- Campos en Reuniones
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Reuniones]') AND name = 'ExpedienteId')
BEGIN
    ALTER TABLE [Reuniones] ADD [ExpedienteId] int NULL;
    ALTER TABLE [Reuniones] ADD [ExpedienteCodigo] nvarchar(max) NULL;
END;
GO

-- Campos en AcuerdosReunion (Compromisos)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[AcuerdosReunion]') AND name = 'ExpedienteId')
BEGIN
    ALTER TABLE [AcuerdosReunion] ADD [ExpedienteId] int NULL;
    ALTER TABLE [AcuerdosReunion] ADD [TramiteIndex] int NULL;
    ALTER TABLE [AcuerdosReunion] ADD [TramiteNombre] nvarchar(max) NULL;
END;
GO

-- Campos en Expedientes (Contraparte Institucional y Plazo)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Expedientes]') AND name = 'ContraparteUsuarioId')
BEGIN
    ALTER TABLE [Expedientes] ADD [ContraparteUsuarioId] uniqueidentifier NULL;
    ALTER TABLE [Expedientes] ADD [ContraparteUsuarioNombre] nvarchar(max) NULL;
    ALTER TABLE [Expedientes] ADD [FechaLimiteEntrega] date NULL;
END;
GO
```

---

## [2026-07-28] Datos de demo: catálogo realista de CONSUCOOP y usuarios de prueba

### Descripción
Script **de datos** (no cambia el esquema). Hace tres cosas:

1. **Renombra el catálogo genérico de CONSUCOOP.** `AREA 1`/`AREA 2` y `UNIDAD 1..4` pasan a
   nombres reales, y sus IDs dejan de ser genéricos (`AREA-01` → `CSC-SUPV`, etc.). Como las FK
   son `NO_ACTION` en update, el cambio de ID se hace insertando la fila nueva, repuntando los
   hijos y borrando la vieja.
2. **Crea 8 usuarios de demo** (contraseña `Demo#2026` para todos): 2 de mantenimiento/soporte,
   2 jefes de unidad de instituciones distintas y 2 empleados por cada una de esas unidades.
3. **Crea 6 tickets de demo** repartidos entre temas, instituciones y estados, con vencimientos
   de SLA para que el filtro "Solo vencidos" y los KPIs del tablero muestren datos.

> ⚠️ **Los dos usuarios de mantenimiento van sin área ni unidad, a propósito.** El filtro global
> de `Ticket` en `AppDbContext` para el rol `Empleado` es `t.UnidadId == _activeUnidad`, **sin**
> el `|| t.UnidadId == null` que sí tienen `Expediente`, `Contacto` y `Reunion`. Como
> `CrearTicketCommand` nunca asigna área ni unidad al ticket, un técnico **con** unidad asignada
> vería **cero tickets**. Dejarlos sin unidad es lo que les permite atender tickets de cualquier
> institución, acotados únicamente por sus temas (`UsuarioTemas`).

### Migración EF Core Asociada
Ninguna — es un script de datos. No modifica el esquema ni requiere `dotnet ef database update`.

### Requisitos previos
- Debe existir el usuario `admin@diger.gob.hn` (los tickets se registran a su nombre).
- Deben existir las instituciones `DIGER`, `CONSUCOOP`, `SENASA`, `CNBS`, `IHADFA`, `SESAL`.
- Deben existir los temas `Acceso`, `Error en plataforma`, `Configuración`, `Datos`,
  `Capacitación` y `Otro`.
- Si su BD ya usa los números `TCK-2026-0007` a `TCK-2026-0012`, cambie los números del bloque 5.

### Script SQL Directo para Ejecutar en SQL Server:

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;   -- requerido: el esquema tiene índices filtrados
SET ANSI_NULLS ON;
GO

-- ── 1. Catálogo de CONSUCOOP: nombres e IDs realistas ─────────────────────
IF NOT EXISTS (SELECT 1 FROM Areas WHERE Id = N'CSC-SUPV')
    INSERT INTO Areas (Id, InstitucionId, Nombre, NombreCorto, CreatedAt, Activo)
    VALUES (N'CSC-SUPV', N'CONSUCOOP', N'Supervisión y Vigilancia', N'Supervisión', SYSUTCDATETIME(), 1);

IF NOT EXISTS (SELECT 1 FROM Areas WHERE Id = N'CSC-REG')
    INSERT INTO Areas (Id, InstitucionId, Nombre, NombreCorto, CreatedAt, Activo)
    VALUES (N'CSC-REG', N'CONSUCOOP', N'Registro y Autorizaciones', N'Registro', SYSUTCDATETIME(), 1);
GO

IF NOT EXISTS (SELECT 1 FROM Unidades WHERE Id = N'CSC-SUPV-IS')
    INSERT INTO Unidades (Id, AreaId, Nombre, NombreCorto, CreatedAt, Activo)
    VALUES (N'CSC-SUPV-IS', N'CSC-SUPV', N'Supervisión In Situ', N'In Situ', SYSUTCDATETIME(), 1);

IF NOT EXISTS (SELECT 1 FROM Unidades WHERE Id = N'CSC-SUPV-ES')
    INSERT INTO Unidades (Id, AreaId, Nombre, NombreCorto, CreatedAt, Activo)
    VALUES (N'CSC-SUPV-ES', N'CSC-SUPV', N'Supervisión Extra Situ', N'Extra Situ', SYSUTCDATETIME(), 1);

IF NOT EXISTS (SELECT 1 FROM Unidades WHERE Id = N'CSC-REG-COOP')
    INSERT INTO Unidades (Id, AreaId, Nombre, NombreCorto, CreatedAt, Activo)
    VALUES (N'CSC-REG-COOP', N'CSC-REG', N'Registro de Cooperativas', N'Reg. Coop.', SYSUTCDATETIME(), 1);

IF NOT EXISTS (SELECT 1 FROM Unidades WHERE Id = N'CSC-REG-AUT')
    INSERT INTO Unidades (Id, AreaId, Nombre, NombreCorto, CreatedAt, Activo)
    VALUES (N'CSC-REG-AUT', N'CSC-REG', N'Autorizaciones y Licencias', N'Autorizaciones', SYSUTCDATETIME(), 1);
GO

-- Repuntar cualquier referencia a los IDs viejos antes de borrarlos
UPDATE AsignacionesUsuario SET AreaId = N'CSC-SUPV' WHERE AreaId = N'AREA-01';
UPDATE AsignacionesUsuario SET AreaId = N'CSC-REG'  WHERE AreaId = N'AREA-02';
UPDATE AsignacionesUsuario SET UnidadId = N'CSC-SUPV-IS'  WHERE UnidadId = N'UNID-01';
UPDATE AsignacionesUsuario SET UnidadId = N'CSC-SUPV-ES'  WHERE UnidadId = N'UNID-02';
UPDATE AsignacionesUsuario SET UnidadId = N'CSC-REG-COOP' WHERE UnidadId = N'UNID-03';
UPDATE AsignacionesUsuario SET UnidadId = N'CSC-REG-AUT'  WHERE UnidadId = N'UNID-04';

UPDATE Expedientes SET AreaId = N'CSC-SUPV' WHERE AreaId = N'AREA-01';
UPDATE Expedientes SET AreaId = N'CSC-REG'  WHERE AreaId = N'AREA-02';
UPDATE Expedientes SET UnidadId = N'CSC-SUPV-IS'  WHERE UnidadId = N'UNID-01';
UPDATE Expedientes SET UnidadId = N'CSC-SUPV-ES'  WHERE UnidadId = N'UNID-02';
UPDATE Expedientes SET UnidadId = N'CSC-REG-COOP' WHERE UnidadId = N'UNID-03';
UPDATE Expedientes SET UnidadId = N'CSC-REG-AUT'  WHERE UnidadId = N'UNID-04';

UPDATE Tickets SET AreaId = N'CSC-SUPV' WHERE AreaId = N'AREA-01';
UPDATE Tickets SET AreaId = N'CSC-REG'  WHERE AreaId = N'AREA-02';
UPDATE Tickets SET UnidadId = N'CSC-SUPV-IS'  WHERE UnidadId = N'UNID-01';
UPDATE Tickets SET UnidadId = N'CSC-SUPV-ES'  WHERE UnidadId = N'UNID-02';
UPDATE Tickets SET UnidadId = N'CSC-REG-COOP' WHERE UnidadId = N'UNID-03';
UPDATE Tickets SET UnidadId = N'CSC-REG-AUT'  WHERE UnidadId = N'UNID-04';

UPDATE Contactos SET AreaId = N'CSC-SUPV' WHERE AreaId = N'AREA-01';
UPDATE Contactos SET AreaId = N'CSC-REG'  WHERE AreaId = N'AREA-02';
UPDATE Contactos SET UnidadId = N'CSC-SUPV-IS'  WHERE UnidadId = N'UNID-01';
UPDATE Contactos SET UnidadId = N'CSC-SUPV-ES'  WHERE UnidadId = N'UNID-02';
UPDATE Contactos SET UnidadId = N'CSC-REG-COOP' WHERE UnidadId = N'UNID-03';
UPDATE Contactos SET UnidadId = N'CSC-REG-AUT'  WHERE UnidadId = N'UNID-04';

UPDATE Reuniones SET AreaId = N'CSC-SUPV' WHERE AreaId = N'AREA-01';
UPDATE Reuniones SET AreaId = N'CSC-REG'  WHERE AreaId = N'AREA-02';
UPDATE Reuniones SET UnidadId = N'CSC-SUPV-IS'  WHERE UnidadId = N'UNID-01';
UPDATE Reuniones SET UnidadId = N'CSC-SUPV-ES'  WHERE UnidadId = N'UNID-02';
UPDATE Reuniones SET UnidadId = N'CSC-REG-COOP' WHERE UnidadId = N'UNID-03';
UPDATE Reuniones SET UnidadId = N'CSC-REG-AUT'  WHERE UnidadId = N'UNID-04';
GO

DELETE FROM Unidades WHERE Id IN (N'UNID-01', N'UNID-02', N'UNID-03', N'UNID-04');
DELETE FROM Areas    WHERE Id IN (N'AREA-01', N'AREA-02');
GO

-- ── 2. Usuarios del demo (contraseña: Demo#2026) ──────────────────────────
-- Hash PBKDF2-SHA256, 100 000 iteraciones, formato {iteraciones}.{salt}.{hash}
IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Correo = N'soporte.plataforma@diger.gob.hn')
    INSERT INTO Usuarios (Id, Nombre, Correo, PasswordHash, Activo, CreatedAt, CreatedBy)
    VALUES ('A0000001-0000-4000-8000-000000000001', N'Ana Maradiaga', N'soporte.plataforma@diger.gob.hn',
            N'100000.Ohvofbx5kXeIudFWhwOzKA==.igdef1I7hy3RsUu/D1SZvKm4uaHgX2P/277xU+ciKhQ=',
            1, SYSUTCDATETIME(), N'Demo');

IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Correo = N'mesa.ayuda@diger.gob.hn')
    INSERT INTO Usuarios (Id, Nombre, Correo, PasswordHash, Activo, CreatedAt, CreatedBy)
    VALUES ('A0000002-0000-4000-8000-000000000002', N'Carlos Zelaya', N'mesa.ayuda@diger.gob.hn',
            N'100000.C+soGRMHnBWZ9yE3jjkKtg==.wWA/VJ6DSuJp6spYjYKuJdz2GMfZCu0fX6mCWg1n+Fo=',
            1, SYSUTCDATETIME(), N'Demo');

IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Correo = N'jefe.ditra@diger.gob.hn')
    INSERT INTO Usuarios (Id, Nombre, Correo, PasswordHash, Activo, CreatedAt, CreatedBy)
    VALUES ('A0000003-0000-4000-8000-000000000003', N'Marlon Discua', N'jefe.ditra@diger.gob.hn',
            N'100000.c4mzpbfwUVjlG8Us2K4huw==.TiN3M6WJ7ErpdlcWb38gixWqRFEUxb4ahC04JbRk3NQ=',
            1, SYSUTCDATETIME(), N'Demo');

IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Correo = N'jefe.insitu@consucoop.gob.hn')
    INSERT INTO Usuarios (Id, Nombre, Correo, PasswordHash, Activo, CreatedAt, CreatedBy)
    VALUES ('A0000004-0000-4000-8000-000000000004', N'Karla Núñez', N'jefe.insitu@consucoop.gob.hn',
            N'100000.oBlm0rgoYzixKBkf1wRLDw==.Hfus3Obbd+c0RD64N39sTgG86ZvaFcyOs33SFZswgbU=',
            1, SYSUTCDATETIME(), N'Demo');

IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Correo = N'oscar.banegas@diger.gob.hn')
    INSERT INTO Usuarios (Id, Nombre, Correo, PasswordHash, Activo, CreatedAt, CreatedBy)
    VALUES ('A0000005-0000-4000-8000-000000000005', N'Óscar Banegas', N'oscar.banegas@diger.gob.hn',
            N'100000.WdPsLgvIgsJjGgWfWKhtBA==./5lMZzDTdeEWzmFP2AYJ+e8Y1O9dd09R+FAyM6GPXzc=',
            1, SYSUTCDATETIME(), N'Demo');

IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Correo = N'lourdes.fajardo@diger.gob.hn')
    INSERT INTO Usuarios (Id, Nombre, Correo, PasswordHash, Activo, CreatedAt, CreatedBy)
    VALUES ('A0000006-0000-4000-8000-000000000006', N'Lourdes Fajardo', N'lourdes.fajardo@diger.gob.hn',
            N'100000.Y2JNxKs2DVqurtFdKJ0bZA==.xMHrgjIFVV5BhuW5gyYwXuVYe7w13ARNOohLKzCQlBg=',
            1, SYSUTCDATETIME(), N'Demo');

IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Correo = N'rene.portillo@consucoop.gob.hn')
    INSERT INTO Usuarios (Id, Nombre, Correo, PasswordHash, Activo, CreatedAt, CreatedBy)
    VALUES ('A0000007-0000-4000-8000-000000000007', N'René Portillo', N'rene.portillo@consucoop.gob.hn',
            N'100000.ZlNFxGtYv58hae17B+3Yew==.M6gdW3c1u+drl2Es1C6CbSaQjnyyFmMEeEgrund/+E4=',
            1, SYSUTCDATETIME(), N'Demo');

IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Correo = N'dilcia.herrera@consucoop.gob.hn')
    INSERT INTO Usuarios (Id, Nombre, Correo, PasswordHash, Activo, CreatedAt, CreatedBy)
    VALUES ('A0000008-0000-4000-8000-000000000008', N'Dilcia Herrera', N'dilcia.herrera@consucoop.gob.hn',
            N'100000.SNUGjKNTxf3vbSqGbrFARQ==.RI0NRE1mYWHGCq2xvlMpH0v/e6MNsmwZDoYQXjM/3sI=',
            1, SYSUTCDATETIME(), N'Demo');
GO

-- ── 3. Asignaciones (rol + alcance jerárquico) ────────────────────────────
IF NOT EXISTS (SELECT 1 FROM AsignacionesUsuario WHERE UsuarioId = 'A0000001-0000-4000-8000-000000000001')
    INSERT INTO AsignacionesUsuario (Id, UsuarioId, InstitucionId, AreaId, UnidadId, Rol, CreatedAt)
    VALUES ('B0000001-0000-4000-8000-000000000001', 'A0000001-0000-4000-8000-000000000001', N'DIGER', NULL, NULL, N'Empleado', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM AsignacionesUsuario WHERE UsuarioId = 'A0000002-0000-4000-8000-000000000002')
    INSERT INTO AsignacionesUsuario (Id, UsuarioId, InstitucionId, AreaId, UnidadId, Rol, CreatedAt)
    VALUES ('B0000002-0000-4000-8000-000000000002', 'A0000002-0000-4000-8000-000000000002', N'DIGER', NULL, NULL, N'Empleado', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM AsignacionesUsuario WHERE UsuarioId = 'A0000003-0000-4000-8000-000000000003')
    INSERT INTO AsignacionesUsuario (Id, UsuarioId, InstitucionId, AreaId, UnidadId, Rol, CreatedAt)
    VALUES ('B0000003-0000-4000-8000-000000000003', 'A0000003-0000-4000-8000-000000000003', N'DIGER', N'GOBDIG', N'DITRA', N'JefeUnidad', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM AsignacionesUsuario WHERE UsuarioId = 'A0000004-0000-4000-8000-000000000004')
    INSERT INTO AsignacionesUsuario (Id, UsuarioId, InstitucionId, AreaId, UnidadId, Rol, CreatedAt)
    VALUES ('B0000004-0000-4000-8000-000000000004', 'A0000004-0000-4000-8000-000000000004', N'CONSUCOOP', N'CSC-SUPV', N'CSC-SUPV-IS', N'JefeUnidad', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM AsignacionesUsuario WHERE UsuarioId = 'A0000005-0000-4000-8000-000000000005')
    INSERT INTO AsignacionesUsuario (Id, UsuarioId, InstitucionId, AreaId, UnidadId, Rol, CreatedAt)
    VALUES ('B0000005-0000-4000-8000-000000000005', 'A0000005-0000-4000-8000-000000000005', N'DIGER', N'GOBDIG', N'DITRA', N'Empleado', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM AsignacionesUsuario WHERE UsuarioId = 'A0000006-0000-4000-8000-000000000006')
    INSERT INTO AsignacionesUsuario (Id, UsuarioId, InstitucionId, AreaId, UnidadId, Rol, CreatedAt)
    VALUES ('B0000006-0000-4000-8000-000000000006', 'A0000006-0000-4000-8000-000000000006', N'DIGER', N'GOBDIG', N'DITRA', N'Empleado', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM AsignacionesUsuario WHERE UsuarioId = 'A0000007-0000-4000-8000-000000000007')
    INSERT INTO AsignacionesUsuario (Id, UsuarioId, InstitucionId, AreaId, UnidadId, Rol, CreatedAt)
    VALUES ('B0000007-0000-4000-8000-000000000007', 'A0000007-0000-4000-8000-000000000007', N'CONSUCOOP', N'CSC-SUPV', N'CSC-SUPV-IS', N'Empleado', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM AsignacionesUsuario WHERE UsuarioId = 'A0000008-0000-4000-8000-000000000008')
    INSERT INTO AsignacionesUsuario (Id, UsuarioId, InstitucionId, AreaId, UnidadId, Rol, CreatedAt)
    VALUES ('B0000008-0000-4000-8000-000000000008', 'A0000008-0000-4000-8000-000000000008', N'CONSUCOOP', N'CSC-SUPV', N'CSC-SUPV-IS', N'Empleado', SYSUTCDATETIME());
GO

-- ── 4. Temas que atiende cada técnico de mantenimiento ────────────────────
INSERT INTO UsuarioTemas (UsuarioId, TemaId)
SELECT 'A0000001-0000-4000-8000-000000000001', t.Id
FROM TemasTicket t
WHERE t.Nombre IN (N'Error en plataforma', N'Configuración', N'Datos')
  AND NOT EXISTS (SELECT 1 FROM UsuarioTemas ut
                  WHERE ut.UsuarioId = 'A0000001-0000-4000-8000-000000000001' AND ut.TemaId = t.Id);

INSERT INTO UsuarioTemas (UsuarioId, TemaId)
SELECT 'A0000002-0000-4000-8000-000000000002', t.Id
FROM TemasTicket t
WHERE t.Nombre IN (N'Acceso', N'Capacitación', N'Otro')
  AND NOT EXISTS (SELECT 1 FROM UsuarioTemas ut
                  WHERE ut.UsuarioId = 'A0000002-0000-4000-8000-000000000002' AND ut.TemaId = t.Id);
GO

-- ── 5. Tickets de demo ────────────────────────────────────────────────────
-- AreaId/UnidadId quedan NULL a propósito (ver la nota del encabezado).
-- Las fechas son relativas para que los vencimientos de SLA sigan siendo
-- válidos en el momento en que se ejecute el script.
DECLARE @admin  uniqueidentifier = (SELECT TOP 1 Id FROM Usuarios WHERE Correo = N'admin@diger.gob.hn');
DECLARE @ana    uniqueidentifier = 'A0000001-0000-4000-8000-000000000001';
DECLARE @carlos uniqueidentifier = 'A0000002-0000-4000-8000-000000000002';

IF NOT EXISTS (SELECT 1 FROM Tickets WHERE Numero = N'TCK-2026-0007')
    INSERT INTO Tickets (Numero, Titulo, Descripcion, TemaId, Prioridad, Estado, InstitucionId, Institucion,
                         ReportanteNombre, ReportanteCorreo, CreadoPorId, CreadoPor, AsignadoAId, AsignadoA, CreatedAt, IsDeleted)
    SELECT N'TCK-2026-0007', N'Error 500 al guardar la ficha del trámite',
           N'Al guardar la sección 2 del expediente el sistema devuelve un error 500 y se pierde lo capturado.',
           t.Id, N'Alta', N'EnProgreso', N'CONSUCOOP', (SELECT Nombre FROM Instituciones WHERE Id = N'CONSUCOOP'),
           N'René Portillo', N'rene.portillo@consucoop.gob.hn', @admin, N'Admin Global', @ana, N'Ana Maradiaga',
           DATEADD(day, -3, SYSUTCDATETIME()), 0
    FROM TemasTicket t WHERE t.Nombre = N'Error en plataforma';

IF NOT EXISTS (SELECT 1 FROM Tickets WHERE Numero = N'TCK-2026-0008')
    INSERT INTO Tickets (Numero, Titulo, Descripcion, TemaId, Prioridad, Estado, InstitucionId, Institucion,
                         ReportanteNombre, ReportanteCorreo, CreadoPorId, CreadoPor, AsignadoAId, AsignadoA,
                         FechaResolucion, NotaResolucion, CreatedAt, IsDeleted)
    SELECT N'TCK-2026-0008', N'Inconsistencia en el reporte de trámites por institución',
           N'El total de trámites del tablero no coincide con el listado de expedientes.',
           t.Id, N'Media', N'Resuelto', N'SENASA', (SELECT Nombre FROM Instituciones WHERE Id = N'SENASA'),
           N'Dilcia Herrera', N'dilcia.herrera@consucoop.gob.hn', @admin, N'Admin Global', @ana, N'Ana Maradiaga',
           DATEADD(day, -1, SYSUTCDATETIME()), N'Se corrigió el conteo: excluía los expedientes cerrados.',
           DATEADD(day, -6, SYSUTCDATETIME()), 0
    FROM TemasTicket t WHERE t.Nombre = N'Datos';

IF NOT EXISTS (SELECT 1 FROM Tickets WHERE Numero = N'TCK-2026-0009')
    INSERT INTO Tickets (Numero, Titulo, Descripcion, TemaId, Prioridad, Estado, InstitucionId, Institucion,
                         ReportanteNombre, ReportanteCorreo, CreadoPorId, CreadoPor, CreatedAt, IsDeleted)
    SELECT N'TCK-2026-0009', N'Usuario nuevo no puede iniciar sesión',
           N'Se creó la cuenta institucional pero al iniciar sesión indica credenciales inválidas.',
           t.Id, N'Critica', N'Abierto', N'CNBS', (SELECT Nombre FROM Instituciones WHERE Id = N'CNBS'),
           N'Óscar Banegas', N'oscar.banegas@diger.gob.hn', @admin, N'Admin Global',
           DATEADD(day, -5, SYSUTCDATETIME()), 0
    FROM TemasTicket t WHERE t.Nombre = N'Acceso';

IF NOT EXISTS (SELECT 1 FROM Tickets WHERE Numero = N'TCK-2026-0010')
    INSERT INTO Tickets (Numero, Titulo, Descripcion, TemaId, Prioridad, Estado, InstitucionId, Institucion,
                         ReportanteNombre, ReportanteCorreo, CreadoPorId, CreadoPor, AsignadoAId, AsignadoA, CreatedAt, IsDeleted)
    SELECT N'TCK-2026-0010', N'Solicitud de permisos para el módulo de Reuniones',
           N'El enlace institucional necesita acceso al módulo de Reuniones para registrar actas.',
           t.Id, N'Media', N'EnProgreso', N'IHADFA', (SELECT Nombre FROM Instituciones WHERE Id = N'IHADFA'),
           N'Lourdes Fajardo', N'lourdes.fajardo@diger.gob.hn', @admin, N'Admin Global', @carlos, N'Carlos Zelaya',
           DATEADD(hour, -6, SYSUTCDATETIME()), 0
    FROM TemasTicket t WHERE t.Nombre = N'Acceso';

IF NOT EXISTS (SELECT 1 FROM Tickets WHERE Numero = N'TCK-2026-0011')
    INSERT INTO Tickets (Numero, Titulo, Descripcion, TemaId, Prioridad, Estado, InstitucionId, Institucion,
                         ReportanteNombre, ReportanteCorreo, CreadoPorId, CreadoPor, CreatedAt, IsDeleted)
    SELECT N'TCK-2026-0011', N'Solicitud de capacitación en el módulo de Expedientes',
           N'La unidad de Registro de Cooperativas solicita una sesión de capacitación para 8 personas.',
           t.Id, N'Baja', N'Abierto', N'CONSUCOOP', (SELECT Nombre FROM Instituciones WHERE Id = N'CONSUCOOP'),
           N'Karla Núñez', N'jefe.insitu@consucoop.gob.hn', @admin, N'Admin Global',
           DATEADD(day, -1, SYSUTCDATETIME()), 0
    FROM TemasTicket t WHERE t.Nombre = N'Capacitación';

IF NOT EXISTS (SELECT 1 FROM Tickets WHERE Numero = N'TCK-2026-0012')
    INSERT INTO Tickets (Numero, Titulo, Descripcion, TemaId, Prioridad, Estado, InstitucionId, Institucion,
                         ReportanteNombre, ReportanteCorreo, CreadoPorId, CreadoPor, AsignadoAId, AsignadoA,
                         FechaResolucion, NotaResolucion, CreatedAt, IsDeleted)
    SELECT N'TCK-2026-0012', N'Consulta sobre el uso del catálogo SEFIN',
           N'Duda sobre qué rubro corresponde al pago TGR de un trámite de licencia.',
           t.Id, N'Baja', N'Resuelto', N'SESAL', (SELECT Nombre FROM Instituciones WHERE Id = N'SESAL'),
           N'Marlon Discua', N'jefe.ditra@diger.gob.hn', @admin, N'Admin Global', @carlos, N'Carlos Zelaya',
           DATEADD(day, -2, SYSUTCDATETIME()), N'Se indicó el rubro correcto y se compartió el instructivo.',
           DATEADD(day, -8, SYSUTCDATETIME()), 0
    FROM TemasTicket t WHERE t.Nombre = N'Otro';
GO
```

### Nota de ejecución
El archivo debe guardarse en **UTF-8** y ejecutarse con `QUOTED_IDENTIFIER ON`. Desde `sqlcmd`:

```
sqlcmd -S .\SQLEXPRESS -E -C -I -f 65001 -d <BaseDeDatos> -i datos_demo.sql
```

El flag `-I` activa `QUOTED_IDENTIFIER` (obligatorio por los índices filtrados del esquema) y
`-f 65001` preserva los acentos. Desde SSMS no hace falta nada de esto.
