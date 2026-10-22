# Especificación: Tablero ejecutivo de Proyectos

Fecha: 19 de octubre de 2026 · Rama: `DashBoards` · Autor: diseñador de tableros (DIGER)
Estado: **aprobada por Jamil en sus decisiones de fondo** (cartera, formato, bloqueos, semáforo, ubicación). Lista para que `constructor-tableros` la implemente.
Insumos: `docs/tableros/auditoria-tableros.md` y `docs/tableros/datos-proyectos-gobierno-digital.md` (inventario, base `GestionGD_DashBoards`).

Decisiones vinculantes de Jamil que esta especificación cumple:

| # | Decisión |
|---|---|
| 1 | Cartera = **un área** (hoy GOBDIG, 37 proyectos) con el filtro `AreaIds` existente. Sin "Q3". Título dinámico: área desde la base, institución desde `InstitucionOptions`. Filtros por unidad y responsable solo si son baratos (lo son: se incluyen). |
| 2 | La imagen es referencia de diseño; los datos salen de la base tal cual. No se cargan proyectos ni se agrupan programas. |
| 3 | Bloqueos = **vigentes** (regla existente). Sección "Necesita decisión" con texto, autor y días. |
| 4 | **Web en vivo**, clic de cada proyecto a `/Tableros/Proyecto/{id}`, impresión decente con `@media print`. Sin PDF. |
| 5 | `/Tableros/Index` se convierte en índice: arriba un **resumen** del ejecutivo con enlace (ver §10 por qué resumen y no el tablero entero); abajo "Todos los tableros" con insignias. Menú Tableros: Ejecutivo / Todos los tableros / Mi tablero. Informes no se mueve. En esta entrega solo se **marcan** los tableros actuales. |
| 6 | Expedientes, SIGER y Recursos siguen vivos (auditoría corregida). |
| 7 | Barras coloreadas **por salud** con etiqueta de texto siempre, con marca del esperado. Dona **por estado**. |
| 8 | Subtítulo de cada proyecto = responsable + "último reporte hace N días" (o "sin reportes"). Sin campo nuevo. |
| Defaults | Permiso `Proyectos.Ver`; avance promedio sobre proyectos con estructura, 1 decimal, contra esperado; nombres visibles; sin auto-refresco; sin tendencia mensual (trabajo futuro); **sin migraciones ni cambios de esquema**. |

---

## 1. Objetivo, audiencia y preguntas

**Objetivo.** Que una jefatura, en sala o en su escritorio, sepa en cinco segundos cómo va la cartera de proyectos de un área, cuáles están en problemas y qué decisiones esperan, y pueda bajar con un clic al tablero de cada proyecto.

**Audiencia.** Director/a de la institución, jefe/a de área, jefes de unidad. Pantalla de escritorio y proyector como prioridad; tablet secundaria; impresión desde el navegador.

**Preguntas (en orden de lectura):**

1. ¿Cuántos proyectos tiene la cartera y en qué estado están?
2. ¿Vamos en plan? (avance real contra esperado hoy)
3. ¿Cuáles están en rojo o ámbar, y por qué?
4. ¿Qué necesita una decisión de la jefatura hoy? (bloqueos vigentes, cierres vencidos, trabajo que arrancó bloqueado)
5. ¿De cuándo son estos datos?

Todo elemento del tablero responde a una de estas cinco; si no, sobra.

---

## 2. Catálogo de indicadores

Convenciones: `hoy` = `DateOnly.FromDateTime(DateTime.UtcNow)`; `ahoraUtc` = `DateTime.UtcNow`; "abierto" = `ProyectoEstadoReglas.Abierto(estado)` (Planificado, EnEjecucion, Suspendido). Todo sale de `GetProyectosDashboardQuery` (`src/Application/Dashboards/Queries/GetProyectosDashboardQuery.cs`) llamada con `AreaIds = [área]`, más una extensión pequeña descrita en §9. Alcance institucional: lo aplican los filtros globales de `AppDbContext`; el tablero no los salta.

### 2.1 Indicadores de cabecera (5 KPI)

| # | Nombre | Definición para jefaturas | Fórmula exacta | Fuente | Meta / semáforo |
|---|---|---|---|---|---|
| K1 | **Proyectos** | Cuántos proyectos tiene la cartera | `Total` = número de filas del `Semaforo` tras los filtros (incluye cerrados y cancelados) | `ProyectosDashboardDto.Total` | Sin semáforo. Subtítulo: "N finalizados · N en proceso · N no iniciados". |
| K2 | **Finalizados** | Proyectos cerrados | `Count(Estado == Cerrado)`; porcentaje = `Round(Finalizados*100/Total)` | `Semaforo[].Estado` | Sin semáforo. Subtítulo: "N % del total". |
| K3 | **En proceso** | Proyectos en ejecución | `Count(Estado == EnEjecucion)`; porcentaje igual que K2 | `Semaforo[].Estado` | Sin semáforo propio; subtítulo: "N % del total · R en rojo" donde R = en ejecución con salud Rojo. |
| K4 | **Avance real** (con desviación contra el esperado) | Cuánto se ha hecho, y cuánto falta respecto a lo que debería estar hecho a la fecha | Número grande: `AvanceReal = Avg(AvancePct)` sobre proyectos **con estructura** (`TotalEntregables > 0`), 1 decimal. Línea 2: `Desviacion = Avg(AvancePct − AvanceEsperadoProyecto)` sobre proyectos **con línea base** (§2.3), 1 decimal. Línea 3: `AvanceRealConLineaBase = Avg(AvancePct)` y `AvanceEsperado = Avg(AvanceEsperadoProyecto)`, **ambos sobre la misma subpoblación con línea base**, 1 decimal (Revisión 1, R1). | `Semaforo[].AvancePct`, `Semaforo[].AvanceEsperado` (nuevo, §9) | Color de la línea 2: `≤ DesviacionRoja` rojo, `≤ DesviacionAmbar` ámbar, resto verde (constantes de §3). Línea 2: "−30.6 pts vs esperado". Línea 3: "13 con plan: 68.5 % real · 99.1 % esperado". Sin ninguno con línea base: línea 2 "sin línea base en la cartera", sin línea 3. Nunca se ponen lado a lado dos cifras de poblaciones distintas. |
| K5 | **Bloqueos vigentes** | Proyectos cuyo último reporte declara un bloqueo | `Bloqueos.Count` (la consulta ya deja solo el último reporte de cada proyecto, así que cuenta proyectos) | `ProyectosDashboardDto.Bloqueos` | Rojo si > 0 (`kpi-cell is-crit`), `is-zero` si 0. Subtítulo: "el más antiguo hace N días" o "ninguno". |

No se agregan más KPI. "No iniciados", "Suspendidos" y "Sin dato" se leen en la dona y en la lista.

### 2.2 Por proyecto (lista "Avance y salud por proyecto")

| Campo | Fórmula | Fuente |
|---|---|---|
| Número de orden | Posición 1..N tras ordenar (§5) | — |
| Nombre y código | `Nombre`, `Codigo` | `Semaforo[]` |
| Responsable | `Responsable ?? "sin responsable"` | `Semaforo[].Responsable` (hoy 8 vacíos en la base) |
| Último reporte | `UltimoAvance` → "último reporte hace N días" con `N = DiasSinReporte`; "hoy" si 0; null → "sin reportes" | `Semaforo[].UltimoAvance`, `DiasSinReporte` |
| Unidad | `UnidadNombre ?? UnidadId ?? "sin unidad"`; solo se muestra cuando NO hay filtro de unidad | `Semaforo[].UnidadNombre/UnidadId` |
| Avance real | `AvancePct` (entero, ya calculado y guardado; no se recalcula) | `Semaforo[].AvancePct` |
| Avance esperado | §2.3; null = sin línea base, no se pinta la marca | `Semaforo[].AvanceEsperado` |
| Desviación | `AvancePct − AvanceEsperado` (puntos, entero); null si no hay esperado | derivado |
| Sin dato | `TotalEntregables == 0` → barra vacía, texto "Sin dato · sin entregables cargados" en lugar del porcentaje | `Semaforo[].TotalEntregables` |
| Salud | §3 | `ProyectoEstadoReglas.Salud(...)` (nuevo) |
| Bloqueo vigente | `Bloqueos.Any(b => b.ProyectoId == p.ProyectoId)` | `ProyectosDashboardDto.Bloqueos` |

