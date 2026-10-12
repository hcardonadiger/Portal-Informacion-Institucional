# DESIGN.md — Sistema de diseño del portal DIGER

Este documento es la referencia de los patrones visuales ya establecidos en
`src/Web/wwwroot/css/diger.css` y `src/Web/wwwroot/css/tokens.css`. El
objetivo es que trabajo futuro (asistido por IA o no) reutilice lo que ya
existe en vez de reinventarlo por página, que es como se llegó al estado
anterior a esta limpieza.

No cubre `expediente.css` (usado solo por `Expedientes/Editor.cshtml` y
`Reuniones/Editor.cshtml`), que ya tiene su propio bloque `:root` de
variables sin prefijo (`--azul`, `--verde`, etc.) y queda fuera de esta
pasada — ver "Deuda conocida" más abajo.

## 1. Tokens (`tokens.css`)

Todas las variables usan el prefijo `--diger-` para no colisionar con las de
`expediente.css`. Se cargan antes de `diger.css` en `_Layout.cshtml`.

### 1.1 Tres capas, cada una con su regla (2026-10-12)

El defecto de origen no era ningún color concreto: era que **no existía
frontera entre color de marca y color de interfaz**. Por eso un dorado pudo
instalarse en el header invocando al Manual de Marca —en el que no aparece—
sin que nadie pudiera contradecirlo con una regla. La paleta declara tres
capas:

| Capa | Qué contiene | Regla que la gobierna |
|---|---|---|
| **Marca** | Los tres colores del manual (pág. 09) | Literales exactos, inmodificables, con la página citada. No se derivan ni se ajustan. |
| **Derivada** | Sombras y tintes para hover, degradados, pills, bordes | Tono dentro de **±8°** de un color de marca. Es una prueba que corre, no un criterio que se discute. |
| **Función** | Neutros de texto y borde; verde, ámbar y rojo de estado | Fuera del manual **a propósito**, y prohibidos en cualquier elemento que represente al logo. |

La capa función está fuera del manual porque el manual norma una identidad
gráfica, no una interfaz de datos: **un error no se puede señalar en azul**.

**Regla de acento:** celeste sobre fondo navy, azul medio sobre fondo claro.
Nunca al revés — el celeste rinde 6.52 sobre el navy del header pero cae a
1.84 sobre blanco, donde se vería lavado.

**Quien lo vigila es `scripts/verificar_marca.py`**, que falla con código de
salida 1 mientras algo incumpla. Comprueba colores prohibidos (en hex y en
`rgb()`/`rgba()`, que es la misma cosa escrita en decimal), pertenencia de
tono, contraste AA de cada par declarado, y el censo de literales. Recorre
las cuatro superficies: `wwwroot/css`, `Pages/**/*.cshtml`, `wwwroot/js` y
los `.cs` que pintan color.

### 1.2 Tabla de tokens

