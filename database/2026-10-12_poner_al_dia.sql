/* ╔══════════════════════════════════════════════════════════════════╗
   ║  2026-10-12 — Colores de rol al Manual de Marca DIGER            ║
   ║                                                                  ║
   ║  QUÉ TRAE: actualiza los seis colores sembrados de la tabla      ║
   ║  Roles a la paleta del Manual de Marca. Son el fondo de los      ║
   ║  badges de rol en /Accesos/Roles y /Usuarios, y los seis         ║
   ║  estaban fuera de la paleta.                                     ║
   ║                                                                  ║
   ║  NO es una migración de EF: no cambia el esquema, solo valores.  ║
   ║  No se escribe fila en __EFMigrationsHistory a propósito —       ║
   ║  registrar un MigrationId que el código no reconoce solo         ║
   ║  confunde a quien luego compare historial contra migraciones.    ║
   ║                                                                  ║
   ║  VA DESPUÉS DE: database/2026-10-02_poner_al_dia.sql             ║
   ║                                                                  ║
   ║  CÓMO CORRERLO:                                                  ║
   ║  sqlcmd -S <servidor> -d <base> -b -I -f 65001                   ║
   ║         -i 2026-10-12_poner_al_dia.sql                           ║
   ╚══════════════════════════════════════════════════════════════════╝ */

SET NOCOUNT ON;

IF OBJECT_ID('dbo.Roles', 'U') IS NULL
BEGIN
    THROW 50000, 'No existe la tabla Roles. Corra primero database/00_diagnostico.sql.', 1;
END

/* Cada UPDATE exige el color sembrado en el WHERE. Si alguien ya cambió el
   color a mano desde el editor de roles, su elección se respeta y la fila no
   se toca. Eso es además lo que hace idempotente al script: la segunda
   corrida no encuentra ninguna fila que cambiar.

   Los seis colores nuevos pasan WCAG AA contra el texto blanco del badge
   (medido: 12.00, 8.42, 6.11, 5.02, 6.20 y 5.02) y son distintos entre sí:
   la separación más corta es 12.3, entre JefeUnidad y Empleado. Que se
   distingan importa tanto como que cumplan el manual — el badge existe para
   diferenciar roles de un vistazo. */

DECLARE @cambios TABLE (Rol NVARCHAR(100), Nuevo NVARCHAR(20));

UPDATE dbo.Roles SET Color = '#02395e'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'Administrador'   AND Color = '#3c3489';

UPDATE dbo.Roles SET Color = '#0c5278'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'JefeInstitucion' AND Color = '#0c447c';

UPDATE dbo.Roles SET Color = '#1a65a3'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'JefeArea'        AND Color = '#1a5fa0';

UPDATE dbo.Roles SET Color = '#15803d'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'JefeUnidad'      AND Color = '#1b5e20';

UPDATE dbo.Roles SET Color = '#0f6e56'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'Empleado'        AND Color = '#085041';

UPDATE dbo.Roles SET Color = '#b45309'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'Consultor'       AND Color = '#6d4c00';

/* El conteo va a una variable antes del PRINT: SQL Server no admite una
   subconsulta dentro de CONCAT en ese contexto y aborta el lote entero al
   compilarlo, sin ejecutar ni un UPDATE. */
DECLARE @n INT = (SELECT COUNT(*) FROM @cambios);

IF @n > 0
    PRINT CONCAT('actualizados: ', @n, ' rol(es)');
ELSE
    PRINT 'ya estaban al dia (o fueron cambiados a mano): no se toco ninguna fila';

/* ── Verificación: todo control debe leer 'presente' ──────────────────────
   Un 'FALTA' aquí significa que esa fila conserva un color fuera de la
   paleta. Puede ser legítimo —alguien lo cambió a mano— pero hay que
   mirarlo, no darlo por bueno. */
SELECT
    Id,
    Color,
    CASE WHEN Color IN ('#02395e','#0c5278','#1a65a3','#15803d','#0f6e56','#b45309')
         THEN 'presente' ELSE 'FALTA' END AS Control
FROM dbo.Roles
WHERE Id IN ('Administrador','JefeInstitucion','JefeArea',
             'JefeUnidad','Empleado','Consultor')
ORDER BY Id;
