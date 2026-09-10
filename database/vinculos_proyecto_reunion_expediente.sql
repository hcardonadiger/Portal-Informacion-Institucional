/*
================================================================================
  Vínculo opcional entre proyectos y sus reuniones y expedientes.
  Migración 20260831022204_VincularReunionesYExpedientesAProyectos
================================================================================

  CÓMO SE USA
  -----------
  Ejecútelo una vez contra la base del ambiente. No modifica ni borra datos: solo
  crea dos tablas y las anota en el historial de EF. Es idempotente.

      sqlcmd -S <servidor> -d <base> -U <usuario> -P <clave> -C -I ^
             -i database\vinculos_proyecto_reunion_expediente.sql

  El -I (QUOTED_IDENTIFIER ON) es obligatorio: las tablas del portal llevan
  índices filtrados y sin él SQL Server rechaza la escritura con el error 1934.

  QUÉ HACE
  --------
  Crea ProyectoReuniones y ProyectoExpedientes: la relación es OPCIONAL POR LOS
  DOS LADOS y de muchos a muchos. Una reunión puede no pertenecer a ningún
  proyecto —la mayoría no pertenece— y puede tocar a varios; un proyecto vive sin
  reuniones ni expedientes vinculados.

  POR QUÉ TABLA DE VÍNCULO Y NO UNA COLUMNA
  -----------------------------------------
  Una columna en Reuniones habría impuesto «una reunión, un proyecto» desde el
  primer día. El precedente está a la vista: Reuniones.ExpedienteId existe desde
  hace año y medio, sin interfaz que lo llene y con cero filas.

  EL DESAJUSTE DE ALCANCE, QUE HAY QUE CONOCER
  --------------------------------------------
  El vínculo se ancla en el PROYECTO: quien puede abrirlo ve con qué está
  relacionado. Pero la institución del proyecto es la que EJECUTA —DIGER en el
  portafolio interno— y la de la reunión o el expediente es la beneficiaria, así
  que el vínculo cruza instituciones por construcción.

  Consecuencia: ver el vínculo no implica poder abrir su destino. La ficha
  muestra lo que la persona alcanza y DICE CUÁNTOS quedan fuera, en vez de
  esconderlos sin avisar o de saltarse el filtro. Un conteo no revela ni título,
  ni institución, ni fecha.

  BORRADO EN CASCADA
  ------------------
  Borrar el proyecto, la reunión o el expediente se lleva el vínculo. Quitar el
  vínculo desde la ficha no toca ninguno de los dos extremos.

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

IF OBJECT_ID('Proyectos') IS NULL OR OBJECT_ID('Reuniones') IS NULL OR OBJECT_ID('Expedientes') IS NULL
BEGIN
    RAISERROR('Faltan Proyectos, Reuniones o Expedientes. No se hizo nada.', 16, 1);
    RETURN;
END

IF EXISTS (SELECT 1 FROM [__EFMigrationsHistory]
           WHERE [MigrationId] = N'20260831022204_VincularReunionesYExpedientesAProyectos')
BEGIN
    PRINT 'La migración ya estaba aplicada en esta base. No se hizo nada.';
    RETURN;
END

-- ── Migración (guion idempotente generado por EF Core) ─────────────────────
BEGIN TRANSACTION;
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
SELECT 'ProyectoReuniones: ' +
       CASE WHEN OBJECT_ID('ProyectoReuniones') IS NOT NULL THEN 'creada' ELSE 'NO' END
     + ' | ProyectoExpedientes: ' +
       CASE WHEN OBJECT_ID('ProyectoExpedientes') IS NOT NULL THEN 'creada' ELSE 'NO' END
     + ' | índices únicos: ' + CAST((SELECT COUNT(*) FROM sys.indexes
         WHERE object_id IN (OBJECT_ID('ProyectoReuniones'), OBJECT_ID('ProyectoExpedientes'))
           AND is_unique = 1 AND type > 0) AS varchar)
     + ' | FK: ' + CAST((SELECT COUNT(*) FROM sys.foreign_keys
         WHERE parent_object_id IN (OBJECT_ID('ProyectoReuniones'), OBJECT_ID('ProyectoExpedientes'))) AS varchar)
       AS Resultado;

PRINT '';
PRINT '*** Vínculos de proyecto aplicados. ***';
GO
