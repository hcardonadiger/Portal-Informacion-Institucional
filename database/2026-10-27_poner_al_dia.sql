/* =====================================================================================
   Poner al día la base con los cambios del 2026-10-26 y el 2026-10-27
   -------------------------------------------------------------------------------------
   Va DESPUÉS de 2026-10-26_poner_al_dia.sql (el script único de producción). Corra antes
   00_diagnostico.sql. Es el ÚNICO que hace falta después de ese: ya trae dentro lo de
   2026-10-26_equipo_institucional.sql y lo de 2026-10-27_tecnicos_soporte_ditra.sql, así
   que ninguno de los dos se corre aparte (si ya se corrieron, no pasa nada: aquí todo se
   salta lo que ya está).

   MEDIDO CONTRA LA BASE REAL. Sobre el respaldo de GestionGD_QA de PKI-WEB-TEST-01
   (2026-10-26 10:16, 92 migraciones, la última 20261022223203_PanelControlNotificaciones)
   restaurado y comparado objeto por objeto contra el esquema que espera ike_opt: la única
   diferencia de esquema son la tabla y las dos columnas que trae este script, y el único
   dato fuera de lugar son los temas de soporte del paso 3. Nada más falta en QA.

   QUÉ TRAE — dos migraciones EF y un cambio de datos que no es migración:

   1. 20261026192746_ExpedienteEquipoInstitucional (2026-10-26)

     ExpedienteEquipo   el equipo institucional de cada expediente: las personas de la
                        institución que atienden las consultas, con su orden de prioridad
                        (1 = contacto principal; si no está disponible, la 2, y así).
                        Reemplaza en pantalla al «Contacto institucional principal», que era
                        una sola persona.

     Y UN PASO DE DATOS, que la migración también hace: el contacto que ya tenía cada
     expediente (Expedientes.ContactoNombre/Cargo/Correo/Tel) entra a su equipo como
     prioridad 1. Solo en expedientes que todavía no tienen equipo, así que correrlo dos
     veces no duplica a nadie. Las columnas Expedientes.Contacto* se quedan: el portal las
     sigue llenando con la prioridad 1, porque el resumen, los recordatorios y los informes
     leen de ahí.

   2. 20261027182217_PlantillaCategoriaYAccion (2026-10-27)

     PlantillasProyecto.CategoriaId   la categoría de proyecto para la que sirve la plantilla
                                      (FK a CategoriasProyecto, ON DELETE NO ACTION: quitar
                                      una categoría no se lleva por delante las plantillas).
     PlantillasProyecto.Accion        la acción de DIGER para la que sirve (Acompanamiento,
                                      Digitalizacion, Soporte, Desarrollo), como texto.

     Con las dos, el alta de un proyecto ofrece solo las plantillas que encajan con la
     categoría y la acción que se le eligen. NULL = «sirve para cualquiera». Las plantillas
     existentes quedan sin clasificar y siguen apareciendo para todos.

   3. Técnicos de soporte: solo la unidad DITRA (2026-10-27) — NO es migración de EF

     Técnico de soporte es quien tiene temas asignados (Usuarios › Editor › «Temas que
     atiende»): ese filtro decide a quién se le puede asignar un chat de la cola y qué
     tickets ve en «Sus temas». Los temas estaban marcados a casi todo el personal, así que
     el «Asignar a…» del chat era el directorio entero. Por ahora atiende soporte solo
     DITRA, de modo que se le quitan los temas a quien no tenga asignación en esa unidad.

     Antes de borrar, lo que se quita se copia a UsuarioTemas_Respaldo_20261027 (UsuarioTemas
     no guarda historial). Para deshacerlo:
       INSERT INTO UsuarioTemas (UsuarioId, TemaId)
       SELECT r.UsuarioId, r.TemaId FROM UsuarioTemas_Respaldo_20261027 r
       WHERE NOT EXISTS (SELECT 1 FROM UsuarioTemas ut
                         WHERE ut.UsuarioId = r.UsuarioId AND ut.TemaId = r.TemaId);

     Por ser datos y no esquema, no se registra en __EFMigrationsHistory. Y por ser el único
     paso que borra, va al final y con red: si no existe la unidad DITRA, o si nadie de DITRA
     tiene temas —dejar el soporte sin técnicos sería peor que dejarlo de más—, el paso se
     OMITE con el motivo impreso y los pasos 1 y 2 se quedan puestos. En GestionGD_QA sí
     aplica: quita 124 filas de 24 personas y deja 7 técnicos de DITRA.

   ES ADITIVO EN ESQUEMA. Una tabla nueva, dos columnas nulables, índices y llaves foráneas.
   Mueve datos en dos lugares: el paso 1 copia (no borra) y el paso 3 borra filas de
   UsuarioTemas, con respaldo y reversa. Correrlo dos veces no cambia nada más.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-27_poner_al_dia.sql
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'[Expedientes]', N'U') IS NULL
   OR COL_LENGTH(N'Expedientes', N'ContactoNombre') IS NULL
   OR OBJECT_ID(N'[PlantillasProyecto]', N'U') IS NULL
   OR OBJECT_ID(N'[CategoriasProyecto]', N'U') IS NULL
   OR OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
   OR OBJECT_ID(N'[UsuarioTemas]', N'U') IS NULL
   OR OBJECT_ID(N'[AsignacionesUsuario]', N'U') IS NULL
   OR OBJECT_ID(N'[Unidades]', N'U') IS NULL
   OR OBJECT_ID(N'[Usuarios]', N'U') IS NULL
BEGIN
    THROW 50000,
        'Falta alguna tabla base: Expedientes (o su columna ContactoNombre), PlantillasProyecto, CategoriasProyecto, __EFMigrationsHistory, UsuarioTemas, AsignacionesUsuario, Unidades o Usuarios. Corra 00_diagnostico.sql: no es la base del portal o le falta 2026-10-26_poner_al_dia.sql.',
        1;
END
GO

/* El paso 3 se decide aquí, antes de tocar nada, y la decisión viaja en una tabla temporal
   —sobrevive a los GO de esta misma conexión—. No se usa THROW: el paso 3 es datos, y
   abortar por él dejaría los pasos 1 y 2 puestos pero sin su fila de historial. */
