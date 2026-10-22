# Auditoría de tableros y propuesta del tablero ejecutivo de proyectos

Fecha: 19 de octubre de 2026 · Rama: `DashBoards` · Autor: diseñador de tableros (DIGER)
Estado: **propuesta, pendiente de decisión de Jamil**. No se tocó código.

Leyenda de veredictos (una sola por tablero):

| Veredicto | Significado |
|---|---|
| **MANTENER** | Se queda tal cual; a lo sumo ajustes de tokens. |
| **REPETIDO** | Responde lo mismo que otro tablero; se indica con cuál y en qué parte. |
| **NO NECESARIO** | Se retira; se indica a dónde redirigir. |
| **REDISEÑO** | Se queda, pero incumple principios concretos que se listan. |

---

## 0. Hipótesis de trabajo: Expedientes, SIGER y Recursos SIGUEN VIVOS (decisión de Jamil, 19-10-2026)

El contexto de diseño inicial decía que esos módulos se habían retirado. Jamil confirmó que **no**: siguen vivos y así se clasifica. La evidencia en el código de la rama `DashBoards`:

- Páginas presentes: `src/Web/Pages/Expedientes/{Editor,Seguimiento,Resumen,Cronograma}.cshtml`, `src/Web/Pages/Siger/{Index,Tablero,Editor,Detalle,Conciliacion,Completitud,CapturaLote}.cshtml`, `src/Web/Pages/Recursos/Index.cshtml`, `src/Web/Pages/PlanTrabajo/{Index,Editor}.cshtml`.
- Menú (`src/Web/Pages/Shared/_Layout.cshtml` líneas 256-284): grupos **Expedientes** (Expedientes, Plan de Trabajo, Recursos y Plantillas) y **SIGER** (Inventario, Observatorio, Conciliación) se pintan cuando el rol tiene `Expedientes.Ver` / `Siger.Ver`.
- `src/Web/Common/ModulosPortal.cs` declara `Expedientes` y `Siger` como módulos del portal.
- Cinco tableros dependen de Expedientes: `/Tableros/Digitalizacion`, `/Tableros/Tramites`, `/Tableros/Expedientes`, `/Informes` y la pestaña "Mis Expedientes" de `/Tableros/MiTablero`. Uno depende de SIGER: `/Siger/Tablero` (Observatorio).

Con los módulos vivos, los tableros de expedientes se juzgan por sus méritos: Digitalización se queda y se corrige, Trámites es repetido con Digitalización, Expedientes sigue siendo innecesario por ser un subconjunto, y el Observatorio SIGER se mantiene. Nada de lo que sigue diseña indicadores nuevos sobre esos módulos: el tablero ejecutivo nuevo es solo de Proyectos.

**Alcance de la entrega actual (decisión 5 de Jamil):** solo se construye el ejecutivo, se convierte `/Tableros/Index` en índice y se **marcan** los tableros actuales con insignias. No se fusiona, rediseña ni retira ninguno, e Informes se queda donde está. Lo demás de esta auditoría queda como plan. La especificación está en `docs/tableros/ejecutivo-proyectos.md`.

---

## 1. TAREA 1 — Auditoría de los tableros actuales

### 1.1 Cómo se llega a cada uno (navegación real)

Grupo **Tableros** del menú (`_Layout.cshtml` 310-327) solo tiene tres entradas: **Mi Tablero**, **Inicio** (`/Tableros/Index`, que además es el destino del login y del logo) e **Informes** (gateado por `Expedientes.Ver`, aunque la página exige `Informes.Ver`: quien tiene lo primero y no lo segundo ve el enlace y recibe "acceso denegado").

Todo lo demás se alcanza **solo desde las tarjetas del Inicio** (`Tableros/Index.cshtml` 138-197): Tickets, Reuniones, Digitalización, Avance de trámites y Proyectos. El tablero de Proyectos además se alcanza desde `/Proyectos/Index` ("Tablero gerencial →") y la ficha de proyecto ("Tablero →"). `/Tableros/Expedientes` **no está enlazado desde ningún menú ni hub**: solo desde `Instituciones/Editor.cshtml` línea 98. Los tableros de Área y Unidad solo por las pestañas `_TabsProyectos`.

Conclusión de navegación: no existe un índice de tableros. El "Inicio" hace de hub sin decirlo, y mezcla indicadores propios con atajos. Eso es lo que la Tarea 3 resuelve.

### 1.2 Tabla de clasificación

