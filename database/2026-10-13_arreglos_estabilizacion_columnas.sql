/* ═══════════════════════════════════════════════════════════════════════════════════════
   Las dos columnas que le faltan a la base para correr feature/arreglos-estabilizacion
   ───────────────────────────────────────────────────────────────────────────────────────
   La rama trae dos migraciones propias que una base salida de TEST no tiene:

     20260902010828_DuracionReunionEnMinutos   Reuniones.DuracionMinutos (int), en lugar del
                                               texto libre Reuniones.Duracion
     20260902013548_TokenCalendarioUsuario     Usuarios.CalendarioToken + su índice único

   Sin ellas el portal no arranca: falla al leer Usuarios —o sea, al iniciar sesión— con
   «Invalid column name 'CalendarioToken'», y al abrir reuniones con «Invalid column name
   'DuracionMinutos'». No falta ninguna tabla.

   Hace lo mismo que las dos migraciones y las anota en __EFMigrationsHistory, para que
   «dotnet ef database update» no intente repetirlas. Con UNA diferencia, a propósito:

   La migración convierte el texto de la duración a minutos y borra el texto. Su regla lee
   solo el primer número, así que las duraciones compuestas salen mal: «1 hora y 30 minutos»
   queda en 1 minuto, «2 horas 1 minuto» en 2 y «1:30» en 60. Como el texto se borra, después
   ya no hay de dónde corregir. Aquí esos casos se interpretan bien —90, 121 y 90— ANTES de
   borrar la columna. Todo lo demás sigue la regla de la migración al pie de la letra.

   Cada paso comprueba su propia condición contra la base: se puede correr las veces que
   haga falta y la segunda corrida no hace nada.

       sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-13_arreglos_estabilizacion_columnas.sql

   Saque copia de seguridad antes: borra la columna Reuniones.Duracion.
   ═══════════════════════════════════════════════════════════════════════════════════════ */

