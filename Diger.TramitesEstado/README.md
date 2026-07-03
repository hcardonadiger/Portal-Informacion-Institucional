# DIGER — Expedientes de Digitalización · .NET 9 · Clean Architecture · CQRS · SQL Server

Portal interno DIGER para el **Expediente de Digitalización** de trámites por institución.
Portado del formulario `expediente_digitalizacion.html` a Clean Architecture
(.NET 9 · EF Core · MediatR · Razor Pages · autenticación por cookie).

> **Nota:** este módulo **reemplazó** al antiguo `Levantamiento` (y su flujo de asignación/
> resultados/seguimiento). Se conservan los usuarios, el login por cookie y los roles.

---

## Módulo: Expediente de Digitalización

Asistente de 7 secciones, todo persistido en SQL Server del portal:

1. **Apertura** — institución, fecha, analista, dirección, contacto y trámites a modelar (1–10).
2. **Ficha del trámite** (por trámite) — datos generales, tiempos y costos (incl. **pago TGR** con
   catálogo SEFIN institución→rubros), objetivo/alcance, requisitos, descripción y contacto.
3. **Marco legal y documentación** — fundamentos legales y documentos solicitados.
4. **Proceso actual** — métricas de línea base, documentos internos y **constructor de flujos**
   (nodos inicio/paso/decisión/fin, con diagrama SVG).
5. **Infraestructura SOL** — perfiles técnicos, data center, checklist de requerimientos mínimos.
6. **Modelo propuesto** — flujo propuesto, comparación **antes/después** y acción por requisito
   (eliminar / simplificar / digitalizar / mantener).
7. **Estado del expediente** — estado general, por sección, y validaciones de cierre.

El estado del expediente (`EnExploración → EnLevantamiento → EnModelado → EnValidación → Cerrado`)
alimenta el historial.

**Importación desde el portal demo (Supabase)** — `Admin/ImportarExpedientes` (solo Administrador y
solo en `Development`) lee el blob JSON de expedientes del demo (`diger_tram.key = 'expedientes'`, un
array con la forma `OriginalExpedienteDto`) y los crea reutilizando el pipeline del editor
(`OriginalShapeMapper.ToInput → CrearExpedienteCommand`). Es **idempotente**: `Expediente.OrigenExternoId`
guarda `institución|_ts` de origen (índice único filtrado) y los ya importados se omiten. El analista
vacío se completa con `(Sin asignar)` para satisfacer la validación. Como el formulario del demo no
limpiaba la ficha al cambiar de trámite (arrastraba la del primero), el importador **des-duplica**:
vacía los campos de ficha de cada trámite que sean idénticos al anterior (conserva nombre, requisitos
y flujos, que sí son propios). El editor .NET no tiene ese arrastre (usa un juego de campos por trámite).

---

## Módulo: Reuniones y Asistencias

Acta de reuniones y capacitaciones, persistida en SQL Server. Formulario por secciones
(`Reuniones/Editor`) con model-binding estándar de Razor (no JSON), agregado de captura `Reunion`
con hijos reemplazados en bloque:

- **Datos generales** — título, fecha/hora/duración, modalidad, tipo, institución beneficiaria, lugar
  y bandera *“capacitación de la plataforma”* (muestra el bloque de detalle de capacitación).
- **Memoria** — objetivo/agenda y desarrollo.
- **Enlace institucional** y **Facilitador DIGER**.
- **Asistentes** — tabla dinámica (alta/baja de filas); el Nº de asistentes se cuenta de los registros.
- **Resultados** — convocados, % asistencia, satisfacción, compromisos.
- **Acuerdos** — compromiso, responsable y plazo (tabla dinámica).
- **Validación y evidencias** — validaciones DIGER/institución, documentos y fotos (URL).

