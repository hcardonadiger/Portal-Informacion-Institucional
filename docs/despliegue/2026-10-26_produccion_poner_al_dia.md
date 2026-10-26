# Despliegue a producción de `ike_opt` con la base actual — 2026-10-26

Paquete para llevar el código de `ike_opt` (commit `b062f5d`) a producción, sobre la base que hoy
está en producción. Lo ejecuta Jamil; aquí no hay contraseñas ni cadenas de conexión.

## Qué cambia y por qué

La base de producción (respaldo `GestionGD_021026`, copia del 2026-10-26) tiene 74 migraciones; la
última es `20260923223127_AddReunionEncuestaActiva`. El código de `ike_opt` trae **18 migraciones
más**, que suman 19 tablas nuevas y columnas nuevas en 7 tablas existentes:

| Llega con | Qué agrega |
|---|---|
| SIGER de agosto (10 migraciones de la rama Jamil) | ficha completa del trámite, archivo SIGER original, cola de llenado asistido, URL de SOL compuesta (`Instituciones.RutaSol`) |
| 2026-10-02 | tokens de acceso MCP, adjuntos de cierre de reunión |
| 2026-10-14 | institución dueña de cada reunión (corrige el alcance de Reuniones) |
| 2026-10-19 | plantillas de proyecto; fecha de cambio de contraseña |
| 2026-10-22 | catálogo de insumos; panel de control de notificaciones |
| 2026-10-23 | reuniones a Outlook (queda apagado hasta configurar Entra ID) |

**El código nuevo no funciona con la base vieja.** Por ejemplo, «Nueva reunión» y el editor de
instituciones fallan con `Invalid column name 'RutaSol'`. **El código viejo tampoco funciona con
la base nueva**: el script quita `ConciliacionesSiger.ExpedienteTramiteId`. Por eso **el script y
la publicación van juntos, con el sitio detenido.**

## El script

`database/2026-10-26_poner_al_dia.sql`: un solo archivo. Reúne sin reescribirlos los nueve
«poner al día» del 2026-10-02 al 2026-10-23, con los scripts SIGER 11 a 15 en línea. No necesita
modo SQLCMD.

**Toca datos, no solo esquema:**
- rellena la institución dueña de las reuniones;
- en SIGER, rellena la clave estable de las conciliaciones y **borra las que apuntan a un trámite
  de expediente que ya no existe**;
- reduce la modalidad del trámite al catálogo Virtual / Presencial / Híbrido. El texto original
  queda en `ModalidadDetalle`;
- deja **el correo de las notificaciones apagado en los cinco módulos**: desde el despliegue, los
  recordatorios no mandan correo hasta que se enciendan en el panel;
- actualiza los colores de los seis roles sembrados, solo si conservan el color de fábrica.

## Pasos

1. **Avisar a los usuarios y detener el sitio** (o su grupo de aplicaciones) en IIS.
2. **Respaldar:**
   - la base;
   - `App_Data\uploads`;
   - la carpeta de claves de Data Protection: la de `Storage__DataProtectionKeysPath`, o
     `C:\PortalDigital_Keys` si no está configurada.
3. **Confirmar que la base es la correcta:**
   ```
   sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 00_diagnostico.sql -o diagnostico.txt
   ```
   Debe decir 74 migraciones, con la última `20260923223127_AddReunionEncuestaActiva`. El
   diagnóstico va a recomendar `2026-10-02_poner_al_dia.sql` porque no conoce lo posterior:
   **no lo corra**, corra el del paso 4, que ya lo incluye.
4. **Correr el script, con `-b`:**
   ```
   sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-26_poner_al_dia.sql -o salida.txt
   ```
   - La última tabla, «Comprobación final», debe decir `presente` en sus 55 filas.
   - Si aparece cualquier `Msg` de error, **no publique**: vea «Reversión».
   - En SSMS un error no detiene los lotes siguientes. Si lo corre ahí, revise toda la salida.
5. **Publicar `ike_opt`**, como en las entregas anteriores. El perfil **no** debe tener «Quitar
   archivos adicionales en el destino»: borraría `App_Data\uploads`.
6. **Revisar la configuración del sitio** (ver la tabla de abajo) y arrancarlo.
7. **Verificar** (ver la sección de abajo).
8. **Después, en el portal:**
   - otorgar `TokensMcp.*` en `/Accesos/Permisos` al rol que deba emitir tokens MCP;
   - escribir la contraseña del correo saliente en el panel de notificaciones;
   - encender el correo de los módulos que corresponda, cuando el servidor de correo esté
     revisado.