SET QUOTED_IDENTIFIER ON;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID('Reuniones') IS NULL OR OBJECT_ID('Usuarios') IS NULL OR OBJECT_ID('__EFMigrationsHistory') IS NULL
BEGIN
    RAISERROR('Esta base no tiene Reuniones, Usuarios o el historial de migraciones. No se hizo nada.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

-- ══ 1 · Reuniones.DuracionMinutos ══════════════════════════════════════════
IF COL_LENGTH('Reuniones', 'DuracionMinutos') IS NULL
BEGIN
    ALTER TABLE Reuniones ADD DuracionMinutos int NULL;
    PRINT N'   columna Reuniones.DuracionMinutos creada';
END
ELSE PRINT N'   columna Reuniones.DuracionMinutos ya estaba';

/* La conversión va dentro de EXEC: menciona dos columnas que en una segunda corrida ya no
   están las dos —Duracion se borra al final—, y SQL Server compila el lote entero antes de
   ejecutar nada. Si no se ejecuta, no se compila. */
IF COL_LENGTH('Reuniones', 'Duracion') IS NOT NULL
BEGIN
    EXEC(N'
    WITH origen AS (
        SELECT Id, REPLACE(LOWER(LTRIM(RTRIM(Duracion))), '','', ''.'') AS t
        FROM   Reuniones
        WHERE  Duracion IS NOT NULL AND LTRIM(RTRIM(Duracion)) <> ''''
    ),
    interpretado AS (
        SELECT o.Id, o.t,
               -- El primer número del texto: la misma expresión de la migración.
               TRY_CONVERT(decimal(9,2), NULLIF(LEFT(o.t, PATINDEX(''%[^0-9.]%'', o.t + ''|'') - 1), '''')) AS num,
               -- «1:30»: horas y minutos separados por dos puntos.
               CASE WHEN o.t LIKE ''[0-9]:[0-5][0-9]'' OR o.t LIKE ''[0-9][0-9]:[0-5][0-9]''
                    THEN TRY_CONVERT(int, LEFT(o.t, CHARINDEX('':'', o.t) - 1)) * 60
                       + TRY_CONVERT(int, SUBSTRING(o.t, CHARINDEX('':'', o.t) + 1, 2)) END AS hhmm,
               -- «1 hora y 30 minutos»: el número que viene después de la palabra de las horas.
               CASE WHEN o.t LIKE ''[0-9]%h%[0-9]%min%'' THEN
                    TRY_CONVERT(int, LEFT(resto.r, PATINDEX(''%[^0-9]%'', resto.r + ''|'') - 1)) END AS minDespues
        FROM   origen o
        OUTER APPLY (SELECT SUBSTRING(o.t, CHARINDEX(''h'', o.t), 200) AS desdeH) h
        OUTER APPLY (SELECT CASE WHEN PATINDEX(''%[0-9]%'', h.desdeH) > 0
                                 THEN SUBSTRING(h.desdeH, PATINDEX(''%[0-9]%'', h.desdeH), 200) END AS r) resto
    )
    UPDATE r
    SET    r.DuracionMinutos =
               CASE
                   -- Lo que la migración no contempla: duraciones compuestas.
                   WHEN i.hhmm IS NOT NULL                        THEN i.hhmm
                   WHEN i.minDespues IS NOT NULL AND i.num IS NOT NULL
                                                                  THEN CAST(ROUND(i.num * 60, 0) AS int) + i.minDespues
                   -- De aquí en adelante, la regla de la migración tal cual.
                   WHEN i.num IS NULL           THEN NULL
                   WHEN i.t LIKE ''%min%''      THEN CAST(ROUND(i.num, 0) AS int)
                   WHEN i.t LIKE ''%h%''        THEN CAST(ROUND(i.num * 60, 0) AS int)
                   WHEN i.num <= 12             THEN CAST(ROUND(i.num * 60, 0) AS int)
                   ELSE                              CAST(ROUND(i.num, 0) AS int)
               END
    FROM   Reuniones r
    JOIN   interpretado i ON i.Id = r.Id;

    -- Una duración de cero o negativa no es un dato, es ruido.
    UPDATE Reuniones SET DuracionMinutos = NULL WHERE DuracionMinutos <= 0;

    ALTER TABLE Reuniones DROP COLUMN Duracion;');
    PRINT N'   duraciones convertidas a minutos y columna Reuniones.Duracion eliminada';
END
ELSE PRINT N'   columna Reuniones.Duracion ya no estaba: nada que convertir';

-- ══ 2 · Usuarios.CalendarioToken ═══════════════════════════════════════════
IF COL_LENGTH('Usuarios', 'CalendarioToken') IS NULL
BEGIN
    ALTER TABLE Usuarios ADD CalendarioToken uniqueidentifier NULL;
    PRINT N'   columna Usuarios.CalendarioToken creada';
END
ELSE PRINT N'   columna Usuarios.CalendarioToken ya estaba';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Usuarios') AND name = N'IX_Usuarios_CalendarioToken')
BEGIN
    EXEC(N'CREATE UNIQUE INDEX IX_Usuarios_CalendarioToken ON Usuarios (CalendarioToken) WHERE [CalendarioToken] IS NOT NULL;');
    PRINT N'   índice IX_Usuarios_CalendarioToken creado';
END
ELSE PRINT N'   índice IX_Usuarios_CalendarioToken ya estaba';

-- ══ 3 · Historial de migraciones ═══════════════════════════════════════════
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
SELECT v.Id, N'9.0.0'
FROM  (VALUES (N'20260902010828_DuracionReunionEnMinutos'),
              (N'20260902013548_TokenCalendarioUsuario')) v(Id)
WHERE NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory h WHERE h.MigrationId = v.Id);

COMMIT;

-- ══ Comprobación ═══════════════════════════════════════════════════════════
SELECT  Paso = v.Paso, Estado = CASE WHEN v.Ok = 1 THEN N'presente' ELSE N'FALTA' END
FROM   (VALUES
    (N'Reuniones.DuracionMinutos',       CASE WHEN COL_LENGTH('Reuniones','DuracionMinutos') IS NOT NULL THEN 1 ELSE 0 END),
    (N'Reuniones.Duracion eliminada',    CASE WHEN COL_LENGTH('Reuniones','Duracion') IS NULL THEN 1 ELSE 0 END),
    (N'Usuarios.CalendarioToken',        CASE WHEN COL_LENGTH('Usuarios','CalendarioToken') IS NOT NULL THEN 1 ELSE 0 END),
    (N'IX_Usuarios_CalendarioToken',     CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Usuarios') AND name = N'IX_Usuarios_CalendarioToken') THEN 1 ELSE 0 END),
    (N'Migraciones registradas (2)',     CASE WHEN (SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId IN (N'20260902010828_DuracionReunionEnMinutos', N'20260902013548_TokenCalendarioUsuario')) = 2 THEN 1 ELSE 0 END)
) v(Paso, Ok);

PRINT N'';
PRINT N'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
