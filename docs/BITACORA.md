# Bitácora — Gestión Digital (DIGER)

Registro de trabajo del proyecto. Es la fuente para los informes a jefaturas:
se escribe en lenguaje claro y **sin datos sensibles** (ni contraseñas, ni cadenas
de conexión, ni datos personales de ciudadanos).

---

## Registro por fecha

### 2026-10-26 — Un solo script para poner al día la base de producción
Pedido de Jamil. Rama `Jamil-GestionGD`, adelantada a `origin/ike_opt` (`b062f5d`), que ya tenía
todo lo de esta rama y además 62 commits de Henry.
- **Producción tiene 74 migraciones** (respaldo `GestionGD_021026`, copia del 2026-10-26); la
  última es la del 2026-09-23. El código trae **18 más**. Todas estaban cubiertas por scripts que
  ya existían, del 2026-10-02 al 2026-10-23.
- **Nuevo:** `database/2026-10-26_poner_al_dia.sql` reúne esos nueve scripts en orden, sin
  reescribirlos. Los SIGER 11 a 15, que se encadenaban con `:r`, van en línea, así que no hace falta
  modo SQLCMD. Tiene una guarda inicial y una comprobación final con 55 controles.
- **Probado** sobre una copia local de ese respaldo:
  - primera corrida sin errores y 55 de 55 «presente»;
  - el esquema queda igual al de una base creada con las migraciones de `ike_opt`, salvo las tres
    columnas sin migración que producción ya tenía;
  - la segunda corrida no cambió ningún dato en las 101 tablas;
  - EF no ve migraciones pendientes ni cambios de modelo sin migración.
- **Paquete de despliegue:** `docs/despliegue/2026-10-26_produccion_poner_al_dia.md`.
  `database/DESPLIEGUE.md` ahora empieza por el script único.
- **Bases locales nuevas:** `GestionGD_Prod_20261026` (copia de producción ya actualizada, con
  datos reales; solo para pruebas locales) y `GestionGD_Ref_ikeopt` (vacía, creada con las
  migraciones).
- **Revisión visual del portal** (encargo del Analista): en pausa desde el 2026-10-23 por pedido
  de Jamil. La primera pasada está medida; faltan la segunda pasada, los datos ficticios para las
  listas largas y la revisión de `diger-ui`.

### 2026-10-22 — Panel de control → Notificaciones (entrega 1)
Rama `panel-notificaciones`. Especificación:
`docs/superpowers/specs/2026-10-22-panel-control-notificaciones-design.md`. Plan:
`docs/superpowers/plans/2026-10-22-panel-control-notificaciones-entrega1.md`.
- **Panel de control nuevo** (`/PanelControl`), con su primer submódulo, **Notificaciones**
  (`/PanelControl/Notificaciones`), y su grupo en el menú. Lo que antes estaba en
  `appsettings.json` se cambia desde ahí, sin reiniciar. Commit `29b53db`.
- **Revisión automática:** cada cuánto revisa el sistema, en horas y minutos (de 5 min a 24 h).
  Arranca en 4 horas. El revisor lee el intervalo en cada vuelta y se despierta al guardar.
  Commit `21ac0f6`.
- **Interruptores por módulo** (Tickets, Reuniones, Expedientes, Proyectos, Soporte): Campana,
  Correo y Agrupar. El correo arranca apagado en los cinco. Los recordatorios manuales respetan el
  interruptor de correo de su módulo (commit `91538fd`). Cada tipo de aviso pertenece a un módulo
  (commit `2c5a63b`).
- **Una sola puerta para los avisos:** agrupación y bandeja de correo, un correo por persona por
  revisión como máximo (commits `d358f8e` y `29bbb59`). El detalle de un aviso agrupado se ve en
  `/Notificaciones/{id}` (commit `9c92cf0`).
- **Correo saliente desde el panel:** servidor, usuario y remitente en la base, con la contraseña
  cifrada con Data Protection (commit `ed1f1b3`). Si la configuración no se puede leer, el envío
  falla sin romper la página (commit `fd422a0`). La primera vez que se guarda hay que escribir la
  contraseña o marcar «Quitar la contraseña». Cada regla del validador tiene su mensaje en español
  (commit `5cc9e1a`).
- **Bitácora del panel:** quién cambió qué queda en `PanelControlBitacora`.
- **Permiso** `PanelControl.Notificaciones` (Ver / Editar). Consultas y comandos en el commit
  `f0ae64f`.
- **Base de datos:** migración `20261022223203_PanelControlNotificaciones` y script
  `database/2026-10-22_panel_control_notificaciones.sql`, que va después de
  `2026-10-22_poner_al_dia.sql` (commits `85bdc68` y `86203cd`). Tablas nuevas:
  `AjustesNotificaciones`, `ModulosNotificacion`, `NotificacionElementos`, `CorreosPendientes` y
  `PanelControlBitacora`. Corrido dos veces sobre la base local: la segunda vuelta dice «ya estaba»
  en todo y los controles dicen `presente`.