| # | Tablero (ruta) | Permiso | Audiencia | Preguntas que responde | Indicadores | Veredicto | Motivo |
|---|---|---|---|---|---|---|---|
| 1 | **Inicio** `/Tableros/Index` | `Tableros.Ver` | Todos (landing del login) | ¿Hay tickets críticos? ¿Acuerdos vencidos? ¿Cumplimiento? ¿Capacitados? | 5 KPI (tickets críticos, acuerdos vencidos, % cumplimiento, personas capacitadas, tickets resueltos) · línea tickets creados/resueltos 12 m · 3 conteos (abiertos, reuniones, reuniones mes) · atajos | **REDISEÑO** | Es un resumen de tickets+reuniones disfrazado de inicio; **no muestra nada de Proyectos** salvo un enlace; repite 5 de los 7 KPI de Tickets y 3 de Reuniones; sin "datos al"; colores de Chart.js en hex literal. Debe convertirse en el **hub** (ver Tarea 3). |
| 2 | **Mi Tablero** `/Tableros/MiTablero` | autoservicio | Cada usuario | ¿Qué tengo pendiente yo? | 5 KPI personales · pestañas Compromisos / Reuniones / Tickets / Expedientes | **MANTENER** (con dos ajustes) | Cumple su función (es personal, no de jefatura). Ajustes: la pestaña "Mis Expedientes" cae con el módulo si se retira; y no tiene "Mis actividades de proyecto", que ya existe en `/Proyectos/MisActividades` (enlazar, no duplicar). Emojis en pestañas y hex sueltos: cosmética. |
| 3 | **Tablero de Proyectos (Institución)** `/Tableros/Proyectos` | `Proyectos.Ver` | Jefatura de institución, rol global | ¿Qué exige atención en el portafolio? | **12 KPI** · semáforo (tabla) · dona por estado · reportes/mes · barras por categoría · carga por responsable · actividades vencidas · bloqueadas · entregables vencidos · bloqueos reportados | **REDISEÑO** | La consulta (`GetProyectosDashboardQuery`) es excelente y es la base del nuevo ejecutivo. La página no: 12 KPI (máximo 5-7), 7 secciones de detalle sin jerarquía (no se lee en 5 s), sin comparación contra plan (el "esperado hoy" solo existe en el tablero de un proyecto), sin "datos al", hex literales en gráficos. Se queda como **tablero operativo** del portafolio; lo ejecutivo va aparte. |
| 4 | **Proyectos de mi área** `/Tableros/ProyectosArea` | `Proyectos.Ver` | Jefe de área | Igual que 3, acotado al área + ¿qué unidad apoyar? | Los **mismos 12 KPI y las mismas 4 tablas** que 3 · conmutador "toda mi área / solo lo mío" · bloque "Unidades de mi área" | **REPETIDO** con 3 | Es 3 con `AreaIds=[área activa]` (la consulta ya lo soporta) más un bloque de unidades. Fusionar: una sola página con selector de alcance (las pestañas `_TabsProyectos` ya son ese selector) y el bloque de unidades condicional. |
| 5 | **Mis proyectos** `/Tableros/ProyectosUnidad` | `Proyectos.Ver` | Empleado / unidad | ¿Cómo van los proyectos donde participo? | 4 KPI · tabla de proyectos | **REPETIDO** con 4 (modo "Solo donde soy interesado") y con 3 (`DeUsuarioId`) | `GetMisProyectosDashboardQuery` calcula el mismo predicado que `GetProyectosDashboardQuery(DeUsuarioId)`. Dos consultas para el mismo conjunto. Además `/Proyectos/MisActividades` ya cubre "lo mío" a nivel de actividad. Fusionar en el alcance "Mío" de la página única. |
| 6 | **Tablero de un proyecto** `/Tableros/Proyecto/{id}` | `Proyectos.Ver` | Responsable y jefatura | ¿Este proyecto va en plan? ¿Qué lo frena? ¿Cuándo cerraría? | Cobertura del plan · avance real vs esperado vs físico · **desviación en puntos** · 9 KPI · estado de actividades · ritmo y cierre proyectado · carga por responsable · exige atención · bloqueo vigente · "Calculado al" | **MANTENER** | Es el mejor diseñado: contexto contra plan, advertencias cuando falta línea base, fecha de cálculo. Es el destino natural del clic desde el ejecutivo. Dos notas: ventana "próximos" de 15 días aquí vs 30 en el portafolio (unificar o explicar), y hex literales en barra de actividades. |
| 7 | **Tablero de Reuniones** `/Tableros/Reuniones` | `Tableros.Ver` | Jefatura / coordinación | ¿Se cumplen los acuerdos? ¿Cuántas reuniones? ¿A quién capacitamos? | 7 KPI · barras reuniones/mes · dona compromisos por estado · **dona por tipo (hasta 8 partes)** · barras por institución · tabla compromisos · listado de personas capacitadas | **REDISEÑO** | Dona de tipo con 8 colores (pastel con muchas partes); KPI "Próximos" siempre en ámbar aunque sea 0; "Reuniones" y "Este mes" son conteos sin comparación; el listado de **personas capacitadas expone nombre + institución** (dato personal, revisar con Jamil); CSS inline extenso. Único tablero de reuniones: se queda, se ordena. |
| 8 | **Tablero de Tickets** `/Tableros/Tickets` | `Tableros.Ver` | Soporte y jefatura | ¿Cuántos abiertos y críticos? ¿Tiempo de resolución? ¿Dónde se concentran? | 7 KPI · línea 12 m · **4 donas** (estado, prioridad, categoría, tema) · barras por institución · antiguos | **REDISEÑO** | Cuatro donas, dos de ellas con 8 partes; "Abiertos" siempre ámbar y "Resueltos" siempre verde sin umbral; "Vencidos" sin decir cuál es el SLA; subtítulo **"Soporte de la plataforma SOL" escrito a mano** (texto de una institución en un tablero que debe servir a cualquiera). Lo bueno: drill-down por clic. Único tablero de tickets: se queda, se corrige. |
| 9 | **Tablero de trámites** `/Tableros/Tramites` | `Tableros.Ver` (escribe con `Expedientes.Editar`) | Analistas de digitalización | ¿Qué trámite está rezagado? ¿En qué etapa? | 4 KPI con bandas documentadas · tabla por trámite con semáforo · modales de etapas, notas y bitácora | **REPETIDO** con 10 (misma fuente y misma pregunta; 10 ya trae detalle por etapa y por rango) | Es una **lista de trabajo con acciones de escritura**, no un tablero: a futuro debería vivir en el módulo Expedientes como "Seguimiento de trámites". Su sistema de bandas (`SemaforoAvance`) es el único semáforo con reglas escritas del portal: esa idea se rescata en el ejecutivo. |
| 10 | **Tablero de Digitalización** `/Tableros/Digitalizacion` | `Tableros.Ver` | Jefatura de digitalización | ¿Cómo va la digitalización por institución y etapa? | 4 KPI · 3 tarjetas de distribución (**"En operación" aparece dos veces**) · avance por institución · barras por rango y por etapa con drill-down · Excel | **REDISEÑO** | Gerencial y útil, pero: KPI repetido, 300 líneas de JS que arman tablas en el cliente, colores por umbral sin leyenda ni regla escrita, hex literales. Es el que debe sobrevivir de los tres de expedientes (9, 10, 11). |
| 11 | **Tablero de Expedientes** `/Tableros/Expedientes` | `Tableros.Ver` | — | ¿Cuántos expedientes y cuántos cerrados? | 4 KPI · barras creados/mes · dona por estado · barras por institución | **NO NECESARIO** | Subconjunto estricto de 10 (total, cerrados, por institución, por estado); "% completado = cerrados/total" es una métrica pobre; no está enlazado desde ningún menú ni hub (solo desde `Instituciones/Editor` línea 98). Retirar y redirigir ese enlace a 10. |
| 12 | **Informes** `/Informes/Index` | `Informes.Ver` (menú lo gatea con `Expedientes.Ver`) | Quien genera PDF/Excel | ¿Dame el informe de una institución y periodo? | Fila de 8 estadísticas (repite 10) · tabla expedientes/trámites · PDF y Excel | **MANTENER** (pero **no es un tablero**) | Es un generador de informes de expedientes. A futuro: sacarlo del grupo "Tableros" del menú y corregir la clave con la que se pinta el enlace. **En esta entrega no se mueve** (decisión de Jamil). |
| 13 | **Portafolio** `/Proyectos/Index` | `Proyectos.Ver` | Quien gestiona proyectos | ¿Qué proyectos hay, filtrados por señal? | Lista con avance, estructura, cierre plan, badges; filtro "Señal" alineado con el tablero; CSV | **MANTENER** | Es la lista operativa y el destino de los KPI con drill-down. No es un tablero. Solapa columnas con el semáforo de 3 (aceptable: lista vs tablero). |
| 14 | **Observatorio SIGER** `/Siger/Tablero` (fuera de la lista pedida, pero es un tablero) | `Siger.Ver` | Inventario SIGER | ¿Qué trámites del inventario tienen brechas? | 5 KPI · focos de atención · barras | **MANTENER** | Vive en su módulo, no en Tableros; se lista en el catálogo del hub sin insignia. Nota: usa un morado `#7c3aed` fuera de la paleta (capa función no declarada en tokens). |

