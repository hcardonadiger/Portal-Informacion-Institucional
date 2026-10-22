# Inventario de datos: tablero ejecutivo «Proyectos de Gobierno Digital»

Fecha: 19 de octubre de 2026 · Rama: `DashBoards` · Autor: constructor de tableros (DIGER)
Base medida: `GestionGD_DashBoards` en `localhost\SQL2025` (copia local de GestionGD, autorizada
por Jamil para esta rama). Solo se ejecutaron consultas `SELECT`. No se tocó código ni datos.

Referencia visual: la imagen que entregó Jamil (encabezado «PROYECTOS DE GOBIERNO DIGITAL · Q3
2026», 5 KPI, avance por proyecto, dona de estado, barras de bloqueos y pie «Página 1 de 3»).
Complementa la auditoría del diseñador (`docs/tableros/auditoria-tableros.md`, sección de la
propuesta ejecutiva y sus preguntas P1 a P10).

> **Conclusión corta.** El tablero se puede construir hoy casi entero con lo que ya calcula
> `GetProyectosDashboardQuery`. Lo que **no** se puede reproducir es la imagen tal cual: sus cifras
> no salen de esta base. De los 11 proyectos, 4 no existen, 3 son agrupaciones de varios proyectos
> que en el portal están separados, y los porcentajes, estados y bloqueos de los que sí existen
> no coinciden. Además la fecha «Datos al 05/10/2026» es posterior al último reporte que hay en
> la base (24/09/2026). Todo indica que la imagen se armó a mano desde otra fuente.

---

## 1. Tablas del modelo que alimentan el tablero

La fuente de verdad del esquema es EF Core: `src/Domain/Entities/Proyecto.cs` (contiene
`Proyecto`, `EntregableProyecto`, `ActividadProyecto` y `AvanceProyecto`),
`RiesgoProyecto.cs`, `InteresadoProyecto.cs`, `CategoriaProyecto.cs`, `PrioridadProyecto.cs`,
`VinculoProyecto.cs`, y los enums en `src/Domain/Enums/Enums.cs`.

| Tabla (DbSet) | Qué es en lenguaje de negocio | Campos clave | Relación |
|---|---|---|---|
| `Proyectos` | Proyecto interno de DIGER | `Codigo` (PRY-AAAA-NN), `Nombre`, `Objetivo`, `InstitucionId`, `AreaId`, `UnidadId`, `ResponsableId` + `Responsable` (nombre guardado), `PrioridadId`, `CategoriaId` (opcional), `Accion` (opcional), `Estado`, fechas plan y real de inicio y fin, `AvancePct` (calculado y guardado), `IsDeleted` | Ancla del filtro de alcance (institución, área, unidad; el responsable siempre ve su proyecto) |
| `ProyectoEntregables` | Entregable (antes «hito») | `ProyectoId`, `Orden`, `Nombre`, `FechaPlan`, `FechaReal`, `Estado`, responsable | N a 1 con proyecto |
| `ProyectoActividades` | Actividad de un entregable | `EntregableId`, fechas plan y real, `AvancePct`, `Estado`, responsable | N a 1 con entregable |
| `ProyectoDependenciasActividad` | La actividad B no arranca hasta que termine A | `PredecesoraId`, `SucesoraId` | Entre actividades |
| `ProyectoAvances` | Reporte de avance (bitácora de seguimiento) | `ProyectoId`, `EntregableId`, `ActividadId`, `Fecha`, `Autor`, `Descripcion`, `PorcentajeReportado`, **`Bloqueo`** (texto), `RiesgoId` | N a 1 con proyecto |
| `ProyectoRiesgos` | Riesgo del proyecto | `Categoria`, `Probabilidad`, `Impacto` (severidad = P×I, calculada), `Estrategia`, **`Estado`** (Abierto, EnTratamiento, Materializado, Cerrado), fechas | N a 1 con proyecto |
| `ProyectoInteresados` | Interesados (personas del portal) | `UsuarioId`, `Nombre`, `Institucion`, `Cargo`, `Rol`, `Automatico` | N a 1 con proyecto |
| `CategoriasProyecto` | Catálogo «Q1, Q2, Q3» | `Nombre`, `Orden`, `Color`, `Activo` | 1 a N con proyectos |
| `PrioridadesProyecto` | Catálogo Alta, Media, Baja | `Nombre`, `Orden`, `Color` | 1 a N |
| `Areas`, `Unidades` | Estructura de DIGER | `GOBDIG` = «GOBIERNO DIGITAL»; unidades DITRA, DAI, DPD, CONEC, TALDIG, etc. | Proyecto → área y unidad |
| `ProyectoExpedientes`, `ProyectoReuniones`, `ProyectoTickets` | Vínculos a otros módulos | `ProyectoId`, Id del otro registro | N a N |
| `BitacoraProyecto` | Auditoría inmutable de cambios | `Tipo`, `Detalle`, `Actor`, `Fecha` | N a 1 |

**Estados (enums):**

- `EstadoProyecto`: Planificado(1), EnEjecucion(2), Suspendido(3), Cerrado(4), Cancelado(5). Las
  transiciones las valida `Proyecto.CambiarEstado`; `Reabrir` devuelve un cerrado a ejecución.
