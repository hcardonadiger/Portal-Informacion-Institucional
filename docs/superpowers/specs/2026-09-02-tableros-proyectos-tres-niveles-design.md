# Tableros de Proyectos en tres niveles — diseño

**Estado:** aprobado para plan de implementación
**Fecha:** 2026-09-02

## Problema

`Proyectos.Ver` da hoy acceso a todo el portafolio interno o a nada — no existe
un filtro de alcance real como el que sí tiene `Expedientes` vía `NivelAlcance`
en `AppDbContext`. El único tablero que existe (`/Tableros/Proyectos`) es denso
y fue pensado para quien quiere ver todo el detalle; no sirve como pantalla de
un jefe de unidad o de área, que necesitan algo liviano y acotado a lo suyo.

Se necesitan tres vistas — Unidad, Área, Institución — y una forma real (no
solo visual) de que un jefe de área/unidad no pueda ver proyectos ajenos,
salvo las excepciones que el propio módulo ya reconoce.

## Fuera de alcance

La paginación configurable (10 por defecto, ajustable) en listas largas del
portal es un proyecto aparte, sin relación de código con este. Se aborda en su
propio spec.

## Mecanismo de acceso

**No se agrega un filtro de alcance nuevo en `AppDbContext`.** El acceso a un
proyecto sigue mediado por lo que el módulo ya tiene: ser **Interesado** o
**Responsable** da visibilidad al proyecto completo (`InteresadoProyecto`,
2026-08-24) y, según el rol dentro del proyecto, permite quedar a cargo de
entregables y actividades. Lo que se agrega es que ciertas personas quedan
como interesados **automáticos y bloqueados** (no removibles desde la ficha),
en vez de depender de que alguien las agregue a mano.

### Dos capacidades nuevas en `Roles`

Junto a `EsAdministrador`, `EsSoloLectura`, `EsSupervisor`, `EsTecnicoSoporte`
(tabla `Roles`, administrados en `/Accesos/Roles`), se agregan:

- `EsJefeDeArea` — quien tenga un rol con esta capacidad, dentro de un área,
  queda como interesado automático de **todos los proyectos de esa área**
  (`Proyecto.AreaId` igual a la suya — sin importar si tienen `UnidadId` o
  no: un proyecto transversal del área también le corresponde).
- `EsPmo` — mismo mecanismo pero a nivel de unidad: interesado automático de
  todo proyecto cuyo `Proyecto.UnidadId` sea la suya.

Se identifican por capacidad, no por nombre de rol — el mismo principio que ya
sigue el resto del portal (`RolesModule`, `PermissionCatalogSyncService`): un
admin puede renombrar el rol sin romper la sincronización.

### `InteresadoProyecto.Automatico`

Columna nueva, `bool`, por defecto `false`. Las filas que crea la
sincronización automática la traen en `true`. `QuitarInteresadoCommand` (`Application/Proyectos/Commands/InteresadoCommands.cs`)
rechaza la operación con
`DomainException` cuando `Automatico == true` — el mensaje explica que esa
persona sale sola cuando deja de tener el rol, no se quita a mano.

Mapeo a `RolInteresado` (no se agrega un valor nuevo al enum):

- Jefe de área → `Patrocinador` («respalda y decide»; encaja con supervisar
  sin ejecutar).
- PMO → `Ejecutor` («hace el trabajo»; encaja con «accionar sobre el
  proyecto» — PMO sí puede quedar a cargo de entregables/actividades).

### Disparadores de la sincronización

Un servicio de aplicación (`SincronizarInteresadosAutomaticosService` o
nombre equivalente que decida el plan de implementación) corre en dos puntos:

1. **Al crear un proyecto con `AreaId`/`UnidadId`**: agrega como interesados
   automáticos a quien tenga `EsJefeDeArea` en esa área y a quien tenga
   `EsPmo` en esa unidad, si existen.