### 1.3 Resumen de los solapamientos

```
Inicio ──repite KPI──► Tickets, Reuniones
ProyectosUnidad ──mismo conjunto──► ProyectosArea (modo "mío") ──misma página──► Proyectos (Institución)
Tramites ──misma fuente──► Digitalizacion ◄──subconjunto── Expedientes ◄──misma fila de stats── Informes
```

Resultado a futuro, si se ejecuta todo el plan: de 12 páginas en `/Tableros` quedan **7**: Hub (Inicio convertido en índice), Ejecutivo de proyectos (nuevo), Proyectos operativo (3+4+5 fusionados), Proyecto (ficha), Reuniones, Tickets y Digitalización. Mi Tablero sigue como autoservicio. **En esta entrega** solo ocurren el hub, el ejecutivo y las insignias.

### 1.4 Problemas transversales (aplican a casi todos)

1. **Ningún tablero muestra "datos al"** salvo el de un proyecto ("Calculado al"). Va contra el principio de confianza en el dato.
2. **Colores de gráficos en hex literal** dentro de Chart.js en todas las páginas (`#1a65a3`, `#2f7d32`, `#ef9f27`, `#888780`, `#534ab7`, `#d4537e`...). `DESIGN.md` ya documenta `tokenColor()` para leer tokens desde JS. No existen tokens de serie de gráfico: **propongo `--diger-chart-1..5` y `--diger-chart-muted`** (ver dudas).
3. **Semáforos sin regla escrita** en Tickets, Reuniones, Digitalización (color por umbral decidido en la vista). Solo `SemaforoAvance` (trámites) y `ProyectoEstadoReglas` (proyectos) tienen umbrales centralizados.
4. **KPI siempre coloreados** (`kpi-card warn` fijo) en Tickets y Reuniones: el color deja de significar.
5. **Formato de fechas** `dd/MM/yyyy` en todos; el principio pide "15 de octubre de 2026" para lo ejecutivo (en tablas densas `dd/MM/yyyy` es aceptable).