- Notas de despliegue: `docs/despliegue/2026-10-22_panel_control_notificaciones.md`. Hay que
  conservar la carpeta de claves de Data Protection entre despliegues.
- Resultado: compila sin advertencias nuevas y pasan 1443 pruebas (1 omitida). Antes eran 1380.
- Revisado en la app real con un administrador: la pantalla abre, muestra los tres bloques y el
  correo apagado, y el menú tiene «Panel de control». No se guardó nada en la base local.
- **Sigue la entrega 2: las reglas de correo.**
- Pendiente aparte: cambiar la contraseña de Gmail versionada en `appsettings.json` desde
  `0462b14` y escribir la nueva en el panel.

### 2026-10-22 (noche, 6) — Menú lateral contraíble y encabezado a la orilla
Pedido del Analista. Rama `Jamil-GestionGD`, con `ike_opt` ya al día (no traía nada nuevo). Solo
el layout compartido (`_Layout.cshtml`, `diger.css` y `diger.js`). Sin cambiar los elementos, los
nombres ni el orden del menú, y sin tocar `src/Web/Mcp`.
- **Secciones contraíbles:**
  - todas nacen contraídas; el servidor abre la sección de la página, así que no hay un parpadeo
    con el menú cerrado;
  - un clic abre o cierra, y abrir una cierra la otra;
  - en las páginas sin sección (Ayuda, perfil) se abre la última que eligió el usuario, guardada en
    el navegador (`localStorage`, protegido con `try/catch`); sin dato, o con el almacenamiento
    bloqueado, todo queda contraído y el menú sigue funcionando;
  - el título es un botón con `aria-expanded` y `aria-controls`, y ese atributo es el que contrae
    en el CSS: lo que se ve y lo que oye un lector de pantalla no pueden separarse;
  - Escape, un clic fuera u ocultar el cajón ya no contraen la sección (venía de los desplegables
    de la barra horizontal que el cajón reemplazó el 2026-10-13).
- **Encabezado:** sin ancho máximo; la hamburguesa y el logo quedan en x=12 a cualquier ancho
  (antes, a 1920 px, en x≈225), en la misma vertical que los títulos del menú.
- **375 px:** la cabecera ya no se sale del ancho; el logo se reduce en vez de empujar al usuario, y
  el panel de notificaciones se ancla a la cabecera.
- **`diger-ui`:** propuesta aplicada; revisión «aprobado con cambios menores», 3 de 4 aplicados (el
  cuarto, la línea guía en oscuro, es decorativa y no tiene token mejor).
- **Navegador** (claro y oscuro; 375, 768, 1366 y 1920 px, y una ventana de 420 px de alto): todos
  los criterios se comprobaron con medidas, sin espacio entre cabecera y menú en ninguna posición
  del scroll, y con el menú llegando hasta «Ayuda».
- **Visual Studio** tenía el portal abierto y bloqueaba `src/Web/bin`; no se tocó. Se compiló y se
  probó en una carpeta temporal, con el portal en los puertos 49185 a 49187.
- **Pruebas:** `MenuLateralTests` (8). Suite completa: 1455 pruebas (1 omitida), sin advertencias
  nuevas.

### 2026-10-22 (noche, 5) — Entrega 4: catálogo de insumos y ventana por actividad
Pedido del Analista. Rama `Jamil-GestionGD`, con `ike_opt` ya al día (no traía nada nuevo). Diseño
aceptado por Jamil, de la investigación de la tanda 2 de GestionDigital (sección 5). Sin tocar
`src/Web/Mcp`, «Recursos y plantillas», las plantillas de proyecto ni el responsable del proyecto.
- **Catálogo:** dos páginas nuevas en Catálogos, «Categorías de insumo» e «Insumos», con el patrón
  de Categorías de proyecto. Solo para el Administrador: clave nueva `Insumos.Catalogo.Editar`, que
  no se le otorga a ningún rol. No se borra nada: insumos y categorías se desactivan. El nombre del
  insumo no se repite dentro de su categoría. La página de insumos filtra por texto y categoría y
  dice en cuántas actividades está cada uno.
- **Categorías iniciales:** Materiales, Equipo y maquinaria, Herramientas, Equipo informático,
  Servicios y Otros. Se siembran solo si la tabla está vacía, para que una renombrada no vuelva a
  aparecer.
- **En la actividad:** botón «+ insumos…» bajo cada actividad, con una ventana de casillas
  agrupadas por categoría y un buscador, como la de dependencias. Sin cantidad, costo ni
  inventario. Los insumos viajan con el guardado de la estructura del proyecto.
- **Regla en el servidor:** solo se valida lo que cambia. Un insumo nuevo tiene que estar activo y
  en una categoría activa. Uno ya asignado que después se desactivó se conserva, y la ficha lo
  marca «desactivado».
- **Alcance:** la tabla de asignación no tiene acceso propio: se llega a ella solo por el
  proyecto, que ya filtra por alcance. Un guardado armado a mano contra un proyecto fuera de
  alcance responde «no encontrado», y una actividad de otro proyecto colada en el formulario se
  ignora.
