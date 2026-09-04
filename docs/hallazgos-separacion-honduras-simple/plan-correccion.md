# Plan de corrección — hallazgos de la separación SIGER / Honduras Simple

**Origen:** `hallazgos-separacion-honduras-simple.md`, corrida del 4 de septiembre de 2026 (19 pasos OK, 1 falla, 6 observaciones).
**Fecha del plan:** 4 de septiembre de 2026.
**Base sobre la que se diagnosticó:** `DigerTramitesEstado_Unificada_Sandbox`.

Quien probó tenía instrucción de **no** investigar causas. Este plan sí las investigó, y el
triaje cambia bastante el cuadro: **de los siete hallazgos, tres no son fallos del sistema**.
Uno de ellos, la falla del paso 8, la causó el usuario de prueba que se creó para el recorrido.

---

## Triaje

| # | Hallazgo | Veredicto | Causa verificada | Fase |
|---|---|---|---|---|
| H-02 | Cerrar sesión pide permisos | **Fallo real, alta** | `ConsultorReadOnlyPageFilter` bloquea todo POST de un rol de solo lectura | 1 |
| H-04 | Lo aprobado no aparece en la ficha | **Fallo real, media-alta** | Se escribe en `TiempoTexto`; la ficha muestra `Temporalidad` | 2 |
| H-06 | Aviso de éxito sin cambios | **Fallo real, baja** | `TempData["SuccessMsg"]` incondicional | 3 |
| H-01 | Conciliación abre vacía | **No es fallo** | El operador de prueba está en DIGER y no hay ni un expediente de DIGER | 4 |
| H-03 | «Aprobar las N del filtro» no hace nada | **No es fallo** | El `confirm()` de JavaScript bloquea al probador automatizado | 4 |
| H-05 | No hay candidatas de las tres instituciones | **No es fallo** | Sembrado del sandbox | 4 |
| H-07 | El documento dice «menú de la izquierda» | Documento | El menú es una barra horizontal arriba | 4 |

### Lo que se demostró al diagnosticar

**H-01 no es un fallo, y el dato estrella del informe está mal leído.** El informe dice que
«el contador *Inventario SIGER* marca 0». No es un contador: en
`src/Web/Pages/HondurasSimple/Conciliacion.cshtml:70` es un enlace al inventario
(`<a class="btns" asp-page="/Siger/Index">Inventario SIGER</a>`). No muestra ningún número.

Los ceros de verdad vienen del alcance del usuario. En el sandbox hay **8 expedientes y 58
trámites en expedientes** — no está vacío. Pero pertenecen a CONSUCOOP (21 filas), IHADFA (18),
FOSOVI (13) e INPREMA (6): **ninguno a DIGER**. El operador de prueba se creó como
`InstitucionId = 'DIGER'`, `AreaId = 'GOBDIG'`, rol Jefe de Área, cuyo `NivelAlcance` es `Area`.
El filtro RLS de `Expediente` (`AppDbContext.cs:135-146`) ancla toda rama no global en
`e.InstitucionId == _activeInst`, así que devuelve cero filas. La pantalla funciona; el usuario
no tiene nada dentro de su alcance.

**H-03 no es un fallo.** El handler `OnPostAprobarFiltroAsync`
(`src/Web/Pages/HondurasSimple/Llenado.cshtml.cs:98`) se disparó por HTTP saltándose el
JavaScript: devolvió `302` y aprobó la propuesta pendiente que coincidía con el filtro
`400-009`. Lo que falló fue el `onclick="return confirm(...)"` de
`Llenado.cshtml:128`: un navegador automatizado se queda bloqueado en ese diálogo. Encaja con
lo que reporta el propio informe — «Aprobar marcadas», que no lleva diálogo, funcionó a la
primera; y «la página quedó sin responder unos segundos» es exactamente un modal esperando.