---

## 2. TAREA 2 — Crítica de la imagen de referencia y propuesta

Imagen: "PROYECTOS DE GOBIERNO DIGITAL · Q3 2026 — Seguimiento de avance, estado y bloqueos de 11 proyectos". Chip "DATOS AL 05/10/2026". 5 KPI, lista "Avance por proyecto", dona "Estado de los proyectos", barras "Bloqueos por proyecto", pie "Página 1 de 3".

### 2.1 Lo que está bien (y se conserva)

- **Cinco KPI, no doce.** Lectura en 5 segundos. Cada uno con una segunda línea de contexto ("18% del total", "9 proyectos con dato", "en 6 proyectos").
- **Chip "Datos al"** arriba a la derecha: exactamente lo que falta en los tableros actuales.
- **Lista de proyectos con barra + responsable + una línea de resultado**, y "Sin dato" declarado como estado en vez de una barra vacía.
- **Dona de estado con ≤5 partes** y el total en el centro: aceptable (el enum `EstadoProyecto` tiene 5 valores).
- **Notas al pie honestas** ("Sin bloqueos: …", "Sin dato: …"): la ausencia de dato se nombra.
- Sobriedad: un color de marca, blanco, cuadrícula de 2/3 + 1/3, sin 3D ni decoración.

### 2.2 Lo que cambiaría (priorizado)

**P1 — El avance no tiene contra qué compararse.** "72.6 %" y "63 %" no dicen si vamos bien. El sistema ya sabe calcular el **avance esperado hoy** por interpolación de las ventanas de actividades (`TableroProyectoQuery` líneas 148-164), pero solo para un proyecto. Propuesta: llevar ese cálculo a la consulta del portafolio y mostrar, por proyecto, una barra tipo **bala**: relleno = real, marca vertical = esperado hoy, y el número de desviación ("−14 pts") al lado. El KPI "Avance promedio" pasa a **"Avance real vs esperado: 72 % / 81 % (−9 pts)"**. Cuando un proyecto no tiene fechas en sus actividades, la marca no se pinta y el estado dice "sin línea base" (ya existe como señal).

**P2 — El color de la barra es el estado, no la salud.** En la imagen verde = finalizado, azul = en proceso. Un proyecto azul al 54 % puede tener la fecha de cierre vencida y tres bloqueos. Propuesta: **semáforo de salud con reglas** (siempre con etiqueta de texto junto al color):