- **Herramienta MCP:** no cambia. La de estructura no manda insumos, y eso significa «no tocarlos».
- **Ficha y PDF:** una línea «Insumos: …» bajo cada actividad que tenga.
- **Base de datos:** migración `20261022203532_AgregarCatalogoDeInsumos`, script
  `database/2026-10-22_catalogo_insumos.sql` y reversión `database/2026-10-22_revertir_insumos.sql`.
  Probados en `GestionGD_Jamil`:
  - la migración se instaló, se revirtió con su `Down` y se volvió a instalar;
  - el script corrido sobre la base migrada no cambió nada, y una segunda vez tampoco;
  - la reversión retiró todo, y una segunda vez no hizo nada;
  - el script instaló todo desde cero y una segunda corrida no cambió nada;
  - una categoría renombrada no se volvió a sembrar.
- **Ayuda:** sección de insumos en Proyectos y en la guía de administración.
- **Pruebas:**
  - de dominio, la regla de la actividad;
  - de comandos, el catálogo, la asignación y el alcance;
  - de página, solo el Administrador, la ventana, lo conservado y el alcance;
  - y la línea del PDF.
- **Interfaz compartida, corregida de paso:**
  - las ventanas de dependencias y de insumos se abrían pegadas a la esquina superior izquierda
    por un reinicio global de márgenes; ahora se centran;
  - los avisos `.hint` que la página esconde salían siempre, porque `display:flex` le ganaba al
    atributo `hidden`. Entre ellos, «Cambió el orden» de la estructura, que aparecía al cargar.
- **`diger-ui`:**
  - Propuesta: aplicada.
  - Revisión: aprobado con 6 cambios menores, todos aplicados:
    - espaciado de la ventana;
    - rótulos de grupo del catálogo;
    - el foco al quitar un insumo;
    - y el orden de tres reglas del CSS.
  - Fuera de alcance, señaló:
    - el mismo problema de foco al quitar una dependencia;
    - el contraste del texto atenuado en tema claro (4.05:1, por debajo de 4.5), en todo el portal;
    - dos `radius-pill` que la doctrina no admite;
    - que su doctrina todavía dice que no hay tema oscuro.
- **Revisión en el navegador** (`GestionGD_Jamil`, correo apagado, datos ficticios):
  - catálogo, ventana y ficha en tema claro, oscuro y a 375 px;
  - el foco entra al buscador y vuelve al botón;
  - un insumo desactivado deja de ofrecerse y se conserva marcado;
  - el PDF real trae la línea «Insumos: …».
- Resultado: compila sin advertencias nuevas y pasan 1447 pruebas (1 omitida).

### 2026-10-22 (noche, 4) — Tanda 1, entrega 3: responsable de los riesgos
Pedido del Analista. Rama `Jamil-GestionGD`, con `ike_opt` ya al día (no traía nada nuevo).
Referencia: commit `cd6c912` de GestionDigital. Sin migraciones ni cambios en `src/Web/Mcp`, y
sin tocar el responsable del proyecto.
- **Servidor** (commit `4d5955b`): registrar y actualizar un riesgo aceptan como responsable solo
  a un interesado del proyecto o a su responsable, con la misma regla que entregables y
  actividades. Solo se valida lo que cambia: un riesgo antiguo con otro responsable se sigue
  editando sin tocarlo. Antes bastaba con cambiar el valor del selector en la petición.
- **Selector** (commits `73eb136` y `a4c6311`): ofrece los interesados más el responsable del
  proyecto, sin duplicados. Si no hay nadie, avisa que hay que registrar interesados. En la tabla,
  un responsable guardado que ya no es elegible se marca «fuera de los interesados». Propuesta y
  revisión de `diger-ui`: aprobado con cambios menores, ya aplicados.
- **Ayuda:** explica de quién puede ser responsable un riesgo.
- **Herramienta MCP `registrar_riesgo`:** cambia lo que hace. Antes aceptaba cualquier
  `responsableId`; ahora rechaza a quien no sea del equipo del proyecto. No se corrigió: solo se
  reporta.
- Resultado: compila sin advertencias nuevas y pasan 1392 pruebas (1 omitida).
- **Base local nueva `GestionGD_Jamil`** en `SQL2025`: creada con las migraciones más
  `database/2026-09-24_poner_al_dia.sql`, que agrega las tres columnas que ninguna migración crea
  (sin ellas el acta no abre). Usuarios de `DbSeeder` y datos de prueba ficticios. Se apunta con
  User Secrets; `appsettings.Development.json` no cambió.
- **Revisión en el navegador** (con el correo apagado):
  - selector de riesgos en claro, oscuro y a 375 px;
  - advertencia del recordatorio en el acta y el compromiso en tema oscuro;
  - impresión del acta: la advertencia no se imprime;
  - editor de expedientes a 375 px: la advertencia cabe.

### 2026-10-22 (noche, 3) — Entregas 1 y 2 integradas en `ike_opt`
Pedido del Analista. Entrega 2 aceptada.
- `Jamil-GestionGD` se subió a origin. `ike_opt` traía 70 commits nuevos de origin (entre ellos la
  rama Jamil y la rama DashBoards); se puso al día por avance rápido.
