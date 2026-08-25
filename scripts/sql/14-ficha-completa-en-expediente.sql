-- =============================================================================
-- 14 — El expediente aprende a guardar todo lo que guarda una ficha SIGER
--      (plan de promoción SIGER, Fase 8)
--
-- POR QUÉ HACE FALTA
--   D-17 invierte quién manda: en cuanto una ficha SIGER queda enlazada a un
--   expediente, sus campos de contenido pasan a ser de SOLO LECTURA en la ficha
--   y solo se editan desde el expediente. Un campo que el expediente no sepa
--   guardar es un campo que, a partir de ese momento, nadie puede editar en
--   ninguna parte — y nada avisaría.
--
-- QUÉ HACE, EN ESTE ORDEN
--   1. Agrega ocho columnas a ExpedienteTramites (categoría, detalle de la
--      modalidad, gratuidad, vigencia, temporalidad, observaciones DIGER, si
--      está en SOL y el tramo del enlace) y crea las tablas
--      ExpedienteTramiteEntregables y ExpedienteTramiteLugares.
--   2. **Convierte** las modalidades de texto libre que ya existen y recién
--      entonces pone el CHECK del catálogo cerrado.
--   3. **Siembra** las dos tablas nuevas con lo que el expediente ya sabía.
--
-- EL ORDEN DE 2 NO ES NEGOCIABLE
--   El CHECK no se crea junto con las columnas a propósito. En Ensayo hay 202
--   trámites con la modalidad escrita a mano —«En línea», «En línea (total)»,
--   «Trámite en línea»…— y ninguno cumple el catálogo. Puesto antes, el script
--   fallaría contra cualquier base con datos.
--
-- ESTE SÍ TOCA DATOS. Es el primero de la serie que lo hace:
--   • UPDATE sobre ExpedienteTramites.Modalidad — el texto original NO se pierde,
--     se copia antes a ModalidadDetalle, y el Down de la migración lo devuelve.
--   • INSERT en las dos tablas nuevas, que están vacías.
--   Ninguna fila se borra.
--
-- Medido en Ensayo el 25-08-2026 sobre 240 trámites de expediente:
--   modalidades → 183 Virtual · 16 Hibrido · 3 Presencial · 38 sin modalidad
--                 (0 fuera del catálogo, 202 con su texto original conservado)
--   siembra     → 202 entregables (los 202 que tenían DocEntregado)
--                 236 lugares (197 con teléfono, 46 con dirección de sede)
--
-- CÓMO SE CORRE
--   sqlcmd -S <servidor> -d <base> -U <usuario> -P <clave> -f 65001 -i 14-ficha-completa-en-expediente.sql
--
--   El -f 65001 no es opcional: el archivo va en UTF-8 y sin él los acentos de
--   los comentarios llegan rotos.
--
-- ORDEN RESPECTO A LOS SCRIPTS 12 Y 13
--   Este supone el 13 aplicado (la fase anterior). Cada bloque se guarda por su
--   propia fila de __EFMigrationsHistory, así que correrlo dos veces no hace
--   nada la segunda.
--
-- POR QUÉ EL SET DE ABAJO
--   Hay índices FILTRADOS en juego y SQL Server exige QUOTED_IDENTIFIER ON para
--   crearlos y para insertar en las tablas que los tienen; sqlcmd arranca con la
--   opción en OFF y fallaría con «Msg 1934». Que EF lo envuelva en EXEC(N'...')
--   no salva: el SQL dinámico hereda la opción del lote que lo llama.
-- =============================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [CategoriaId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [EsGratuito] bit NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [EstaEnSol] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [ModalidadDetalle] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [ObservacionesDiger] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [SolTramo] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [Temporalidad] nvarchar(60) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [VigenciaDocumento] nvarchar(120) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    CREATE TABLE [ExpedienteTramiteEntregables] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [TramiteIndex] int NOT NULL,
        [Orden] int NOT NULL,
        [Entregable] nvarchar(500) NOT NULL,
        [Formato] nvarchar(120) NULL,
        [Presentacion] nvarchar(120) NULL,
        CONSTRAINT [PK_ExpedienteTramiteEntregables] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpedienteTramiteEntregables_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    CREATE TABLE [ExpedienteTramiteLugares] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [TramiteIndex] int NOT NULL,
        [Orden] int NOT NULL,
        [Lugar] nvarchar(400) NOT NULL,
        [Ciudad] nvarchar(120) NULL,
        [Direccion] nvarchar(500) NULL,
        [Telefonos] nvarchar(200) NULL,
        CONSTRAINT [PK_ExpedienteTramiteLugares] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpedienteTramiteLugares_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ExpedienteTramites_CategoriaId] ON [ExpedienteTramites] ([CategoriaId]) WHERE [CategoriaId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    CREATE INDEX [IX_ExpedienteTramiteEntregables_ExpedienteId_TramiteIndex] ON [ExpedienteTramiteEntregables] ([ExpedienteId], [TramiteIndex]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    CREATE INDEX [IX_ExpedienteTramiteLugares_ExpedienteId_TramiteIndex] ON [ExpedienteTramiteLugares] ([ExpedienteId], [TramiteIndex]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD CONSTRAINT [FK_ExpedienteTramites_CategoriasTramite_CategoriaId] FOREIGN KEY ([CategoriaId]) REFERENCES [CategoriasTramite] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825165843_FichaCompletaEnTramiteExpediente', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170007_ConvertirModalidadesExistentes'
)
BEGIN

                    EXEC(N'
                        UPDATE ExpedienteTramites
                        SET    ModalidadDetalle = Modalidad
                        WHERE  Modalidad IS NOT NULL
                          AND  LTRIM(RTRIM(Modalidad)) <> ''''
                          AND  ModalidadDetalle IS NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170007_ConvertirModalidadesExistentes'
)
BEGIN

                    UPDATE ExpedienteTramites
                    SET    Modalidad = CASE
                             WHEN Modalidad COLLATE Latin1_General_CI_AI IN ('Virtual', 'Presencial', 'Hibrido')
                                  THEN Modalidad
                             WHEN (Modalidad COLLATE Latin1_General_CI_AI LIKE '%linea%'
                                OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%virtual%'
                                OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%online%')
                              AND  Modalidad COLLATE Latin1_General_CI_AI LIKE '%presencial%'
                                  THEN 'Hibrido'
                             WHEN  Modalidad COLLATE Latin1_General_CI_AI LIKE '%linea%'
                                OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%virtual%'
                                OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%online%'
                                  THEN 'Virtual'
                             WHEN  Modalidad COLLATE Latin1_General_CI_AI LIKE '%presencial%'
                                  THEN 'Presencial'
                             ELSE NULL
                           END
                    WHERE  Modalidad IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170007_ConvertirModalidadesExistentes'
)
BEGIN
    EXEC(N'ALTER TABLE [ExpedienteTramites] ADD CONSTRAINT [CK_ExpedienteTramites_Modalidad] CHECK ([Modalidad] IS NULL OR [Modalidad] IN (''Virtual'', ''Presencial'', ''Hibrido''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170007_ConvertirModalidadesExistentes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825170007_ConvertirModalidadesExistentes', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170158_SembrarEntregablesYLugares'
)
BEGIN

                    INSERT INTO ExpedienteTramiteEntregables (ExpedienteId, TramiteIndex, Orden, Entregable)
                    SELECT ExpedienteId, TramiteIndex, 0, LTRIM(RTRIM(DocEntregado))
                    FROM   ExpedienteTramites
                    WHERE  DocEntregado IS NOT NULL AND LTRIM(RTRIM(DocEntregado)) <> '';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170158_SembrarEntregablesYLugares'
)
BEGIN

                    INSERT INTO ExpedienteTramiteLugares
                           (ExpedienteId, TramiteIndex, Orden, Lugar, Direccion, Telefonos)
                    SELECT t.ExpedienteId, t.TramiteIndex, 0,
                           e.Institucion, NULLIF(LTRIM(RTRIM(e.DirSede)), ''), NULLIF(LTRIM(RTRIM(t.Telefono)), '')
                    FROM   ExpedienteTramites t
                    JOIN   Expedientes e ON e.Id = t.ExpedienteId
                    WHERE  LTRIM(RTRIM(ISNULL(e.Institucion, ''))) <> ''
                      AND  ((t.Telefono IS NOT NULL AND LTRIM(RTRIM(t.Telefono)) <> '')
                        OR  (e.DirSede  IS NOT NULL AND LTRIM(RTRIM(e.DirSede))  <> ''));
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170158_SembrarEntregablesYLugares'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825170158_SembrarEntregablesYLugares', N'9.0.0');
END;

COMMIT;
GO

