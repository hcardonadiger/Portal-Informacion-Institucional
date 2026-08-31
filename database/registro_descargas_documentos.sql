/*
================================================================================
  Registro de descargas de documentos de proyecto.
  Migración 20260831005008_RegistroDescargasDocumentos
================================================================================

  CÓMO SE USA
  -----------
  Ejecútelo una vez contra la base del ambiente. No tiene modo simulación porque
  no modifica ni borra datos: solo crea una tabla nueva y la registra en el
  historial de EF. Es idempotente — volver a correrlo no hace nada.

      sqlcmd -S <servidor> -d <base> -U <usuario> -P <clave> -C -I ^
             -i database\registro_descargas_documentos.sql

  El modificador -I (QUOTED_IDENTIFIER ON) es obligatorio: las tablas del portal
  llevan índices filtrados y sin él SQL Server rechaza la escritura con el
  error 1934.

  QUÉ HACE
  --------
  Crea `ProyectoDocumentoDescargas`, la bitácora de quién descargó qué versión de
  qué documento y cuándo, y la deja anotada en `__EFMigrationsHistory` para que un
  `dotnet ef database update` posterior no intente crearla de nuevo.

  POR QUÉ ES UNA TABLA Y NO UN CONTADOR
  -------------------------------------
  El módulo de Recursos resuelve su caso con `DescargasCount`, un entero que se
  incrementa, y ahí alcanza: interesa cuánto se usa una plantilla, no quién la
  usó. La pregunta de coordinación es otra —«quién se llevó el convenio antes de
  que se firmara»— y esa no se responde con un número.

  Apunta a la VERSIÓN, no al documento: saber que alguien descargó «Convenio
  marco» no dice nada si no consta que se llevó la v1 y no la v2 ya corregida.

  QUÉ NO REGISTRA
  ---------------
  La dirección IP. El portal no la guarda en ninguna otra parte, y añadirla
  convertiría una traza de uso en un rastro de ubicación. Es una decisión que
  tendría que tomarse aparte, no colarse en esta tabla.

  ALCANCE
  -------
  La tabla hereda el filtro institucional a través de la versión —que cuelga del
  documento y este del proyecto—, igual que `ProyectoDocumentoVersiones`. Una
  consulta directa no se salta el alcance: nadie ve descargas de proyectos que no
  puede abrir.

  BORRADO EN CASCADA
  ------------------
  Borrar la versión se lleva su bitácora de descargas: sin el archivo, saber
  quién lo bajó no responde nada. La versión, a su vez, solo desaparece si
  desaparece el proyecto.

================================================================================
*/

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

-- ── Guardas previas ────────────────────────────────────────────────────────
IF OBJECT_ID('__EFMigrationsHistory') IS NULL
BEGIN
    RAISERROR('Esta base no tiene __EFMigrationsHistory: no es una base del portal creada por EF. No se hizo nada.', 16, 1);
    RETURN;
END

IF OBJECT_ID('ProyectoDocumentoVersiones') IS NULL
BEGIN
    RAISERROR('Falta ProyectoDocumentoVersiones. Aplique antes la migración del repositorio documental (RepositorioDocumentalProyectos). No se hizo nada.', 16, 1);
    RETURN;
END

IF EXISTS (SELECT 1 FROM [__EFMigrationsHistory]
           WHERE [MigrationId] = N'20260831005008_RegistroDescargasDocumentos')
BEGIN
    PRINT 'La migración ya estaba aplicada en esta base. No se hizo nada.';
    RETURN;
END

-- ── Migración (guion idempotente generado por EF Core) ─────────────────────
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831005008_RegistroDescargasDocumentos'
)
BEGIN
    CREATE TABLE [ProyectoDocumentoDescargas] (
        [Id] int NOT NULL IDENTITY,
        [VersionId] int NOT NULL,
        [UsuarioId] uniqueidentifier NOT NULL,
        [Usuario] nvarchar(200) NOT NULL,
        [FechaHora] datetime2 NOT NULL,
        CONSTRAINT [PK_ProyectoDocumentoDescargas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProyectoDocumentoDescargas_ProyectoDocumentoVersiones_VersionId] FOREIGN KEY ([VersionId]) REFERENCES [ProyectoDocumentoVersiones] ([Id]) ON DELETE CASCADE
    );
END;

-- Las dos preguntas que se le hacen a esta tabla: «quién descargó este archivo»
-- y «qué se llevó esta persona».
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831005008_RegistroDescargasDocumentos'
)
BEGIN
    CREATE INDEX [IX_ProyectoDocumentoDescargas_UsuarioId_FechaHora] ON [ProyectoDocumentoDescargas] ([UsuarioId], [FechaHora]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831005008_RegistroDescargasDocumentos'
)
BEGIN
    CREATE INDEX [IX_ProyectoDocumentoDescargas_VersionId_FechaHora] ON [ProyectoDocumentoDescargas] ([VersionId], [FechaHora]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831005008_RegistroDescargasDocumentos'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260831005008_RegistroDescargasDocumentos', N'9.0.0');
END;

COMMIT;

-- ── Comprobación ───────────────────────────────────────────────────────────
SELECT 'Tabla creada: ' +
       CASE WHEN OBJECT_ID('ProyectoDocumentoDescargas') IS NOT NULL THEN 'sí' ELSE 'NO' END
     + ' | columnas: ' + CAST((SELECT COUNT(*) FROM sys.columns
         WHERE object_id = OBJECT_ID('ProyectoDocumentoDescargas')) AS varchar)
     + ' | índices: ' + CAST((SELECT COUNT(*) FROM sys.indexes
         WHERE object_id = OBJECT_ID('ProyectoDocumentoDescargas') AND type > 0) AS varchar)
     + ' | FK: ' + CAST((SELECT COUNT(*) FROM sys.foreign_keys
         WHERE parent_object_id = OBJECT_ID('ProyectoDocumentoDescargas')) AS varchar)
     + ' | migración anotada: '
     + CASE WHEN EXISTS (SELECT 1 FROM [__EFMigrationsHistory]
              WHERE [MigrationId] = N'20260831005008_RegistroDescargasDocumentos')
            THEN 'sí' ELSE 'NO' END AS Resultado;

PRINT '';
PRINT '*** Registro de descargas aplicado. ***';
GO