Al guardar, los asistentes con correo **alimentan el Directorio de Contactos** (`ContactoFeeder`,
deduplicado por correo, `Origen = Reunion`). Gestión limitada por la policy `PuedeGestionarReuniones`
(**Administrador**/**Coordinador**); **Técnico** solo consulta.

**Editor en pasos (wizard)** — `Reuniones/Editor` es un asistente de 5 pasos (Generales · Asistencia ·
Memoria · Capacitación · Cierre, este último paso de capacitación se muestra según la bandera). Incluye:
**editor de texto enriquecido** (contenteditable, negrita/cursiva/subrayado/listas, sin librerías) en
*Desarrollo* —guarda HTML—; **% de asistencia automático** (asistentes/convocados); campos **"Otro"**
para modalidad/tipo; y **subida de fotos** a carpeta local (`wwwroot/uploads/reuniones`, JPG/PNG/WEBP/GIF
≤5 MB, ruta guardada en `Foto*Url`). El alcance institucional limita el selector de institución.

**Acta imprimible** — `Reuniones/Acta` renderiza la reunión como documento de una sola lectura
(solo muestra campos con contenido, con líneas de firma) y exporta a PDF con 🖨 (`window.print()`
+ `@@media print`). Accesible para cualquier usuario autenticado desde el listado o el editor.

**Importación desde el portal demo (Supabase)** — `Admin/ImportarReuniones` (solo Administrador y
solo en `Development`) lee las tablas `reuniones` + `asistencias` del portal demo vía la API REST de
Supabase (`Supabase:Url` / `Supabase:AnonKey` en `appsettings.Development.json`) y las crea en SQL
Server. El blob JSON `tema` de cada reunión se mapea con `ReunionImportMapper` a `ReunionFormDto` +
asistentes + acuerdos; los asistentes alimentan el Directorio de Contactos. Es **idempotente**:
`Reunion.OrigenExternoId` guarda el id de origen (índice único filtrado) y las ya importadas se omiten,
por lo que puede ejecutarse varias veces sin duplicar. (Los expedientes del demo **no** estaban en
Supabase, así que no hay datos de expedientes que migrar desde esa fuente.)

---

## Módulo: Tickets de soporte

Mesa de ayuda para **incidencias de la plataforma SOL**. Cada ticket (`Ticket` + `TicketComentario`)
tiene número correlativo (`TCK-2026-0001`), categoría (acceso, error de plataforma, configuración,
datos, capacitación, otro), prioridad (baja/media/alta/crítica) y **flujo de estados**
`Abierto → En progreso → Resuelto → Cerrado`. Vínculos **opcionales** a institución y expediente,
datos del reportante y asignación a un usuario interno.

- **Listado** (`Tickets/Index`) con filtro por estado; badges de estado/prioridad.
- **Editor** (`Tickets/Editor`, policy `PuedeGestionarTickets` = Admin/Coordinador) — alta/edición de
  la incidencia y sus vínculos.
- **Detalle** (`Tickets/Detalle`) — vista de trabajo: cambia estado (con nota), asigna usuario y registra
  **comentarios**. El historial (cambios de estado y asignaciones) se guarda como comentarios del sistema.
- **Roles:** Admin/Coordinador crean, asignan y cierran; **Técnico** atiende y resuelve los tickets
  **asignados a él** (y comenta); todos consultan. Asignar un ticket abierto lo pasa a *En progreso*.
- **Filtros** por estado, prioridad, institución y **“Mis tickets”** (asignados al usuario actual).

## Módulo: Usuarios

Gestión de usuarios internos DIGER desde la UI (`Usuarios/Index` · policy `PuedeAdministrarUsuarios`,
solo **Administrador**). Alta con contraseña inicial (hash PBKDF2), edición de nombre/correo/rol,
activar-desactivar y **restablecer contraseña**. Correo único validado. Antes solo se sembraban en runtime.

### Alcance institucional (autorización por fila)

Cada usuario se asigna a una o más **instituciones** (`UsuarioInstitucion`, multi-select en su editor).
Ese alcance restringe lo que puede ver y gestionar en **Tickets, Expedientes, Reuniones y Contactos**:

- **Administrador**: acceso global (sin restricción).
- **Coordinador / Técnico**: solo registros de **sus instituciones asignadas**; el Técnico puede
  **crear/editar** dentro de su alcance (eliminar sigue siendo Admin/Coordinador).
- **Lectura**: se aplica con un **filtro global de EF Core** por institución (alimentado por los claims
  de la sesión) sobre las 4 entidades — cubre listas y detalles a la vez; un registro fuera de alcance
  responde 404.
- **Escritura**: validación de alcance al crear (`PuedeAccederInstitucion`) y dropdowns de institución
  limitados al alcance. Las consultas de unicidad/secuencia (número de ticket, código de expediente,
  dedup de contactos/import) usan `IgnoreQueryFilters()`.
- El alcance viaja en los **claims** de la cookie: los cambios de asignación toman efecto al
  **volver a iniciar sesión**.

> **Nota de operación:** tras habilitar el alcance, los usuarios Coordinador/Técnico **sin instituciones
> asignadas no ven registros**. El Administrador debe asignarles instituciones desde `Usuarios/Editor`.

---

## Tableros (dashboards)

Página de inicio del portal (`Tableros/Index`, landing tras el login). Cuatro tableros con KPIs y
gráficos de barras CSS (sin dependencias externas), **acotados por el alcance institucional** del
usuario (un técnico ve solo las métricas de sus instituciones):

- **Resumen general** — tickets críticos/abiertos, acuerdos vencidos/próximos, expedientes cerrados,
  atajos y últimos tickets.
- **Tickets** — conteos por estado, prioridad, categoría e institución; tickets abiertos más antiguos.
- **Expedientes** — por estado (Exploración→Cerrado), por institución, total de trámites, % completado.
- **Reuniones / acuerdos** — por tipo e institución, asistentes, y **seguimiento de acuerdos** con
  plazo (vencidos / próximos), enlazados a su acta.

Las queries de dashboard usan `IApplicationDbContext`, por lo que heredan el filtro global por
institución automáticamente; los conteos de acuerdos/asistentes se hacen vía la navegación `Reuniones`
para respetar también ese alcance.

**Enriquecimiento (Chart.js):** gráficos de **dona** (estado/prioridad/categoría/tipo) y de **línea/área**
de **tendencia a 12 meses** (tickets creados vs resueltos, reuniones y expedientes por mes); **KPIs con
tendencia ▲▼** (mes actual vs anterior) y **tiempo promedio de resolución** + % resueltos de tickets;
**filtros** por institución y rango de fechas; y **drill-down** (clic en una dona/segmento de tickets lleva
a la lista ya filtrada por estado/prioridad). **Chart.js está auto-hospedado** en
`wwwroot/lib/chart/chart.umd.min.js` (no CDN), para no depender de red externa en el entorno interno.

---

## Estructura (resumen)

```
src/
├── Domain/Entities/        ← Expediente (raíz) + Tramite, Requisito, FlujoNodo, Legal,
│                              DocumentoSolicitado/Interno, Infra (Perfil/Condicion/Checklist),
│                              SeccionEstado · Reunion (+ Asistente, AcuerdoReunion) ·
│                              Contacto · Usuario · Institucion
├── Application/
│   ├── Expedientes/         ← Crear/Actualizar/Eliminar + GetById/GetList/GetCatalogos (CQRS)
│   ├── Reuniones/           ← Crear/Actualizar/Eliminar + GetById/GetList · ContactoFeeder
│   ├── Tickets/             ← Crear/Actualizar/CambiarEstado/Asignar/Comentar/Eliminar (CQRS)
│   ├── Contactos/ · Instituciones/
│   ├── Usuarios/            ← Autenticar (login) + Crear/Actualizar/RestablecerPassword + GetList/GetById
│   └── Common/Catalogs/     ← TgrCatalog (SEFIN) · InfraCatalog
├── Infrastructure/         ← EF Core (AppDbContext + configs), repos, PasswordHasher (PBKDF2),
│                              CurrentUserService, DbSeeder (usuarios)
└── Web/                    ← Razor Pages: Index (historial) · Expedientes/Editor (wizard) ·
                               Reuniones/ · Tickets/ · Contactos/ · Instituciones/
                               + wwwroot/js/expediente.js · css/expediente.css · css/diger.css
```

### Puente UI ↔ dominio

El sidebar del wizard agrupa las secciones por **ámbito**: *Generales del expediente* (Apertura, Marco
legal, Infraestructura, Estado) y *Por trámite* (Ficha, Proceso actual, Modelo propuesto — con pestañas
de trámite). El editor (`expediente.js`, portado del HTML original) serializa el formulario a JSON y lo envía al
handler de `Expedientes/Editor`. `OriginalShapeMapper` (en Web) traduce esa forma ⇄ `ExpedienteInputDto`,
y `ExpedienteMapper` (Application) mapea el DTO ⇄ entidad. Se eliminó Supabase: todo persiste en SQL Server.

---

## Módulo: Instituciones (catálogo)

Mantenimiento del catálogo (`Instituciones/Index` · `Editor`, solo Administrador): nombre/sigla, estado
activo y **trámites definidos** (uno por línea). El editor muestra además un panel de **relaciones de la
institución** con los conteos de **expedientes, tickets (y abiertos), reuniones, contactos, usuarios
asignados y trámites**, con enlaces a las vistas filtradas (tablero de expedientes/reuniones y lista de
tickets por institución). El listado incluye esas columnas de conteo. La **eliminación está protegida**:
si la institución tiene expedientes, tickets, reuniones, contactos o usuarios asignados, no se puede borrar
(se indica qué la bloquea) — debe desactivarse. Los conteos usan `IgnoreQueryFilters` (vista global de Admin).

## Identidad y roles

- Autenticación por **cookie** + hash **PBKDF2-SHA256** (`PasswordHasher`); auditoría `CreatedBy/UpdatedBy`.
- Roles: **Administrador** y **Coordinador** crean/editan/eliminan expedientes; **Técnico** solo consulta.
- Usuarios sembrados en runtime (`DbSeeder`):

| Correo | Contraseña | Rol |
|--------|-----------|-----|
| admin@diger.gob.hn | `Admin#2026` | Administrador |
| coordinador@diger.gob.hn | `Coord#2026` | Coordinador |
| tecnico@diger.gob.hn | `Tecnico#2026` | Técnico |

---

## Ejecutar

```bash
dotnet run --project src/Web
```

En `Development` aplica migraciones y siembra usuarios automáticamente. Entra con el coordinador
para crear un expediente nuevo.

### Migraciones

```bash
dotnet ef migrations add <Nombre> --project src/Infrastructure --startup-project src/Web
dotnet ef database update          --project src/Infrastructure --startup-project src/Web
```

### appsettings.json (fragmento)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=DigerTramitesEstado;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

---

## Próximos pasos sugeridos

1. **Evidencias como archivos** — subir fotos/documentos (reuniones y expediente) a Azure Blob / S3
   en lugar de solo URL.
2. **Autocompletado de asistentes/enlace** desde el Directorio de Contactos (hoy el flujo es
   Reunión → Contactos; falta el sentido inverso en el formulario de reuniones).
3. **Seguimiento de acuerdos** — vista/tablero de acuerdos pendientes por responsable y plazo.
4. **Exponer Expediente y Reuniones en la API** (`Presentation`) además del Web (Razor Pages).
5. **Endpoints de catálogo TGR cacheados** (SEFIN).
6. **Legal/Docs/Proceso por-trámite** (hoy a nivel de expediente, como en el formulario original).
> Hecho: ✅ Reuniones + Acta PDF · ✅ Tickets de soporte (+ filtros) · ✅ Gestión de usuarios ·
> ✅ Catálogo de instituciones completo · ✅ Alcance institucional por usuario (autorización por fila) ·
> ✅ Tableros (resumen + tickets + expedientes + reuniones/acuerdos).

### Estado de la migración desde el portal demo (Supabase)

| Origen (`gnpvqiyantdtksfvqtqk`) | Destino | Estado |
|---|---|---|
| `reuniones` + `asistencias` (14 + 80) | Reuniones / Asistentes | ✅ importado (idempotente) |
| `diger_tram.expedientes` (2) | Expedientes (IHADFA, CONSUCOOP) | ✅ importado (idempotente) |

Ambos importadores corren contra **Dev** desde `Admin/Importar*` (solo Administrador, solo `Development`).
La institución de cada reunión se muestra desde el **snapshot** (`Reunion.Institucion`), por lo que las
no catalogadas (SENASA, FOSOVI, SESAL…) se ven correctamente en listado y acta aunque no tengan `InstitucionId`.

**Pendiente (requiere acción del usuario):** replicar a **producción** (necesita la cadena de conexión y
`Supabase:*` en `appsettings.Production.json`) · **cerrar las políticas RLS** del proyecto Supabase
(hoy las tablas son legibles de forma anónima con la anon key pública).
