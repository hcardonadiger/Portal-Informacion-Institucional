/* ╔══════════════════════════════════════════════════════════════════╗
   ║  2026-10-19 — Cambio de contraseña obligatorio por única vez     ║
   ║                                                                  ║
   ║  QUÉ TRAE: la columna Usuarios.PasswordCambiadoEl (datetime2     ║
   ║  NULL), que es lo único que el portal necesita para exigir el    ║
   ║  cambio de contraseña una sola vez a partir del 01/11/2026.      ║
   ║                                                                  ║
   ║  Nula significa «la contraseña vigente no la eligió el dueño     ║
   ║  de la cuenta». Toda fila existente nace nula, y eso es          ║
   ║  justamente la tanda única: en su primer ingreso a partir del    ║
   ║  corte, cada usuario pasa una vez por la pantalla de cambio y    ║
   ║  queda estampado. La misma columna sirve después para las        ║
   ║  cuentas nuevas con contraseña temporal, sin agregar nada.       ║
   ║                                                                  ║
   ║  NO HACE FALTA BORRAR NI TOCAR DATOS: la columna se agrega       ║
   ║  vacía a propósito. No se rellena con fechas inventadas —        ║
   ║  cualquier valor puesto acá eximiría del cambio a ese usuario.   ║
   ║                                                                  ║
   ║  SÍ es una migración de EF:                                      ║
   ║  20261019174802_AddPasswordCambiadoEl. La fila del historial     ║
   ║  se escribe al final del script.                                 ║
   ║                                                                  ║
   ║  OJO, NO ES PARTE DE ESTE SCRIPT: la fecha de corte vive en      ║
   ║  appsettings.json, en Auth:CambioPasswordObligatorioDesde. Sin   ║
   ║  esa clave el portal no retiene a nadie por más que la columna   ║
   ║  exista — está hecho para fallar abierto.                        ║
   ║                                                                  ║
   ║  VA DESPUÉS DE: database/2026-10-19_poner_al_dia.sql             ║
   ║                                                                  ║
   ║  CÓMO CORRERLO:                                                  ║
   ║  sqlcmd -S <servidor> -d <base> -b -I -f 65001                   ║
   ║         -i 2026-10-19_cambio_password_obligatorio.sql            ║
   ╚══════════════════════════════════════════════════════════════════╝ */

SET NOCOUNT ON;

IF OBJECT_ID('dbo.Usuarios', 'U') IS NULL
BEGIN
    THROW 50000, 'No existe la tabla Usuarios. Corra primero database/00_diagnostico.sql.', 1;
END

PRINT '── 1. Usuarios.PasswordCambiadoEl ──────────────────────────────────';

/* La condición se comprueba contra el catálogo de la base, no contra
   __EFMigrationsHistory: la fila del historial se escribe al final, así que si
   esta corrida muriera a la mitad no serviría para saber qué quedó hecho. */
IF COL_LENGTH('dbo.Usuarios', 'PasswordCambiadoEl') IS NULL
BEGIN
    ALTER TABLE dbo.Usuarios ADD PasswordCambiadoEl datetime2 NULL;
    PRINT '   agregada';
END
ELSE
    PRINT '   ya estaba';
GO

PRINT '── 2. Historial de migraciones ─────────────────────────────────────';

/* El paso de arriba sí corresponde a una migración que EF reconoce, así que se
   registra. */
IF OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NOT NULL
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT v.Id, N'9.0.0'
    FROM   (VALUES (N'20261019174802_AddPasswordCambiadoEl')) v(Id)
    WHERE  NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory h WHERE h.MigrationId = v.Id);
    PRINT '   historial al dia';
END
ELSE
    PRINT '   *** no existe __EFMigrationsHistory: no se registro nada';
GO

PRINT '';
PRINT '════════ Comprobación ════════';

/* ── Verificación: todo control debe leer 'presente' ──────────────────────
   Un 'FALTA' significa que el paso no se aplicó y el portal fallará al leer
   la columna en el login. */
SELECT
    'Usuarios.PasswordCambiadoEl' AS Objeto,
    CASE WHEN COL_LENGTH('dbo.Usuarios', 'PasswordCambiadoEl') IS NOT NULL
         THEN 'presente' ELSE 'FALTA' END AS Control
UNION ALL
SELECT
    'tipo datetime2, acepta nulos',
    CASE WHEN EXISTS (
            SELECT 1
            FROM sys.columns c
            JOIN sys.types  t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID('dbo.Usuarios')
              AND c.name      = 'PasswordCambiadoEl'
              AND t.name      = 'datetime2'
              AND c.is_nullable = 1)
         THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT
    'historial 20261019174802_AddPasswordCambiadoEl',
    CASE WHEN EXISTS (
            SELECT 1 FROM __EFMigrationsHistory
            WHERE MigrationId = N'20261019174802_AddPasswordCambiadoEl')
         THEN 'presente' ELSE 'FALTA' END;

/* Cuántos usuarios quedan pendientes del cambio. No es un control que pueda
   fallar: es el número de personas que el 01/11 pasarán una vez por la
   pantalla. Si la columna acaba de agregarse, son todos. */
SELECT
    COUNT(*)                                                  AS UsuariosActivos,
    SUM(CASE WHEN PasswordCambiadoEl IS NULL THEN 1 ELSE 0 END) AS PendientesDeCambio
FROM dbo.Usuarios
WHERE Activo = 1 AND IsDeleted = 0;
