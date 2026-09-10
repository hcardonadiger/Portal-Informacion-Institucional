/*
================================================================================
  Cambios de esquema del 31 de agosto de 2026.
  Portal DIGER — Trámites Estado
================================================================================

  CÓMO SE USA
  -----------
  Ejecútelo una vez contra la base del ambiente de pruebas. No modifica ni borra
  datos existentes: solo crea tablas nuevas y las anota en el historial de EF.
  Es idempotente y se puede volver a correr sin efecto.

      sqlcmd -S <servidor> -d <base> -U <usuario> -P <clave> -C -I ^
             -i database\cambios_2026-08-31.sql

  El -I (QUOTED_IDENTIFIER ON) es OBLIGATORIO: las tablas del portal llevan
  índices filtrados y sin él SQL Server rechaza la escritura con el error 1934.

  QUÉ TRAE
  --------
  Dos migraciones, independientes entre sí. Cada una lleva su propia guarda, así
  que si una ya estuviera aplicada se aplica solo la otra.

  1) 20260831005008_RegistroDescargasDocumentos
     ProyectoDocumentoDescargas — quién descargó qué versión de qué documento y
     cuándo. Es una bitácora, no un contador: la pregunta que responde es «quién
     se llevó el convenio antes de que se firmara», y esa no la contesta un
     número. Apunta a la VERSIÓN, no al documento: saber que alguien descargó
     «Convenio marco» no dice nada si no consta que se llevó la v1 y no la v2 ya
     corregida. No se guarda la dirección IP — el portal no la registra en
     ninguna otra parte y añadirla es otra decisión.

  2) 20260831022204_VincularReunionesYExpedientesAProyectos
     ProyectoReuniones y ProyectoExpedientes — la relación opcional, y de muchos
     a muchos, entre un proyecto y las reuniones o expedientes que lo tocan. Una
     reunión puede no pertenecer a ninguno y puede tocar a varios.

  ALCANCE, QUE CONVIENE CONOCER
  -----------------------------
  Las tres tablas heredan el filtro institucional por sus anclas: las descargas
  por versión → documento → proyecto; los vínculos por el proyecto. Nadie ve
  descargas ni vínculos de proyectos que no puede abrir.

  Con una consecuencia en los vínculos: la institución del proyecto es la que
  EJECUTA —DIGER en el portafolio interno— y la de la reunión o el expediente es
  la beneficiaria, así que el vínculo cruza instituciones por construcción. Ver
  el vínculo no implica poder abrir su destino; la ficha muestra lo que la
  persona alcanza y dice cuántos quedan fuera.

  QUÉ NO INCLUYE
  --------------
  Datos. La puesta al día de PRY-2026-17 para la socialización va aparte, en
  database\poner_al_dia_portal_digitalizacion.sql, porque toca contenido y no
  esquema y no todos los ambientes la quieren.

================================================================================
*/

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

-- ── Comprobaciones previas ─────────────────────────────────────────────────
IF OBJECT_ID('__EFMigrationsHistory') IS NULL
BEGIN
    RAISERROR('Esta base no tiene __EFMigrationsHistory: no es una base del portal creada por EF. No se hizo nada.', 16, 1);
    RETURN;
END

IF OBJECT_ID('ProyectoDocumentoVersiones') IS NULL
BEGIN
    RAISERROR('Falta ProyectoDocumentoVersiones. Aplique antes la migración del repositorio documental. No se hizo nada.', 16, 1);
    RETURN;
END

IF OBJECT_ID('Proyectos') IS NULL OR OBJECT_ID('Reuniones') IS NULL OR OBJECT_ID('Expedientes') IS NULL
BEGIN
    RAISERROR('Faltan Proyectos, Reuniones o Expedientes. No se hizo nada.', 16, 1);
    RETURN;
END

-- ── Migraciones (guion idempotente generado por EF Core) ───────────────────
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

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831022204_VincularReunionesYExpedientesAProyectos'
)
BEGIN
    CREATE TABLE [ProyectoExpedientes] (
        [Id] int NOT NULL IDENTITY,
        [ProyectoId] int NOT NULL,
        [ExpedienteId] int NOT NULL,
        [Nota] nvarchar(400) NULL,
        [VinculadoPor] nvarchar(200) NOT NULL,
        [VinculadoEn] datetime2 NOT NULL,
        CONSTRAINT [PK_ProyectoExpedientes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProyectoExpedientes_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProyectoExpedientes_Proyectos_ProyectoId] FOREIGN KEY ([ProyectoId]) REFERENCES [Proyectos] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831022204_VincularReunionesYExpedientesAProyectos'
)
BEGIN
    CREATE TABLE [ProyectoReuniones] (
        [Id] int NOT NULL IDENTITY,
        [ProyectoId] int NOT NULL,
        [ReunionId] int NOT NULL,
        [Nota] nvarchar(400) NULL,
        [VinculadoPor] nvarchar(200) NOT NULL,
        [VinculadoEn] datetime2 NOT NULL,
        CONSTRAINT [PK_ProyectoReuniones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProyectoReuniones_Proyectos_ProyectoId] FOREIGN KEY ([ProyectoId]) REFERENCES [Proyectos] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProyectoReuniones_Reuniones_ReunionId] FOREIGN KEY ([ReunionId]) REFERENCES [Reuniones] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831022204_VincularReunionesYExpedientesAProyectos'
)
BEGIN
    CREATE INDEX [IX_ProyectoExpedientes_ExpedienteId] ON [ProyectoExpedientes] ([ExpedienteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831022204_VincularReunionesYExpedientesAProyectos'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProyectoExpedientes_ProyectoId_ExpedienteId] ON [ProyectoExpedientes] ([ProyectoId], [ExpedienteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831022204_VincularReunionesYExpedientesAProyectos'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProyectoReuniones_ProyectoId_ReunionId] ON [ProyectoReuniones] ([ProyectoId], [ReunionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831022204_VincularReunionesYExpedientesAProyectos'
)
BEGIN
    CREATE INDEX [IX_ProyectoReuniones_ReunionId] ON [ProyectoReuniones] ([ReunionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831022204_VincularReunionesYExpedientesAProyectos'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260831022204_VincularReunionesYExpedientesAProyectos', N'9.0.0');
END;

COMMIT;


-- ── Comprobación ───────────────────────────────────────────────────────────
SELECT t.Tabla,
       CASE WHEN OBJECT_ID(t.Tabla) IS NOT NULL THEN 'creada' ELSE 'FALTA' END AS Estado,
       (SELECT COUNT(*) FROM sys.indexes i WHERE i.object_id = OBJECT_ID(t.Tabla) AND i.type > 0) AS Indices,
       (SELECT COUNT(*) FROM sys.foreign_keys f WHERE f.parent_object_id = OBJECT_ID(t.Tabla)) AS FKs
FROM (VALUES ('ProyectoDocumentoDescargas'), ('ProyectoReuniones'), ('ProyectoExpedientes')) AS t(Tabla);

SELECT [MigrationId] AS MigracionesDeHoyAnotadas
FROM [__EFMigrationsHistory]
WHERE [MigrationId] IN (N'20260831005008_RegistroDescargasDocumentos',
                        N'20260831022204_VincularReunionesYExpedientesAProyectos')
ORDER BY [MigrationId];

PRINT '';
PRINT '*** Cambios del 31-08-2026 aplicados. ***';
GO