- `EstadoEntregable`: Pendiente, EnProceso, Completado, Cancelado.
- `EstadoActividad`: Pendiente, EnProceso, Completada, Cancelada (lo deriva el porcentaje, salvo
  Cancelada).
- `EstadoRiesgo`: Abierto, EnTratamiento, Materializado, Cerrado.
- **No existe ningún estado «Bloqueado»** en ningún enum.

**Volumen hoy** (filas vivas): 39 proyectos (más 1 borrado lógico), 91 entregables, 404
actividades, 80 reportes de avance, 8 dependencias, 2 riesgos, 82 interesados, 361 entradas de
bitácora. Es un portafolio de decenas, no de miles: la consulta en memoria que ya usa el
portafolio sobra en rendimiento.

**Índices existentes que sirven:** `IX_ProyectoAvances_ProyectoId_Fecha`,
`IX_ProyectoRiesgos_ProyectoId_Estado`, `IX_ProyectoEntregables_ProyectoId_Orden`,
`IX_ProyectoActividades_EntregableId_Orden`, `IX_Proyectos_Estado_FechaFinPlan`,
`IX_Proyectos_CategoriaId`. No hace falta un índice nuevo para este tablero.

---

## 2. Cada elemento de la imagen: de dónde sale

### 2.1 Encabezado: «PROYECTOS DE GOBIERNO DIGITAL · Q3 2026»

**«Gobierno Digital»** coincide con el área `GOBDIG` («GOBIERNO DIGITAL») de DIGER. El script
`database/clasificar_proyectos_gobdig_ditra.sql` pasó todo el portafolio a GOBDIG/DITRA, y luego
aparecieron unidades DAI y DPD. Hoy, en la base:

| Área | Unidad | Proyectos |
|---|---|---|
| GOBDIG | DITRA (Digitalización de Trámites) | 26 |
| GOBDIG | DAI (Desarrollo en Inteligencia Artificial) | 6 |
| GOBDIG | DPD (Desarrollo de Proyectos de Transformación) | 5 |
| SIGER | MSD (Monitoreo y Seguimiento del Desempeño) | 2 |

Por lo tanto, «cartera de Gobierno Digital» se resuelve con el filtro `AreaIds = ["GOBDIG"]` que ya
acepta `GetProyectosDashboardQuery`: 37 proyectos. El nombre del área se lee de la tabla `Areas`,
no se escribe a mano. El nombre de la institución sale de `InstitucionOptions`.

**«Q3»: en este sistema no es un trimestre.** `CategoriasProyecto` trae Q3, Q2 y Q1 como **las tres
rondas de clasificación del portafolio** (término de la Fórmula 1), según
`database/2026-09-18_las_q_son_categorias.md` y la migración `LasQSonCategoriasNoPrioridades`.
Q3 es la de «los más rápidos» (orden 1, color verde).

- En `GestionGD_DashBoards` **ningún proyecto tiene categoría: los 39 están en NULL**, y por lo
  tanto ninguno es Q3.
- `database/2026-09-28_categoria_q3_a_los_demos.sql` solo clasificaba como Q3 los proyectos con
  nombre «DEMO …», y se escribió para `GestionGD_new`. En esta base no hay proyectos DEMO (faltan
  los Id 26 a 29, que probablemente eran esos). El comentario del script dice expresamente que los
  proyectos reales quedan en NULL a propósito.
- Si «Q3 2026» quiere decir **tercer trimestre (julio a septiembre)**, no hay campo que lo
  represente. Se podría filtrar por periodo con las fechas (plan o real), con las fechas de los
  reportes de avance, o solo usarlo como etiqueta del informe. **Hay que preguntarle a Jamil.**

### 2.2 Chip «DATOS AL 05/10/2026»

No hay un campo «fecha de corte». Las fechas candidatas son estas:

| Candidata | Valor hoy | Comentario |
|---|---|---|
| Último reporte de avance del alcance: `MAX(ProyectoAvances.Fecha)` | 24/09/2026 21:51 | Es lo que «reporta» la gente |
| Último cambio de estructura o avance de actividad: `MAX(ProyectoActividades.UpdatedAt)` | 28/09/2026 15:09 | `AvancePct` cambia cuando se mueve una actividad, aunque no haya reporte |
| Última entrada de bitácora del proyecto | 28/09/2026 15:09 | Igual que la anterior |
| Momento del cálculo (ahora) | — | Es lo que muestra `/Tableros/Proyecto` como «Calculado al» |

Lo razonable es mostrar las dos cosas: **«Calculado al <ahora>»** (el tablero se calcula en vivo)
y **«Último reporte: <MAX(Fecha) de ProyectoAvances dentro del alcance filtrado>»**. La fecha
05/10/2026 de la imagen no existe en la base: es posterior a todo lo registrado.

### 2.3 KPI 1: Total de proyectos

`COUNT` de `Proyectos` dentro del alcance y del filtro de cartera. Ya está:
`ProyectosDashboardDto.Total`. Duda: ¿se cuentan los cancelados? Hoy no hay ninguno.