### 2.3 Avance esperado por proyecto (fórmula única, la misma que `/Tableros/Proyecto`)

Es la interpolación que ya hace `GetTableroProyectoQueryHandler` (`src/Application/Proyectos/Queries/TableroProyectoQuery.cs` líneas 148-164), llevada al portafolio:

```
Para cada actividad NO cancelada del proyecto con FechaInicioPlan y FechaFinPlan (ambas):
    si hoy <= ini            → 0
    si hoy >= fin            → 100
    si fin == ini            → 100
    si no                    → (hoy − ini) * 100 / (fin − ini)       [DayNumber]
AvanceEsperado = Round(promedio simple de esas actividades)          [entero]
ActividadesConFechas = cuántas entraron en el promedio
Línea base = ActividadesConFechas >= 1
```

Debe vivir como `ProyectoEstadoReglas.AvanceEsperado(IEnumerable<(DateOnly Ini, DateOnly Fin)> ventanas, DateOnly hoy) : int?` para que el tablero del proyecto y el ejecutivo digan lo mismo; que `TableroProyectoQuery` pase a llamarla es deseable pero no obligatorio en esta entrega (**obligatorio**: fórmula idéntica, y una prueba que lo demuestre con la misma entrada).

Limitación conocida (se documenta en el pie del tablero, no se oculta): el real promedia entregables (con la regla 0/50/100 para los que no tienen actividades) y el esperado promedia actividades con fechas. Es la misma asimetría que ya acepta el tablero de un proyecto.

### 2.4 Fechas de confianza

| Dato | Fórmula | Formato |
|---|---|---|
| Calculado al | `DateTime.UtcNow.ToLocalTime()` al renderizar | "19 de octubre de 2026, 14:30" |
| Último reporte de la cartera | `Max(Semaforo[].UltimoAvance)` tras filtros; null → "sin reportes" | "24 de septiembre de 2026" |
| Dato desactualizado | `(ahoraUtc − UltimoReporte).TotalDays > ProyectoEstadoReglas.DiasSinReporte` → el chip del último reporte pasa a ámbar con texto "hace N días" | — |

---

## 3. Reglas del semáforo de salud (constantes con nombre)

Se agregan a `src/Application/Dashboards/Common/ProyectoEstadoReglas.cs` (en memoria, nunca dentro de un `IQueryable`):

```csharp
public enum SaludProyecto { Rojo, Ambar, Verde, Neutro }

public sealed record SaludEvaluada(SaludProyecto Nivel, string Etiqueta, string Motivo);

/// Puntos de desviación (real − esperado) a partir de los cuales el proyecto está en rojo.
public const int DesviacionRoja  = -20;
/// Puntos de desviación a partir de los cuales está en ámbar. Entre ámbar y 0 se considera en plan.
public const int DesviacionAmbar = -5;
// DiasSinReporte = 30 ya existe y se reutiliza.

public static SaludEvaluada Salud(
    EstadoProyecto estado, DateOnly? fechaFinPlan, DateTime? ultimoAvance,
    bool bloqueoVigente, int totalEntregables, int? desviacion,
    DateOnly hoy, DateTime ahoraUtc)
```

Orden de evaluación (la primera regla que aplica gana; la etiqueta se muestra siempre junto al color):

| Paso | Condición | Nivel | Etiqueta | Motivo (texto corto bajo la etiqueta) |
|---|---|---|---|---|
| 1 | `estado == Cerrado` | Neutro | Finalizado | "cerrado el d de mes" si hay `FechaFinReal` disponible en la proyección; si no, vacío |
| 2 | `estado == Cancelado` | Neutro | Cancelado | — |
| 3 | `totalEntregables == 0` | Neutro | Sin dato | "sin entregables cargados" |
| 4 | `ProyectoEstadoReglas.Atrasado(fechaFinPlan, estado, hoy)` | Rojo | Vencido | "cierre planificado el d de mes, hace N días" |
| 5 | `bloqueoVigente` | Rojo | Bloqueado | "bloqueo en el último reporte" |
| 6 | `desviacion is not null && desviacion <= DesviacionRoja` | Rojo | Atrasado | "N pts por detrás del plan" |
| 7 | `desviacion is not null && desviacion <= DesviacionAmbar` | Ámbar | En riesgo | "N pts por detrás del plan" |
| 8 | `ProyectoEstadoReglas.SinReportar(estado, ultimoAvance, CorteSinReporte(ahoraUtc))` (solo EnEjecucion) | Ámbar | Sin reportar | "hace N días" o "nunca" |
| 9 | `estado == Suspendido` | Ámbar | Suspendido | — |
| 10 | `estado == EnEjecucion && (fechaFinPlan is null \|\| desviacion is null)` | Ámbar | Sin línea base | "sin fecha de cierre comprometida" o "sin fechas en las actividades" |
| 11 | `estado == Planificado` | Neutro | No iniciado | "inicio planificado el d de mes" o "sin fecha de inicio" |
| 12 | resto | Verde | En plan | "+N pts" / "en el plan" / "sin desviación medible" si desviación null |

Notas:
- El paso 10 es más amplio que `SinLineaBase` del tablero operativo (que solo mira `FechaFinPlan`). Es deliberado: para el ejecutivo "sin línea base" significa "no hay contra qué comparar", y eso ocurre también cuando ninguna actividad tiene fechas. Se documenta en el pie ("reglas del semáforo").
- Un proyecto Planificado con avance > 0 (hay 5 hoy) se pinta Neutro "No iniciado" con la barra en su valor: es incoherencia de datos y el tablero no la maquilla.
- La prioridad del proyecto **no** entra en la salud.

Orden de los niveles para contar y ordenar: Rojo < Ámbar < Verde < Neutro.

---

## 4. Distribución (bandas, cuadrícula y orden de lectura)

Contenedor `.container` con `max-width: 1280px`. Cuadrícula de la banda 2: `grid-template-columns: minmax(0, 2fr) minmax(300px, 1fr)`; bajo 900 px, una columna (lista primero, luego dona, luego decisiones).

```
┌──────────────────────────────────────────────────────────────────────────────────────┐
│ Proyectos de GOBIERNO DIGITAL                       [Calculado al 19 de octubre de     │
│ Seguimiento de avance, salud y decisiones · 37 proyectos · DIGER     2026, 14:30]     │
│ [Área ▾]* [Unidad ▾] [Responsable ▾] [Limpiar]   [Último reporte: 24 de septiembre]   │
│                                                   [Imprimir] [Seguimiento operativo →] │
├──────────┬──────────┬──────────────┬──────────────────────────┬──────────────────────┤
│ PROYECTOS│ FINALIZ. │ EN PROCESO   │ AVANCE REAL / ESPERADO   │ BLOQUEOS VIGENTES    │
│ 37       │ 4        │ 25           │ 53.3 % / 61.0 %          │ 8                    │
│ 4·25·8   │ 11 % tot.│ 68 % · 8 rojo│ −7.7 pts · 36 estr·20 LB │ el más antiguo 93 d  │
├──────────┴──────────┴──────────────┴─────────────┬────────────┴──────────────────────┤
│ AVANCE Y SALUD POR PROYECTO                       │ ESTADO DE LOS PROYECTOS            │
│  1  SOL - FOSOVI                      [BLOQUEADO] │   ╭───╮  ■ En proceso     25        │
│     sin responsable · último reporte hace 55 d    │   │37 │  ■ No iniciado     8        │
│     ▓▓▓▓▓▓▓░░|░░░░░░░░  33 % · esp. 48 % · −15    │   ╰───╯  ■ Finalizado      4        │
│  2  SOL - CONSUCOOP                   [BLOQUEADO] │          1 sin entregables (sin dato)│
│     Brizzio Zelaya · último reporte hace 63 d     ├────────────────────────────────────┤
│     ▓▓▓▓▓▓▓▓▓░|░░░░░░░  45 % · esp. 50 % · −5     │ NECESITA DECISIÓN (8)              │
│  …                                                │  ● SOL - FOSOVI · hace 55 d        │
│  9  SOL - SESAL                   [SIN REPORTAR]  │    "…texto del bloqueo…" — Autor   │
│     Brizzio Zelaya · sin reportes                 │  ● SOL - CONSUCOOP · hace 63 d     │
│     ▓▓▓▓▓░░░░░░░░░░░░░  25 % · sin línea base     │    "…"                              │
│  …                                                │  ● Talent Up Honduras · hace 46 d  │
│ 33  Portal de Digitalización          [FINALIZADO]│  …                                  │
│     ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓ 100 %                      │  (vencidos y arrancaron bloqueadas) │
│ 37  DIGER Sitio web - Mantenimiento     [SIN DATO]│                                     │
│     ░░░░░░░░░░░░░░░░░░  sin entregables cargados  │                                     │
├───────────────────────────────────────────────────┴────────────────────────────────────┤
│ Fuente: módulo Proyectos · Reglas del semáforo [desplegar] · Esperado: interpolación de │
│ las actividades con fechas · Trabajo futuro: tendencia mensual                           │
└──────────────────────────────────────────────────────────────────────────────────────────┘
* El selector de área solo aparece para quien alcanza nivel Institución o Global.
```