| Token | Valor | Uso típico |
|---|---|---|
| `--diger-blue` | `#02395e` | **Capa marca** — azul oscuro del manual (pág. 09), tono 204°. Botones primarios, enlaces, foco, títulos |
| `--diger-blue-mid` | `#1a65a3` | **Capa marca** — azul medio corporativo, tono 207°. Acento sobre fondo claro |
| `--diger-celeste` | `#7acbdd` | **Capa marca** — celeste, tono 191°. Acento sobre fondo navy (header, pie, avatar). Antes se llamaba `--diger-cyan` |
| `--diger-blue-dark` | `#022a45` | Capa derivada — sombra de `--diger-blue`, tono 204°. Degradados, hover |
| `--diger-blue-light` | `#e8f5ff` | Capa derivada — tinte. Era `#e8f0ff`, violáceo a 219° |
| `--diger-blue-pale` | `#c8ddee` | Capa derivada — borde suave. Era `#c8d5ee`, violáceo a 219° |
| `--diger-privado` | `#b45309` | Capa función — reunión privada en el calendario. **Es estado, no marca**: antes usaba el dorado y el CSS lo llamaba "institucional" |
| `--diger-border` | `#e2e8f0` | Borde por defecto de cards, inputs, tablas |
| `--diger-text` | `#1a1a1a` | Texto principal |
| `--diger-text-secondary` | `#4a5568` | Labels, texto secundario |
| `--diger-text-muted` | `#6b7fa3` | Metadatos, texto de apoyo |
| `--diger-text-faint` | `#9aadcc` | Texto deshabilitado / muy secundario |
| `--diger-bg` | `#eef1f7` | Fondo de la página |
| `--diger-bg-soft` | `#fafbfd` | Fondo de inputs |
| `--diger-success` / `--diger-success-bg` | `#15803d` / `#dcfce7` | Estados positivos |
| `--diger-warning` / `--diger-warning-bg` | `#b45309` / `#fef3c7` | Estados de alerta |
| `--diger-danger` / `--diger-danger-bg` | `#dc2626` / `#fee2e2` | Estados de error/eliminar |
| `--diger-danger-text` | `#b91c1c` | Texto sobre `--diger-danger-bg` (más oscuro que `--diger-danger`) |
| `--diger-danger-strong` | `#a32d2d` | Rojo alterno (agregado 2026-08-07) — NO es alias de `--diger-danger-text`, hex distinto a propósito, ver "Deuda conocida" |
| `--diger-warning-strong` | `#854f0b` | Ámbar alterno (agregado 2026-08-07), mismo criterio que arriba |
| `--diger-success-strong` | `#15803d` | Verde alterno. **Faltaba en `:root`** hasta 2026-08-13: solo estaba en `@media dark`, `[data-theme=dark]` y `[data-theme=light]`, así que con la preferencia del sistema en claro y sin elección explícita quedaba sin valor — el patrón que advierte la regla de abajo |
| ~~`--diger-blue-title`~~ | — | **Eliminado 2026-10-12.** Valía `#0a2d6e`, un azul violáceo a 12° del manual. Sus 78 usos pasaron a `--diger-blue` |
| ~~`--diger-blue-strong`~~ | — | **Eliminado 2026-10-12.** Valía `#0c447c`. Sus 89 usos pasaron a `--diger-blue-mid` |
| `--diger-text-strong` | `#2d3748` | Texto oscuro alterno, más suave que `--diger-text` (agregado 2026-08-07) |
| `--diger-text-slate` | `#94a3b8` | Gris-azulado para íconos/labels secundarios (agregado 2026-08-07) |
| `--diger-text-slate-dark` | `#475569` | Variante más oscura de `--diger-text-slate` (agregado 2026-08-07) |
| `--diger-bg-blue` / `-alt` / `-pale` | `#f5f8ff` / `#f8fbff` / `#f0f4ff` | Tres fondos azules muy suaves casi idénticos (agregado 2026-08-07) — candidatos a fusionarse en uno solo si alguien confirma que no hay diferencia intencional |
| `--diger-border-soft` | `#f2f5f9` | Borde/divisor muy sutil (agregado 2026-08-07) |
| `--diger-radius-sm` | `8px` | Radio de inputs y botones pequeños (agregado 2026-08-07) |
| `--diger-radius-md` | `12px` | Radio usado en botones, badges, cards pequeñas |
| `--diger-radius-lg` | `16px` | Radio de `.card` — la superficie base de casi todo el portal (agregado 2026-08-07) |
| `--diger-radius-pill` | `20px` | Radio "pill" — badges de estado, barras de progreso |
| `--diger-shadow-sm` | `0 2px 8px rgba(10,45,110,.06)` | Sombra por defecto de cards |
| `--diger-shadow-md` | `0 6px 20px rgba(10,45,110,.12)` | Sombra de hover en `.hist-item` |
| `--diger-shadow-lg` | `0 4px 14px rgba(10,45,110,.3)` | Sombra de `.btnp` |
| `--diger-transition` | `.2s` | Referencia (la mayoría del código sigue usando `.2s`/`.22s`/`.25s` literal) |
| `--diger-ring` | `0 0 0 4px rgba(20,85,164,.1)` | Anillo de foco de inputs/selects (agregado 2026-08-07) — no usar para estados activos persistentes, ver "Deuda conocida" |