### 2.4 KPI 2 y 3, y la dona «Estado de los proyectos»

Sale de `Proyectos.Estado`. Ya está: `ProyectosDashboardDto.PorEstado` (con `Etiquetas.Estado`).
Mapeo propuesto a las cuatro etiquetas de la imagen:

| Imagen | Modelo | Comentario |
|---|---|---|
| Finalizado | `Cerrado` | Directo |
| En proceso | `EnEjecucion` | ¿`Suspendido` va aquí o aparte? Hoy hay 0 suspendidos |
| No iniciado | `Planificado` | Ojo: hay 5 planificados con avance mayor que 0 (ver sección 4) |
| Sin datos | **No existe.** `Estado` nunca es nulo | Opciones: proyecto sin estructura (sin entregables vigentes) o proyecto sin ningún reporte. Hay que decidirlo |
| (no aparece) | `Cancelado` | ¿Se excluye? Hoy hay 0 |

En la imagen, «Sin datos» como estado (1 proyecto) no es lo mismo que «Sin dato» como avance (2
proyectos): uno de los dos «sin dato» de avance figura «En proceso». Conviene separar los dos
conceptos en la especificación.

### 2.5 KPI 4: Avance promedio («72.6 %, 9 proyectos con dato»)

**Cálculo existente del avance de un proyecto (se reutiliza, no se reinventa):**

1. Actividad: `ActividadProyecto.AvancePct`, el porcentaje reportado. El estado se deriva del
   porcentaje.
2. Entregable: `EntregableProyecto.AvanceCalculado` es el promedio simple de sus actividades no
   canceladas. Si no tiene actividades, se aplica la regla 0/50/100 por estado (Pendiente 0, En
   proceso 50, Completado 100).
3. Proyecto: `Proyecto.RecalcularAvance` es el promedio simple de los entregables no cancelados,
   redondeado. **Sin entregables vigentes vale 0**, y el comentario del dominio dice que ese cero
   es la señal de que falta cargar la estructura.
4. El resultado se **guarda** en `Proyectos.AvancePct` y lo recalcula `ProyectoConEstructura` en
   cada cambio. Verificado: en los 39 proyectos el valor guardado coincide con el recalculado desde
   el árbol (0 diferencias). Hay que leer `AvancePct`, sin recalcular.

**Promedio del portafolio:** la imagen promedia todos los proyectos que tienen dato, cerrados
incluidos (653 / 9 = 72.6), con un decimal. El existente
(`ProyectoEstadoReglas.AvancePromedio`, usado en `ProyectosDashboardDto.AvancePromedio`)
**promedia solo los proyectos en ejecución y redondea a entero**. Son definiciones distintas; la
especificación tiene que elegir una.

Sobre «con dato»: la forma honesta de distinguir un 0 % real de un «no hay contra qué medir» es
**«tiene al menos un entregable vigente»**. Hoy solo 1 proyecto de GOBDIG no tiene estructura
(PRY-2026-38).

Valores de hoy (GOBDIG, 37 proyectos):

| Definición | Valor |
|---|---|
| Promedio de todos | 51.8 % |
| Promedio de los que tienen estructura (36) | 53.3 % |
| Promedio de los que están en ejecución (25), la regla actual | 49.7 % (el tablero muestra 50) |

### 2.6 KPI 5 y barras: «Bloqueos registrados: 24, en 6 proyectos»

En el modelo hay cinco cosas que se pueden llamar «bloqueo». Conteos en GOBDIG:

| Opción | Fuente | Hoy |
|---|---|---|
| a) Bloqueos reportados (histórico) | `ProyectoAvances.Bloqueo IS NOT NULL` | **14 en 10 proyectos** (15 contando el proyecto borrado PRY-2026-02, que el filtro global ya excluye) |
| b) Bloqueos vigentes: el último reporte del proyecto trae bloqueo | Regla ya implementada en `GetProyectosDashboardQuery` (sección «Bloqueos vigentes») y en `TableroProyectoQuery` (`BloqueoVigente`) | **8 en 8 proyectos** |
| c) Actividades bloqueadas por dependencia (la predecesora sigue abierta) | `ProyectoDependenciasActividad`. Ya implementado: `ProyectosDashboardDto.ActividadesBloqueadas` | **2 en 2 proyectos** (PRY-2026-27 y PRY-2026-30) |
| d) Riesgos materializados | `ProyectoRiesgos.Estado = Materializado` | **1** (SOL - FOSOVI) |
| e) Riesgos abiertos o en tratamiento | `ProyectoRiesgos.Estado IN (Abierto, EnTratamiento)` | **1** (SOL - INPREMA) |
| (f) Reporte con bloqueo vinculado a un riesgo | `ProyectoAvances.RiesgoId IS NOT NULL` | 0: la función existe pero nadie la usa |

El «24» de la imagen, por su tamaño y por el reparto (SOL 9, Talent Up 6, BIEN 4…), solo se parece
a la opción (a), el histórico. Con la (b) cada proyecto tiene como máximo 1, así que unas barras
«por proyecto» serían todas de largo 1 y no dirían nada. Si se elige (b), el gráfico de barras
pierde sentido y conviene la lista «Necesita decisión» que propone el diseñador (P3).