Orden de lectura: título y fecha → 5 KPI → lista (columna ancha, de rojo a neutro) → dona → decisiones → pie.

---

## 5. Componentes

### 5.1 Cabecera
- `h2`: "Proyectos de {AreaNombre}" (nombre de `Areas`, vía `GetAreasQuery(currentUser.ActiveInstitucionId)`); sin área seleccionada (solo roles de institución/global que elijan "todas"): "Proyectos de {InstitucionOptions.NombreCorto}".
- Subtítulo: "Seguimiento de avance, salud y decisiones · N proyectos · {InstitucionOptions.NombreCorto}".
- Chips a la derecha (`.ejec-chip`): "Calculado al …" y "Último reporte: …" (ámbar si desactualizado, §2.4).
- Botones: "Imprimir" (`window.print()`), "Seguimiento operativo →" (`/Tableros/Proyectos`, visible con `Proyectos.Ver`; respeta el patrón de gatear por la clave del destino).
- Filtros en `.seg-filters`, método GET, auto-submit `onchange`, enlace "Limpiar" cuando hay filtro (igual que `Tableros/Proyectos.cshtml`).

### 5.2 Tira de KPI
`<div class="kpi-strip" style="--kpi-cols:5">` con `.kpi-cell` (ya existe, lo usa `Tableros/Index`). Estructura por celda: `.kpi-cell-top` (icono del sprite + `.kpi-cell-lbl`), `.kpi-cell-num`, `.kpi-cell-sub`. Estados: K5 usa `is-crit`/`is-zero`; K4 pinta solo el número de desviación con `color: var(--diger-danger-text | --diger-warning-strong | --diger-success-strong)` y un signo "+"/"−" explícito. K1-K3 sin tono. Celdas K3 y K5 son `<a>` hacia el ancla de la lista y de decisiones respectivamente. Números con `font-variant-numeric: tabular-nums` (ya lo da la tipografía Manrope; no hace falta regla extra).

### 5.3 Lista "Avance y salud por proyecto" (componente principal)
Por qué lista y no tabla: en proyector se lee fila a fila; una tabla de 7 columnas no. Por qué barra con marca (bala) y no dos barras: la misma unidad, una sola escala, y lo que se lee es la distancia.

Fila (`<li class="ejec-fila">`, todo clicable como un solo enlace a `/Tableros/Proyecto/{id}`):

```
[ nº ] [ Nombre del proyecto ............................ ] [ ETIQUETA SALUD ]
       [ responsable · último reporte hace N días · unidad ]
       [ ▓▓▓▓▓▓▓▓░|░░░░░ ]  63 % · esp. 78 % · −15 pts
```

- Barra: `.bar-track` (16 px, existente) + `.bar-fill` con clase de salud: Rojo `c-red`, Ámbar `c-amber`, Verde `c-green`, Neutro `c-gray` (las cuatro existen en `diger.css` 4081-4095). Marca del esperado: nueva clase `.bar-mark` (§6), posicionada en `left: {AvanceEsperado}%`; se omite si no hay esperado.
- Texto a la derecha de la barra (`.ejec-cifras`): "63 % · esp. 78 % · −15 pts". Sin esperado: "63 % · sin línea base". Sin dato: "sin entregables cargados".
- Etiqueta de salud: `.salud-badge` (§6) con icono del sprite y texto (Rojo `#i-alerta`, Ámbar `#i-vencido`, Verde `#i-check`, Neutro sin icono). Bajo la etiqueta, el motivo en `.res-meta` (clase existente).
- Orden: por nivel (Rojo, Ámbar, Verde, Neutro), dentro del rojo por `Desviacion` ascendente y luego días sin reporte descendente; dentro de los demás por `Desviacion` ascendente (null al final) y nombre. Los neutros al final: Finalizados, No iniciados, Sin dato, Cancelados.
- Sin paginar. Con más de 60 filas se habilita `_TablaPaginada` (no aplica hoy: 37).
- Accesibilidad: la barra lleva `role="img"` y `aria-label="Avance 63 por ciento, esperado 78, 15 puntos por detrás del plan"`; el color nunca es la única señal (etiqueta + motivo + cifras).

### 5.4 Dona "Estado de los proyectos"
- Por estado real (`EstadoProyecto`), **no** por salud: Planificado → "No iniciado", EnEjecucion → "En proceso", Suspendido → "Suspendido", Cerrado → "Finalizado", Cancelado → "Cancelado". Segmentos con 0 se omiten. Suma = `Total` (hoy 8 + 25 + 4 = 37).
- "Sin dato" **no es un segmento**: es un atributo del avance (`TotalEntregables == 0`). Se muestra como nota bajo la leyenda: "1 sin entregables (sin dato de avance)". Así la dona cuadra con el total y con la lista.
- Implementación: `conic-gradient` en CSS con los tokens de abajo (sin Chart.js: pinta con tokens, cambia solo de tema, imprime sin canvas). Centro: número total + "proyectos". Leyenda HTML a la derecha con cuadrito de color, etiqueta y conteo (`<ul>`; la dona lleva `aria-hidden="true"`, la leyenda es el contenido accesible). Si el constructor prefiere Chart.js 4.4.4 (`doughnut`, `cutout: '70%'`, leyenda desactivada y leyenda HTML propia), los colores se leen con `tokenColor()` (`wwwroot/js/tramites-indicadores.js` línea 12) y nunca en hex.
- Tokens por segmento: Finalizado `--diger-success`; En proceso `--diger-blue-mid`; No iniciado `--diger-text-faint`; Suspendido `--diger-warning`; Cancelado `--diger-text-slate`. Todos existen y tienen valor en `.tema-oscuro`.

### 5.5 "Necesita decisión"
Lista `<ol class="ejec-decisiones">`, máximo 10 filas visibles y enlace "ver N más en seguimiento operativo →" si hay más. Tres tipos, en este orden:

| Tipo | Fuente | Fila |
|---|---|---|
| Bloqueo vigente | `Bloqueos[]` | Proyecto (enlace a `/Tableros/Proyecto/{id}`) · "hace N días" (`ahora − Fecha`) · texto del bloqueo (máx. 160 caracteres, el resto en `title`) · "— Autor" |
| Cierre vencido sin fecha nueva | `Semaforo[].Atrasado` | Proyecto · "cierre planificado el d de mes, vencido hace N días" |
| Arrancó bloqueada | `Bloqueadas[].Where(Arrancada)` | Proyecto · "«Actividad» avanza al N % pese a esperar a «Predecesora»" |