| Color | Token | Regla (se evalúa en este orden) | Etiqueta |
|---|---|---|---|
| Rojo | `--diger-danger` / `--diger-danger-bg` | Fecha de cierre planificada vencida y abierto; **o** desviación ≤ −20 pts; **o** bloqueo vigente en el último reporte | "Atrasado" / "Bloqueado" |
| Ámbar | `--diger-warning` / `--diger-warning-bg` | Desviación entre −20 y −5 pts; **o** 30+ días sin reportar (`ProyectoEstadoReglas.DiasSinReporte`); **o** sin línea base; **o** actividades vencidas > 0 | "En riesgo" / "Sin reportar" / "Sin línea base" |
| Verde | `--diger-success` / `--diger-success-bg` | Resto de los abiertos | "En plan" |
| Neutro | `--diger-text-faint` | Cerrado, cancelado, planificado sin iniciar | "Cerrado" / "No iniciado" |

Los umbrales −20 / −5 / 30 días son propuesta; Jamil confirma. Deben vivir en `ProyectoEstadoReglas`, no en la vista, para que el ejecutivo y el operativo digan lo mismo del mismo proyecto.

**P3 — "Bloqueos registrados: 24" cuenta histórico y premia al que más reporta.** El portal ya define "bloqueo vigente = el del último reporte de cada proyecto; si el siguiente no lo menciona, se dio por superado" (`GetProyectosDashboardQuery` 254-269). Propuesta: el KPI pasa a **"Bloqueos vigentes: N (en M proyectos)"** y el gráfico "Bloqueos por proyecto" se sustituye por la sección **"Necesita decisión"**: lista de 3-7 filas con proyecto, texto del bloqueo, quién lo reportó, hace cuántos días, y enlace a la ficha. Entran también las **actividades que arrancaron bloqueadas** (`ArrancaronBloqueadas`, "o el plan está mal o la dependencia no era tal") y los **proyectos vencidos sin fecha reprogramada**. Esa es la pregunta que una jefatura hace en sala.

**P4 — Falta la fecha del último reporte por proyecto.** En la lista, bajo el responsable: "último reporte 3 de octubre · hace 16 días". Y "Sin dato" debe decir por qué: "sin actividades cargadas", "sin reportes", "sin fechas planificadas" (las tres señales ya existen: `SinDesglose`, `SinReportar`, `SinLineaBase`).

**P5 — Título, periodo e institución escritos a mano.** "PROYECTOS DE GOBIERNO DIGITAL" y "Q3 2026" deben salir de la configuración y de los filtros: nombre de la institución de `InstitucionOptions`; la cartera, de un filtro (categoría, área o todo el portafolio); el periodo, de un filtro de fechas. Ojo: en este sistema **las categorías "Q1/Q2/Q3" son rondas de clasificación del portafolio, no trimestres** (`CategoriaProyecto`, comentarios en `Proyectos.cshtml` 74-76 "las tres rondas de clasificación"). Hay que aclararlo antes de diseñar el filtro.

**P6 — Fecha "Datos al" en formato ejecutivo y con hora.** "Datos al 5 de octubre de 2026, 14:30" (es-HN), más "último reporte del portafolio: 3 de octubre". Un tablero en proyector sin hora no permite saber si se refrescó.

**P7 — "Página 1 de 3" revela que la imagen es un informe impreso.** En pantalla la lista no debe paginar (11-20 proyectos caben con scroll o con altura de proyector); para PDF sí. Decidir si el entregable incluye exportación. Ya existe `ProyectoPdfService` (ficha de un proyecto) con Poppins embebida: reutilizable.

**P8 — Línea de resultado bajo cada proyecto ("5 certificados en producción", "62 de 82 instituciones han respondido").** No hay campo para eso: `Proyecto` tiene `Objetivo` (texto largo) y el último `AvanceProyecto.Descripcion`. Opciones: (a) mostrar la descripción del último reporte truncada a 80 caracteres, (b) crear un campo corto "Resultado a la fecha" en la ficha. Decisión de Jamil; (a) no requiere migración.

**P9 — Colores y accesibilidad.** La dona pinta "No iniciado" en naranja, que se lee como alerta sin serlo; y la leyenda es solo color. Mapa propuesto por estado: Planificado `--diger-text-faint`, En ejecución `--diger-blue-mid`, Cerrado `--diger-success`, Suspendido `--diger-warning`, Cancelado `--diger-text-slate`. Toda barra lleva su porcentaje en texto y las filas rojas/ámbar llevan icono + etiqueta.