- Merge `7a6ecb8` (sin rebase ni force), subido a origin. Hubo un solo conflicto, mecánico, en
  `Infrastructure/DependencyInjection.cs`: `ike_opt` registró `SolOptions` y nosotros
  `CorreoOptions` en el mismo lugar. Quedaron las dos. `src/Web/Mcp` no cambió con el merge.
- En `ike_opt`: compila con las 3 advertencias de siempre y pasan 1380 pruebas (1 omitida).
- `ike_opt` se trajo de vuelta a `Jamil-GestionGD`; las dos ramas quedan con el mismo contenido.
- Revisión en el navegador con la base `GestionGD_DashBoards` (la eligió Jamil; `GestionGD_new`
  ya no existe en la instancia). El portal corrió solo en la sesión, sin tocar appsettings y con
  el correo apagado. Resultado en Riesgos y bloqueos.

### 2026-10-22 (noche, 2) — Tanda 1, entrega 2: notificaciones y correo
Pedido del Analista. Rama `Jamil-GestionGD`. Cada defecto tiene una prueba que fallaba antes. Sin
migraciones, sin tocar `src/Web/Mcp` ni el servidor, el usuario o la contraseña del correo.
- **Hora de Honduras:** los recordatorios automáticos (reunión de mañana, compromisos vencidos y
  próximos), su deduplicación diaria y el cronograma de la ficha del proyecto usaban la fecha UTC.
  Desde las 6:00 p. m. daban por vencido lo que vence ese día. Ahora usan
  `Notificaciones:ZonaHoraria` (`America/Tegucigalpa`; UTC-6 fijo con advertencia si el servidor
  no la reconoce). Commit `044e5b7`. Las etapas de cronograma de Expedientes van aparte, en el
  commit `4209a94`.
- **Enlaces absolutos y nombre de la institución:** los enlaces de los recordatorios se arman con
  `Correo:UrlBase`. Los seis asuntos y la firma de la recuperación toman el nombre de la sección
  `Institucion`. Commit `7f0e155`.
- **Tiempo máximo de envío:** 15 s, configurable con `Smtp:TimeoutSegundos`. Commit `bed99dd`.
- **La pantalla dice si el correo no salió:** el correo y los recordatorios devuelven lo que pasó
  (commit `d1cd68c`). Las cuatro pantallas de recordatorio muestran una advertencia si la campana
  llegó y el correo no (commit `b56f26b`). Con el correo bien, cada una conserva su texto. Los
  textos los propuso y revisó el agente de interfaz `diger-ui`. Veredicto: aprobado con cambios
  menores, ya aplicados.
- **Advertencia al arrancar** si falta `Correo:UrlBase` o el servidor de correo. Commit `d43edac`.
- **Ayuda:** la pregunta frecuente de notificaciones explica los avisos automáticos con la hora de
  Honduras y qué pasa si el correo no sale.
- Notas de despliegue con los pasos de IIS (siempre activo, precarga y comprobar Application
  Initialization): `docs/despliegue/2026-10-22_tanda1_entrega2_notificaciones.md`.
- Resultado: compila sin advertencias nuevas y pasan 1035 pruebas (1 omitida). Antes eran 989.
- Pruebas existentes modificadas, solo por la firma o el tipo de retorno nuevos:
  `EnviarRecordatorioTests` (la de reunión ahora mira los correos intentados, porque el asistente no
  tiene usuario), `CorreosEscapadosTests`, `PasswordRecoveryTests` y `RecuperacionUrlBaseTests`.

### 2026-10-22 (noche) — Tanda 1, entrega 1: seguridad (S1 a S5)
Pedido del Analista. Rama `Jamil-GestionGD`. Cada falla tiene una prueba que fallaba antes de la
corrección. Sin migraciones, sin cambios de interfaz, sin tocar `src/Web/Mcp` ni la sección `Smtp`.
- **S1, correo en el registro:** el registro ya no guarda el cuerpo, los enlaces, los tokens ni
  la dirección de destino de un correo. Commit `e24c828`.
- **S2, datos sin escapar en los correos:** se escapan los nombres, títulos y mensajes en los
  recordatorios de ticket, reunión y compromiso, y en la recuperación de contraseña (commit
  `8182303`). El recordatorio de Expediente va aparte (commit `ea2538d`).
- **S3, riesgos de otro proyecto:** editar, cambiar de estado y eliminar comprueban el proyecto
  y el alcance. La herramienta MCP queda protegida por el comando. Commit `fc9aa20`.
- **S4, interesados de otro proyecto:** editar y quitar comprueban el proyecto y el alcance.
  Commit `cffa71c`.
- **S5, enlace de recuperación:** sale solo de `Correo:UrlBase`. Si falta, no se envía. El QR de
  asistencia usa esa dirección si existe. Commit `65daa70`.
