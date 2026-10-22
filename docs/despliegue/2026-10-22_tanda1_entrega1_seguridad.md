# Tanda 1, entrega 1 (seguridad) — notas de despliegue

Rama `Jamil-GestionGD`, desde `ike_opt` (`d3bbd38`). Commits de la entrega: `e24c828`, `8182303`,
`ea2538d`, `fc9aa20`, `cffa71c` y `65daa70`.

> **Ojo:** esta rama sale de `ike_opt`. Publicarla lleva a producción **todo** `ike_opt`, no solo
> esta entrega. Lo de `ike_opt` (sus scripts de base y sus pasos) está en `database/DESPLIEGUE.md`
> y va primero. Esta entrega en sí **no cambia la base de datos**: no tiene migraciones ni scripts.

## Qué cambia y por qué

| Falla | Antes | Ahora |
|---|---|---|
| S1 | Si un correo fallaba o no había servidor, el registro (en IIS, el Visor de eventos) guardaba el correo completo, con el enlace y el token de recuperación de contraseña. | El registro solo guarda el tipo de correo, un identificador del envío y el error. |
| S2 | Los nombres, títulos y mensajes entraban sin escapar al HTML de los correos (recordatorios de ticket, expediente, reunión y compromiso, y recuperación de contraseña). | Se escapan solo esos datos; los correos se ven igual. |
| S3 | Cambiando el número del riesgo en la petición se podía editar, cambiar de estado o eliminar un riesgo de otro proyecto. Lo mismo pasaba con la herramienta MCP de cambio de estado. | Se comprueba que quien lo pide alcance el proyecto del riesgo y, desde la ficha, que el riesgo sea de ese proyecto. Si no, «no encontrado». |
| S4 | Lo mismo con editar y quitar interesados. Quitar a alguien le quitaba el acceso al proyecto. | Misma comprobación que en riesgos. |
| S5 | El enlace de recuperación se armaba con la dirección que manda quien hace la petición, que se puede falsificar. | El enlace sale solo de la configuración `Correo:UrlBase`. Si falta, no se envía el correo (la pantalla muestra el mismo mensaje de siempre). El QR de asistencia también usa esa dirección si está configurada. |

## Configuración (antes de publicar)

**Obligatorio: `Correo__UrlBase`.** Es la dirección pública del portal, tal como la escribe una
persona en el navegador, **incluida la carpeta virtual** si el sitio cuelga de una (por ejemplo,
`https://servidor.dominio/GestionGD`). Va sin barra final, aunque con barra también funciona.

- **Sin esta variable, la recuperación de contraseña deja de enviar correos.**
- Conviene ponerla como variable de entorno del sitio, para que una publicación no la pise. En el
  Administrador de IIS: sitio → *Editor de configuración* → sección
  `system.webServer/aspNetCore` → en «Desde», elegir `ApplicationHost.config <location path='…'>`
  → `environmentVariables` → agregar `Correo__UrlBase` con la dirección. Aplicar.
- No hace falta reiniciar a mano: el cambio recicla la aplicación.

No cambia ninguna otra configuración. En particular, la sección `Smtp` queda igual.

## Pasos de publicación en IIS

1. Seguir primero `database/DESPLIEGUE.md` para lo que trae `ike_opt`.
2. **Respaldar** la carpeta `App_Data\uploads` del sitio, porque ahí viven los archivos subidos.
3. Revisar que el perfil de publicación **no** tenga marcada la opción «Quitar archivos adicionales
   en el destino» (*Remove additional files at destination*). Con esa opción se borran los archivos
   subidos.
4. Configurar `Correo__UrlBase` (ver arriba).
5. Publicar como siempre.

**Reversión:** volver a publicar la compilación anterior. La variable `Correo__UrlBase` puede
quedarse: la versión anterior la ignora. No hay nada de base de datos que revertir.

## Verificación después de publicar

1. **Recuperación de contraseña:** pedirla para una cuenta propia. El correo debe llegar y su
   enlace debe empezar con la dirección configurada en `Correo__UrlBase`. Abrir el enlace y
   comprobar que deja cambiar la contraseña.
2. **Registro:** en el Visor de eventos (*Registros de Windows → Aplicación*), las entradas
   recientes del portal sobre correo dicen «Correo RecuperacionContrasena (envío xxxxxxxx)
   enviado». No deben tener cuerpo, enlaces ni tokens. Si aparece «no enviado: falta la dirección
   pública del portal (Correo:UrlBase)», la variable no quedó configurada.
3. **QR de asistencia:** abrir *Reuniones → una reunión → Asistencia*. El enlace bajo el QR debe
   empezar con la dirección configurada.
4. **Riesgos e interesados:** en la ficha de un proyecto, editar un riesgo, cambiarle el estado y
   eliminarlo. Después, editar y quitar un interesado de prueba. Todo debe funcionar igual que
   antes.
5. **Recordatorio manual:** enviar un recordatorio de un ticket. El correo debe llegar con el
   mismo aspecto de siempre.

## Riesgos

- **Correos ya registrados:** el Visor de eventos del servidor puede guardar, de antes de esta
  entrega, correos completos con datos de personas y enlaces de recuperación. Los tokens viejos ya
  vencieron, pero los datos siguen ahí. Decide Jamil si se vacía ese registro o se restringe quién
  lo lee.
- **Contraseña del correo saliente:** sigue versionada en `src/Web/appsettings.json`. Esta entrega
  no la toca. Su cambio lo decide la jefatura.
- **Si `Correo__UrlBase` no se configura,** la recuperación de contraseña no envía correos y la
  persona no se entera, porque la pantalla mantiene su mensaje genérico. Por eso es el paso 4.