2. **Al crear, editar o eliminar una `AsignacionUsuario`** (cambia de rol,
   gana/pierde `EsJefeDeArea`/`EsPmo`, o cambia de área/unidad): resincroniza
   las filas automáticas de ese usuario en los proyectos afectados — agrega
   las que le tocan nuevas, borra las que ya no aplican. Si dos personas
   comparten el rol con la capacidad en la misma área/unidad, ambas quedan
   como interesados automáticos — no se asume una sola persona por área.

Si el área o la unidad de un proyecto cambian después de creado, el mismo
servicio corre otra vez para ese proyecto puntual (agrega lo nuevo, retira lo
que ya no corresponde).

### Coordinadores y directores ejecutivos

Ven todo el portafolio. No se crea una capacidad nueva para esto: se resuelve
con lo que el portal ya tiene — un rol con `NivelAlcance = Institucion` o
`Global` (el mismo campo que ya gobierna el alcance en el resto del portal) o
`EsAdministrador`. Si los roles de Coordinador/Director Ejecutivo no están
configurados así hoy, es una tarea de configuración en `/Accesos/Roles`, no
de código nuevo.

## Las tres vistas

Todas viven bajo `/Tableros/Proyectos`, con pestañas para moverse entre las
que a cada quien le tocan — nadie ve una pestaña a la que no tiene acceso.

| Vista | Quién la ve | Qué muestra |
|---|---|---|
| **Unidad** | Cualquiera con `Proyectos.Ver` (siempre disponible) | Proyectos donde la persona es interesado o responsable — su vista natural de trabajo. |
| **Área** | `EsJefeDeArea` | Agregado de todas las unidades del área — para no tener que entrar unidad por unidad. |
| **Institución** | `NivelAlcance` Institución/Global, o `EsAdministrador` | El portafolio completo, filtrable por una o varias áreas a la vez (SIGER, Gobierno Digital, ambas, todas). |

**Vista por defecto**: la más amplia a la que la persona tenga acceso
(Institución > Área > Unidad) — quien tiene más alcance normalmente lo quiere
ver primero; puede bajar a las más específicas desde las pestañas.

### Contenido: Área y Unidad livianas, Institución con el detalle de hoy

El tablero actual (`/Tableros/Proyectos`, ver `Proyectos.cshtml`) tiene ~11
tarjetas KPI y cinco tablas separadas (semáforo, actividades vencidas,
bloqueadas, entregables, bloqueos). Eso se conserva **tal cual para la vista
Institución** — es el tablero que ya existe hoy, con el filtro de área
agregado encima.

Área y Unidad son versiones reducidas, pensadas para no cargar a alguien que
solo necesita el panorama:

- 4 tarjetas KPI: total de proyectos, avance promedio, atrasados, sin
  reportar 30+ días.
- Uno o dos gráficos (avance por unidad dentro del área / distribución por
  estado — a definir el detalle exacto en el plan).
- **Una sola tabla resumida al final** (no las cinco de hoy): proyecto,
  responsable, estado, avance, próxima fecha que vence — lo mínimo para saber
  qué exige atención, con enlace a la ficha del proyecto para el detalle.

## Testing

- Application.Tests: la sincronización automática (alta, cambio de rol,
  cambio de área/unidad, dos personas con la misma capacidad, intento de
  quitar un interesado automático que debe rechazarse).
- Domain.Tests: la nueva regla en `InteresadoProyecto`/el comando de
  eliminación.
- Verificación manual en navegador de las tres vistas y el cambio de pestaña
  según capacidades del rol de la sesión de prueba.

## Preguntas que quedan para el plan de implementación (no bloquean el spec)

- Nombre exacto de la clase del servicio de sincronización nuevo (`QuitarInteresadoCommand`
  ya existe con ese nombre en `InteresadoCommands.cs` y es donde se agrega el rechazo).
- Gráfico(s) exactos de Área/Unidad — el spec fija el principio (uno o dos,
  ligeros), el plan decide cuáles con el detalle de datos disponible.
- Migración de EF para las columnas nuevas (`Roles.EsJefeDeArea`,
  `Roles.EsPmo`, `InteresadoProyecto.Automatico`).