Dentro de cada tipo, los más antiguos primero. Vacío: "Ningún proyecto reporta bloqueos vigentes, cierres vencidos ni trabajo arrancado con dependencias abiertas." Icono `#i-alerta` en bloqueos, `#i-vencido` en vencidos, `#i-enlace` en arrancadas.

### 5.6 Pie
`.res-meta`: "Fuente: módulo Proyectos de {NombreCorto}. Esperado = interpolación de las actividades con fechas planificadas. Avance real = promedio de entregables. Datos filtrados por el alcance de su rol." Más un `<details>` "Reglas del semáforo" con la tabla de §3 en lenguaje de jefaturas, y la línea "Trabajo futuro: tendencia mensual del avance".

### 5.7 Formato de números y fechas (es-HN)
- Cultura `CultureInfo.GetCultureInfo("es-HN")`: decimal con punto, miles con coma (no aplica a estas magnitudes).
- Porcentajes: KPI con 1 decimal (`53.3 %`), por proyecto entero (`63 %`), con espacio fino antes de `%` en los KPI y en la lista (`&#8239;%`), sin espacio en las tablas densas de otros tableros (se respeta lo existente allí).
- Puntos: signo explícito, "−15 pts", "+4 pts", "0 pts".
- Fechas largas: `"d 'de' MMMM 'de' yyyy"` → "19 de octubre de 2026"; con hora `", HH:mm"`. Hora local del servidor (`ToLocalTime()`, como el resto de los tableros).
- Días: "hace 1 día" / "hace N días" / "hoy".

---

## 6. Tokens visuales y clases

**Tokens: no se crea ninguno.** Todo lo que el tablero pinta ya existe en `tokens.css` y tiene valor claro y oscuro:

| Uso | Token |
|---|---|
| Títulos, enlaces, número total | `--diger-blue` |
| Acento (segmento En proceso, botón primario) | `--diger-blue-mid` |
| Fondo de página / superficies / bordes | `--diger-bg`, `--diger-surface`, `--diger-border` |
| Texto, metadatos | `--diger-text`, `--diger-text-secondary`, `--diger-text-muted`, `--diger-text-faint` |
| Rojo (badge fondo/texto, número de desviación) | `--diger-danger-bg` / `--diger-danger-text` |
| Ámbar | `--diger-warning-bg` / `--diger-warning-strong` |
| Verde | `--diger-success-bg` / `--diger-success-strong` |
| Neutro | `--diger-bg-soft` / `--diger-text-muted`, borde `--diger-border` |
| Marca del esperado | `--diger-text` (oscura sobre barra clara en tema claro; clara sobre barra oscura en `.tema-oscuro`, porque el token se redefine) |
| Chip ámbar de dato desactualizado | `--diger-warning-bg` / `--diger-warning-strong` |
| Tipografía | `--diger-font-ui` (una sola familia hoy); KPI número `--diger-text-3xl`/`-4xl`, etiquetas `--diger-fs-xs`, filas `--diger-fs-base`, metadatos `--diger-fs-sm` |
| Espaciado | `--diger-space-2/3/4/6` |
| Radios y sombra | `--diger-radius-sm` (barras), `--diger-radius-md` (tarjetas), `--diger-shadow-sm` |

Los rellenos de barra `.bar-fill.c-red/c-amber/c-green/c-gray` llevan hex de capa función en `diger.css`; se reutilizan tal cual (no se tocan en esta entrega).

**Clases nuevas (en `diger.css`, sección nueva "TABLERO EJECUTIVO", todas con tokens):**

```css
/* Marca del avance esperado sobre .bar-track (position: relative en el track del ejecutivo) */
.bar-mark { position: absolute; top: -2px; bottom: -2px; width: 2px;
            background: var(--diger-text); opacity: .85; pointer-events: none; }

/* Etiqueta de salud: texto + icono, nunca solo color */
.salud-badge { display: inline-flex; align-items: center; gap: .35em;
               padding: 2px 10px; border-radius: var(--diger-radius-pill);
               font-size: var(--diger-fs-2xs); font-weight: 700; text-transform: uppercase;
               letter-spacing: .06em; border: 1px solid transparent; white-space: nowrap; }
.salud-badge.rojo   { background: var(--diger-danger-bg);  color: var(--diger-danger-text); }
.salud-badge.ambar  { background: var(--diger-warning-bg); color: var(--diger-warning-strong); }
.salud-badge.verde  { background: var(--diger-success-bg); color: var(--diger-success-strong); }
.salud-badge.neutro { background: var(--diger-bg-soft);    color: var(--diger-text-muted); border-color: var(--diger-border); }

/* Insignia del catálogo de tableros (hub), §10 */
.tablero-badge { /* misma geometría que .salud-badge */ }
.tablero-badge.nuevo    { background: var(--diger-blue-light); color: var(--diger-blue-mid); }
.tablero-badge.repetido { background: var(--diger-warning-bg); color: var(--diger-warning-strong); }
.tablero-badge.rediseno { background: var(--diger-bg-blue-pale); color: var(--diger-blue-mid); border-color: var(--diger-blue-pale); }
.tablero-badge.retirar  { background: var(--diger-danger-bg);  color: var(--diger-danger-text); }
```

Contraste: los pares `-bg`/`-text|-strong` son los mismos que ya usan `.badge-*` y están en `PARES_AA` de `scripts/verificar_marca.py`; `.tema-oscuro` los redefine (líneas 410-422 de `tokens.css`). Antes de dar por buena la entrega, **correr `scripts/verificar_marca.py`**: debe salir en 0.

Clases de maquetación propias de la página (`.ejec-*`) van en un bloque `<style>` de la página o en `diger.css` bajo la misma sección; sin hex, sin `px` donde exista token.

---

## 7. Comportamiento claro / oscuro

- La página **participa del tema** (no declara `ViewData["BodyClass"]`): hereda `tema-claro`/`tema-oscuro` del `<body>` según la cookie `diger_tema` (`_Layout.cshtml` 81-87).
- Nada se pinta con hex: en oscuro, `--diger-surface` es translúcida (deja ver la trama), `--diger-blue` pasa a ser legible como texto y los estados cambian a sus valores AA sobre `#050f1a`. Las tarjetas usan `--diger-surface`; **ningún panel flotante** en esta página (si el constructor agrega un menú o tooltip propio, usa `--diger-panel`, que es opaco en oscuro).
- La dona en `conic-gradient` toma los tokens en tiempo de pintado: cambia con el tema sin JS. Si se usa Chart.js, hay que repintar al cambiar el tema (escuchar el clic del botón de tema o releer `tokenColor()` al cargar; el cambio de tema recarga la página, así que releer al cargar basta).
- `.bar-mark` usa `--diger-text`: línea oscura sobre barra clara, línea clara sobre barra oscura.

---

## 8. Estados

| Estado | Comportamiento |
|---|---|
| Cargando | Render del lado del servidor, sin esqueleto. Meta: respuesta completa < 3 s (§9). Si en pruebas supera 1.5 s, se añade `Stopwatch` y se registra con `LoggingBehavior`. |
| Cartera vacía (área sin proyectos o filtros que no devuelven nada) | KPI en cero con `is-zero`; en lugar de la lista, `.empty-state` (existente): icono `#i-plan`, "No hay proyectos en {área} con estos filtros", enlace "Limpiar filtros" y, con `Proyectos.Crear`, "Crear proyecto →". Dona y decisiones se ocultan. |
| Sin línea base en toda la cartera | K4 muestra solo el real y el subtítulo "esperado: sin línea base en la cartera"; ninguna fila lleva marca. |
| Sin reportes en toda la cartera | Chip "Último reporte: sin reportes" en ámbar. |
| Dato desactualizado | Chip del último reporte en ámbar con "hace N días" cuando N > 30. |
| Área pedida que no existe o no es de la institución | Se descarta el parámetro y se aplica el valor por defecto (mismo patrón que `Proyectos.cshtml.cs` 75-77); nunca un tablero vacío sin explicación. |
| Error de consulta | No se captura en la página: sube a la página de error del portal (comportamiento estándar). |
| Sin permiso | `[Permission("Proyectos", AccionModulo.Ver)]` → acceso denegado estándar. El enlace del menú y del hub se pinta solo con `PuedeClaveAsync("Proyectos.Ver")`. |