Si todavía no se hicieron los pasos de IIS de la entrega 2 (sitio siempre activo, sin tiempo de
inactividad del grupo), hacerlos ahora: `docs/despliegue/2026-10-22_tanda1_entrega2_notificaciones.md`.

## Configuración (sin valores)

| Clave | Qué hacer |
|---|---|
| `ConnectionStrings:DefaultConnection` | El `appsettings.json` del repositorio dice `Database=GestionGD_TEST`. La base de producción se llama `GestionGD`. Confirmar que la del servidor apunta a la base viva. |
| `Correo:UrlBase` (`Correo__UrlBase`) | **Obligatoria.** Dirección pública del portal, con `https://`. Sin ella no se envía la recuperación de contraseña y los enlaces de los avisos quedan relativos. |
| `Storage:DataProtectionKeysPath` | Carpeta que se conserva entre despliegues. Si se pierde, la contraseña de correo guardada en el panel y las conexiones a Outlook dejan de leerse. |
| `Auth:CambioPasswordObligatorioDesde` | Viene en `2026-11-01`. A partir de esa fecha **cada usuario cambia su contraseña una vez**, al entrar. Si no se quiere, vaciarla en el servidor. |
| `Notificaciones:IntervaloHoras` | Deja de usarse: el intervalo se cambia en el panel. |
| `MicrosoftGraph:*` | Se deja vacío. Outlook queda apagado hasta que TI registre la aplicación en Entra ID. |
| `Smtp:Password` | Sigue versionada en `appsettings.json` (riesgo ya registrado). La contraseña vigente se escribe en el panel. |

## Cómo verificar que quedó bien

- `salida.txt`: sin `Msg` de error y con 55 de 55 `presente` en «Comprobación final».
- Entrar al portal y abrir:
  - «Nueva reunión», que fallaba sin `RutaSol`;
  - el editor de un expediente;
  - `/Catalogos/Insumos`;
  - el panel de notificaciones;
  - el acta de una reunión.
- La campana muestra los avisos de antes.

## Reversión

**No hay script SQL de reversión, a propósito.** El script borra conciliaciones huérfanas y
reescribe modalidades; ningún script puede devolver esas filas. Un «revertir» parcial dejaría una
base que no corresponde ni al código viejo ni al nuevo.

La reversión es **volver al respaldo del paso 2**:
1. detener el sitio;
2. restaurar la base desde ese respaldo (`RESTORE DATABASE ... WITH REPLACE`) y, si hizo falta,
   `App_Data\uploads`;
3. volver a publicar la versión anterior del portal;
4. arrancar el sitio.

Para quitar **solo** los insumos existe `database/2026-10-22_revertir_insumos.sql`.

## Riesgos

- **Script y publicación juntos.** Si el script corre y la publicación no, el portal viejo falla
  en SIGER. Si se publica sin el script, falla «Nueva reunión».
- **Correo apagado al desplegar.** Los recordatorios por correo se detienen hasta encenderlos en el
  panel. Es a propósito, pero hay que avisar a quien los espera.
- **Cambio de contraseña obligatorio** desde el 2026-11-01, si la clave se queda como viene.
- **Carpeta de claves de Data Protection:** si se pierde, hay que volver a escribir la contraseña
  del correo en el panel.
- `00_diagnostico.sql` no conoce lo posterior al 2026-10-02 (deuda registrada en la bitácora).

## Evidencia (prueba local del 2026-10-26)

Sobre una copia local del respaldo `GestionGD_021026`, en su copia del 2026-10-26:
- **Primera corrida:** sin errores; 55 de 55 `presente`; las 18 migraciones registradas.
- **Esquema resultante:** igual, tabla por tabla y columna por columna (tipos, nulos, índices y
  llaves foráneas), a una base creada desde cero con las migraciones de `ike_opt`. La única
  diferencia son las tres columnas que crearon las migraciones sin atributo y su índice
  (`Asistentes.Confirmado`, `Asistentes.EsPreregistro`, `Instituciones.Color`). El modelo las usa
  y producción ya las tenía.
- **Segunda corrida:** sin errores y sin ningún cambio en los datos (conteo y suma de verificación
  de las 101 tablas, iguales).
- **EF:**
  - `dotnet ef migrations list` contra la copia: 89 migraciones, ninguna pendiente;
  - `dotnet ef migrations has-pending-model-changes`: el modelo no tiene cambios sin migración.