- Notas de despliegue: `docs/despliegue/2026-10-22_tanda1_entrega1_seguridad.md`. Antes de
  publicar hay que configurar `Correo__UrlBase` en IIS.
- Resultado: compila sin advertencias nuevas y pasan 989 pruebas (1 omitida). Antes eran 950, así
  que hay 39 nuevas.
- Pruebas existentes modificadas, solo por la firma nueva de los comandos: `EnviarRecordatorioTests`
  y `PasswordRecoveryTests`. `PortalFactory` acepta configuración y servicios de prueba por
  instancia.

### 2026-10-22 (tarde) — Traspaso desde GestionDigital e investigación de la tanda 1
- **El mensaje de traspaso llegó a Ingeniería por error.** Iba dirigido al Analista de GestionGD.
  El plan de la tanda 1 lo define el Analista; Ingeniería solo aportó la investigación. No se
  modificó código, datos ni configuración por ese mensaje.
- Jamil decidió que los cambios de su lista se hacen en GestionGD, en la rama `Jamil-GestionGD`
  sacada de `ike_opt` (`d3bbd38`), no de `dev`. La rama `Jamil` no se toca: solo se subió a
  `origin` como respaldo, porque era un avance rápido. GestionDigital queda congelado.
- Investigación de solo lectura. Confirmadas en GestionGD, y también en `dev`, que tiene los mismos
  archivos:
  - el registro guarda el cuerpo de los correos, con el token de recuperación;
  - los correos llevan datos de las personas sin escapar, incluido el recordatorio de Expediente;
  - se pueden editar, cambiar de estado o eliminar riesgos de otro proyecto;
  - se pueden editar y quitar interesados de otro proyecto;
  - el enlace de recuperación se arma con la dirección de la petición;
  - la contraseña del correo saliente está versionada en `appsettings.json`.
- Hallazgos entregados al Analista (ver Pendiente).
- Línea base de `Jamil-GestionGD`: compila con 3 advertencias que ya existían y pasan 950 pruebas
  (1 omitida).

### 2026-10-22
- Revisión inicial del repositorio para retomar el proyecto: rama `DashBoards`,
  documentación existente, agentes de apoyo y convenciones del código.
- Línea base verificada: la solución compila y la suite completa pasa
  (997 pruebas correctas, 1 omitida que ya estaba omitida, 0 con error).
- Se crea esta bitácora.

### 2026-10-19 (rama `DashBoards`, no incluida en `Jamil-GestionGD`)
- Tablero ejecutivo de proyectos para jefaturas (`/Tableros/Ejecutivo`): indicadores
  de cartera, semáforo de salud por proyecto con etiqueta de texto, asuntos que
  requieren decisión. El Inicio de `/Tableros` ordena y marca los demás tableros
  (rediseño pendiente, repetido, propuesto para retirar). Commit `382ae85`.
- Corrección: el avatar de la cabecera no se veía en tema oscuro. Commit `6285619`.
- Corrección: el panel del usuario se transparentaba en tema oscuro. Commit `39629b3`.

---

## Hecho
- Script único para poner al día la base de producción con `ike_opt` (2026-10-26, rama
  `Jamil-GestionGD`).
- Panel de control → Notificaciones, entrega 1 (2026-10-22, rama `panel-notificaciones`).
- Tanda 1, entrega 1 (seguridad): S1 a S5 (2026-10-22, rama `Jamil-GestionGD`).
- Tanda 1, entrega 2 (notificaciones y correo) (2026-10-22, rama `Jamil-GestionGD`).
- Tanda 1, entrega 3 (responsable de los riesgos) (2026-10-22, rama `Jamil-GestionGD`).
- Entrega 4 (catálogo de insumos y ventana por actividad) (2026-10-22, rama `Jamil-GestionGD`).
- Menú lateral contraíble y encabezado a la orilla (2026-10-22, rama `Jamil-GestionGD`).
- Tablero ejecutivo de proyectos (2026-10-19, rama `DashBoards`). Sin cambios de base de datos.

## En curso
- Revisión visual de todo el portal (investigación del Analista). En pausa: primera pasada medida
  (2 temas × 3 anchos); faltan la segunda pasada (ventanas, validación, listas vacías, impresión
  del acta), los datos ficticios para listas largas y la revisión de `diger-ui`.

## Pendiente
0. **Tanda 1**, según el plan del Analista. Las entregas 1 (seguridad) y 2 (notificaciones y
   correo) están hechas; siguen las que defina el Analista. Antes de publicar hay que configurar
   `Correo__UrlBase` en IIS y seguir los pasos de IIS de la entrega 2.
0. **Panel de control → Notificaciones, entrega 2:** las reglas de correo. Antes de publicar la
   entrega 1 hay que correr su script y seguir sus notas de despliegue.
1. Decidir el siguiente paso de tableros: rediseñar los tableros marcados
   (Proyectos operativo, Reuniones, Tickets, Digitalización, Trámites, Expedientes)
   o unificar los tres tableros de Proyectos por alcance. Decide Jamil.