Bloqueos por proyecto hoy (histórico / vigente):

| Proyecto | Histórico | Vigente |
|---|---|---|
| PRY-2026-07 SOL - FOSOVI | 3 | sí |
| PRY-2026-03 SOL - CONSUCOOP | 2 | sí |
| PRY-2026-05 SOL - IHADFA | 2 | sí |
| PRY-2026-01 Mesa técnica de simplificación | 1 | no |
| PRY-2026-06 SOL - SRECI (Cancillería) | 1 | no |
| PRY-2026-12 SOL - Instituto de la Propiedad | 1 | sí |
| PRY-2026-15 Talent Up Honduras | 1 | sí |
| PRY-2026-16 Componente 3 SENPRENDE | 1 | sí |
| PRY-2026-23 Portal Nacional de Datos Abiertos | 1 | sí |
| PRY-2026-26 SOL - INPREMA | 1 | sí |

Agrupados por programa: los SOL suman 10 históricos (5 vigentes), Talent Up 1 y los demás 0.

### 2.7 «Avance por proyecto»: nombre, subtítulo, barra y porcentaje

- **Nombre:** `Proyectos.Nombre`. Número de orden: posición en la lista (la imagen no sigue
  ningún orden evidente; hay que definirlo).
- **Barra y color:** `AvancePct` y `Estado`, con el mapeo de la sección 2.4. «Sin dato» se usa
  cuando el proyecto no tiene entregables vigentes.
- **Responsable:** `Proyectos.Responsable` (nombre guardado). **8 de 39 proyectos (21 %) no
  tienen responsable**, entre ellos Firma Electrónica (PRY-2026-13) y Talent Up Honduras
  (PRY-2026-15). En la imagen «Alejandra» aparece como responsable de Talent Up, pero en la base
  no lo es; en el mejor caso es interesada.
- **Línea de meta o alcance («85 trámites · 5 instituciones», «62 de 82 instituciones han
  respondido», «20,000 becas»): no existe ningún campo para esto.** `Proyecto` no tiene meta,
  alcance, indicador ni «resultado a la fecha». Lo que hay:
  - `Objetivo`: lo tienen los 39, pero es un párrafo (unos 193 caracteres de media), no una cifra.
  - La `Descripcion` del último `ProyectoAvances`: texto libre del último reporte.
  - Conteos derivables: entregables completados de N, actividades completadas de N.
  - Para SOL, «N trámites · M instituciones» se podría calcular desde `ExpedienteTramites` y
    `Expedientes`: hoy hay **46 trámites en 5 instituciones** (CONSUCOOP 14, FOSOVI 13, IHADFA 12,
    INPREMA 6, HBCBH 1). Coincide en «5 instituciones», pero no en «85 trámites». Además los
    proyectos SOL solo tienen 1 vínculo `ProyectoExpedientes` en total, así que la relación
    proyecto → trámites hoy no está registrada.
  - «62 de 82 instituciones han respondido» (encuesta de madurez) y «20,000 / 10,000 becas»
    (Talent Up) son datos de **sistemas externos** (portal de encuestas, plataforma Talent Up). El
    portal no los tiene.
  - Para tenerla de verdad hace falta un campo nuevo, por ejemplo `Proyecto.MetaResumen`
    (nvarchar(120)) o «Resultado a la fecha», editable en la ficha. Eso implica migración más
    script. Es la opción (b) del diseñador en P8.

### 2.8 Pie «Página 1 de 3»

Sugiere un informe en PDF de varias páginas. El aplicativo ya tiene QuestPDF (licencia Community)
y ClosedXML en `Infrastructure`, y un `IProyectoPdfService` para la ficha de un proyecto. Un PDF
del tablero ejecutivo sería un servicio nuevo con el mismo patrón. Hay que confirmar si el alcance
es una página web, un PDF o las dos cosas.

---

## 3. Valores que mostraría hoy el tablero (GestionGD_DashBoards, cartera GOBDIG)

### 3.1 KPI

| KPI | Imagen | Hoy (GOBDIG, 37) | Toda la institución (39) |
|---|---|---|---|
| Total de proyectos | 11 | **37** | 39 |
| Finalizados (Cerrado) | 2 (18 %) | **4 (11 %)** | 4 (10 %) |
| En proceso (EnEjecucion) | 7 (64 %) | **25 (68 %)** | 25 (64 %) |
| No iniciado (Planificado) | 1 | **8** | 10 |
| Sin datos | 1 | **0** como estado; 1 sin estructura | 1 |
| Avance promedio | 72.6 % (9 con dato) | **53.3 %** (36 con estructura); 51.8 % todos; 49.7 % en ejecución | 50.4 % (37 con estructura) |
| Bloqueos | 24 en 6 | **14 en 10** (histórico); **8 en 8** (vigentes) | igual (SIGER no tiene) |
| Sin reportar más de 30 días (en ejecución) | — | **24 de 25** | 24 |
| Nunca reportados | — | 10 | 12 |