---

## 9. Datos, consulta y rendimiento

### 9.1 Reutilización
- **`GetProyectosDashboardQuery`** con `AreaIds = [areaId]` y `ResponsableId` opcional. Ya trae: `Semaforo` (estado, avance, entregables, último avance, días sin reporte, atrasado, sin reportar, unidad), `Bloqueos` (vigentes), `Bloqueadas` (con `Arrancada`), `PorEstado`. Respeta el alcance global.
- **`ProyectoEstadoReglas`**: `Abierto`, `Atrasado`, `SinReportar`, `CorteSinReporte`, `DiasSinReporte`; se le agregan `AvanceEsperado`, `Salud`, `DesviacionRoja`, `DesviacionAmbar`, `SaludProyecto`.
- **`Etiquetas.Estado`** para los nombres de estado en la dona (con el mapeo Planificado → "No iniciado", EnEjecucion → "En proceso", Cerrado → "Finalizado" hecho en un helper `Etiquetas.EstadoEjecutivo`, no en la vista).
- **`GetAreasQuery`**, **`GetUnidadesQuery(AreaId)`**, **`GetUsuariosAsignablesQuery`** para los desplegables (los tres existen).
- **`InstitucionOptions`** para nombre corto y logo.

### 9.2 Extensión mínima a la consulta existente (sin tocar esquema)
En `GetProyectosDashboardQueryHandler`, tras obtener `filas`/`idsFiltrados`, una consulta adicional:

```csharp
var ventanas = await ctx.ProyectoActividades.AsNoTracking()
    .Where(a => a.FechaInicioPlan != null && a.FechaFinPlan != null && a.Estado != EstadoActividad.Cancelada)
    .Join(ctx.ProyectoEntregables.AsNoTracking(), a => a.EntregableId, e => e.Id, (a, e) => new { e.ProyectoId, Ini = a.FechaInicioPlan!.Value, Fin = a.FechaFinPlan!.Value })
    .Where(x => idsFiltrados.Contains(x.ProyectoId))      // 37 ids: cabe en un IN
    .ToListAsync(ct);
```
y en memoria, por proyecto, `AvanceEsperado = ProyectoEstadoReglas.AvanceEsperado(...)` y `ActividadesConFechas`. Se agregan a `ProyectoSemaforoDto` **al final y con valor por defecto** (`int? AvanceEsperado = null, int ActividadesConFechas = 0`), como ya hace el record con `UnidadId`/`Categoria`, para no romper a nadie. Los tableros de Institución/Área no los muestran en esta entrega.

### 9.3 Consulta nueva del ejecutivo
`src/Application/Dashboards/Queries/GetEjecutivoProyectos/GetEjecutivoProyectosQuery.cs`:

```csharp
public sealed record GetEjecutivoProyectosQuery(
    string? AreaId, string? UnidadId = null, Guid? ResponsableId = null) : IRequest<EjecutivoProyectosDto>;
```
Handler: inyecta `ISender`, envía `GetProyectosDashboardQuery(ResponsableId: q.ResponsableId, AreaIds: q.AreaId is null ? null : [q.AreaId])`, filtra en memoria por `UnidadId` (`Semaforo`, `Bloqueos`, `Bloqueadas` llevan `ProyectoId`/`UnidadId`) y arma:

```csharp
public sealed record EjecutivoProyectosDto(
    int Total, int Finalizados, int EnProceso, int NoIniciados, int Suspendidos, int Cancelados, int SinEstructura,
    int ConEstructura, decimal AvanceReal,
    int ConLineaBase, decimal? AvanceEsperado, decimal? Desviacion,
    int BloqueosVigentes, int? DiasBloqueoMasAntiguo,
    int Rojos, int Ambar, int Verdes, int Neutros,
    DateTime? UltimoReporte, DateTime CalculadoEnUtc,
    IReadOnlyList<ConteoDto> PorEstado,                       // solo segmentos > 0, etiquetas ejecutivas
    IReadOnlyList<ProyectoEjecutivoDto> Proyectos,            // ya ordenados (§5.3)
    IReadOnlyList<DecisionDto> Decisiones);                   // ya ordenadas (§5.5)

public sealed record ProyectoEjecutivoDto(
    int ProyectoId, string Codigo, string Nombre, string? Responsable, string? UnidadNombre,
    EstadoProyecto Estado, int AvancePct, int? AvanceEsperado, int? Desviacion, bool SinEstructura,
    DateTime? UltimoAvance, int? DiasSinReporte, bool BloqueoVigente,
    SaludProyecto Salud, string EtiquetaSalud, string MotivoSalud);

public enum TipoDecision { BloqueoVigente, CierreVencido, ArrancoBloqueada }
public sealed record DecisionDto(TipoDecision Tipo, int ProyectoId, string Proyecto, string Texto, string? Autor, DateTime Fecha, int Dias);
```
El redondeo de los decimales: `Math.Round(x, 1, MidpointRounding.AwayFromZero)`.

### 9.4 Página
`src/Web/Pages/Tableros/Ejecutivo.cshtml(.cs)`, `[Authorize] [Permission("Proyectos", AccionModulo.Ver, "Ver proyectos")]`. Parámetros GET: `Area`, `Unidad`, `Responsable`.
Área por defecto: (1) `currentUser.ActiveAreaId` si el rol tiene alcance Área o Unidad; (2) si no, `Tableros:EjecutivoProyectos:AreaPredeterminada` de `appsettings` (para DIGER: `"GOBDIG"`; configuración, no esquema); (3) si no existe o no es de la institución, la primera área activa por nombre; (4) "todas" solo si el usuario la elige explícitamente (`Area=`). El selector de área se muestra solo cuando `currentUser.EsGlobal || NivelAlcance is Institucion or Global` (misma regla que `_TabsProyectos`). Para alcance Área/Unidad el parámetro `Area` se ignora.

### 9.5 Rendimiento
Una pasada a Proyectos (37 filas) + 4 consultas pequeñas de la consulta existente + 1 consulta de ventanas. Meta: < 3 s en total; esperado < 300 ms con esta base. Sin caché, sin vistas ni procedimientos almacenados. Sin auto-refresco (decisión de Jamil); el chip "Calculado al" deja claro cuándo se generó. Trabajo futuro anotado: auto-refresco para proyector y foto mensual del avance para una tendencia.

### 9.6 Pruebas (en `tests/Application.Tests`, plantilla: las de `GetProyectosDashboardQuery`)
1. `AvanceEsperado`: antes de inicio = 0, después de fin = 100, a mitad de ventana = 50, ventana de un día = 100, sin fechas = null.
2. `Salud`: una prueba por paso de §3 (12 casos) y dos de precedencia (vencido + bloqueo → "Vencido"; bloqueo + desviación −30 → "Bloqueado").
3. Ejecutivo: con 3 proyectos (uno sin estructura, uno cerrado, uno en ejecución con bloqueo) los KPI y el orden salen como se espera; el filtro de unidad recorta la lista y las decisiones.
4. La fórmula del esperado del ejecutivo y la de `TableroProyectoQuery` dan el mismo resultado sobre la misma entrada.

---

## 10. Ubicación: hub en `/Tableros/Index`, menú y catálogo con insignias

### 10.1 Por qué un resumen arriba y no el tablero completo
El Inicio es el destino del login para **todos** los roles, incluidos los que no tienen `Proyectos.Ver` (técnicos de soporte, consultores). Embeber 37 filas convertiría el landing en una página de proyectos para quien no los ve; y quien sí los ve necesita el tablero en su propia ruta para proyectarlo e imprimirlo limpio. El resumen lee mejor: misma tira de 5 KPI, una línea "N necesitan decisión" y el botón.

### 10.2 Bandas del Inicio rediseñado (`Tableros/Index.cshtml`)