**Escala de densidad `--diger-fs-*`** (agregada 2026-08-07, ver "Deuda
conocida"): `--diger-fs-2xs`/`xs`/`sm`/`base` (10.5/11/12.5/14px) — la
escala real de `diger.css` (tablas/badges), distinta de `--diger-text-*`
de abajo (esa es la del sitio público, para contenido editorial). Usalas
para cualquier texto chico nuevo (metadatos, badges, celdas de tabla).

**Escala tipográfica y de espaciado** (agregada 2026-08-07, ver sección 6):
`--diger-text-xs`→`--diger-text-5xl`, `--diger-weight-*`, `--diger-leading-*`
y `--diger-space-1`→`--diger-space-16`, mismos valores que `tokens.css` del
sitio público (diger.gob.hn). Es aditivo — el CSS existente sigue con
literales por componente hasta que se migre pieza por pieza (ver "Deuda
conocida"). Usalas en cualquier código nuevo en vez de un `px`/`rem` suelto.

Al agregar un color nuevo: si el hex ya existe como token, usá el token. Si
es realmente nuevo, agregalo a `tokens.css` con el prefijo `--diger-` antes
de usarlo suelto en `diger.css`.

## 2. Clases de componentes

| Clase | Qué es | Notas |
|---|---|---|
| `.card` | Contenedor blanco con borde, radio 16px y sombra | Base de casi toda superficie de contenido |
| `.dash-card`, `.kpi-card` | Variantes de `.card` para tableros | Radio 14px (no 16px — ver deuda) |
| `.hist-header` / `.hist-item` / `.hist-empty` | Encabezado de listado + fila + estado vacío | Patrón estándar para listas (Contactos, Reuniones, Tickets, Instituciones) |
| `.seg-table` | Tabla con header gris claro y hover de fila | Tabla estándar del portal |
| `.edit-grid` | Modificador de `.seg-table` para tablas **con controles en las celdas** | Estila `input` y `select` (que si no quedan con la apariencia por defecto del navegador, incluido fondo blanco en modo oscuro) y aplica `table-layout: fixed`. **Con layout fijo hay que dar anchos en píxeles**: `width:1%` deja de significar «tan angosta como el contenido» y se toma literal, así que los botones desbordan |
| `.wf-badge` + `.wf-registrada/.wf-asignada/.wf-enproceso/.wf-completada/.wf-cancelada` | Badge de estado de flujo (Expedientes) | |
| `.status-badge` + `.abierto/.enproceso/.resuelto/.default/.vencido` | Badge de estado de ticket | Nuevo — antes eran estilos inline con una función C# `EstadoColor` duplicada en 3 archivos |
| `.prio-badge` + `.critica/.alta/.media/.default` | Badge de prioridad de ticket | Nuevo, mismo motivo que arriba |
| `.rol-badge` + `.admin/.coordinador/.default` | Badge de rol de usuario | Nuevo, mismo motivo |
| `.btnp` / `.btns` | Botón primario (degradado azul) / secundario (outline) | Ver "`.btnp` vs `.btn-p`" en deuda conocida |
| `.field` | Wrapper de label + input con el espaciado estándar | `.field input/select/textarea` ya trae el estilo — no hace falta clase extra en el input |
| `.seg-filters` | Fila de filtros de varios campos (selects + checkboxes + búsqueda) | Usar esta para cualquier barra de filtros con 2+ campos — **no** envolver en `.card` |
| `.lista-buscar` | Fila de un solo campo de búsqueda + botón | Usar solo cuando el filtro es un único `<input search>` |
| `.pager` / `.pager-btns` / `.pager-b` | Paginación estándar | Usada vía `<partial name="_Paginacion" model="..."/>` |
| `_SuccessBanner.cshtml` (partial) | Banner verde de éxito post-redirect | `<partial name="_SuccessBanner" />` — lee `TempData["SuccessMsg"]` una sola vez; no usar si la página no sigue el patrón redirect+TempData (ver deuda) |

## 3. Deuda conocida (documentada a propósito, no resuelta en esta pasada)

- **Radios de borde consolidados (2026-08-07).** Escala de 4 tokens:
  `--diger-radius-sm` (8px, absorbe 6/7/8/9/10px — 65 declaraciones),
  `--diger-radius-md` (12px, absorbe 11/12/13/14px — 26 declaraciones),
  `--diger-radius-lg` (16px, promueve el radio de `.card` — 3 declaraciones),
  `--diger-radius-pill` (20px, absorbe 99px — 13 declaraciones). Incluye las
  variantes compuestas (`0 Xpx Xpx 0`, `Xpx Xpx 0 0`, etc.) con el mismo
  mapeo. **Deliberadamente sin tocar**: `50%` (círculos/avatares, no es un
  radio de "esquina"), `0`/`0 !important` (esquinas cuadradas intencionales),
  y 5 casos sueltos de bajo volumen (4px×2, 5px×1, 25px×1, 30px×1, un
  recorte de 2px) — muy pocos usos cada uno para justificar forzarlos a la
  escala sin revisar el componente puntual.
- **`.btnp`/`.btns` (`diger.css`) vs `.btn-p`/`.btn-s` (`expediente.css`).**
  Son dos sistemas de botones distintos que componen contra bases CSS
  diferentes y **nunca se cargan juntos** en una misma página dentro del
  alcance actual. No se unificaron porque `expediente.css` está fuera de
  esta pasada — se retoma si algún día se rehace `Expedientes/Editor.cshtml`.
- **`Expedientes/Editor.cshtml` completo.** Wizard grande con 70+ estilos
  inline y su propio `expediente.css`. Requiere una revisión dedicada, no
  una pasada "ligera".
- **`font-size` migrado (2026-08-07).** La escala `--diger-text-*` del sitio
  público no calzaba con los tamaños reales de `diger.css` (mucho más chicos
  — tablas/badges densos, no contenido editorial), así que se agregó una
  escala propia `--diger-fs-2xs/xs/sm/base` (10.5/11/12.5/14px) agrupando
  los clústeres reales de 9-14.5px; de 15px para arriba ya empalma con
  `--diger-text-base` y el resto de la escala pública. 201 de 216
  declaraciones migradas; quedan 15 casos sueltos de 1-3 usos (17/21/22/26/
  28/29/40px + varios `rem`) sin tocar por ser demasiado ambiguos/dispersos
  para forzarlos.
- **`padding` — solo el caso fácil resuelto.** De 149 declaraciones, 127 son
  compuestas (`Xpx Ypx`/`Xpx Ypx Zpx`, cada una específica de su
  componente) y no tienen clústeres limpios como los de `font-size` —
  migrarlas es trabajo de criterio por componente, no mecánico. Los 12
  casos de valor único con match exacto a `--diger-space-*` (4/8/12/16px,
  `1rem`, `1.5rem`) sí se migraron; los otros 7 sueltos (6/5/2/13/14px,
  `.85rem`, `1.1rem`) y las 127 compuestas quedan pendientes. Sin escala de
  z-index todavía.
- **`.alert-error` sin centralizar.** A diferencia de `.alert-ok` (que sí
  tiene partial `_SuccessBanner`), el banner de error rojo se sigue
  repitiendo inline por página. Decisión explícita de no tocarlo en esta
  pasada.
- **Markup de validación sin unificar** (`<span class="hint">` vs
  `<p class="hint">`, `asp-validation-for` vs `asp-validation-summary`
  según la página). Sin tocar.
- **Favicon resuelto (2026-08-07).** Isotipo propio "Escalones" (3 barras
  redondeadas en los colores de marca, generado como PNG con fondo
  transparente en `wwwroot/img/favicon-{32,180,192,512}.png`, enlazado en
  `_Layout.cshtml`) — ya no depende del banner horizontal `logo_diger.png`,
  que sigue usándose para la identidad institucional (login, modal QR).
  Mismo isotipo, en su variante de contraste sobre azul, también se agregó
  como SVG inline junto al wordmark del header.
- **Botones de ícono sin nombre accesible — resuelto (2026-08-07).** Los 22
  botones `✕` (eliminar/cerrar) que solo tenían el símbolo, sin
  `aria-label` ni `title`, repartidos en 12 páginas — incluido
  `.cwp-close` en `_Layout.cshtml`, el botón de cerrar el chat, visible en
  todo el portal — ya tienen `aria-label` descriptivo (`"Eliminar
  contacto"`, `"Cerrar chat"`, etc.). Los `.asi-del` de `Reuniones/Editor.cshtml`
  y los dos de `Expedientes/Editor.cshtml` no se tocaron porque ya tenían
  `title`/`aria-label` propio. Sigue sin haber una auditoría ARIA completa
  más allá de este patrón puntual (botones de ícono).
- **Colores sueltos — parcialmente auditado (2026-08-07).** De 119 hex
  distintos: 12 eran duplicados exactos de tokens ya definidos (25 usos
  crudos, ej. 7× `#6b7fa3` que ya era `--diger-text-muted`) — corregidos
  sin riesgo, mismo valor. Otros 11 hex repetidos 4+ veces (60 usos) no
  tenían token y se agregaron como nuevos (ver tabla de la sección 1) — a
  propósito **sin fusionarlos** con tokens de valor distinto que ya
  existían, para no arriesgar el contraste de texto sin poder verlo
  renderizado.
  - **Bloqueo levantado (2026-10-12).** Esa frase —"sin poder verlo
    renderizado"— frenó la fusión durante dos meses, y era un bloqueo
    legítimo mientras el contraste se juzgara a ojo. Ya no hace falta
    verlo: `scripts/verificar_marca.py` lo mide con la fórmula WCAG. Los
    cinco colapsos de azul se midieron antes de aplicarlos y pasan AA o
    AAA (`#0a2d6e`→`#02395e` 13.02→12.00 AAA; `#1455a4`→`#1a65a3`
    7.33→6.11; `#0c447c`→`#1a65a3` 9.84→6.11; `#1d4ed8`→`#1a65a3`
    6.70→6.11), y los 14 pares que salen de C# están en `PARES_AA`, el
    más ajustado en 5.64. **Nadie tiene que volver a frenarse por esta
    razón: si hay duda de contraste, se corre el guardián.**
  - Lo que **no** se fusionó, y tampoco por falta de medición: los ~65
    literales de la capa función (verdes, rojos, ámbares, grises, cianes
    de estado). Están a más de 8° de tono de cualquier color del manual y
    colapsarlos contra un azul de marca rompería la distinción que
    existen para hacer. Quedan documentados como capa función, que es
    distinto de quedar sin documentar.
  Quedan ~96 hex distintos sin tocar, casi todos de 1-3 usos
  — screenshots-tap final, no vale la pena forzarlos a un token para tan
  poco volumen. El sistema de "semáforo" (`.sem-rojo`/`.sem-naranja`/etc.,
  con sus propios `--sem-fondo`/`--sem-texto`/`--sem-solido` locales) ahora
  referencia `--diger-danger-strong`/`--diger-warning-strong` en el
  `--sem-texto` en vez de repetir el hex.
- **Las cuatro superficies que no son CSS (2026-10-12).** La auditoría de
  color de 2026-08-07 solo miró `wwwroot/css` y las vistas. Faltaban cuatro
  sitios que también pintan marca, con ~200 literales más, y conviene
  dejarlos nombrados para que la próxima auditoría no los vuelva a pasar
  por alto:
  - **JavaScript** (`expediente.js`, `tramites-indicadores.js`). Tres
    contextos y solo uno admite token: una cadena que arma HTML con
    `style=""` sí acepta `var()`; un **atributo de SVG** (`fill`, `stroke`)
    y la **configuración de Chart.js** no — ahí el `var()` llega como texto
    y no pinta. Para esos dos se usa `tokenColor()`, que lee el token del
    CSS con un respaldo.
  - **Modelos de UI en C#** (`CalendarioUi.cs`, `CompromisoUi.cs`).
    Devuelven `(fondo, texto)` desde C#, donde ningún token CSS llega. Sus
    14 pares están en `PARES_AA` del guardián.
  - **Reportes PDF** (`ActaPdfService`, `InformeService`,
    `ProyectoPdfService`). Salían en **Arial**: documentos oficiales que
    salen de la institución con la marca encima. Ahora embarcan Poppins
    como `EmbeddedResource` (QuestPDF Community no descarga fuentes) y lo
    cubre una prueba que lee el nombre de la fuente embebida en el PDF.
  - **Colores de rol en la base** (`Roles.Color`). Único cambio de datos;
    va por `database/2026-10-12_poner_al_dia.sql`, idempotente, porque las
    bases desplegadas se actualizan corriendo SQL a mano.
- **Bug real de contraste en modo oscuro, encontrado por el usuario y corregido (2026-08-07).** El primer intento de modo oscuro dejaba `--diger-blue`/`--diger-blue-dark`/`--diger-blue-mid` sin cambiar entre temas (criterio "la marca no cambia") — pero esos tres tokens se usan como `color:` (texto) en ~100 declaraciones repartidas por todo `diger.css` (nombres de institución, títulos, montos), no solo como fondo/borde. Navy oscuro sobre fondo ahora-oscuro daba contraste real de **1.0–2.4:1** (medido con la fórmula WCAG, no a ojo — el mínimo aceptable es 4.5:1). Capturado en vivo por el usuario con una captura de pantalla real del portal.
  - Fix: los usos de `color:` (nunca los de `background`/`border`) de esos tres tokens se re-enrutaron por sed a `--diger-blue-title`/`--diger-blue-strong` (ya con buen contraste en oscuro, 5.75–9.9:1) — `background`/`border-color` de los mismos tokens quedaron intactos, la marca sigue igual ahí.
  - Segunda vuelta de verificación (barrido de contraste con fondo heredado, no solo el del propio nodo) encontró 2 casos más: `--diger-blue-light` como fondo de badge nunca se hizo theme-aware pese a que el texto que llevaba encima sí, y `.chat-widget-panel` tenía `background:#fff` literal sin tokenizar. Ambos corregidos.
  - **Lección para la próxima vez que se toque un token "de marca" usado también como texto**: `grep -c "color:\s*var(--token)"` antes de decidir si puede quedarse constante entre temas — si el conteo no es 0, probablemente no puede.
- **Segundo bug de contraste, esta vez mío (encontrado con una captura de `Expedientes/Editor.cshtml` en oscuro, 2026-08-07).** Al agregar dark mode a `--diger-success`/`--diger-warning`/`--diger-danger` (ver punto anterior) asumí que solo se usaban como texto de badge — pero se usan MUCHO más como fondo sólido (puntos de estado, círculos de flujo `.flow-num`, `.perfil-cb`, `.dg-confirm-ok`, `.ic3`/`.ic6`, utilidades `.bg-success/.bg-warning/.bg-danger`). Al aclararlos globalmente, esos fondos sólidos se volvieron pastel con texto blanco encima — ilegible en la otra dirección.
  - **Corrección**: `--diger-success`/`-warning`/`-danger` (base) volvieron a ser constantes entre temas. Se agregó `--diger-success-strong` (nuevo, falta su par en la familia `-strong` que ya existía para warning/danger) y `--diger-danger-text` se hizo theme-aware (antes constante, con un único uso conflictivo como `background:` en el hover de `.dg-confirm-ok` que se cambió a literal `#b91c1c` para no arrastrarlo). Los usos de `color:` que sí van pareados con `-bg` (badges: `.badge-success/warning/danger`, `.tp.on-v`, `.ret-badge`, `.flow-warn-item`, `.fn-del:hover`) se re-enrutaron a los `-strong`.
  - De paso se encontró y corrigió la causa real del bug de la captura: `.bva-col.before/.after` (el comparador "Estado actual" vs "Versión propuesta") tenía fondos pálidos **sin tokenizar en absoluto** (`#fff5f5`/`#f0fdf4` literales) — se tokenizaron a `--diger-danger-bg`/`--diger-success-bg`. También el modal `#node-modal` ("Detalle de nodo de flujo", inline en `Editor.cshtml`) tenía su propio `background:#fff` y colores sueltos sin tokenizar.
  - **Lección más importante de toda la sesión**: antes de declarar un token "seguro" para hacer theme-aware por su rol textual, buscar TODOS sus usos como `background`/`border`, no asumir por el nombre. Un `grep` que solo mira `color:` da una foto incompleta.
- **Toggle de tema agregado (2026-08-07).** Botón `#themeToggleBtn` en el
  header (`.header-icon`, junto a la campana de notificaciones) — el
  `[data-theme]` que quedó "listo pero sin botón" ya tiene quien lo setee.
  Persiste en `localStorage['diger-theme']`; un script inline al principio
  de `<head>` (antes de cargar `tokens.css`/`diger.css`) lo aplica antes del
  primer paint para no parpadear claro→oscuro en cada carga. Si no hay
  preferencia guardada, sigue el `@@media (prefers-color-scheme)` del
  sistema operativo como hasta ahora.
- **Anillo de foco tokenizado (2026-08-07).** `--diger-ring` en
  `tokens.css` reemplaza 5 `box-shadow: 0 0 0 Npx rgba(20,85,164,X)`
  inconsistentes (3px/4px de spread, .1/.15 de opacidad) por un solo valor,
  y se agregó el mismo anillo a `.omini`/`.seg-upd-form`, que antes solo
  cambiaban `border-color` al enfocar sin el glow que sí tienen sus
  componentes hermanos. **No confundir con** `.step-item.active` (línea
  ~127) ni `.seg-etapa-destacada`/`@keyframes seg-etapa-pulso` — usan el
  mismo rgba de marca pero son estados persistentes (paso activo, "llegaste
  aquí por un enlace"), no de foco de teclado; se dejaron con su valor
  propio a propósito.
- **`_SuccessBanner` solo cubre el patrón `TempData["SuccessMsg"]`.** Las
  páginas `Admin/ImportarExpedientes.cshtml` y `Admin/ImportarReuniones.cshtml`
  muestran su resultado desde `Model.Resultado` (post síncrono, no
  redirect+TempData) y **no** usan el partial — tienen su propio bloque
  `@if (Model.Resultado is { } r) { ... }`. `Tableros/Tickets.cshtml` tiene
  un aviso estático (no ligado a `TempData`) y tampoco lo usa.
  `Asistencia/Registro.cshtml` es pública y su estado viene del modelo de
  la request, no de `TempData`.
- **`confirm()` de Reuniones sin migrar al helper `data-confirm`.**
  `Reuniones/Asistencia.cshtml` (un botón + un `onsubmit` a nivel de
  `<form>`) y `Reuniones/Index.cshtml` quedan con `onclick="return
  confirm(...)"` inline por decisión explícita — ese módulo se rediseñó en
  una sesión reciente y se prefirió no volver a tocarlo.

## 4. Breakpoints

| Breakpoint | Dónde | Qué ajusta |
|---|---|---|
| `max-width: 600px` | `diger.css` (bloque "MOBILE") | Colapsa `.row2`/`.row3` a 1 columna, oculta `.step-label`, `.col-hdr`, ajusta `.card`/`.container` padding |
| `max-width: 900px` | `diger.css` (bloque "TABLET", **antes** del de 600px en el archivo para que la cascada respete el más específico) | `.dash-grid`/`.kpi-grid` con columnas más angostas, `.row2`/`.row3` a 2 columnas, `.topnav-inner` con menos gap/padding |
| `max-width: 980px` | `diger.css`, específico de `.cal-layout` | Colapsa el layout de 2 columnas del Calendario a 1 — preexistente, no tocado |

## 5. Checklist — cómo agregar una página nueva

0. **Declará su permiso.** Toda página lleva `[Permission(modulo, accion)]`,
   `[AllowAnonymous]` o `[PermisoNoRequerido(razon)]`; si no, arranca con una
   advertencia en el log. Y cada botón o enlace hacia otra pantalla se muestra
   según la clave de **su destino** (`await Acceso.PuedeClaveAsync(...)`), no
   según la de la página donde vive — si no, se ve y termina en «acceso
   denegado». Ver la sección de autorización en `CLAUDE.md`.
1. ¿Es un listado? Usá `.hist-header` + `.hist-item`/`.seg-table` +
   `.hist-empty` para el estado vacío, y `<partial name="_Paginacion"
   model="..."/>` si pagina. Si las celdas llevan `input`/`select`, agregá
   `.edit-grid` **y anchos en píxeles** (ver sección 2).
2. ¿Tiene filtros? 2+ campos → `.seg-filters` (sin envolver en `.card`).
   Un solo campo de búsqueda → `.lista-buscar`.
3. ¿Redirige tras una acción con mensaje de éxito? Seteá
   `TempData["SuccessMsg"]` en el handler y agregá `<partial
   name="_SuccessBanner" />` justo después de abrir `.container` — no
   copies el bloque `@if (TempData["SuccessMsg"] is string ok) { ... }`.
4. ¿Tiene un botón de eliminar/dar de baja con confirmación? Usá
   `data-confirm="mensaje"` en el `<button type="submit">` en vez de
   `onclick="return confirm(...)"` — el listener global en `diger.js` ya lo
   intercepta (excepto en `Reuniones/*`, que sigue el patrón viejo por
   decisión explícita, ver sección 3).
5. ¿Necesita badges de color (estado, prioridad, rol)? Revisá si ya existe
   una familia (`.status-badge`, `.prio-badge`, `.rol-badge`, `.wf-badge`,
   `.hist-badge`) antes de inventar estilos inline nuevos.
6. Colores: usá los tokens de la sección 1. Si el hex no existe todavía como
   token y se va a repetir, agregalo a `tokens.css` primero.
7. Radios de borde: usá `var(--diger-radius-sm)` (8px, inputs/botones
   pequeños), `var(--diger-radius-md)` (12px, botones/badges), 
   `var(--diger-radius-lg)` (16px, card grande) o `var(--diger-radius-pill)`
   (20px, pills) según corresponda — no inventes un valor nuevo sin
   necesidad ni uses un literal si ya hay un token que calza.