### 3.2 Por proyecto (39 vivos; categoría NULL en todos)

E/A = entregables / actividades. Último rep. = fecha del último `ProyectoAvances`. Bloq. =
histórico / vigente. Todos son de la institución DIGER.

| Código | Nombre | Área/Unidad | Estado | Avance | E/A | Responsable | Último rep. | Bloq. | Riesgos |
|---|---|---|---|---|---|---|---|---|---|
| PRY-2026-01 | Mesa técnica de simplificación | GOBDIG/DITRA | EnEjecucion | 54 | 1/12 | Henry Ortez | 23/08 | 1/no | 0 |
| PRY-2026-03 | SOL - CONSUCOOP | GOBDIG/DITRA | EnEjecucion | 45 | 1/10 | Brizzio Zelaya | 17/08 | 2/sí | 0 |
| PRY-2026-04 | SOL - SESAL | GOBDIG/DITRA | EnEjecucion | 25 | 1/4 | Brizzio Zelaya | — | 0 | 0 |
| PRY-2026-05 | SOL - IHADFA | GOBDIG/DITRA | EnEjecucion | 29 | 1/12 | Brizzio Zelaya | 17/08 | 2/sí | 0 |
| PRY-2026-06 | SOL - SRECI (Cancillería) | GOBDIG/DITRA | EnEjecucion | 39 | 1/9 | Henry Ortez | 19/08 | 1/no | 0 |
| PRY-2026-07 | SOL - FOSOVI | GOBDIG/DITRA | EnEjecucion | 33 | 1/6 | **(sin)** | 25/08 | 3/sí | 1 materializado |
| PRY-2026-08 | SOL - IHT / CANATURH | GOBDIG/DITRA | EnEjecucion | 40 | 1/5 | Brizzio Zelaya | — | 0 | 0 |
| PRY-2026-09 | SOL - SENASA | GOBDIG/DITRA | EnEjecucion | 22 | 1/9 | **(sin)** | 11/06 | 0 | 0 |
| PRY-2026-10 | SOL - Cuerpo de Bomberos (HBCBH) | GOBDIG/DITRA | EnEjecucion | 30 | 1/5 | Brizzio Zelaya | 22/07 | 0 | 0 |
| PRY-2026-11 | SOL - ARSA | GOBDIG/DITRA | Planificado | 50 | 1/2 | **(sin)** | — | 0 | 0 |
| PRY-2026-12 | SOL - Instituto de la Propiedad | GOBDIG/DITRA | EnEjecucion | 36 | 1/7 | **(sin)** | 23/08 | 1/sí | 0 |
| PRY-2026-13 | Firma electrónica avanzada y certificados PKI | GOBDIG/DITRA | EnEjecucion | 30 | 1/5 | **(sin)** | — | 0 | 0 |
| PRY-2026-14 | Expediente Digital GEXFILE - SDE | GOBDIG/DITRA | EnEjecucion | 25 | 1/8 | **(sin)** | 25/06 | 0 | 0 |
| PRY-2026-15 | Talent Up Honduras | GOBDIG/DITRA | EnEjecucion | 20 | 1/5 | **(sin)** | 03/09 | 1/sí | 0 |
| PRY-2026-16 | Componente 3 SENPRENDE - constitución de empresas | GOBDIG/DITRA | EnEjecucion | 50 | 1/7 | **(sin)** | 16/07 | 1/sí | 0 |
| PRY-2026-17 | Portal de Digitalización de Trámites | GOBDIG/DITRA | Cerrado | 100 | 1/33 | Henry Ortez | 23/08 | 0 | 0 |
| PRY-2026-18 | Actualización del Portal de Encuestas - Madurez Institucional | GOBDIG/DITRA | Cerrado | 100 | 1/23 | Henry Ortez | 22/09 | 0 | 0 |
| PRY-2026-19 | SGSEC - Portal de solicitudes de certificados digitales | GOBDIG/DITRA | Cerrado | 100 | 1/10 | Henry Ortez | 23/08 | 0 | 0 |
| PRY-2026-20 | Data Center Gubernamental Unificado (BID 4942/BL-HO) | GOBDIG/DITRA | EnEjecucion | 40 | 1/5 | Henry Ortez | 23/08 | 0 | 0 |
| PRY-2026-21 | Diagnóstico de Madurez de Gobierno Digital - aplicación | GOBDIG/DITRA | Planificado | 0 | 1/5 | Henry Ortez | — | 0 | 0 |
| PRY-2026-22 | Encuesta ILIA - Índice Latinoamericano de IA | GOBDIG/DITRA | Cerrado | **93** | 1/7 | Henry Ortez | 23/08 | 0 | 0 |
| PRY-2026-23 | Portal Nacional de Datos Abiertos | GOBDIG/DITRA | EnEjecucion | 61 | 1/9 | Henry Ortez | 13/03 | 1/sí | 0 |
| PRY-2026-24 | SOL - CNI (Consejo Nacional de Inversiones) | GOBDIG/DITRA | EnEjecucion | 75 | 1/4 | Brizzio Zelaya | 07/08 | 0 | 0 |
| PRY-2026-25 | Ventanilla Única - Honduras Ágil | GOBDIG/DITRA | EnEjecucion | 93 | 1/7 | Jamil Garcia | 23/08 | 0 | 0 |
| PRY-2026-26 | SOL - INPREMA | GOBDIG/DITRA | EnEjecucion | 30 | 1/10 | Henry Cardona | 24/08 | 1/sí | 1 abierto |
| PRY-2026-27 | Implementación Portal de Gestión Digital | GOBDIG/DITRA | EnEjecucion | 72 | 3/13 | Henry Cardona | 18/09 | 0 (1 act. bloqueada por dep.) | 0 |
| PRY-2026-28 | SIGER - Control y estabilización | GOBDIG/DPD | Planificado | 31 | 14/8 | Maynor Coello Uriarte | 24/09 | 0 | 0 |
| PRY-2026-29 | DIGER Sitio web - Control y estabilización | GOBDIG/DPD | Planificado | **100** | 6/2 | Maynor Coello Uriarte | — | 0 | 0 |
| PRY-2026-30 | SNV - Integración - fase 1 | GOBDIG/DPD | Planificado | 50 | 3/4 | Maynor Coello Uriarte | — | 0 (1 act. bloqueada por dep.) | 0 |
| PRY-2026-31 | Simplificación de Trámites con IA | GOBDIG/DAI | EnEjecucion | 78 | 5/20 | Luis Carlos Flores Meza | 17/09 | 0 | 0 |
| PRY-2026-32 | Agente SIGER AI | GOBDIG/DAI | EnEjecucion | 88 | 5/22 | Luis Carlos Flores Meza | 17/09 | 0 | 0 |
| PRY-2026-33 | Sistema de Flujos | GOBDIG/DAI | EnEjecucion | 81 | 6/45 | Luis Carlos Flores Meza | 17/09 | 0 | 0 |
| PRY-2026-34 | Talent Up Plataforma Técnica | GOBDIG/DAI | EnEjecucion | 77 | 6/32 | Luis Carlos Flores Meza | 22/09 | 0 | 0 |
| PRY-2026-35 | Papito | GOBDIG/DAI | Planificado | 50 | 5/6 | Luis Carlos Flores Meza | — | 0 | 0 |
| PRY-2026-36 | Portal Talento Digital | GOBDIG/DAI | EnEjecucion | 70 | 5/12 | Luis Carlos Flores Meza | 17/09 | 0 | 0 |
| PRY-2026-37 | Receta Digital - Estabilización y expansión | GOBDIG/DPD | Planificado | 0 | 3/4 | Maynor Coello Uriarte | — | 0 | 0 |
| PRY-2026-38 | DIGER Sitio web - Mantenimiento | GOBDIG/DPD | Planificado | 0 (**sin estructura**) | 0/0 | Maynor Coello Uriarte | — | 0 | 0 |
| PRY-2026-39 | Instituto Nacional Agrario I semestre 2026 | SIGER/MSD | Planificado | 0 | 1/13 | (asignado) | — | 0 | 0 |
| PRY-2026-40 | Empresa Hondureña de Telecomunicaciones I semestre 2026 | SIGER/MSD | Planificado | 0 | 2/2 | (asignado) | — | 0 | 0 |