**H-04 sí es un fallo, y peor de lo que parecía.** El valor **sí se escribió**:
`TramitesSiger.Id = 9` tiene `TiempoTexto = '46 días hábiles'`. Lo que pasa es que
`Detalle.cshtml:205-206` muestra `Temporalidad`, que es otra columna y contiene el literal
`'No registrada'`. `TiempoTexto` se carga en el modelo (`Detalle.cshtml.cs:78` y `:117`) pero
no se pinta en ninguna parte. O sea: el llenado asistido funciona y la ficha no lo enseña.

**H-02 es más grande que el logout.** El filtro no mira `[AllowAnonymous]` ni
`[PermisoNoRequerido]`, solo el verbo HTTP. Hoy, con un rol de solo lectura, **tampoco se puede
cambiar la propia contraseña** — ni editar el perfil, ni vincular un certificado, ni cambiar de
contexto. Son diez páginas marcadas de autoservicio, todas rotas por lo mismo, y ninguna se
reportó porque el recorrido no las tocaba.

---

## Restricciones globales

- **No tocar `DigerTramitesEstado_Unificada`** ni `VentanillaDigital_Net`: van a producción. Todo
  contra los `_Sandbox`.
- El guion `scripts\pruebas\sql\usuarios-cowork.sql` conserva su guarda: aborta si la base no
  termina en `_Sandbox`.
- Toda la suite verde antes de cada commit: **665 pruebas** (54 Domain + 390 Application + 44 Api
  + 177 Web).
- Comprometer sí; **`git push` no** — lo hace el usuario.
- Rama `Jamil`.

---

## Fase 1 · H-02 — Cerrar sesión y el autoservicio

**Por qué primero:** es el único fallo real de severidad alta, es de producción y se nota el
primer día. Y su alcance real son diez páginas, no una.

**Archivos**
- Modificar: `src/Web/Common/ConsultorReadOnlyPageFilter.cs`
- Modificar: `src/Infrastructure/Persistence/AppDbContext.cs:287-290`
- Pruebas: `tests/Web.Tests/`, junto a las pruebas de filtros existentes

### Tarea 1.1 — El filtro respeta las excepciones declaradas

`ConsultorReadOnlyPageFilter.OnPageHandlerExecutionAsync` decide solo por el verbo. Debe leer,
igual que hace `PermissionPageFilter`, los atributos del handler y de su clase, y dejar pasar:

- `[AllowAnonymous]` — es el caso de `Cuenta/Logout.cshtml.cs`, que además tiene que poder
  cerrar sesión aunque la sesión sea de un rol de solo lectura.
- `[PermisoNoRequerido]` — los diez de autoservicio ya marcados: `Cuenta/CambiarContexto`,
  `CambiarFiltroJerarquia`, `Certificado`, `Contrasena`, `Perfil`, `VerificarCertificado`,
  `VincularCertificado`, `Ayuda/Index`, `Notificaciones/Index`, `Tableros/MiTablero`.

El patrón de lectura de atributos ya está resuelto en `PermissionPageFilter.cs:23-25`
(`method?.GetCustomAttribute<T>() ?? method?.DeclaringType?.GetCustomAttribute<T>()`); conviene
copiarlo tal cual para que las dos capas decidan igual.

**Pruebas a escribir antes del cambio**

1. Un rol con `EsSoloLectura = true` hace POST a `/Cuenta/Logout` → **no** recibe `Forbid`, y la
   cookie de sesión queda invalidada.
2. Ese mismo rol hace POST a una página con `[PermisoNoRequerido]` → no recibe `Forbid`.
3. Ese mismo rol hace POST a una página de mutación normal, sin marcadores → **sigue**
   recibiendo `Forbid`. Esta es la que evita que el arreglo abra la puerta de par en par.

**Criterio de aceptación:** cerrar sesión funciona con cualquier rol y la sesión queda
efectivamente cerrada; las páginas de mutación siguen bloqueadas para roles de solo lectura.

### Tarea 1.2 — La red de última línea y la contraseña propia

Arreglar el filtro **no basta** para el autoservicio que escribe. `AppDbContext.cs:287-290`
lanza `UnauthorizedAccessException` ante cualquier mutación de un rol de solo lectura, sin
excepciones. Cerrar sesión no guarda nada, así que la tarea 1.1 lo deja resuelto; pero cambiar
la contraseña propia, editar el perfil o vincular un certificado sí guardan, y morirían ahí.