```
A. [Proyectos.Ver]  TABLERO EJECUTIVO DE PROYECTOS · Proyectos de GOBIERNO DIGITAL   [NUEVO]
                    kpi-strip con los 5 KPI de §2.1 (mismos componentes, misma consulta)
                    "8 asuntos necesitan decisión"                      [Abrir tablero ejecutivo →]
B. ATENCIÓN HOY     la tira de 5 kpi-cell que ya existe (tickets críticos, acuerdos vencidos,
                    cumplimiento, capacitados, resueltos), sin cambios
C. TODOS LOS TABLEROS  rejilla .exe-nav-grid (existente) con una tarjeta por tablero del catálogo,
                    cada una con título, descripción de una línea e insignia de texto+icono+token
```
Salen del Inicio el gráfico "Tickets — creados vs resueltos (12 meses)" y los tres conteos (abiertos, reuniones, reuniones del mes): ya viven en `/Tableros/Tickets` y `/Tableros/Reuniones`. Si Jamil prefiere conservarlos, van debajo de la banda C sin cambios.

La banda A se calcula solo si `PuedeClaveAsync("Proyectos.Ver")`, con `GetEjecutivoProyectosQuery` y la misma área por defecto que la página (§9.4).

### 10.3 Menú (`_Layout.cshtml`, grupo Tableros)
Entradas, en este orden: **Ejecutivo** (`/Tableros/Ejecutivo`, solo con `Proyectos.Ver`), **Todos los tableros** (`/Tableros/Index`, con `Tableros.Ver`; es la entrada que hoy se llama "Inicio"), **Mi tablero** (como hoy), **Informes** (como hoy, sin mover). El logo sigue llevando a `/Tableros/Index`. La regla de máximo seis entradas se cumple (cuatro).

### 10.4 Catálogo de tableros (`src/Web/Common/TablerosCatalogo.cs`)
Una sola lista estática que leen el hub y las cabeceras; retirar una insignia es cambiar una línea.

```csharp
public enum EstadoTablero { Vigente, Nuevo, Repetido, EnRediseno, PropuestoRetirar }

public sealed record TableroCatalogoItem(
    string Ruta, string Titulo, string Descripcion, string ClavePermiso,
    EstadoTablero Estado, string? Relacionado = null, string? Nota = null,
    Func<ICurrentUserService, bool>? Visible = null);
```

| Ruta | Título | Clave | Estado | Relacionado / nota |
|---|---|---|---|---|
| `/Tableros/Ejecutivo` | Tablero ejecutivo de proyectos | `Proyectos.Ver` | Nuevo | "Avance, salud y decisiones de la cartera" |
| `/Tableros/Proyectos` | Proyectos — seguimiento operativo | `Proyectos.Ver` | EnRediseno | "Doce indicadores, semáforo y tablas de atención; redirige al alcance del rol" |
| `/Tableros/ProyectosArea` | Proyectos de mi área | `Proyectos.Ver` | Repetido | con `/Tableros/Proyectos`; `Visible = u => u.EsJefeDeArea` |
| `/Tableros/ProyectosUnidad` | Mis proyectos | `Proyectos.Ver` | Repetido | con `/Tableros/Proyectos` (mismo predicado `DeUsuarioId`); se enlaza al operativo y no a "mi área" porque este último no es visible para todos los roles (Revisión 1, R6) |
| `/Tableros/Reuniones` | Reuniones y acuerdos | `Tableros.Ver` | EnRediseno | — |
| `/Tableros/Tickets` | Tickets de soporte | `Tableros.Ver` | EnRediseno | — |
| `/Tableros/Digitalizacion` | Digitalización | `Expedientes.Ver` | EnRediseno | — |
| `/Tableros/Tramites` | Avance de trámites | `Expedientes.Ver` | Repetido | con `/Tableros/Digitalizacion` |
| `/Tableros/Expedientes` | Expedientes | `Expedientes.Ver` | PropuestoRetirar | "usa Digitalización" |
| `/Siger/Tablero` | Observatorio SIGER | `Siger.Ver` | Vigente | "vive en el módulo SIGER" |
| `/Tableros/MiTablero` | Mi tablero | (autoservicio) | Vigente | sin insignia |

`/Tableros/Proyecto/{id}` no se lista (necesita un id); se menciona en la descripción del operativo. `/Informes` no se lista: no es un tablero y sigue en el menú.

**Insignias** (texto + icono + token, nunca solo color), con `.tablero-badge` de §6:

| Estado | Texto | Icono | Clase |
|---|---|---|---|
| Nuevo | "Nuevo" | `#i-plan` | `.nuevo` |
| Repetido | "Repetido con {Relacionado}" (el nombre enlazado) | `#i-enlace` | `.repetido` |
| EnRediseno | "Rediseño pendiente" | `#i-grafica` | `.rediseno` |
| PropuestoRetirar | "Propuesto retirar · usa {Relacionado}" | `#i-vencido` | `.retirar` |
| Vigente | sin insignia | — | — |

La **misma insignia se repite en la cabecera** de cada tablero marcado, como una línea `.res-meta` bajo el título ("Este tablero está marcado: repetido con Proyectos. Ver auditoría."), leída del catálogo por un partial `_InsigniaTablero.cshtml` que recibe la ruta. En esta entrega **no** se fusiona, rediseña ni retira ningún tablero: solo se marcan. Sin redirecciones todavía.

---

## 11. Impresión (`@media print`, en el `<style>` de la página)

- `@page { size: A4 landscape; margin: 12mm; }`.
- Se ocultan: `.main-nav`, `.app-header` (salvo el logo y el título, que se imprimen desde la propia cabecera de la página), `.seg-filters`, botones "Imprimir"/"Seguimiento operativo", el `<details>` cerrado.
- La cuadrícula pasa a una columna: KPI, lista completa, dona + leyenda, decisiones, pie. `.ejec-fila { break-inside: avoid; }`.
- `-webkit-print-color-adjust: exact; print-color-adjust: exact;` en `.bar-fill`, `.bar-mark`, `.salud-badge`, la dona y las celdas KPI con tono, para que la tinta de fondo no desaparezca. Aun si el navegador la omite, **la etiqueta de texto y las cifras bastan** para leer la salud.
- El `@media print` global ya devuelve el fondo a blanco en ambos temas (`diger.css` 7751-7767); no hay que repetirlo.
- Encabezado impreso: logo (`InstitucionOptions.Logo`, versión positiva), título, "Calculado al …", "Último reporte …", y los filtros aplicados en texto ("Área: GOBIERNO DIGITAL · Unidad: todas · Responsable: todos").

---

## 12. Pantallas

| Pantalla | Comportamiento |
|---|---|
| Escritorio ≥ 1200 px | Cuadrícula 2/3 + 1/3; KPI en una fila de 5; filas de la lista a 56-64 px de alto. |
| Proyector (1920×1080 a 2-3 m) | Mismo diseño; números de KPI con `--diger-text-4xl`; nombres de proyecto `--diger-text-lg`; los primeros 12-14 proyectos caben sin scroll, que son justamente los rojos y ámbar. Con el menú contraído (cookie `diger_menu=oculto`) se gana el ancho del cajón. |
| Tablet 768-1199 px | Una columna: KPI en 5 celdas estrechas (la tira ya usa `minmax(0,1fr)`), lista, dona, decisiones. |
| Móvil < 600 px | No es objetivo; debe degradar sin romper: KPI en 2 columnas (regla `max-width:600px` propia), barras al ancho completo. |

---

## 13. Seguridad

- Permiso: `[Permission("Proyectos", AccionModulo.Ver, "Ver proyectos")]` en la página y `PuedeClaveAsync("Proyectos.Ver")` en el enlace del menú y del hub (clave del destino).
- Alcance: lo imponen los filtros globales de `AppDbContext` (institución, área, unidad; el responsable siempre ve su proyecto). El filtro de área del tablero es de presentación: recorta, nunca amplía. Un `Area`, `Unidad` o `Responsable` que no pertenece a lo visible se descarta antes de consultar.
- Lo que **no** se muestra: `Objetivo` de los proyectos, descripciones de los reportes (solo el texto de **bloqueo**), evidencia adjunta, correos, nada de Expedientes/SIGER/Tickets vinculados. Los nombres de responsables y autores de bloqueos se muestran (decisión de Jamil); se advierte en el pie "Datos para seguimiento interno".
- No hay escritura: la página solo tiene `OnGet`.