### 3.3 Los 11 proyectos de la imagen contra la base

| # | Imagen | En la base | Imagen: estado, avance, bloqueos, responsable | Base | ¿Coincide? |
|---|---|---|---|---|---|
| 1 | Billetera Electrónica Nacional (BIEN) | **No existe** | Finalizado, 100 %, 4, Cesar Maldonado | — | Falta |
| 2 | Receta Digital | PRY-2026-37 | En proceso, 63 %, 1, Maynor Coello | Planificado, 0 %, 0 bloqueos | No |
| 3 | Firma Electrónica Avanzada | PRY-2026-13 (quizá también PRY-2026-19 SGSEC, cerrado) | Finalizado, 100 %, 1, Cesar Maldonado | En ejecución, 30 %, 0, sin responsable, nunca reportado | No |
| 4 | SOL – Digitalización de trámites | **12 proyectos separados** «SOL - <institución>» (PRY-03 a 12, 24 y 26) | En proceso, 54 %, 9, Henry Ortez | Promedio simple 37.8 %; 10 bloqueos históricos y 5 vigentes; responsables Brizzio Zelaya, Henry Ortez, Henry Cardona o ninguno | No; además es una agrupación |
| 5 | Índice de Madurez Digital | PRY-2026-18 (portal de encuestas, cerrado 100 %) y PRY-2026-21 (aplicación, planificado 0 %) | En proceso, 76 %, 0, Henry Ortez | Dos proyectos distintos | No; agrupación |
| 6 | Conectividad | **No existe** (la unidad CONEC existe, sin proyectos) | No iniciado, 0 %, Mario Castejon | — | Falta |
| 7 | Honduras Fácil | PRY-2026-25 «Ventanilla Única - Honduras **Ágil**» (probable) | En proceso, 93 %, 3, Henry Ortez | En ejecución, 93 %, 0 bloqueos, Jamil Garcia | Solo el avance |
| 8 | Programa Oracle University | **No existe** | Sin dato, 20,000 becas, Alejandra | — | Falta |
| 9 | Talent Up | PRY-2026-15 (20 %) y PRY-2026-34 (77 %); quizá PRY-2026-36 Portal Talento Digital (70 %) | En proceso, 69 %, 6, Alejandra | Dos o tres proyectos; 1 bloqueo; sin responsable o Luis Carlos Flores Meza | No; agrupación |
| 10 | Validador de Firmas Online | **No existe** | Sin dato, Cesar Maldonado | — | Falta |
| 11 | Sistema de Flujos | PRY-2026-33 | En proceso, 98 %, 0, Luis Flores | En ejecución, 81 %, 0 | Estado y bloqueos sí; avance no |

