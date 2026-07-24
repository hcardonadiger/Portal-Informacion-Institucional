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
