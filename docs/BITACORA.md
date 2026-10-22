# Bitácora — Gestión Digital (DIGER)

Registro de trabajo del proyecto. Es la fuente para los informes a jefaturas:
se escribe en lenguaje claro y **sin datos sensibles** (ni contraseñas, ni cadenas
de conexión, ni datos personales de ciudadanos).

---

## Registro por fecha

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
- Tablero ejecutivo de proyectos (2026-10-19, rama `DashBoards`). Sin cambios de base de datos.

## En curso
- (nada abierto)

## Pendiente
0. **Tanda 1**, según el plan del Analista. La primera entrega es de seguridad: las fallas S1 a S5.
1. Decidir el siguiente paso de tableros: rediseñar los tableros marcados
   (Proyectos operativo, Reuniones, Tickets, Digitalización, Trámites, Expedientes)
   o unificar los tres tableros de Proyectos por alcance. Decide Jamil.
2. Crear `docs/ux/GUIA-UX.md` a partir de la aplicación existente
   (`DESIGN.md` y `.claude/agents/references/lengua-de-casa-diger.md` como base).

## Descartado
- (nada registrado)

## Para después
- (nada registrado)

## Decisiones
- 2026-10-19 — Jamil: Expedientes, SIGER y Recursos siguen activos; la entrega de
  tableros se limita al tablero ejecutivo.

## Bugs
- 2026-10-19 — Panel del usuario transparente en tema oscuro. Causa: el panel no
  tenía fondo propio para el tema oscuro. Solución: fondo con token de panel.
- 2026-10-19 — Avatar invisible en tema oscuro. Causa: una regla más específica
  (`.user-menu-btn .side-avatar`) dejaba azul sobre azul. Solución: fondo celeste
  con texto oscuro en tema oscuro.

## Riesgos y bloqueos
- **Contraseña del correo saliente versionada** desde el commit `b709609` (2026-08-07). Está en
  `dev` y en varias ramas remotas, y el registro de eventos del servidor puede guardar tokens de
  recuperación. Necesita: que la jefatura la cambie, y la decisión de Jamil sobre limpiar el
  historial.
- **Documentos de Expedientes sin descarga.** La subida guarda una dirección `/uploads/...` que
  ya no se sirve desde el 2026-08-26, así que el enlace probablemente no abre. No está reproducido
  todavía.
- **Respaldo de base de datos dentro del repositorio** (`TramitesEstado.bak`,
  13 MB, versionado desde el commit `2d8fa08`). Puede contener datos reales. Sacarlo
  del repositorio requiere decisión de Jamil; borrarlo del historial implicaría
  reescribirlo, cosa que no se hace sin aprobación.
- **`appsettings.json` del Web apunta a una base que ya no existe en local**
  (`GestionGD_TEST`). Cualquier arranque fuera de `Development` falla en la primera
  consulta. El valor real de producción se configura en el servidor.
- **Dos migraciones inertes** (`20260719000000_AddPreregistroAsistente`,
  `20260719100000_AddInstitucionBranding`): una base creada desde cero con las
  migraciones quedaría sin tres columnas que el modelo usa. Las bases en uso sí
  las tienen.
- **El host `Presentation`** sigue apuntando a bases `DigerTramitesEstado*` que no
  corresponden a las del Web.