**P10 — Añadir una mini-tendencia.** Un solo gráfico de línea pequeño: avance promedio del portafolio por mes (últimos 6) contra el esperado. Hoy no se guarda histórico de `AvancePct`; se puede aproximar con los `PorcentajeReportado` por fecha o, mejor, **empezar a registrar una foto mensual** (tabla `ProyectoAvanceMensual`). Es el indicador que no existe y vale la pena crear; no se diseña hasta tener el dato.

### 2.3 Boceto de la distribución propuesta (opción recomendada)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ [Logo inst.]  PROYECTOS · <cartera/filtro> · <periodo>      [Datos al 5 de   │
│ Seguimiento de avance, salud y decisiones de N proyectos     octubre, 14:30] │
├──────────┬──────────┬──────────┬───────────────────┬─────────────────────────┤
│ TOTAL    │ EN PLAN  │ EN RIESGO│ ATRASADOS /       │ AVANCE REAL vs ESPERADO │
│ 11       │ 5  verde │ 3  ámbar │ BLOQUEADOS 3 rojo │ 72 % / 81 %  (−9 pts)   │
│ 2 cerr.  │ 45 %     │ 27 %     │ 2 vencidos·1 bloq.│ sobre 9 con línea base  │
├──────────┴──────────┴──────────┴───────────────────┼─────────────────────────┤
│ AVANCE Y SALUD POR PROYECTO (ordenado: rojo, ámbar, verde, neutro)          │
│  ● Receta Digital            ▓▓▓▓▓▓▓▓░|░░  63 % · esp. 78 % · −15  ATRASADO  │ ESTADO (dona ≤5)
│    M. Coello · último reporte 3 oct (16 d) · 2 act. vencidas                 │   11 proyectos
│  ● SOL – Digitalización      ▓▓▓▓▓▓|░░░░░  54 % · esp. 60 % · −6  EN RIESGO  │ ─────────────
│  ○ Conectividad              ░░░░░░░░░░░░  0 % · sin línea base  SIN DATO    │ NECESITA DECISIÓN
│  ✓ BIEN                      ▓▓▓▓▓▓▓▓▓▓▓▓ 100 %                   CERRADO   │  1. SOL: "falta…"
│  …                                                                           │     H. Ortez · 9 d
│                                                                              │  2. Talent Up: …
│                                                                              │  3. Receta: vencido
│                                                                              │     sin reprogramar
├──────────────────────────────────────────────────────────────────────────────┤
│ Fuente: módulo Proyectos · reglas del semáforo [ver] · Se actualiza cada 5 min│
└──────────────────────────────────────────────────────────────────────────────┘
```

Alternativas que se presentarán en el paso 3 del proceso (cuando Jamil responda las dudas): (B) la lista a todo el ancho y debajo dona + decisiones; (C) sin dona, con mini-tendencia en su lugar. Recomiendo (A) arriba: conserva lo que de la imagen funciona y agrega contexto y decisión sin crecer.

### 2.4 Qué datos ya existen y qué falta

| Necesidad | ¿Existe? | Dónde |
|---|---|---|
| Total, por estado, avance promedio, atrasados, sin reportar, sin línea base, bloqueos vigentes, actividades bloqueadas/arrancadas bloqueadas, carga por responsable | Sí | `GetProyectosDashboardQuery` / `ProyectosDashboardDto` |
| Avance esperado hoy y desviación | Solo por proyecto | `TableroProyectoQuery` 148-164; hay que extender la consulta del portafolio (mismo cálculo, en memoria, sobre actividades con fechas; el portafolio es de decenas, no miles) |
| Último reporte y días sin reportar por proyecto | Sí | `ProyectoSemaforoDto.UltimoAvance/DiasSinReporte` |
| Semáforo de salud con tres niveles | No (hoy son banderas sueltas) | Nueva regla en `ProyectoEstadoReglas` |
| Línea de resultado por proyecto | No | Opción (a) último `AvanceProyecto.Descripcion`; (b) campo nuevo |
| Tendencia mensual del avance | No | Requiere foto mensual (nuevo) |
| Riesgos abiertos/altos | Solo por proyecto | `TableroProyectoDto.RiesgosAbiertos/Altos`; agregable al portafolio |
| Nombre y logo de la institución | Sí | `InstitucionOptions` |

El inventario de datos del `constructor-tableros` (`docs/tableros/datos-proyectos-gobierno-digital.md`) debe confirmar la calidad: cuántos proyectos tienen fechas en sus actividades (el tablero de un proyecto advierte que "el 89 % de las actividades no tiene fechas planificadas", comentario en `Proyecto.cshtml` 40-41). **Si eso sigue así, el P1 nace vacío**: la especificación debe incluir cómo empezar a cargar fechas antes de prometer desviación.

---

## 3. TAREA 3 — Dónde viven los tableros actuales y cómo se señalan

### 3.1 Propuesta: `/Tableros/Index` pasa a ser el índice de tableros (hub)

Hoy el Inicio ya es el único punto de reunión de los tableros y es el destino del login. Se convierte explícitamente en hub, en tres bandas, de lo general a lo particular:

```
┌───────────────────────────────────────────────────────────────────────┐
│ Tableros                                      Datos al 5 oct, 14:30    │
├───────────────────────────────────────────────────────────────────────┤
│ TABLERO EJECUTIVO DE PROYECTOS                       [NUEVO]           │
│ 11 proyectos · 5 en plan · 3 en riesgo · 3 atrasados · 72 % / 81 %     │
│                                               [Abrir tablero →]        │
├───────────────────────────────────────────────────────────────────────┤
│ ATENCIÓN HOY  (solo lo que está en cero o en rojo, con enlace)         │
│  Tickets críticos 2 · Acuerdos vencidos 5 · Bloqueos vigentes 3        │
├───────────────────────────────────────────────────────────────────────┤
│ TODOS LOS TABLEROS                                                     │
│  ▸ Proyectos — seguimiento operativo      [EN REDISEÑO]                │
│  ▸ Proyectos de mi área                   [REPETIDO · se funde en ↑]   │
│  ▸ Mis proyectos                          [REPETIDO · se funde en ↑]   │
│  ▸ Reuniones y acuerdos                   [EN REDISEÑO]                │
│  ▸ Tickets de soporte                     [EN REDISEÑO]                │
│  ▸ Digitalización                         [DEPENDE DE EXPEDIENTES]     │
│  ▸ Avance de trámites                     [REPETIDO con Digitalización]│
│  ▸ Expedientes                            [A RETIRAR · usa Digitaliz.] │
│  ▸ Mi tablero                             (sin insignia)               │
└───────────────────────────────────────────────────────────────────────┘
```

- La banda 1 solo se pinta con `Proyectos.Ver` (regla de DESIGN.md: gatear por la clave del destino). Sus cifras salen de la misma consulta del ejecutivo (no se duplica cálculo).
- La banda 2 reemplaza los 5 KPI + gráfico actuales del Inicio: se queda lo que es alerta transversal, se va la línea de tickets (ya está en su tablero).
- La banda 3 lista **todos** los tableros, cada uno con su clave de destino, descripción de una línea y la insignia.

**Menú** (`_Layout`, grupo Tableros, máximo 6 entradas por regla del propio layout): "Ejecutivo", "Todos los tableros" (el hub), "Mi tablero", y por ahora "Informes" tal cual (sacarlo al grupo Expedientes queda para después). No se ponen insignias en el menú: ahí son ruido y el menú no es donde se decide. El detalle definitivo (bandas, catálogo `TablerosCatalogo`, insignias y clases) está en `ejecutivo-proyectos.md` §10.

Alternativa descartada: una página nueva `/Tableros/Catalogo` dejando el Inicio como está. Duplica el hub y mantiene un landing que no habla de proyectos.

### 3.2 Las insignias: texto + token, nunca solo color

| Insignia | Texto visible | Icono (sprite `_IconSprite`) | Fondo / texto (tokens) | Cuándo |
|---|---|---|---|---|
| Nuevo | "Nuevo" | `#i-plan` | `--diger-blue-light` / `--diger-blue` | Tablero publicado en los últimos 30 días |
| Repetido | "Repetido · se funde en X" | `#i-enlace` | `--diger-warning-bg` / `--diger-warning-strong` | Veredicto REPETIDO; X enlazado |
| En rediseño | "En rediseño" | `#i-grafica` | `--diger-bg-blue-pale` / `--diger-blue-mid` | Veredicto REDISEÑO |
| A retirar | "Se retira el dd de mes · usa X" | `#i-vencido` | `--diger-danger-bg` / `--diger-danger-text` | Veredicto NO NECESARIO, con fecha |
| ~~Depende de módulo~~ | — | — | — | **Retirada**: los módulos siguen vivos, no hace falta. |