2. Crear `docs/ux/GUIA-UX.md` a partir de la aplicación existente
   (`DESIGN.md` y `.claude/agents/references/lengua-de-casa-diger.md` como base).

## Descartado
- 2026-10-22 — Mostrar el mensaje de éxito en el editor de expedientes. Decidió Jamil: queda como
  está.

## Para después
- (nada registrado)

## Decisiones
- 2026-10-26 — Ingeniería (script único de producción):
  - Unir sin reescribir: cada parte es el script ya probado, con su encabezado. Dos versiones del
    mismo cambio terminarían por divergir.
  - No hay script SQL de reversión. La parte SIGER borra conciliaciones huérfanas y reescribe
    modalidades, y eso no se puede deshacer con un script. La reversión es restaurar el respaldo
    tomado antes de correrlo.
  - El script sale de `2026-10-02_poner_al_dia.sql` en adelante y exige que la base haya llegado
    al 2026-09-24. No incluye `2026-10-12_recuperar_legados.sql`, que es solo para bases locales
    creadas sin los expedientes legados.
- 2026-10-22 — Ingeniería (menú lateral y encabezado):
  - Cerrar una sección borra la sección recordada: quien la contrae no la quiere abierta en la
    próxima página sin sección.
  - Si la sección de la página no se ve (sin permiso), la página cuenta como «sin sección» y se usa
    la recordada.
  - El hueco se corrige midiendo al desplazar, no volviendo pegajosa la cinta de entorno: la cinta
    seguiría quitando 22 px de alto en todas las páginas.
  - La cabecera va de orilla a orilla, con el mismo relleno lateral que el cajón (12 px).
  - Se borraron las reglas de la barra horizontal que el cajón ya pisaba (desplegable flotante,
    ajuste de 901 a 1180 px). Esta última achicaba los títulos del cajón a 12,5 px.
- 2026-10-22 — Ingeniería (entrega 4):
  - Una sola clave, `Insumos.Catalogo.Editar`, para las dos páginas: es un solo catálogo.
  - Las categorías iniciales se siembran solo si la tabla está vacía, no nombre por nombre.
  - El nombre repetido se compara sin distinguir mayúsculas, igual que el índice único en SQL
    Server.
  - Los insumos viajan con el guardado de la estructura, igual que las dependencias, y no con un
    comando propio. Así heredan la carga del proyecto con su alcance.
  - Si la estructura llega sin insumos (null), no se tocan; una lista vacía los quita todos. Por
    eso la herramienta MCP de estructura no cambia de comportamiento.
  - Un insumo de una categoría desactivada tampoco se ofrece ni se puede agregar.
  - En el PDF, los insumos desactivados salen sin marca: al lector no le sirve un estado del
    catálogo que no puede cambiar.
- 2026-10-22 — Ingeniería (entrega 2):
  - El agente de interfaz de GestionGD es `diger-ui`; aquí no existe `ux-ui`.
  - Los textos dicen «el portal» y no «esta institución», porque el servidor de correo es uno
    solo para toda la instalación.
  - No se sugiere «intentarlo de nuevo», porque cada intento vuelve a dejar el aviso en la campana.
  - Sin `Correo:UrlBase`, los enlaces de los recordatorios quedan relativos, como antes; el arranque
    lo advierte.
  - `Smtp:TimeoutSegundos` y `Notificaciones:ZonaHoraria` tienen valor por defecto en el código.
    `ZonaHoraria` además quedó escrita en `appsettings.json`.
  - El tiempo máximo de envío, si se agota, cuenta como correo fallido.
  - En los pasos de IIS se recomienda además quitar el tiempo de inactividad del grupo: si no,
    IIS lo apaga a los 20 minutos aunque esté en `AlwaysRunning`.
- 2026-10-22 — Jamil: la tanda 1 se hace en la rama `Jamil-GestionGD`, sacada de `ike_opt`, en
  esta carpeta y sin worktrees nuevos. La rama `Jamil` no se toca.
- 2026-10-22 — Ingeniería (entrega 1):
  - `IEmailService` recibe el tipo de correo (`TipoCorreo`), que es lo que queda en el registro.
    Tampoco se registra la dirección de destino, que es un dato personal.
  - En riesgos e interesados, el proyecto entra como parámetro **opcional al final** de cada
    comando. Así la herramienta MCP compila sin cambios y queda protegida igual, porque el alcance
    se exige por el proyecto del propio registro.
  - Una `Correo:UrlBase` mal escrita (sin `http`/`https`) se trata igual que una que falta. Sin
    ella tampoco se genera el token de recuperación.
  - El QR, cuando usa `Correo:UrlBase`, le quita a la ruta la carpeta virtual del sitio, porque
    la dirección configurada ya la incluye.
  - `appsettings.json` trae la clave `Correo:UrlBase` vacía, y `appsettings.Development.json` la
    trae apuntando a `https://localhost:49175`.
  - La advertencia por falta de `Correo:UrlBase` sale cuando alguien pide la recuperación, no al
    arrancar. La advertencia al arrancar queda para la entrega de notificaciones, si se quiere.