---

## 14. Criterios de aceptación (verificables con capturas sobre `GestionGD_DashBoards`, cartera GOBDIG, sin filtros)

Valores esperados hoy, verificados contra el inventario (`datos-proyectos-gobierno-digital.md` §3):

| # | Criterio | Valor esperado |
|---|---|---|
| A1 | Título | "Proyectos de GOBIERNO DIGITAL" (nombre de `Areas`), subtítulo con "37 proyectos · DIGER" |
| A2 | K1 Proyectos | **37**, subtítulo "4 finalizados · 25 en proceso · 8 no iniciados" |
| A3 | K2 Finalizados | **4** · "11 % del total" |
| A4 | K3 En proceso | **25** · "68 % del total · R en rojo" con R ≥ 8 (los 8 con bloqueo vigente están en ejecución) |
| A5 | K4 Avance real | Número grande **53.3 %** (36 con estructura). Línea 2: desviación sobre los proyectos con línea base (hoy 13: **−30.6 pts**). Línea 3: "13 con plan: X % real · 99.1 % esperado", donde X es el promedio de `AvancePct` de esos mismos 13 y X − 99.1 ≈ −30.6 (±0.1 por redondeo). Si N = 0, línea 2 "sin línea base en la cartera" y sin línea 3. |
| A6 | K5 Bloqueos vigentes | **8**, en rojo (`is-crit`) |
| A7 | Dona | Segmentos: En proceso 25, No iniciado 8, Finalizado 4; sin Suspendido ni Cancelado; centro "37 proyectos"; nota "1 sin entregables (sin dato de avance)" |
| A8 | Rojos "Bloqueado" | PRY-2026-03, 05, 07, 12, 15, 16, 23, 26 aparecen en rojo con etiqueta "Bloqueado" (o "Vencido" si además su cierre planificado está vencido). Orden dentro del rojo: Vencido → Bloqueado → Atrasado; dentro de cada etiqueta por desviación ascendente (Revisión 1, R3). Los 8 bloqueados van inmediatamente después de los vencidos y antes de cualquier "Atrasado" por desviación. |
| A9 | Neutros | PRY-2026-17, 18, 19, 22 "Finalizado"; PRY-2026-38 "Sin dato · sin entregables cargados"; los 7 planificados restantes "No iniciado" salvo que su cierre planificado esté vencido (entonces "Vencido") o tengan desviación medible |
| A10 | Ámbar "Sin reportar" | Lleva "Sin reportar · hace N días" (o "nunca") todo proyecto en ejecución que **no cayó en una regla anterior** (vencido, bloqueado, atrasado o en riesgo por desviación) y cuyo último reporte es anterior a hoy − 30 días. Hoy: **10**. PRY-2026-34 **no** la lleva si la prueba corre antes del 22 de octubre de 2026 (reportó el 22 de septiembre). |
| A11 | Subtítulo de fila | PRY-2026-07: "sin responsable · último reporte hace N días"; PRY-2026-04: "Brizzio Zelaya · sin reportes" |
| A12 | Marca del esperado | Solo en filas con línea base; `aria-label` con real, esperado y puntos |
| A13 | Necesita decisión | Al menos 8 filas (los bloqueos vigentes) con texto, autor y "hace N días", las más antiguas primero; enlazan a `/Tableros/Proyecto/{id}` |
| A14 | Chips | "Calculado al {fecha de la captura}, HH:mm" y "Último reporte: 24 de septiembre de 2026" (en ámbar si han pasado más de 30 días) |
| A15 | Clic en fila | Lleva a `/Tableros/Proyecto/{id}` del proyecto |
| A16 | Filtros | Con `Unidad=DAI` la lista muestra 6 proyectos y los KPI se recalculan; con un `Area` inexistente se aplica la predeterminada sin error; para un rol de alcance Área no aparece el selector de área |
| A17 | Tema oscuro | Captura con cookie `diger_tema=oscuro`: ninguna cifra ilegible; badges y barras con sus valores de `.tema-oscuro`; `scripts/verificar_marca.py` sale en 0 |
| A18 | Impresión | Vista previa en Chrome, A4 horizontal: una columna, lista completa sin filas partidas, etiquetas de salud legibles aunque se desactiven los fondos |
| A19 | Hub | `/Tableros/Index` muestra arriba la banda del ejecutivo (solo con `Proyectos.Ver`) con los mismos 5 valores de A2-A6 y el botón; abajo "Todos los tableros" con las 11 entradas del catálogo e insignias de texto+icono; sin el gráfico de tickets |
| A20 | Menú | Grupo Tableros: Ejecutivo, Todos los tableros, Mi tablero, Informes; "Inicio" ya no existe como texto |
| A21 | Cabeceras marcadas | `/Tableros/ProyectosArea`, `/ProyectosUnidad`, `/Tramites`, `/Expedientes`, `/Digitalizacion`, `/Reuniones`, `/Tickets`, `/Proyectos` muestran su línea de insignia bajo el título |
| A22 | Rendimiento | Tiempo de respuesta de `/Tableros/Ejecutivo` < 3 s (esperado < 300 ms) medido en Development |
| A23 | Sin cambios de esquema | `git diff` sin archivos en `Migrations/` ni `database/`; `dotnet test` en verde con las pruebas de §9.6 |

---

## 15. Trabajo futuro (fuera de esta entrega, anotado para no perderlo)

- Tendencia mensual del avance (requiere foto mensual del `AvancePct` del portafolio: tabla nueva, migración y script).
- Auto-refresco para proyector (cada 5 min) y modo pantalla completa.
- Fusión de los tres tableros de proyectos en uno con selector de alcance; rediseño de Tickets y Reuniones; retiro de `/Tableros/Expedientes`.
- Campo corto "Resultado a la fecha" en la ficha del proyecto, si Jamil lo pide después.
- Calidad de datos que el tablero hará visible desde el primer día: 8 proyectos sin responsable, 24 sin fecha de cierre, 5 planificados con avance, 24 en ejecución sin reportar. Conviene avisar a los responsables antes del estreno.

---

## Revisión 1 — 19 de octubre de 2026

Revisado contra §14 con las capturas de 1440 px (claro, oscuro, impresión, hub) y el código: `Ejecutivo.cshtml(.cs)`, `_KpiEjecutivo.cshtml`, `Index.cshtml`, `TablerosCatalogo.cs`, `GetEjecutivoProyectosQuery.cs`, `ProyectoEstadoReglas.cs`, sección "TABLERO EJECUTIVO" de `diger.css`. Build y 991 pruebas en verde; ~0.6 s por página.

### Veredicto: **APROBAR CON CORRECCIONES**

Lo construido es fiel a la especificación y de calidad: tokens en todo (oscuro impecable, A17), dona en `conic-gradient` con leyenda accesible, barra con marca, etiquetas de salud con icono y motivo, chips de fechas en es-HN, pie con reglas, impresión con logo y filtros en texto, catálogo con insignias, menú correcto. Criterios verificados en captura: A1, A2, A3, A4 (14 en rojo ≥ 8), A6, A7, A9, A11, A12, A13, A14, A19, A20, A22. Pendientes de captura: A15, A16, A18 (vista previa real, no emulación), A21, A23 (confirmar `verificar_marca.py` en 0).

Las correcciones son de lectura ejecutiva, no de datos: dos de sentido (R1, R2), tres de orden y etiquetas (R3, R4, R5), una de navegación (R6) y ajustes menores.

### Correcciones para el constructor (en orden de importancia)

