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

| Token | Valor | Uso típico |
|---|---|---|
| `--diger-blue` | `#1455a4` | Color de marca — botones primarios, enlaces, foco |
| `--diger-blue-dark` | `#0a2d6e` | Texto de énfasis (títulos, montos), degradados |
| `--diger-blue-light` | `#e8f0ff` | Fondos suaves (pills activos, badges, topnav-rol) |
| `--diger-blue-pale` | `#c8d5ee` | Bordes suaves sobre fondo azul claro |
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
| `--diger-radius-md` | `12px` | Radio usado en botones, badges, cards pequeñas |
| `--diger-radius-pill` | `20px` | Radio "pill" — badges de estado, barras de progreso |
| `--diger-shadow-sm` | `0 2px 8px rgba(10,45,110,.06)` | Sombra por defecto de cards |
| `--diger-shadow-md` | `0 6px 20px rgba(10,45,110,.12)` | Sombra de hover en `.hist-item` |
| `--diger-shadow-lg` | `0 4px 14px rgba(10,45,110,.3)` | Sombra de `.btnp` |
| `--diger-transition` | `.2s` | Referencia (la mayoría del código sigue usando `.2s`/`.22s`/`.25s` literal) |

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

- **Radios de borde sin unificar del todo.** Solo `12px` → `--diger-radius-md`
  y `20px` → `--diger-radius-pill` están tokenizados (eran los únicos
  valores repetidos 3+ veces de forma idéntica). El resto queda como
  literal por componente: `16px` (`.card`, `.doc-attach-card`), `14px`
  (`.kpi-card`, `.dash-card`), `13px` (`.hist-item`, `.grupo-inst`,
  `.seg-table` — candidato a un futuro `--diger-radius-lg`), `10px`/`9px`/
  `8px`/`7px`/`6px` (inputs y botones pequeños, varían por componente).
  Forzarlos todos a una escala de 3-4 valores es un cambio de mayor riesgo
  visual, fuera del alcance "ligero" de esta pasada.
- **`.btnp`/`.btns` (`diger.css`) vs `.btn-p`/`.btn-s` (`expediente.css`).**
  Son dos sistemas de botones distintos que componen contra bases CSS
  diferentes y **nunca se cargan juntos** en una misma página dentro del
  alcance actual. No se unificaron porque `expediente.css` está fuera de
  esta pasada — se retoma si algún día se rehace `Expedientes/Editor.cshtml`.
- **`Expedientes/Editor.cshtml` completo.** Wizard grande con 70+ estilos
  inline y su propio `expediente.css`. Requiere una revisión dedicada, no
  una pasada "ligera".
- **Sin escala de tipografía/espaciado/z-index.** No existía una convención
  previa que migrar; crear una desde cero ya no es un cambio ligero. Los
  tamaños de fuente y paddings actuales son literales por componente.
- **`.alert-error` sin centralizar.** A diferencia de `.alert-ok` (que sí
  tiene partial `_SuccessBanner`), el banner de error rojo se sigue
  repitiendo inline por página. Decisión explícita de no tocarlo en esta
  pasada.
- **Markup de validación sin unificar** (`<span class="hint">` vs
  `<p class="hint">`, `asp-validation-for` vs `asp-validation-summary`
  según la página). Sin tocar.
- **Favicon pendiente.** El logo actual (`img/logo_diger.png`) es un banner
  horizontal de 11827×2312px — no sirve para derivar un ícono legible a
  16/32px. Se retoma cuando exista un asset cuadrado dedicado.
- **ARIA mínimo.** No se hizo auditoría de accesibilidad; solo se corrigieron
  dos `alt=""` puntuales en `Reuniones/Editor.cshtml` (vistas previas de
  foto) por ser triviales y de bajo riesgo.
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

1. ¿Es un listado? Usá `.hist-header` + `.hist-item`/`.seg-table` +
   `.hist-empty` para el estado vacío, y `<partial name="_Paginacion"
   model="..."/>` si pagina.
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
7. Radios de borde: para cards/botones/badges nuevos, preferí `16px` (card
   grande), `var(--diger-radius-md)` (12px, botones/badges), o
   `var(--diger-radius-pill)` (20px, pills) según corresponda al patrón más
   cercano — no inventes un valor nuevo sin necesidad.