- 2026-10-19 — Jamil: Expedientes, SIGER y Recursos siguen activos; la entrega de
  tableros se limita al tablero ejecutivo.

## Bugs
- **2026-10-22 — Espacio vacío entre la cabecera y el menú lateral.**
  - Síntoma: al bajar la página quedaban 22 px vacíos entre la cabecera y el inicio del menú, con
    la página asomando por el hueco.
  - Causa raíz: encima de la cabecera está la cinta de entorno («Entorno de pruebas · base …»),
    que no es pegajosa. El menú toma su borde superior de una medida que se hacía solo al cargar
    (100 px con la cinta a la vista). Al bajar, la cabecera sube a 78 y el menú se quedaba en 100.
    El 2026-10-19 se había quitado la medida al desplazar, creyendo que la cabecera ya empezaba
    siempre en el borde superior.
  - Solución: se vuelve a medir al desplazar, una vez por cuadro y solo con el menú abierto.
  - Archivos: `wwwroot/js/diger.js` (`ajustarTopDelCajon`) y el comentario en `diger.css`.
  - Pruebas: no hay pruebas de JavaScript en la solución. Se reprodujo y se midió en el navegador:
    antes, 22 px; después, 0 px a 0, 5, 11, 22 y 200 px de scroll, en cuatro anchos.
- **2026-10-22 — S1. Token de recuperación y cuerpo del correo en el registro.**
  - Síntoma: cuando un envío fallaba o no había servidor configurado, el registro (en IIS, el
    Visor de eventos) guardaba el correo completo, con el enlace y el token.
  - Causa raíz: `SmtpEmailService` escribía el cuerpo «para desarrollo».
  - Solución: solo se registran el tipo, un identificador del envío y el error.
  - Archivos: `IEmailService.cs`, `SmtpEmailService.cs` y quienes lo llaman
    (`EnviarRecordatorioManualCommands.cs`, `SolicitarRecuperacionPasswordCommand.cs`).
  - Pruebas: `CorreoSeguridadTests`.
- **2026-10-22 — S2. Datos de las personas sin escapar en los correos.**
  - Síntoma: un título con `<b>` o un mensaje con `<script>` entraba como marcado en el correo.
  - Causa raíz: el HTML se armaba concatenando los datos tal cual.
  - Solución: `PlantillasCorreo.E` escapa solo `& < > " '` (no toca las tildes), y se aplica solo
    a los datos de las personas. La plantilla no cambia.
  - Archivos: `PlantillasCorreo.cs`, `EnviarRecordatorioManualCommands.cs` (incluido el bloque de
    Expediente, en un commit aparte) y `SolicitarRecuperacionPasswordCommand.cs`.
  - Pruebas: `CorreosEscapadosTests`.
- **2026-10-22 — S3. Riesgos de otro proyecto.**
  - Síntoma: cambiando el número del riesgo en la petición se editaba, se cambiaba de estado o se
    eliminaba un riesgo de otro proyecto, incluso de otra institución. Afectaba también a la
    herramienta MCP.
  - Causa raíz: los comandos buscaban solo por Id, y la tabla `ProyectoRiesgos` no tiene filtro
    de alcance.
  - Solución: `RiesgoEnAlcance` comprueba el alcance por medio de `Proyectos`, que sí lo tiene, y
    el proyecto que manda la ficha.
  - Archivos: `RiesgoCommands.cs` y `Proyectos/Editor.cshtml.cs`.
  - Pruebas: `RiesgosAlcanceTests` y `RiesgosAlcancePaginaYMcpTests`.
- **2026-10-22 — S4. Interesados de otro proyecto.**
  - Síntoma y causa raíz: los mismos que en S3, con la tabla `ProyectoInteresados`. Quitar a
    alguien le quitaba el acceso al proyecto.
  - Solución: `InteresadoEnAlcance`, igual que en riesgos.
  - Archivos: `InteresadoCommands.cs` y `Proyectos/Editor.cshtml.cs`.
  - Pruebas: `InteresadosAlcanceTests` e `InteresadosAlcancePaginaTests`.
- **2026-10-22 — S5. Enlace de recuperación con la dirección de la petición.**
  - Síntoma: pidiendo la recuperación de otra persona con un host propio, su correo llevaba el
    token a ese host.
  - Causa raíz: `OlvidarPassword` armaba la dirección con `Request.Scheme` y `Request.Host`, que
    los manda el cliente.
  - Solución: `CorreoOptions.UrlBase`. Sin ella no se envía nada y queda una advertencia en el
    registro. El QR de asistencia la usa si existe.
  - Archivos: `CorreoOptions.cs`, `SolicitarRecuperacionPasswordCommand.cs`,
    `OlvidarPassword.cshtml.cs`, `Reuniones/Asistencia.cshtml.cs`, `DependencyInjection.cs` y
    `appsettings*.json`.
  - Pruebas: `RecuperacionUrlBaseTests` y `EnlacesConUrlBaseTests` (recuperación y QR).
  - Revisado sin cambios: las páginas de inicio de sesión con certificado y la cookie por host
    también leen `Request.Host`, pero solo para redirigir a quien hace la petición. No arman
    enlaces para otras personas.
