# Panel de control → Notificaciones (entrega 1) — notas de despliegue

Rama `panel-notificaciones`. **Cambia la base de datos**: trae la migración
`20261022223203_PanelControlNotificaciones` y su script,
`database/2026-10-22_panel_control_notificaciones.sql`. Va después de las tandas 1 y 2 de
notificaciones (`2026-10-22_tanda1_entrega1_seguridad.md` y
`2026-10-22_tanda1_entrega2_notificaciones.md`); sus pasos siguen siendo obligatorios.

## Qué cambia y por qué

| Antes | Ahora |
|---|---|
| Cada cuánto revisa el sistema los plazos se cambiaba en `appsettings.json` (`Notificaciones:IntervaloHoras`) y hacía falta reiniciar el sitio. | Se cambia en **Panel de control → Notificaciones** (`/PanelControl/Notificaciones`), en horas y minutos, sin reiniciar. Mínimo 5 minutos, máximo 24 horas. |
| No había forma de apagar los avisos de un módulo. | Cada módulo (Tickets, Reuniones, Expedientes, Proyectos, Soporte) tiene sus interruptores **Campana**, **Correo** y **Agrupar**. |
| Los avisos automáticos solo llegaban a la campana. | Con «Correo» encendido, también salen por correo, uno por persona por revisión como máximo. El chat de soporte nunca va por correo. |
| Varios avisos del mismo asunto llenaban la campana. | Con «Agrupar» encendido (así arranca), se juntan en uno solo, con su detalle en `/Notificaciones/{id}`. |
| El servidor, el usuario y la contraseña del correo estaban en `appsettings.json`. | Se guardan desde el panel. La contraseña queda cifrada en la base. Mientras nadie guarde ese bloque, el portal sigue usando la sección `Smtp` de la configuración. |
| Nada registraba quién cambiaba la configuración de avisos. | Cada cambio del panel queda en su bitácora (`PanelControlBitacora`). |

## Base de datos

**Orden:** correr `database/2026-10-22_panel_control_notificaciones.sql` **después** de
`2026-10-22_poner_al_dia.sql`. Los controles finales tienen que decir `presente`.

```powershell
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-22_panel_control_notificaciones.sql
```

Si no sabe en qué estado está la base, corra antes `database/00_diagnostico.sql`. El script se
puede correr dos veces: la segunda vuelta dice «ya estaba» en cada paso y no cambia nada.

Crea las tablas `AjustesNotificaciones`, `ModulosNotificacion`, `NotificacionElementos`,
`CorreosPendientes` y `PanelControlBitacora`, y agrega dos columnas a `Notificaciones`
(`Cantidad` y `FechaActualizacion`).

## Lo que hay que saber antes de publicar

- **El correo arranca apagado en los cinco módulos.** Desde el despliegue, los recordatorios
  manuales de Tickets, Reuniones y Expedientes **dejan de mandar correo** hasta que alguien
  encienda «Correo» en Panel de control → Notificaciones. Encenderlo módulo por módulo: con el
  correo encendido, los avisos automáticos del módulo también salen por correo, uno por persona
  por revisión como máximo.
- **El intervalo arranca en 4 horas** aunque el servidor tuviera otro
  `Notificaciones__IntervaloHoras`. Si era distinto, ajustarlo en el panel. Desde aquí esa
  variable deja de usarse.
- **La carpeta de claves de Data Protection** (la de `PersistKeysToFileSystem` en `Program.cs`)
  tiene que conservarse entre despliegues. Si se pierde, la contraseña guardada deja de leerse: el
  panel lo dice en rojo y hay que volver a escribirla. La carpeta es la de
  `Storage__DataProtectionKeysPath`, o `C:\PortalDigital_Keys` si esa variable no está.
- **La primera vez que se guarda el bloque «Correo saliente»** desde el panel hay que escribir la
  contraseña. Si el servidor de correo no la pide, marque «Quitar la contraseña».
- **Permiso:** `PanelControl.Notificaciones` → Ver / Editar en `/Accesos/Permisos`. Los
  administradores entran sin otorgarlo. Quien tiene solo «Ver» ve la pantalla pero no puede
  cambiarla.

## Publicación

Igual que las entregas anteriores:
1. respaldar `App_Data\uploads` y la carpeta de claves de Data Protection;
2. correr el script de la base (ver arriba);
3. comprobar que el perfil **no** tenga «Quitar archivos adicionales en el destino»;
4. publicar.

**Reversión:** volver a publicar la compilación anterior. Lo que trajo el script puede quedarse.
La versión anterior no usa las tablas nuevas. Las dos columnas nuevas de `Notificaciones` tienen
valor por defecto en la base (`Cantidad` = 1, `FechaActualizacion` = `0001-01-01`), así que la
versión anterior sigue guardando avisos sin error. Esa versión vuelve a mandar correo en los
recordatorios manuales y a usar `Notificaciones__IntervaloHoras` y la sección `Smtp` de la
configuración.

Si después se vuelve a publicar esta versión, los avisos que creó la anterior mientras tanto
quedan con `FechaActualizacion` en `0001-01-01` y salen al final de la campana. Se corrigen con:

```sql
UPDATE Notificaciones SET FechaActualizacion = FechaCreacion WHERE FechaActualizacion = '0001-01-01';
```

## Verificación después de publicar

1. **El script.** Todos los controles de la comprobación final dicen `presente`.
2. **El menú.** Con un administrador, el menú tiene «Panel de control», y dentro, «Notificaciones».
3. **La pantalla.** `/PanelControl/Notificaciones` muestra «Revisión automática», los cinco
   módulos y «Correo saliente». «Correo» aparece apagado en Tickets, Reuniones, Expedientes y
   Proyectos. Soporte dice «no aplica», porque el chat nunca va por correo.
4. **El intervalo.** Guardar, por ejemplo, 0 h 15 min. Después de la siguiente revisión, «Próxima»
   queda unos 15 minutos después de «Última». Después, volver a dejar **4 horas (240 minutos)**, o
   el valor que tenía el servidor si era otro.
5. **El correo.** Guardar el bloque «Correo saliente» con la contraseña y usar el correo de
   prueba. Encender «Correo» solo en los módulos que deban mandarlo.

## Contraseña del correo

**Pendiente aparte:** la contraseña de Gmail versionada en `appsettings.json` desde `0462b14`.
Cambiarla en la cuenta y escribir la nueva en el panel. Quitarla del archivo no la borra del
historial de git.

## Riesgos

- **Si nadie enciende «Correo»,** los recordatorios manuales llegan solo a la campana. Es a
  propósito: el correo se enciende cuando el servidor de correo está revisado.
- **Si se pierde la carpeta de claves,** el portal no puede mandar correo con la contraseña
  guardada hasta que alguien la vuelva a escribir en el panel.
