/* =====================================================================================
   Validar qué reuniones ve cada usuario
   -------------------------------------------------------------------------------------
   No cambia nada: solo mira. Reproduce en SQL el mismo filtro que AppDbContext aplica a
   Reunion, usuario por usuario, y dice cuántas reuniones alcanza cada uno y de qué unidad son.

   Para qué sirve: después de atribuirle dueño a las reuniones, confirmar que nadie ve el
   trabajo de otra unidad y que nadie quedó ciego sin querer. La columna DIAGNOSTICO es la que
   hay que leer; el resto son los números que la sostienen.

   Qué dice cada diagnóstico:
     OK                  el usuario ve solo lo que le toca por su alcance.
     VE DE OTRA UNIDAD   un rol de nivel Unidad alcanza reuniones de una unidad distinta a la
                         suya. Eso no debería pasar nunca: es la fuga que se corrigió.
     SIN REUNIONES       no ve ninguna. No siempre es un error —puede que su unidad no haya
                         convocado ninguna todavía— pero conviene mirarlo.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i validar_alcance_reuniones.sql
   ===================================================================================== */

SET NOCOUNT ON;

WITH U AS (
    SELECT u.Id AS UsuarioId, u.Correo, a.InstitucionId, a.AreaId, a.UnidadId, a.Rol,
           ISNULL(CAST(r.NivelAlcance AS nvarchar(20)), N'Unidad') AS Nivel,
           CASE WHEN r.EsAdministrador = 1 THEN 1 ELSE 0 END AS EsGlobal
    FROM Usuarios u
    JOIN AsignacionesUsuario a ON a.UsuarioId = u.Id
    LEFT JOIN Roles r ON r.Id = a.Rol AND r.Activo = 1
    WHERE u.IsDeleted = 0
),
V AS (
    SELECT U.*, m.Id AS ReunionId, m.UnidadId AS UnidadReunion
    FROM U
    JOIN Reuniones m ON m.IsDeleted = 0 AND (
         m.CreadoPorId = U.UsuarioId
      OR (m.Visibilidad <> N'Privada' AND (
             U.EsGlobal = 1
          OR (U.Nivel = N'Institucion' AND m.AreaId IS NULL AND m.UnidadId IS NULL
                                       AND m.InstitucionDuenaId = U.InstitucionId)
          OR (U.Nivel = N'Unidad'      AND m.UnidadId IS NOT NULL AND m.UnidadId = U.UnidadId)
          OR (U.Nivel = N'Area'        AND m.AreaId   IS NOT NULL AND m.AreaId   = U.AreaId)
          OR (U.Nivel = N'Institucion' AND m.AreaId   IS NOT NULL AND EXISTS (
                 SELECT 1 FROM Areas ar WHERE ar.Id = m.AreaId AND ar.InstitucionId = U.InstitucionId))
      )))
)
SELECT  U.Correo, U.Rol, U.Nivel,
        U.InstitucionId AS Institucion, ISNULL(U.AreaId,N'—') AS Area, ISNULL(U.UnidadId,N'—') AS Unidad,
        COUNT(V.ReunionId) AS Reuniones,
        SUM(CASE WHEN V.UnidadReunion IS NOT NULL AND V.UnidadReunion <> ISNULL(U.UnidadId,N'')
                 THEN 1 ELSE 0 END) AS DeOtraUnidad,
        CASE WHEN U.Nivel = N'Unidad' AND U.EsGlobal = 0 AND SUM(CASE WHEN V.UnidadReunion IS NOT NULL
                  AND V.UnidadReunion <> ISNULL(U.UnidadId,N'') THEN 1 ELSE 0 END) > 0
                  THEN N'VE DE OTRA UNIDAD'
             WHEN COUNT(V.ReunionId) = 0 THEN N'SIN REUNIONES'
             ELSE N'OK' END AS Diagnostico
FROM U LEFT JOIN V ON V.UsuarioId = U.UsuarioId AND V.InstitucionId = U.InstitucionId
GROUP BY U.Correo, U.Rol, U.Nivel, U.InstitucionId, U.AreaId, U.UnidadId, U.EsGlobal
ORDER BY CASE WHEN U.Nivel = N'Unidad' AND U.EsGlobal = 0 THEN 0 ELSE 1 END, U.Correo;
GO