IF OBJECT_ID(N'tempdb..#PlanDitra') IS NOT NULL DROP TABLE #PlanDitra;
CREATE TABLE #PlanDitra (Aplicar bit NOT NULL, Motivo nvarchar(300) NULL);

INSERT INTO #PlanDitra (Aplicar, Motivo)
SELECT 0, N'no existe la unidad DITRA en esta base: no se sabe a quién conservar'
WHERE NOT EXISTS (SELECT 1 FROM [Unidades] WHERE [Id] = N'DITRA');

IF NOT EXISTS (SELECT 1 FROM #PlanDitra)
INSERT INTO #PlanDitra (Aplicar, Motivo)
SELECT 0, N'nadie de DITRA tiene temas asignados: el soporte quedaría sin técnicos'
WHERE NOT EXISTS (SELECT 1 FROM [UsuarioTemas] ut
                  JOIN [AsignacionesUsuario] a ON a.[UsuarioId] = ut.[UsuarioId]
                  WHERE a.[UnidadId] = N'DITRA');

IF NOT EXISTS (SELECT 1 FROM #PlanDitra) INSERT INTO #PlanDitra (Aplicar, Motivo) VALUES (1, NULL);
GO

/* ─────────────────────────────────────────────────────────────────────────────────────
   1. Equipo institucional del expediente (2026-10-26)
   ───────────────────────────────────────────────────────────────────────────────────── */
PRINT '════════ 1. Equipo institucional del expediente ════════';
GO

IF OBJECT_ID(N'[ExpedienteEquipo]', N'U') IS NULL
BEGIN
    CREATE TABLE [ExpedienteEquipo] (
        [Id]           int           IDENTITY(1,1) NOT NULL,
        [ExpedienteId] int           NOT NULL,
        [Prioridad]    int           NOT NULL,
        [Nombre]       nvarchar(150) NOT NULL,
        [Cargo]        nvarchar(150) NULL,
        [Correo]       nvarchar(200) NULL,
        [Telefono]     nvarchar(40)  NULL,
        CONSTRAINT [PK_ExpedienteEquipo] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpedienteEquipo_Expedientes_ExpedienteId]
            FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
    PRINT '  ExpedienteEquipo ................ creada';
END
ELSE PRINT '  ExpedienteEquipo ................ ya estaba';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[ExpedienteEquipo]') AND name = N'IX_ExpedienteEquipo_ExpedienteId')
BEGIN
    CREATE INDEX [IX_ExpedienteEquipo_ExpedienteId] ON [ExpedienteEquipo] ([ExpedienteId]);
    PRINT '  IX_ExpedienteEquipo_ExpedienteId  creado';
END
ELSE PRINT '  IX_ExpedienteEquipo_ExpedienteId  ya estaba';
GO

/* El contacto de siempre abre el equipo como prioridad 1. NOT EXISTS por expediente: un
   expediente que ya tiene equipo —porque ya se corrió esto o porque alguien lo llenó en el
   portal— no se toca. */
INSERT INTO [ExpedienteEquipo] ([ExpedienteId], [Prioridad], [Nombre], [Cargo], [Correo], [Telefono])
SELECT e.[Id], 1, LTRIM(RTRIM(e.[ContactoNombre])),
       NULLIF(LTRIM(RTRIM(e.[ContactoCargo])), N''),
       NULLIF(LTRIM(RTRIM(e.[ContactoCorreo])), N''),
       NULLIF(LTRIM(RTRIM(e.[ContactoTel])), N'')
FROM [Expedientes] e
WHERE NULLIF(LTRIM(RTRIM(e.[ContactoNombre])), N'') IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM [ExpedienteEquipo] q WHERE q.[ExpedienteId] = e.[Id]);
PRINT '  contactos a prioridad 1 ......... ' + CAST(@@ROWCOUNT AS varchar(10)) + ' expediente(s)';
GO

/* ─────────────────────────────────────────────────────────────────────────────────────
   2. Clasificación de plantillas de proyecto (2026-10-27)
   ───────────────────────────────────────────────────────────────────────────────────── */
PRINT '';
PRINT '════════ 2. Clasificación de plantillas de proyecto ════════';
GO

IF COL_LENGTH(N'PlantillasProyecto', N'Accion') IS NULL
BEGIN ALTER TABLE [PlantillasProyecto] ADD [Accion] nvarchar(20) NULL; PRINT '  Accion .......................... agregada'; END
ELSE PRINT '  Accion .......................... ya estaba';
GO
IF COL_LENGTH(N'PlantillasProyecto', N'CategoriaId') IS NULL
BEGIN ALTER TABLE [PlantillasProyecto] ADD [CategoriaId] int NULL; PRINT '  CategoriaId ..................... agregada'; END
ELSE PRINT '  CategoriaId ..................... ya estaba';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[PlantillasProyecto]') AND name = N'IX_PlantillasProyecto_CategoriaId')
BEGIN
    CREATE INDEX [IX_PlantillasProyecto_CategoriaId] ON [PlantillasProyecto] ([CategoriaId]);
    PRINT '  índice CategoriaId .............. creado';
END
ELSE PRINT '  índice CategoriaId .............. ya estaba';
GO

IF OBJECT_ID(N'[FK_PlantillasProyecto_CategoriasProyecto_CategoriaId]', N'F') IS NULL
BEGIN
    ALTER TABLE [PlantillasProyecto] ADD CONSTRAINT [FK_PlantillasProyecto_CategoriasProyecto_CategoriaId]
        FOREIGN KEY ([CategoriaId]) REFERENCES [CategoriasProyecto] ([Id]);
    PRINT '  FK a CategoriasProyecto ......... creada';
END
ELSE PRINT '  FK a CategoriasProyecto ......... ya estaba';
GO

/* ─────────────────────────────────────────────────────────────────────────────────────
   Historial de migraciones: al final y cada fila solo si su parte está completa. Escribirlo
   antes haría que `dotnet ef` se saltara una base a medias.
   ───────────────────────────────────────────────────────────────────────────────────── */
PRINT '';
PRINT '════════ Historial de migraciones ════════';
GO

IF OBJECT_ID(N'[ExpedienteEquipo]', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[ExpedienteEquipo]') AND name = N'IX_ExpedienteEquipo_ExpedienteId')
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261026192746_ExpedienteEquipoInstitucional')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES (N'20261026192746_ExpedienteEquipoInstitucional', N'9.0.0');
    PRINT '  ExpedienteEquipoInstitucional ... registrada';
END
ELSE PRINT '  ExpedienteEquipoInstitucional ... ya estaba';
GO

IF COL_LENGTH(N'PlantillasProyecto', N'Accion')      IS NOT NULL
   AND COL_LENGTH(N'PlantillasProyecto', N'CategoriaId') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[PlantillasProyecto]') AND name = N'IX_PlantillasProyecto_CategoriaId')
   AND OBJECT_ID(N'[FK_PlantillasProyecto_CategoriasProyecto_CategoriaId]', N'F') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261027182217_PlantillaCategoriaYAccion')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES (N'20261027182217_PlantillaCategoriaYAccion', N'9.0.0');
    PRINT '  PlantillaCategoriaYAccion ....... registrada';
END
ELSE PRINT '  PlantillaCategoriaYAccion ....... ya estaba';
GO

/* ─────────────────────────────────────────────────────────────────────────────────────
   3. Técnicos de soporte: solo DITRA (2026-10-27) · datos, no migración
   ───────────────────────────────────────────────────────────────────────────────────── */
PRINT '';
PRINT '════════ 3. Técnicos de soporte: solo DITRA ════════';
GO

DECLARE @motivoOmision nvarchar(300) = (SELECT TOP 1 Motivo FROM #PlanDitra WHERE Aplicar = 0);
IF @motivoOmision IS NOT NULL
    PRINT '  *** PASO 3 OMITIDO: ' + @motivoOmision + '. No se tocó nada. ***';
GO

IF EXISTS (SELECT 1 FROM #PlanDitra WHERE Aplicar = 1)
   AND OBJECT_ID(N'[UsuarioTemas_Respaldo_20261027]', N'U') IS NULL
BEGIN
    CREATE TABLE [UsuarioTemas_Respaldo_20261027] (
        [Id]        int              NOT NULL,
        [UsuarioId] uniqueidentifier NOT NULL,
        [TemaId]    int              NOT NULL,
        [Quitado]   datetime2        NOT NULL
            CONSTRAINT [DF_UTResp20261027_Quitado] DEFAULT SYSDATETIME());
    PRINT '  UsuarioTemas_Respaldo_20261027 .. creada';
END
ELSE IF OBJECT_ID(N'[UsuarioTemas_Respaldo_20261027]', N'U') IS NOT NULL
    PRINT '  UsuarioTemas_Respaldo_20261027 .. ya estaba';
GO

/* Respaldar y quitar en una sola transacción: nunca se borra una fila que no quedó copiada.
   El NOT EXISTS por Id hace que una segunda corrida no duplique el respaldo. */
IF EXISTS (SELECT 1 FROM #PlanDitra WHERE Aplicar = 1)
BEGIN
    BEGIN TRAN;
        INSERT INTO [UsuarioTemas_Respaldo_20261027] ([Id], [UsuarioId], [TemaId])
        SELECT ut.[Id], ut.[UsuarioId], ut.[TemaId]
        FROM [UsuarioTemas] ut
        WHERE NOT EXISTS (SELECT 1 FROM [AsignacionesUsuario] a
                          WHERE a.[UsuarioId] = ut.[UsuarioId] AND a.[UnidadId] = N'DITRA')
          AND NOT EXISTS (SELECT 1 FROM [UsuarioTemas_Respaldo_20261027] r WHERE r.[Id] = ut.[Id]);

        DELETE ut
        FROM [UsuarioTemas] ut
        WHERE NOT EXISTS (SELECT 1 FROM [AsignacionesUsuario] a
                          WHERE a.[UsuarioId] = ut.[UsuarioId] AND a.[UnidadId] = N'DITRA');
        PRINT '  temas quitados a quien no es de DITRA  ' + CAST(@@ROWCOUNT AS varchar(10)) + ' fila(s)';
    COMMIT;

    DECLARE @tecnicos int = (SELECT COUNT(DISTINCT [UsuarioId]) FROM [UsuarioTemas]);
    PRINT '  técnicos que quedan ............. ' + CAST(@tecnicos AS varchar(10)) + ' persona(s)';
END
GO

PRINT '';
PRINT '════════ Comprobación ════════';
SELECT N'Tabla ExpedienteEquipo' AS Control,
       CASE WHEN OBJECT_ID(N'[ExpedienteEquipo]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL SELECT N'FK ExpedienteEquipo a Expedientes',
       CASE WHEN OBJECT_ID(N'[FK_ExpedienteEquipo_Expedientes_ExpedienteId]', N'F') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Índice IX_ExpedienteEquipo_ExpedienteId',
       CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                         WHERE object_id = OBJECT_ID(N'[ExpedienteEquipo]') AND name = N'IX_ExpedienteEquipo_ExpedienteId')
            THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT N'Todo contacto tiene su prioridad 1',
       CASE WHEN EXISTS (SELECT 1 FROM [Expedientes] e
                         WHERE NULLIF(LTRIM(RTRIM(e.[ContactoNombre])), N'') IS NOT NULL
                           AND NOT EXISTS (SELECT 1 FROM [ExpedienteEquipo] q WHERE q.[ExpedienteId] = e.[Id]))
            THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Columna PlantillasProyecto.Accion',
       CASE WHEN COL_LENGTH(N'PlantillasProyecto', N'Accion') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Columna PlantillasProyecto.CategoriaId',
       CASE WHEN COL_LENGTH(N'PlantillasProyecto', N'CategoriaId') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Índice IX_PlantillasProyecto_CategoriaId',
       CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                         WHERE object_id = OBJECT_ID(N'[PlantillasProyecto]') AND name = N'IX_PlantillasProyecto_CategoriaId')
            THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT N'FK PlantillasProyecto a CategoriasProyecto',
       CASE WHEN OBJECT_ID(N'[FK_PlantillasProyecto_CategoriasProyecto_CategoriaId]', N'F') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Historial: ExpedienteEquipoInstitucional',
       CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261026192746_ExpedienteEquipoInstitucional')
            THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT N'Historial: PlantillaCategoriaYAccion',
       CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261027182217_PlantillaCategoriaYAccion')
            THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT N'Soporte restringido a DITRA',
       CASE WHEN EXISTS (SELECT 1 FROM #PlanDitra WHERE Aplicar = 0)
                 THEN 'no aplica: ' + (SELECT TOP 1 Motivo FROM #PlanDitra)
            WHEN EXISTS (SELECT 1 FROM [UsuarioTemas] ut
                         WHERE NOT EXISTS (SELECT 1 FROM [AsignacionesUsuario] a
                                           WHERE a.[UsuarioId] = ut.[UsuarioId] AND a.[UnidadId] = N'DITRA'))
                 THEN 'FALTA'
            ELSE 'presente' END
UNION ALL SELECT N'DITRA conserva técnicos con temas',
       CASE WHEN EXISTS (SELECT 1 FROM #PlanDitra WHERE Aplicar = 0)
                 THEN 'no aplica: ' + (SELECT TOP 1 Motivo FROM #PlanDitra)
            WHEN EXISTS (SELECT 1 FROM [UsuarioTemas] ut
                         JOIN [AsignacionesUsuario] a ON a.[UsuarioId] = ut.[UsuarioId]
                         WHERE a.[UnidadId] = N'DITRA')
                 THEN 'presente'
            ELSE 'FALTA' END;

PRINT '';
PRINT 'Quiénes quedan como técnicos de soporte:';
SELECT u.[Nombre] AS Tecnico, COUNT(*) AS Temas
FROM [UsuarioTemas] ut JOIN [Usuarios] u ON u.[Id] = ut.[UsuarioId]
GROUP BY u.[Nombre] ORDER BY u.[Nombre];

PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
PRINT 'Los dos controles de DITRA pueden decir «no aplica»: solo el paso 3 se salta, y arriba';
PRINT 'quedó impreso el motivo. Los pasos 1 y 2 igual entraron completos.';
GO

IF OBJECT_ID(N'tempdb..#PlanDitra') IS NOT NULL DROP TABLE #PlanDitra;
GO