- 2026-10-19 — Panel del usuario transparente en tema oscuro. Causa: el panel no
  tenía fondo propio para el tema oscuro. Solución: fondo con token de panel.
- 2026-10-19 — Avatar invisible en tema oscuro. Causa: una regla más específica
  (`.user-menu-btn .side-avatar`) dejaba azul sobre azul. Solución: fondo celeste
  con texto oscuro en tema oscuro.

## Riesgos y bloqueos
- **`GestionGD_DashBoards` no está completa** (2026-10-22): tiene la última migración, pero a su
  tabla `Reuniones` le falta `Duracion` y a las fichas SIGER del expediente les faltan columnas
  (`ClaveEstable`, `EstaEnSol` y otras). Por eso el acta, el compromiso y el editor de expedientes
  dan error 500 con esa base. No se tocó (decisión de Jamil). Esta rama usa `GestionGD_Jamil`.
- **`appsettings.Development.json` apunta a `GestionGD_new`, que ya no existe en `SQL2025`.** En
  esta máquina lo tapa el User Secret de `GestionGD_Jamil`. Ese secreto vale para todas las copias
  del portal en esta máquina y este usuario, porque comparten el mismo `UserSecretsId`.
- **La herramienta MCP `registrar_riesgo` ahora rechaza a un responsable fuera del equipo del
  proyecto** (entrega 3). Antes lo aceptaba. No se tocó `src/Web/Mcp`; si hace falta ajustar su
  descripción, lo decide Jamil.
- **Detalles de interfaz que ya estaban antes de la tanda 1**, vistos el 2026-10-22:
  - a 375 px, la fila de botones de arriba del editor de expedientes y los KPI de Tableros se
    salen del ancho;
  - a 375 px, el menú de usuario de la cabecera se salía del ancho en todas las páginas (visto en
    Catálogos el 2026-10-22, entrega 4). **Corregido** el 2026-10-22 con el menú lateral;
  - el cajón del menú, cuando está oculto, sigue en el orden del tabulador: está corrido fuera de
    la pantalla, pero no inerte (visto por `diger-ui` el 2026-10-22);
  - en tema oscuro, parte del texto de la Ayuda tiene muy poco contraste (visto por `diger-ui`
    el 2026-10-22);
  - al imprimir el acta salen la franja «Entorno de pruebas», el botón del chat y una segunda
    página en blanco;
  - `diger-ui` advierte que el tema oscuro existe en esta rama aunque su doctrina lo daba por
    retirado el 2026-09-24. Volvió con `ike_opt` (trabajo de Henry).
- **Correos ya escritos en el Visor de eventos del servidor** (de antes de S1): pueden tener datos
  de personas y enlaces de recuperación ya vencidos. Necesita: decisión de Jamil sobre vaciar ese
  registro o restringir quién lo lee.
- **`00_diagnostico.sql` no conoce nada posterior al 2026-10-02** (visto el 2026-10-22, vuelto a
  ver el 2026-10-26). Sobre producción recomienda el script del 2026-10-02 y no nombra los demás.
  `database/DESPLIEGUE.md` ya manda al script único del 2026-10-26, pero el diagnóstico sigue sin
  detectar esos cambios. Necesita: ampliar el diagnóstico (tarea aparte).
- **Publicar `Jamil-GestionGD` lleva todo `ike_opt` a producción**, no solo la tanda 1. Su
  despliegue va primero (`database/DESPLIEGUE.md`).
- **Contraseña del correo saliente versionada** desde el commit `b709609` (2026-08-07). Está en
  `dev` y en varias ramas remotas, y el registro de eventos del servidor puede guardar tokens de
  recuperación. Necesita: que la jefatura la cambie, y la decisión de Jamil sobre limpiar el
  historial.
- **Documentos de Expedientes sin descarga.** La subida guarda una dirección `/uploads/...` que
  ya no se sirve desde el 2026-08-26, así que el enlace probablemente no abre. No está reproducido
  todavía.
- **Respaldo de base de datos en el historial del repositorio** (`TramitesEstado.bak`,
  13 MB, versionado desde el commit `2d8fa08`). Puede contener datos reales. `ike_opt` ya lo
  quitó del árbol (llegó con la rama Jamil), pero sigue en el historial. Borrarlo de ahí
  implicaría reescribirlo, cosa que no se hace sin aprobación.
- **`appsettings.json` del Web apunta a una base que ya no existe en local**
  (`GestionGD_TEST`). Cualquier arranque fuera de `Development` falla en la primera
  consulta. El valor real de producción se configura en el servidor.
- **Dos migraciones inertes** (`20260719000000_AddPreregistroAsistente`,
  `20260719100000_AddInstitucionBranding`): una base creada desde cero con las
  migraciones quedaría sin tres columnas que el modelo usa. Las bases en uso sí
  las tienen.
- **El host `Presentation`** sigue apuntando a bases `DigerTramitesEstado*` que no
  corresponden a las del Web.