Además, la base tiene unos 26 proyectos de GOBDIG que la imagen no muestra (Datos Abiertos,
Data Center, Agente SIGER AI, GEXFILE, SENPRENDE, sitios web, etc.). Es decir: **la «cartera» de
11 proyectos de la imagen no corresponde a ningún filtro que exista hoy** (ni área, ni unidad, ni
categoría, ni prioridad: 25 de los 37 de GOBDIG son prioridad Alta).

---

## 4. Problemas de calidad que afectan a este tablero

1. **Categoría sin uso:** 39 de 39 proyectos con `CategoriaId` NULL. Un filtro «Q3» hoy deja el
   tablero vacío.
2. **Responsable vacío:** 8 de 39 (21 %), todos de DITRA: SOL - FOSOVI, SENASA, ARSA, Instituto
   de la Propiedad, Firma Electrónica, GEXFILE, Talent Up Honduras, SENPRENDE. La línea del
   responsable saldría en blanco.
3. **Sin fecha de fin planificada:** 24 de 39 (62 %). No se puede decir «atrasado» ni calcular el
   esperado contra el plan en la mayoría.
4. **Estado incoherente con el avance:**
   - 5 proyectos «Planificado» con avance mayor que 0: ARSA 50, SIGER Control 31, Sitio web
     Control **100**, SNV 50, Papito 50. En el tablero saldrían como «No iniciado» con la barra a
     medias o llena.
   - 1 «Cerrado» con 93 % (Encuesta ILIA).
   - 3 «En ejecución» sin `FechaInicioReal`.
5. **Reportes desactualizados:** 24 de los 25 proyectos en ejecución llevan más de 30 días sin
   reporte (solo Talent Up Plataforma Técnica reportó después del 19/09); 10 de GOBDIG nunca
   reportaron. El avance en la base es del 24 al 28 de septiembre: un «Datos al» de octubre sería
   engañoso.
6. **Bloqueos como texto libre sin cierre:** nadie marca un bloqueo como resuelto. «Vigente» se
   infiere de que el último reporte lo mencione. La opción de vincular el bloqueo a un riesgo
   (`RiesgoId`) tiene 0 usos, y hay solo 2 riesgos en todo el portafolio. Los conteos de bloqueos
   dependen de cuánto reporta cada equipo.
7. **Programas partidos:** SOL son 12 proyectos, uno por institución, y Talent Up y Madurez son 2
   o 3 cada uno. No hay entidad «programa» ni proyecto padre. Para mostrar «SOL» como una sola
   fila habría que agrupar por nombre (frágil) o crear la agrupación (cambio de modelo).
8. **Estructura mínima heredada:** 25 proyectos tienen un único entregable de la carga inicial.
   Su avance es el promedio de las actividades de ese único entregable, y eso es válido.
9. **Acción sin clasificar:** 24 de 39 con `Accion` NULL. No afecta a la imagen, pero sí a
   cualquier filtro por tipo de proyecto.
10. **Avance guardado contra calculado:** coherente en los 39 (0 diferencias). Es el único
    indicador sin problemas.

---

## 5. Qué se puede calcular: hoy, a medias o no

| Indicador | Estado | Fuente o lo que falta |
|---|---|---|
| Total, por estado (dona), % del total | **Hoy** | `ProyectosDashboardDto.Total`, `PorEstado` |
| Filtro de cartera «Gobierno Digital» | **Hoy** | `AreaIds = ["GOBDIG"]` |
| Filtro «Q3» como categoría | Funciona, pero da 0 | Clasificar el portafolio (`CategoriaId`) |
| «Q3» como trimestre | **No** | Definir el criterio (fechas plan o real, o reportes del periodo) |
| Avance por proyecto con color por estado | **Hoy** | `Semaforo[].AvancePct`, `Estado` |
| «Sin dato» de avance | **Hoy** | `TotalEntregables == 0`, que ya viene en la proyección |
| Avance promedio con decimal y «N con dato» | **Hoy**, cálculo nuevo y trivial | Promedio de `AvancePct` donde hay estructura; no cambiar `ProyectoEstadoReglas.AvancePromedio`, que usa el operativo |
| Bloqueos vigentes por proyecto | **Hoy** | `ProyectosDashboardDto.Bloqueos` |
| Bloqueos históricos por proyecto | **Hoy**, consulta nueva y trivial | `COUNT` de `ProyectoAvances` con `Bloqueo` por proyecto |
| Actividades bloqueadas por dependencia | **Hoy** | `Bloqueadas` |
| Riesgos materializados o abiertos | **Hoy** | `ProyectoRiesgos` (casi vacío) |
| Responsable | **Hoy**, con 21 % vacío | `Proyectos.Responsable` |
| Línea de meta o alcance | **No** | Campo nuevo (migración y script) o mostrar el último reporte truncado |
| «N trámites · M instituciones» (SOL) | **A medias** | `ExpedienteTramites`; falta vincular cada proyecto SOL con sus expedientes |
| «Datos al» | **Hoy** | Ahora más `MAX(ProyectoAvances.Fecha)` |
| Agrupar SOL o Talent Up en una fila | **No** | Entidad o campo «programa», o proyecto padre |
| PDF de varias páginas | **A medias** | QuestPDF ya está; falta el servicio del informe |
| Avance esperado y desviación (P1 del diseñador) | **A medias** | Cálculo existente en `TableroProyectoQuery` (interpolación por ventana de actividad); hay que llevarlo al portafolio |