Reglas:
- Se construyen con la familia `.badge` existente (`badge-warning`, `badge-danger`) más una clase nueva `.tablero-badge` para las dos variantes azules; **pido confirmación antes de crear la clase**.
- La misma insignia se repite en la **cabecera del tablero afectado** ("Este tablero está marcado como repetido: se funde en Proyectos el …"), porque mucha gente llega por enlace directo y no por el hub.
- Las insignias son **temporales**: salen de una lista única en código (`TablerosCatalogo` en `src/Web/Common/`, un registro por tablero: ruta, título, descripción, clave, estado, destino sustituto, fecha). Una sola fuente evita que el hub y las cabeceras se contradigan, y retirar la insignia es cambiar una línea.
- Cuando un tablero se retira de verdad, su ruta **redirige** (301) al sustituto durante un periodo, para no romper enlaces guardados: `/Tableros/Expedientes` → `/Tableros/Digitalizacion`; `/Tableros/ProyectosArea` y `/Tableros/ProyectosUnidad` → `/Tableros/Proyectos?alcance=Area|Mio`.

### 3.3 Orden de ejecución sugerido (para la sesión principal)

1. Decidir dudas (sección 4). 2. Especificar el ejecutivo (`docs/tableros/tablero-ejecutivo-proyectos.md`). 3. Hub + catálogo con insignias (cambio pequeño, visible de inmediato). 4. Fusión de los tres tableros de proyectos. 5. Correcciones de Tickets y Reuniones. 6. Lo de Expedientes, cuando haya decisión de módulo.

