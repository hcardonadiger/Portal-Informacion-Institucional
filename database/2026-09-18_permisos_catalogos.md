# Permisos de los catálogos — quién administra qué

**Fecha:** 2026-09-18
**Rama:** `dev`

## La matriz que se pide

| Catálogo | Quién lo administra |
|---|---|
| Unidades | **JefeArea** y **JefeInstitucion** |
| Áreas | **JefeInstitucion** |
| Instituciones | solo administradores |
| Prioridades de proyectos | solo administradores |
| Prioridades de tickets | solo administradores |
| Temas de tickets | solo administradores |

El administrador no necesita que se le otorgue nada: su rol aprueba por código.

## Ojo: esto **revoca** algo que hoy está dado

`Tickets.Temas.Editar` lo tienen hoy **Empleado, JefeArea, JefeUnidad y JefeInstitucion**. Dejar
los temas de tickets «solo para administradores» significa quitárselo a esos cuatro roles, y con
eso pierden la pantalla de temas y tiempos de atención (el SLA por tema).

Si eso no era la intención, no corra la parte de revocación —o vuelva a marcar la casilla en la
pantalla de permisos—.

## Lo recomendado: hacerlo desde la pantalla

**Administración › Permisos.** Es un puñado de casillas, y es el camino correcto por dos razones
que el SQL no da:

- **Deja rastro.** El cambio queda en `PermisosAuditoria` y se puede consultar en
  Administración › Auditoría. Un `INSERT` directo no escribe ahí.
- **Surte efecto de inmediato.** La matriz vive en una caché por rol; la pantalla la invalida al
  guardar. Cambiando las filas por SQL, las sesiones abiertas siguen con los permisos viejos
  **hasta que se reinicie la aplicación**.

Rol por rol, marque y guarde:

| Rol | Marcar | Desmarcar |
|---|---|---|
| JefeArea | `Unidades.Ver`, `Unidades.Editar` | `Tickets.Temas.Editar` |
| JefeInstitucion | `Areas.Ver`, `Areas.Editar`, `Unidades.Ver`, `Unidades.Editar` | `Tickets.Temas.Editar` |
| JefeUnidad | — | `Tickets.Temas.Editar` |
| Empleado | — | `Tickets.Temas.Editar` |

## Si aun así lo prefiere por SQL

Idempotente: se puede correr dos veces. **Reinicie la aplicación después**, o las sesiones abiertas
seguirán con la matriz vieja.

```sql
SET NOCOUNT ON;
BEGIN TRANSACTION;

-- Otorgar. El NOT EXISTS evita duplicar si ya estuviera dado.
INSERT INTO RolPermisos (RolId, PermisoClave)
SELECT v.RolId, v.Clave
FROM   (VALUES
           ('JefeArea',        'Unidades.Ver'),
           ('JefeArea',        'Unidades.Editar'),
           ('JefeInstitucion', 'Areas.Ver'),
           ('JefeInstitucion', 'Areas.Editar'),
           ('JefeInstitucion', 'Unidades.Ver'),
           ('JefeInstitucion', 'Unidades.Editar')
       ) AS v(RolId, Clave)
WHERE NOT EXISTS (
    SELECT 1 FROM RolPermisos rp
    WHERE rp.RolId = v.RolId AND rp.PermisoClave = v.Clave);

-- Revocar los temas de tickets a los roles no administradores. Ver la advertencia de arriba:
-- si no quiere quitarles esa pantalla, borre este DELETE.
DELETE FROM RolPermisos
WHERE  PermisoClave = 'Tickets.Temas.Editar'
  AND  RolId IN ('Empleado', 'JefeArea', 'JefeUnidad', 'JefeInstitucion');

COMMIT;
```

## Comprobación

```sql
SELECT rp.RolId, rp.PermisoClave
FROM   RolPermisos rp
WHERE  rp.PermisoClave LIKE 'Areas.%'
   OR  rp.PermisoClave LIKE 'Unidades.%'
   OR  rp.PermisoClave LIKE 'Instituciones.%'
   OR  rp.PermisoClave LIKE 'Prioridades.%'
   OR  rp.PermisoClave LIKE 'Tickets.Temas%'
ORDER  BY rp.RolId, rp.PermisoClave;
```

Tiene que quedar exactamente esto, y nada más:

```
JefeArea         Unidades.Editar
JefeArea         Unidades.Ver
JefeInstitucion  Areas.Editar
JefeInstitucion  Areas.Ver
JefeInstitucion  Unidades.Editar
JefeInstitucion  Unidades.Ver
```

## Lo que este permiso NO hace

**No acota qué filas ve cada quien.** Los listados de Áreas y Unidades no llevan filtro de alcance:
un JefeArea con `Unidades.Ver` ve las unidades de **todas** las instituciones, no solo las de su
área, y puede editarlas. El permiso dice «puede entrar al catálogo», no «puede entrar a su parte
del catálogo».

Si hace falta acotarlo, es un cambio aparte —hay que meter el filtro en las consultas, como ya lo
tienen expedientes y reuniones— y conviene decidirlo antes de repartir estos permisos en producción.
