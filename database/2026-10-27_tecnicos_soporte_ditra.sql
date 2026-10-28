/* ═══════════════════════════════════════════════════════════════════════════════════════
   2026-10-27 · Técnicos de soporte: solo la unidad Digitalización de Trámites (DITRA)
   ───────────────────────────────────────────────────────────────────────────────────────
   QUÉ HACE Y POR QUÉ
   Técnico de soporte es quien tiene temas asignados (Usuarios › Editor › «Temas que
   atiende»): ese filtro decide a quién se le puede asignar un chat de la cola y qué tickets
   ve en «Sus temas». Los temas estaban marcados casi a todo el personal, así que el
   «Asignar a…» del chat era el directorio entero. Por ahora atiende soporte solo DITRA.

   - Quita los temas a todo usuario que no tenga una asignación en la unidad DITRA.
   - A quienes sí la tienen no les toca nada.
   - NO es una migración de EF: es un cambio de datos. No se registra en __EFMigrationsHistory.

   RESPALDO Y REVERSA
   UsuarioTemas no guarda historial, así que antes de borrar se copia lo que se quita a
   UsuarioTemas_Respaldo_20261027. Para deshacer:
     INSERT INTO UsuarioTemas (UsuarioId, TemaId)
     SELECT r.UsuarioId, r.TemaId FROM UsuarioTemas_Respaldo_20261027 r
     WHERE NOT EXISTS (SELECT 1 FROM UsuarioTemas ut WHERE ut.UsuarioId = r.UsuarioId AND ut.TemaId = r.TemaId);

   Va después de 2026-10-27_poner_al_dia.sql. Correrlo dos veces no cambia nada más.

   CÓMO CORRERLO
   sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-27_tecnicos_soporte_ditra.sql
   ═══════════════════════════════════════════════════════════════════════════════════════ */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID('dbo.UsuarioTemas') IS NULL OR OBJECT_ID('dbo.AsignacionesUsuario') IS NULL
   OR OBJECT_ID('dbo.Unidades') IS NULL
    THROW 50000, 'Faltan tablas base (UsuarioTemas, AsignacionesUsuario, Unidades). Corra primero 00_diagnostico.sql.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Unidades WHERE Id = 'DITRA')
    THROW 50001, 'No existe la unidad DITRA en esta base: no se sabe a quién conservar. No se tocó nada.', 1;

-- Si nadie de DITRA tiene temas, aplicar esto dejaría el soporte sin técnicos.
IF NOT EXISTS (
    SELECT 1 FROM dbo.UsuarioTemas ut
    JOIN dbo.AsignacionesUsuario a ON a.UsuarioId = ut.UsuarioId
    WHERE a.UnidadId = 'DITRA')
    THROW 50002, 'Nadie de DITRA tiene temas asignados: el soporte quedaría sin técnicos. Asígneles temas primero. No se tocó nada.', 1;

-- Lo que se va a quitar: temas de quien no tiene ninguna asignación en DITRA.
IF OBJECT_ID('tempdb..#Quitar') IS NOT NULL DROP TABLE #Quitar;
SELECT ut.Id, ut.UsuarioId, ut.TemaId
INTO #Quitar
FROM dbo.UsuarioTemas ut
WHERE NOT EXISTS (SELECT 1 FROM dbo.AsignacionesUsuario a
                  WHERE a.UsuarioId = ut.UsuarioId AND a.UnidadId = 'DITRA');

IF OBJECT_ID('dbo.UsuarioTemas_Respaldo_20261027') IS NULL
BEGIN
    CREATE TABLE dbo.UsuarioTemas_Respaldo_20261027 (
        Id        INT              NOT NULL,
        UsuarioId UNIQUEIDENTIFIER NOT NULL,
        TemaId    INT              NOT NULL,
        Quitado   DATETIME2        NOT NULL CONSTRAINT DF_UTResp20261027_Quitado DEFAULT SYSDATETIME());
    PRINT 'Tabla de respaldo UsuarioTemas_Respaldo_20261027: creada';
END
ELSE PRINT 'Tabla de respaldo UsuarioTemas_Respaldo_20261027: ya estaba';

BEGIN TRAN;
    INSERT INTO dbo.UsuarioTemas_Respaldo_20261027 (Id, UsuarioId, TemaId)
    SELECT q.Id, q.UsuarioId, q.TemaId FROM #Quitar q
    WHERE NOT EXISTS (SELECT 1 FROM dbo.UsuarioTemas_Respaldo_20261027 r WHERE r.Id = q.Id);

    DELETE ut FROM dbo.UsuarioTemas ut JOIN #Quitar q ON q.Id = ut.Id;
    PRINT CONCAT('Temas quitados a quienes no son de DITRA: ', @@ROWCOUNT);
COMMIT;

-- Verificación: cada control debe decir «presente», nunca «FALTA».
SELECT 'Nadie fuera de DITRA tiene temas' AS Control,
       CASE WHEN NOT EXISTS (
           SELECT 1 FROM dbo.UsuarioTemas ut
           WHERE NOT EXISTS (SELECT 1 FROM dbo.AsignacionesUsuario a
                             WHERE a.UsuarioId = ut.UsuarioId AND a.UnidadId = 'DITRA'))
            THEN 'presente' ELSE 'FALTA' END AS Estado
UNION ALL
SELECT 'DITRA conserva técnicos con temas',
       CASE WHEN EXISTS (
           SELECT 1 FROM dbo.UsuarioTemas ut
           JOIN dbo.AsignacionesUsuario a ON a.UsuarioId = ut.UsuarioId
           WHERE a.UnidadId = 'DITRA')
            THEN 'presente' ELSE 'FALTA' END;

-- Quiénes quedan como técnicos.
SELECT u.Nombre AS Tecnico, COUNT(*) AS Temas
FROM dbo.UsuarioTemas ut
JOIN dbo.Usuarios u ON u.Id = ut.UsuarioId
GROUP BY u.Nombre
ORDER BY u.Nombre;