**R1. KPI "Avance real / esperado" compara poblaciones distintas (observación 1 del coordinador: válida).** Hoy: `53.3 % / 99.1 %` con "−30.6 pts". Una jefatura resta 53 − 99 = −46 y la cifra dice −30.6: el real sale de 36 proyectos y el esperado de 13. Nueva presentación de la celda K4:
- Rótulo: **AVANCE REAL**. Número grande: `53.3 %` (sobre los 36 con estructura, como hoy).
- Línea 2 (coloreada por umbral como ahora): `−30.6 pts vs esperado`.
- Línea 3 (`.kpi-cell-sub`): `13 con plan: 68.5 % real · 99.1 % esperado` (cifras de ejemplo; la real de esa subpoblación hay que calcularla).
- Cambio en `EjecutivoProyectosDto`: agregar `decimal? AvanceRealConLineaBase` = promedio de `AvancePct` **solo sobre `conLineaBase`**, 1 decimal, `AwayFromZero`. `Desviacion` sigue siendo la media de (real − esperado) sobre esa misma subpoblación, así que ahora las tres cifras de la línea 3 cuadran entre sí (real − esperado ≈ desviación, salvo redondeo).
- Si `ConLineaBase == 0`: línea 2 "sin línea base en la cartera", sin línea 3.
- Aplicar lo mismo en `_KpiEjecutivo.cshtml` (lo usa también el hub). Actualizar §2.1 K4 y A5 con esta redacción (lo hago yo al cerrar la revisión).

**R2. La lista mide ~4 600 px (observación 2: válida). Compactar a dos renglones y plegar los neutros.**
- Fila de dos renglones (`.ejec-fila`):
  - Renglón 1: `nº · Nombre · código` a la izquierda; a la derecha `[ETIQUETA]` y, en la misma línea, el motivo en `.res-meta` (hoy va debajo de la etiqueta).
  - Renglón 2: meta a la izquierda (`responsable · último reporte … · unidad`, `max-width: 42%`, `text-overflow: ellipsis`), barra en el centro (`flex: 1`, mínimo 220 px) y cifras a la derecha (`white-space: nowrap`).
  - Objetivo: ≤ 72 px de alto por fila en 1440 px. Bajo 900 px la fila vuelve a tres renglones (meta encima de la barra).
- Plegar el grupo **Neutro** (Finalizado, No iniciado, Sin dato, Cancelado) en un `<details class="ejec-neutros">` cerrado por defecto, con `summary` "Finalizados, no iniciados y sin dato (12) — mostrar". Rojo, ámbar y verde siempre visibles. En `@media print` el `details` se fuerza abierto (`.ejec-neutros { display:block } .ejec-neutros > summary { display:none } .ejec-neutros[open], .ejec-neutros { open }` — en la práctica: renderizar el `<details>` con `open` cuando `Request.Query["imprimir"]` o, más simple, agregar `open` siempre y cerrarlo con JS al cargar en pantalla; sin JS queda abierto, que es el modo de fallo seguro).
- Resultado esperado hoy: 25 filas visibles ≈ 1 800 px.

**R3. Orden dentro del rojo (observación 5b: válida; decido).** Lo que exige decisión va primero: dentro de Rojo ordenar por etiqueta **Vencido (0) → Bloqueado (1) → Atrasado (2)**, luego `Desviacion` ascendente (null al final), luego días sin reporte descendente, luego nombre. Implementar con un `OrdenRojo(etiqueta)` en el handler, análogo a `OrdenNeutro`. A8 se reescribe: "los 8 con bloqueo vigente aparecen inmediatamente después de los Vencidos, antes de cualquier Atrasado por desviación".

**R4. Etiqueta para Planificado con desviación roja.** Receta Digital, Papito y SNV son Planificado y salen "Atrasado · 100 pts por detrás". Es correcto (debían haber arrancado), pero la jefatura necesita saber que además siguen "planificados". En `Salud`, paso 6, cuando `estado == Planificado` el motivo es `"N pts por detrás del plan · sigue en planificado"`. Misma etiqueta "Atrasado", mismo rojo; no se crea un nivel nuevo. (Observación 3: confirmo que las reglas producen lo pedido y que no hay doble castigo: cada proyecto recibe una sola regla; los 19 rojos son 8 bloqueados + 6 vencidos + 5 atrasados por desviación, y los 14 ámbar son 1 en riesgo + 10 sin reportar + 3 sin línea base. Es la realidad de la base, no un defecto.)

**R5. Criterio A10 (observación 5a: válida).** Redacción nueva: "Lleva 'Sin reportar' todo proyecto en ejecución que no cayó en una regla anterior (vencido, bloqueado, atrasado o en riesgo por desviación) y cuyo último reporte es anterior a hoy − 30 días. Hoy: 10. PRY-2026-34 no la lleva antes del 22 de octubre de 2026."

**R6. Insignia de "Mis proyectos" apunta a un tablero que el admin no ve (observación 6: válida).** `Relacionado` de `/Tableros/ProyectosUnidad` pasa a `"/Tableros/Proyectos"`: también es cierto (el operativo recorta por `DeUsuarioId` con el mismo predicado) y ese destino siempre es visible para quien tiene `Proyectos.Ver`. Texto resultante: "Repetido con Proyectos — seguimiento operativo". Actualizar la fila en §10.4.

**R7. Unidad en el subtítulo de cada fila.** "DESARROLLO DE PROYECTOS DE TRANSFORMACIÓN" ocupa media línea y en mayúsculas pesa más que el nombre del proyecto. Usar el nombre corto: agregar `string? UnidadNombreCorto = null` al final de `ProyectoSemaforoDto` (resuelto en `GetProyectosDashboardQuery` desde `Unidades.NombreCorto`, mismo diccionario que ya arma los nombres) y en la fila mostrar `UnidadNombreCorto ?? UnidadNombre ?? UnidadId`. Hoy daría "DPD", "DAI", "DITRA". Sin cambio de esquema.

**R8. Texto de los bloqueos en "Necesita decisión" (observación 4).** En la captura el texto llega al borde interior de la tarjeta pero no desborda: es el `padding` de `.dash-card` (1.4 rem). No hay defecto. Dos ajustes de legibilidad: (a) `word-break: break-word` en `.ejec-dec-tx` por si llega un texto sin espacios; (b) bajar el recorte de 160 a 140 caracteres, que en la columna de 1/3 son tres líneas y no cuatro. El autor va en línea aparte (`display:block`) para que no quede pegado al "…".

**R9. Hub: la banda "Indicadores operativos" (gráfico de tickets 12 meses y tres conteos) se conservó debajo del catálogo.** La especificación decía quitarla salvo que Jamil la quiera. Como está debajo de todo y no estorba la lectura, **se acepta**; queda registrado para que Jamil decida si se retira. No requiere cambio.

**R10. Impresión.** La emulación muestra bien logo, filtros en texto y colores. Falta: (a) con R2 el `details` de neutros debe salir abierto; (b) `.ejec-grid` en bloque deja la dona y las decisiones después de 37 filas: poner la columna lateral **antes** de la lista en impresión (`.ejec-lateral { order: -1 }` con `display:flex; flex-direction:column` en `.ejec-grid` dentro de `@media print`), para que la primera hoja tenga KPI + dona + decisiones y las siguientes la lista; (c) entregar una captura de la **vista previa real** de Chrome (A18), no de la emulación.

**R11. Capturas pendientes para cerrar la revisión:** A15 (clic en fila), A16 (con `Unidad=DAI` → 6 proyectos; `Area=NOEXISTE` → predeterminada; sesión `jefe.area@` sin selector de área), A21 (cabeceras de los ocho tableros marcados con su línea de insignia), A23 (salida de `scripts/verificar_marca.py`).

### Lo que no cambia
Reglas y umbrales de §3; fórmula del esperado; dona por estado con "Sin dato" como nota; sección "Necesita decisión" (tipos, orden por antigüedad, tope de 10); chips; menú; catálogo (salvo R6); tokens (ninguno nuevo, como se pidió).

Al recibir R1-R10 y las capturas de R11, hago la Revisión 2 y, si cuadra, doy el visto bueno final para Jamil.