Hay una decisión que tomar, y conviene tomarla explícita y no por descuido:

- **Opción A, recomendada:** acotar la excepción a las entidades de la propia cuenta. El
  contexto permite mutar `Usuario` cuando la única fila tocada es la del usuario activo. Es
  estrecha, se puede probar y no abre nada más.
- **Opción B:** una bandera por unidad de trabajo (`PermitirAutoservicio`) que la página levanta
  antes de guardar. Más flexible y más fácil de abusar.

**Prueba:** un rol de solo lectura cambia su propia contraseña → se guarda. El mismo rol intenta
mutar la fila de **otro** usuario → sigue lanzando.

**Criterio de aceptación:** un rol de solo lectura puede cerrar sesión y cambiar su contraseña;
no puede tocar datos de nadie más.

**Commit:** `Cerrar sesión y el autoservicio dejan de pasar por la matriz de permisos`

---

## Fase 2 · H-04 — El valor aprobado que la ficha no enseña

**Archivos**
- Modificar: `src/Web/Pages/Siger/Detalle.cshtml:205-206`
- Pruebas: `tests/Web.Tests/`

### Tarea 2.1 — Mostrar `TiempoTexto` en la ficha

`TiempoTexto` ya viaja al modelo de la página; solo falta pintarlo. Junto a `Temporalidad`, con
etiqueta propia, para que no se confundan: son dos cosas distintas y el informe demuestra que se
confunden.

Conviene revisar de paso si el resto de campos que el llenado asistido escribe tienen el mismo
problema. Basta comparar la lista de campos que aprueba la pantalla de Llenado con lo que
`Detalle.cshtml` pinta, campo por campo.

**Prueba:** una ficha con `TiempoTexto` poblado y `Temporalidad` nula renderiza el texto del
tiempo.

**Criterio de aceptación:** el valor aprobado en Llenado asistido se ve en la ficha, o la
pantalla dice con claridad dónde lo escribió.

**Commit:** `La ficha muestra el tiempo que escribe el llenado asistido`

---

## Fase 3 · H-06 y el diálogo bloqueante

**Archivos**
- Modificar: `src/Web/Pages/Accesos/Permisos.cshtml.cs:125`
- Modificar: `src/Application/Permisos/PermisosModule.cs`, para que el comando devuelva cuántas cambiaron
- Modificar: `src/Web/Pages/Accesos/Permisos.cshtml`, la cabecera fija
- Modificar: `src/Web/Pages/HondurasSimple/Llenado.cshtml:128`

### Tarea 3.1 — El aviso dice la verdad

`GuardarMatrizPermisosCommand` no devuelve nada, así que la página no puede distinguir «guardé
tu cambio» de «no había nada que guardar». Que devuelva cuántas concesiones se añadieron y
cuántas se quitaron, y que el aviso lo refleje: *«Permisos actualizados: 1 otorgado.»* frente a
*«No había cambios que guardar.»*

**Prueba:** guardar sin cambios devuelve cero y el aviso lo dice; guardar con un cambio devuelve
uno.

### Tarea 3.2 — La cabecera fija no tapa la primera fila

El informe apunta a que el clic no llegó porque la fila quedaba bajo la cabecera fija al
desplazarse. Es `scroll-margin-top` en las filas de la matriz, del alto de la cabecera. Barato, y
evita que el próximo que pruebe reporte otro fantasma.

### Tarea 3.3 — Quitar el `confirm()` bloqueante

Esto es lo que produjo H-03, y volverá a producirlo en cada corrida automatizada. Sustituir el
`onclick="return confirm(...)"` por una confirmación en la propia página: un paso intermedio que
muestre cuántas se van a escribir y pida pulsar otra vez. Se conserva el aviso, que es útil
porque la acción escribe en fichas, y deja de bloquear a quien prueba.

**Criterio de aceptación:** aprobar por filtro sigue pidiendo confirmación, y un navegador
automatizado puede completarla.