---

## 6. Qué reutilizar del código

- **`GetProyectosDashboardQuery`** (`src/Application/Dashboards/Queries/GetProyectosDashboardQuery.cs`).
  Ya hace en una sola pasada la proyección de proyectos con estado, prioridad, categoría,
  `AvancePct`, unidad, total de entregables y actividades, vencidos y último avance; además los
  bloqueos vigentes, las actividades bloqueadas por dependencia, `PorEstado` y los filtros
  `AreaIds`, `CategoriaId` y `PrioridadId`. Respeta los filtros globales de alcance. El ejecutivo
  puede **llamarla tal cual** y derivar lo suyo (promedio con decimal, «con dato», agrupación de
  la dona), o compartir su proyección. Solo falta el conteo histórico de bloqueos por proyecto, si
  se elige esa definición.
- **`ProyectoEstadoReglas`** (`Abierto`, `Atrasado`, `SinReportar`) y **`Etiquetas.Estado`**:
  para que el ejecutivo y el operativo digan lo mismo del mismo proyecto. El mapeo a
  Finalizado / En proceso / No iniciado debería ir aquí, no en la vista.
- **`GetTableroProyectoQuery`** (`src/Application/Proyectos/Queries/TableroProyectoQuery.cs`):
  cálculo del avance esperado por interpolación y criterio de «bloqueo vigente». Es el destino del
  clic desde cada fila (`/Tableros/Proyecto/{id}`).
- **`GetMisProyectosDashboardQuery`**: no aplica, es la vista personal.
- **Páginas:** `/Tableros/Proyectos`, `ProyectosArea` y `ProyectosUnidad` con
  `[Permission("Proyectos", Ver)]` y las pestañas `_TabsProyectos.cshtml`, que eligen la pestaña
  por `NivelAlcance` o `EsJefeDeArea`. El ejecutivo para jefaturas encaja en ese patrón.
- **PDF:** QuestPDF y `IProyectoPdfService` como modelo, si se confirma el informe de varias
  páginas.
- **Pruebas:** las de `GetProyectosDashboardQuery` en `tests/Application.Tests` sirven de plantilla.

---

## 7. Dudas para Jamil (bloquean la especificación)

1. **«Q3 2026»:** ¿categoría Q3 (ronda de clasificación, hoy 0 proyectos) o tercer trimestre? Si
   es trimestre, ¿con qué fecha se filtra?
2. **Cartera:** ¿«Gobierno Digital» es el área GOBDIG completa (37) o una selección de 11? Si es
   una selección, ¿con qué dato se marca (categoría Q3, un campo nuevo, una lista)?
3. **Los 4 proyectos que faltan** (BIEN, Conectividad, Oracle University, Validador de Firmas
   Online): ¿hay que darlos de alta en el portal? Sin eso no aparecerán.
4. **Programas:** ¿SOL, Talent Up e Índice de Madurez se muestran como una sola fila (agrupación
   nueva) o como sus proyectos separados?
5. **Bloqueos:** ¿histórico (como la imagen, premia a quien más reporta) o vigentes (regla ya
   existente; entonces el gráfico de barras no sirve)? ¿Se suman los riesgos materializados y las
   actividades bloqueadas por dependencia?
6. **Avance promedio:** ¿todos los proyectos con estructura, cerrados incluidos y con un decimal
   (como la imagen), o solo los que están en ejecución (como el operativo)?
7. **«Sin datos» como estado:** ¿qué significa? ¿Sin estructura, sin reportes o
   Suspendido/Cancelado?
8. **Línea de meta o alcance:** ¿se agrega un campo corto a la ficha (migración más script) o se
   muestra el último reporte truncado o el objetivo?
9. **Formato:** ¿página web en vivo, PDF de varias páginas o ambos?
10. **Datos:** ¿se pide a los responsables poner al día responsable (8 vacíos), estados
    incoherentes (5 planificados con avance y 1 cerrado al 93 %) y reportes antes de estrenar el
    tablero?