---

## 4. Dudas para Jamil — RESUELTAS el 19-10-2026

Respuestas: (1) módulos vivos; (2) cartera = área completa, sin "Q3"; (3) web en vivo con impresión, sin PDF; (4) umbrales aprobados tal cual; (5) bloqueos vigentes; (6) sin campo nuevo: responsable + último reporte; (7) nombres visibles; (8) mismo permiso que los tableros de proyectos; (9) sí, el Inicio es el índice; (10) fusión fuera de esta entrega; (11) sin tokens nuevos (la spec no los necesita); (12) sin auto-refresco; (13) tendencia como trabajo futuro. Se conserva la lista original como registro:

1. **Módulos:** ¿Expedientes, SIGER y Recursos están retirados o no? En la rama siguen vivos. De esto depende el destino de Digitalización, Trámites, Expedientes, Informes, la pestaña de Mi Tablero y el Observatorio SIGER.
2. **"Q3 2026":** ¿es trimestre (filtro de periodo) o la categoría "Q3" (ronda de clasificación del portafolio)? ¿Qué define la cartera "Gobierno Digital": una categoría, un área, o todo el portafolio de la institución?
3. **Entregable:** ¿pantalla para proyector, o también informe PDF paginado como la imagen? ¿Se exporta?
4. **Semáforo de salud:** ¿confirma umbrales rojo ≤ −20 pts / ámbar −20 a −5 / 30 días sin reportar, y "atrasado = fecha de cierre planificada vencida"? ¿Un bloqueo vigente pone en rojo por sí solo?
5. **Bloqueos:** ¿contamos vigentes (regla ya existente) o históricos como en la imagen?
6. **Línea de resultado por proyecto** ("5 certificados en producción"): ¿descripción del último reporte, o campo nuevo "Resultado a la fecha" en la ficha?
7. **Datos de personas:** el ejecutivo muestra responsable por nombre y el operativo "carga por responsable"; Reuniones lista personas capacitadas con nombre e institución. ¿Es aceptable para la audiencia de jefaturas y para proyector en sala?
8. **Permiso:** ¿el ejecutivo pide `Proyectos.Ver` como sus hermanos, o una clave propia (`Tableros.Ejecutivo`) para poder darlo a jefaturas sin abrir el módulo entero? El alcance institución/área se aplica solo.
9. **Hub:** ¿acepta que `/Tableros/Index` (landing del login) se convierta en el índice con el ejecutivo arriba y el resto abajo con insignias, sacando la línea de tickets?
10. **Fusión** de Institución/Área/Unidad en un solo tablero operativo con selector de alcance: ¿sí?
11. **Tokens nuevos:** `--diger-chart-1..5` y `--diger-chart-muted` para series de gráfico (hoy hex literales), y clase `.tablero-badge`. ¿Autoriza?
12. **Refresco:** ¿auto-refresco cada 5 minutos en proyector y meta de carga < 3 s? ¿Hace falta modo "pantalla completa" sin menú?
13. **Tendencia mensual** (foto mensual del avance del portafolio): ¿se empieza a registrar? Sin eso no hay línea de tiempo.