**Commit:** `El aviso de permisos distingue si hubo cambios; confirmación sin modal`

---

## Fase 4 · El terreno de pruebas y el documento

No toca código de la aplicación, así que puede ir antes, después o en paralelo. Va al final
porque nada de producción depende de ello — pero **hay que hacerlo antes de volver a probar**, o
la próxima corrida repite los mismos tres falsos positivos.

**Archivos**
- Modificar: `scripts/pruebas/sql/usuarios-cowork.sql`
- Modificar: `scripts/pruebas/Refrescar-Sandbox.ps1`
- Modificar: `docs/flujo-de-pruebas-separacion-honduras-simple.html`

### Tarea 4.1 — El operador de prueba, en una institución con expedientes (cierra H-01)

Cambiar la asignación del operador de `InstitucionId = 'DIGER'` a **`'CONSUCOOP'`**, que es la
que más expedientes tiene (3 expedientes, 21 trámites) y además es una de las tres que replica
el portal ciudadano, así que sirve también para el tramo E. Los expedientes tienen `AreaId`
nulo, y el filtro RLS admite `e.AreaId == null`, de modo que un rol de alcance Área los ve en
cuanto la institución coincide.

La alternativa sería sembrar expedientes de DIGER en `Refrescar-Sandbox.ps1`. Es más trabajo y
no aporta nada que no dé el cambio de una línea.

**Verificación:** entrar como operador, abrir Conciliación y ver contadores distintos de cero.

### Tarea 4.2 — Una candidata sin publicar de las tres instituciones (cierra H-05)

El sembrado publica todo lo que puede, y en INPREMA deja las 24 fichas publicadas: no queda
candidata y el paso 18 no se puede ejecutar tal como está escrito. Que el sembrado **deje al
menos una ficha aprobada y sin publicar** en INPREMA, IHTT o CONSUCOOP.

**Verificación:** filtrar Candidatas por esas tres devuelve al menos una.

### Tarea 4.3 — Corregir el recorrido (cierra H-07 y previene el mal dato de H-01)

En `docs/flujo-de-pruebas-separacion-honduras-simple.html`:

1. «menú de la izquierda» y «menú lateral» → «barra de arriba» (líneas 213, 216 y 279).
2. Añadir a «Lo que ya sabemos y no hay que reportar»: **«Inventario SIGER», en Conciliación, es
   un botón que lleva al inventario, no un contador.** Es lo que se leyó mal esta vez.
3. Añadir: **si Conciliación abre vacía, comprobar primero el alcance del usuario** — un rol de
   alcance Área o Unidad solo ve expedientes de su propia institución.
4. Reescribir el paso 8 para que diga con qué usuario e institución se espera ver datos.

### Tarea 4.4 — Cerrar el pendiente del paso 15

Quedó sin comprobar la mitad de «sin volver a entrar»: las pruebas fueron secuenciales y entre
el paso 14 y el 15 hubo un cierre y una apertura de sesión. Se cierra con dos sesiones vivas a la
vez —dos juegos de cookies bastan— quitando el permiso en una y recargando la otra sin volver a
entrar. `PermisosModule.cs:171-172` invalida la caché tras guardar, así que debería surtir efecto
al instante; falta demostrarlo.

**Commit:** `El terreno de pruebas deja de producir falsos positivos`

---

## Lo que no hay que romper

De la corrida salió confirmado, y un cambio de la fase 1 lo puede tumbar sin que se note:

- Las seis rutas de escritura rebotan al Consultor a `/Cuenta/Denegado`, escritas a mano.
- Las siete pantallas abren para el operador **sin ser administrador**.
- Las siete rutas viejas `/Siger/*` redirigen conservando la query.
- Quitarle Honduras Simple al Consultor le cierra la puerta al instante y deja SIGER en pie.
- Publicar y despublicar llega al portal ciudadano (~15 s el alta, ~5 min la baja).

La fase 1 toca justamente el filtro que produce el primero de esos cinco. La prueba 3 de la
tarea 1.1 existe para eso.
