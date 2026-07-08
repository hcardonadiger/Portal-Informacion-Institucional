# Análisis General - Portal DIGER Trámites Estado
*(Última actualización: 07-Jul-2026 — Post-refactorización arquitectónica)*

Este documento es la **fuente de verdad** del estado técnico y funcional del sistema. Está diseñado para ser leído al inicio de cualquier sesión de trabajo como sustituto del análisis del código fuente, ahorrando tokens y tiempo.

---

## 1. Stack Tecnológico Completo

| Capa | Tecnología | Detalle |
|------|-----------|---------|
| Framework | .NET 9 + Razor Pages | Server-Side Rendering; ideal para portales administrativos internos |
| Patrón | Clean Architecture + CQRS | 4 capas: `Domain → Application → Infrastructure → Web` |
| Mensajería | MediatR 12.4.1 | Todos los casos de uso son `IRequest<T>` manejados por `IRequestHandler` |
| Validación | FluentValidation 11.11 | Validadores por Command; `ValidationBehavior` los corre antes del handler |
| Persistencia | EF Core 9 + SQL Server | Migraciones gestionadas desde proyecto `Infrastructure` |
| Autenticación | Cookie Auth (ASP.NET Core) | Hash PBKDF2-SHA256; sin Identity framework |
| Caché | `IMemoryCache` (en proceso) | Para catálogos estáticos; integrado via `CachingBehavior` |
| Front-end JS | Vanilla JS + jQuery (local) | Sin frameworks SPA; jQuery 3.7.1 alojado localmente en `wwwroot/lib/jquery/` |
| Gráficas | Chart.js (local) | Alojado en `wwwroot/lib/chart/`; sin CDN externo |
| Generación QR | `QRCoder` library | Para tokens de auto-registro de asistentes en reuniones |

---

## 2. Estructura de Capas y Proyectos

```
src/
├── Domain/                         ← Entidades, interfaces, enums, eventos de dominio
│   ├── Common/
│   │   ├── BaseEntity.cs           ← BaseEntity<TId>, BaseAuditableEntity<TId>, DomainEvents, DomainException
│   │   └── ISoftDeletable.cs       ← Interfaz de Soft-Delete (bool IsDeleted)
│   ├── Entities/                   ← 19 clases de dominio (ver Sección 4)
│   └── Enums/Enums.cs              ← Todos los enums del sistema
│
├── Application/                    ← Casos de uso CQRS, interfaces, DTOs
│   ├── Common/
│   │   ├── Behaviors/PipelineBehaviors.cs   ← LoggingBehavior, ValidationBehavior, CachingBehavior
│   │   └── Interfaces/
│   │       ├── IRepositories.cs    ← Interfaces de repos + ICurrentUserService + IUnitOfWork + IApplicationDbContext
│   │       └── ICacheableQuery.cs  ← Marca Queries para caché automático (CacheKey + CacheDuration)
│   ├── DependencyInjection.cs      ← Registro de MediatR, FluentValidation, Behaviors, IMemoryCache
│   └── [Módulo]/                   ← Commands/, Queries/, Common/ por cada módulo
│
├── Infrastructure/                 ← Implementaciones: BD, repositorios, seguridad
│   ├── Persistence/
│   │   ├── AppDbContext.cs         ← DbContext con RLS + Soft-Delete + Auditoría automática
│   │   └── Repositories/           ← 6 repositorios concretos en un solo Repositories.cs
│   ├── Security/
│   │   ├── CurrentUserService.cs   ← Lee contexto activo de los Claims de la cookie
│   │   └── PasswordHasher.cs       ← PBKDF2-SHA256
│   ├── Migrations/                 ← 3 migraciones: InitialCreate, AddDigerInstitucionSeed, AddSoftDelete
│   └── DependencyInjection.cs      ← Registro de DbContext, Repos, Security
│
└── Web/                            ← Razor Pages, Middleware, Program.cs
    ├── Pages/                      ← Carpetas por módulo (Expedientes, Tickets, Reuniones, etc.)
    ├── Common/
    │   ├── ModuloAccesoMiddleware.cs  ← Bloquea URL directa por módulo según rol (RolModuloAcceso)
    │   └── AccesoModulosService.cs    ← Consulta BD para saber si el rol puede acceder al módulo
    ├── Program.cs                  ← Pipeline completo: Auth, Security Headers, Middleware
    └── wwwroot/lib/jquery/         ← jQuery 3.7.1 + Validate 1.21.0 + Unobtrusive 4.0.0 (LOCAL)
```

---

## 3. Módulos Funcionales y Rutas

| Módulo | Ruta base | Entidades principales | Descripción |
|--------|-----------|----------------------|-------------|
| **Expedientes** | `/` (raíz) y `/Expedientes` | `Expediente`, `ExpedienteTramite`, `TramiteRequisito`, `FlujoNodo`, `FundamentoLegal`, `DocumentoSolicitado`, `DocumentoInterno`, `InfraPerfil`, `InfraCondicion`, `InfraChecklistItem`, `ExpedienteSeccionEstado`, `ExpedienteEtapaAvance` | Wizard de 7 secciones para digitalizar trámites. Tiene un sistema de avance por sección (`EstadoSeccion`). |
| **Reuniones** | `/Reuniones` | `Reunion`, `Asistente`, `AcuerdoReunion` | Gestión de actas. Tiene visibilidad Pública/Privada. Enlace público de auto-registro de asistentes por token UUID + QR. Impresión PDF nativa. Genera contactos automáticamente. |
| **Tickets** | `/Tickets` | `Ticket`, `TicketComentario`, `TicketAdjunto`, `TicketTramite`, `TemaTicket`, `CategoriaTicket`, `UsuarioTema` | Help Desk interno. Ciclo: Abierto→En Progreso→Resuelto→Cerrado. Tiene SLA por tema (HorasResolucion), especialistas asignables por tema, adjuntos en comentarios. |
| **Contactos** | `/Contactos` | `Contacto` | Directorio institucional. Se puede poblar manualmente o se auto-genera al crear asistentes en reuniones (`OrigenContacto.Reunion`). |
| **Dashboards** | `/Tableros` | KPIs sobre Tickets, Expedientes, Reuniones | 4 queries de dashboard separadas, todas con `.AsNoTracking()`. Usa Chart.js local. |
| **Calendario** | `/Calendario` | Vista de Reuniones + Expedientes en formato mensual/semanal | Query unificada que cruza fechas de reuniones y expedientes. |
| **Instituciones** | `/Instituciones` | `Institucion`, `TramiteDefinicion`, `Area`, `Unidad` | Catálogo jerárquico. Solo Admin. Incluye el catálogo de trámites por institución. |
| **Usuarios** | `/Usuarios` | `Usuario`, `AsignacionUsuario`, `UsuarioTema` | CRUD de usuarios + asignación de rol/institución/área/unidad + temas de ticket que atiende. |
| **Accesos** | `/Accesos` | `RolModuloAcceso` | Admin configura qué módulos puede ver cada rol. Controlado por `ModuloAccesoMiddleware`. |

---

## 4. Modelo de Dominio — Entidades Clave

### Jerarquía Organizacional (Multi-institución)
```
Institucion (Id: VARCHAR PK — ej. "DIGER")
  └── Area (Id: VARCHAR PK — ej. "DIGER-TIC")
        └── Unidad (Id: VARCHAR PK — ej. "DIGER-TIC-DEV")
```
- `Institucion.Id`, `Area.Id` y `Unidad.Id` son **strings en mayúsculas sin espacios**, validados por regex `^[A-Z0-9]+$`.
- `Institucion` hereda de `BaseAuditableEntity<string>` (PK string, no int).

### Usuarios y Asignaciones
- `Usuario` hereda de `BaseAuditableEntity<Guid>` (PK GUID).
- `AsignacionUsuario`: conecta un Usuario con su posición en la jerarquía. Un usuario puede tener **múltiples asignaciones** (aparece un Combobox en el Navbar para cambiar de contexto).
- Roles válidos en `AsignacionUsuario.Rol`: `"JefeInstitucion"`, `"JefeArea"`, `"JefeUnidad"`, `"Empleado"`, `"Consultor"`.
- `UsuarioTema`: tabla de relación M:N entre `Usuario` y `TemaTicket` (especialidades del técnico de soporte).

### Entidades Transaccionales (con Soft-Delete)
Estas 4 entidades implementan `ISoftDeletable` (tienen columna `IsDeleted BIT NOT NULL DEFAULT 0`):
- `Expediente` → `BaseAuditableEntity<int>`, `ISoftDeletable`
- `Contacto` → `BaseAuditableEntity<int>`, `ISoftDeletable`
- `Reunion` → `BaseAuditableEntity<int>`, `ISoftDeletable`
- `Ticket` → `BaseAuditableEntity<int>`, `ISoftDeletable`

### Entidades Base del Dominio
```csharp
BaseEntity<TId>          // Id (protected set), DomainEvents (List<INotification>)
BaseAuditableEntity<TId> // + CreatedAt, CreatedBy, UpdatedAt, UpdatedBy (rellenados por AppDbContext)
ISoftDeletable           // bool IsDeleted { get; set; }
```

### Eventos de Dominio Declarados (en `BaseEntity.cs`)
```csharp
ExpedienteCreatedEvent(int ExpedienteId, string Codigo, string Institucion)
ExpedienteUpdatedEvent(int ExpedienteId)
ExpedienteDeletedEvent(int ExpedienteId, string Codigo)
ReunionCreatedEvent(int ReunionId, string Titulo)
ReunionUpdatedEvent(int ReunionId)
TicketCreatedEvent(int TicketId, string Numero, string Titulo)
TicketEstadoCambiadoEvent(int TicketId, string Numero, string Estado)
```
> La infraestructura de eventos existe (AddDomainEvent / ClearDomainEvents en BaseEntity), pero los NotificationHandlers que los consuman aún deben implementarse cuando se necesite lógica desacoplada (ej. envío de emails).

---

## 5. AppDbContext — Motor Central (Detalles Críticos)

**Archivo:** `src/Infrastructure/Persistence/AppDbContext.cs`

### Filtros Globales (RLS + Soft-Delete, FUSIONADOS)
El contexto evalúa el alcance institucional **una sola vez al crearse** (por DI Scoped):
```csharp
private readonly bool    _alcanceGlobal; // true si Administrador o sin sesión
private readonly string? _activeInst;    // ActiveInstitucionId del claim
private readonly string? _activeArea;    // ActiveAreaId del claim
private readonly string? _activeUnidad;  // ActiveUnidadId del claim
private readonly string? _activeRol;     // Rol activo del claim
private readonly Guid?   _usuarioId;     // Para filtro de reuniones privadas
```

Los filtros se aplican automáticamente sin necesidad de `.Where()` manual en ningún Query:

```
Expediente / Contacto / Ticket:
  !IsDeleted && (_alcanceGlobal || JefeInstitucion || JefeArea || JefeUnidad/Empleado/Consultor)

Reunion:
  !IsDeleted && (Publica-respetando-jerarquia || Privada-solo-el-creador)
```

**Para bypassear el filtro** (ej. conteos de admin o importación idempotente): usar `.IgnoreQueryFilters()`.

### SaveChangesAsync — Comportamiento Automático
El override hace **dos cosas antes de guardar**:
1. **Soft-Delete:** Cualquier entidad con `EntityState.Deleted` que implemente `ISoftDeletable` → cambia a `Modified` con `IsDeleted = true`. **Nunca hay DELETE físico para estas entidades.**
2. **Auditoría:** Para toda `BaseAuditableEntity` en `Added` → rellena `CreatedAt/By`; en `Modified` → rellena `UpdatedAt/By`. El nombre del actor se obtiene de `currentUser.Nombre ?? currentUser.Correo`.

---

## 6. Pipeline de MediatR (Orden Estricto)

```
Request → LoggingBehavior → ValidationBehavior → CachingBehavior → Handler → Response
```

| Behavior | Aplica a | Que hace |
|----------|----------|----------|
| `LoggingBehavior` | Todo request | Log de inicio/fin con tiempo en ms; captura y re-lanza excepciones con log de error |
| `ValidationBehavior` | Todo request con `IValidator<TRequest>` registrado | Corre FluentValidation; si hay errores lanza `ValidationException` antes de llegar al Handler |
| `CachingBehavior` | Solo requests que implementen `ICacheableQuery` | HIT → retorna sin tocar BD; MISS → ejecuta Handler, cachea con TTL definido en la Query |

**Para cachear un Query nuevo:** implementar `ICacheableQuery` en el record:
```csharp
public sealed record MiQuery : IRequest<MiDto>, ICacheableQuery
{
    public string CacheKey => "mi-cache-key";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(60);
}
```
**Catálogos actualmente cacheados:** `GetTemasActivosQuery` ("temas-activos", 60 min) y `GetCategoriasActivasQuery` ("categorias-activas", 60 min).

---

## 7. Sistema de Autenticación y Contexto de Usuario

### Cookie y Claims Personalizados
Al hacer login, se escriben los siguientes claims en la cookie de ASP.NET Core:

| Claim | Constante en `AppClaims` | Valor |
|-------|-----------|-------|
| `diger:uid` | `UserId` | GUID del usuario |
| `diger:rol` | `ActiveRol` | Rol activo (ej. "JefeInstitucion") |
| `diger:inst` | `ActiveInstitucion` | InstitucionId activa |
| `diger:area` | `ActiveArea` | AreaId activa (nullable) |
| `diger:unidad` | `ActiveUnidad` | UnidadId activa (nullable) |
| `diger:asignaciones` | `AsignacionesJson` | JSON con todas las asignaciones del usuario (para el Combobox) |

### Cambio de Contexto (Combobox en Navbar)
Cuando un usuario tiene más de una `AsignacionUsuario`, aparece un `<select>` en el Navbar. Al seleccionar, hace POST a `/Cuenta/CambiarContexto`, que **re-emite la cookie** con los nuevos claims del contexto seleccionado. El `AppDbContext` Scoped se recrea en el siguiente request con los nuevos filtros RLS activos.

### `CurrentUserService`
Lee todos los claims del `IHttpContextAccessor`. Propiedades clave:
- `EsGlobal` → true si no autenticado o `Rol == "Administrador"` (acceso a todo sin filtros RLS)
- `PuedeAccederInstitucion(id)` → valida que el contexto activo corresponda a la institución indicada
- `InstitucionesAsignadas` → deserializa `AsignacionesJson` para obtener la colección completa

---

## 8. Seguridad Web (Configurada en Program.cs)

### Security Headers (middleware inline, ANTES de UseStaticFiles)
```
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline'; 
  style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'
```
> La CSP usa `'unsafe-inline'` porque Razor Pages inyecta scripts inline. Se puede endurecer con nonces cuando los formularios migren a Fetch/HTMX.

### Token Anti-CSRF para Peticiones AJAX
En `_Layout.cshtml` hay un meta tag con el RequestVerificationToken:
```html
<meta name="csrf-token" content="@token" />
```
El JS puede leerlo con `document.querySelector('meta[name="csrf-token"]').content` y enviarlo en la cabecera `RequestVerificationToken` en peticiones Fetch/HTMX futuras.

### Control de Acceso a Módulos por URL
`ModuloAccesoMiddleware` (ejecutado después de `UseAuthorization`) mapea la ruta a un módulo (`ModulosPortal.*`) y consulta `RolModuloAcceso` en BD para verificar si el rol activo tiene permiso. Si no, redirige a `/Cuenta/AccessDenied`. La tabla `RolModuloAcceso` es administrada por el Admin desde `/Accesos`.

---

## 9. Base de Datos — Estructura y Scripts

### BD de desarrollo
- Nombre: `DigerTramitesEstado_Dev` (SQL Server Express)
- Migraciones EF Core aplicadas hasta: `20260707154325_AddSoftDelete`
- Proveedor EF: `Microsoft.EntityFrameworkCore.SqlServer:9.0.0`

### Scripts en carpeta `database/`
| Archivo | Proposito |
|---------|-----------|
| `CreateDatabase.sql` | Script idempotente para crear BD desde cero. Incluye bloque final de `ALTER TABLE` para agregar `IsDeleted` a BD existentes. |
| `Esquema_EFCore_Actual.sql` | Volcado completo del esquema al 06-Jul-2026 (ya incluye `IsDeleted` en Contactos, Expedientes, Reuniones, Tickets). |
| `EsquemaBackUp.sql` | Backup del esquema anterior a la migración de escalabilidad (referencia histórica). |
| `MigracionDatos.sql` | Script para migrar datos desde el esquema antiguo (Supabase/JSON) al relacional actual. |

### Agrupación de Tablas
- **Jerarquía:** `Institucion`, `Area`, `Unidad`
- **Usuarios:** `Usuarios`, `AsignacionesUsuario`
- **Catálogos:** `Movimientos`, `Prefijos`, `TramitesDefinicion`
- **Transaccionales (ISoftDeletable):** `Expedientes`, `Contactos`, `Reuniones`, `Tickets`
- **Hijos de Expediente:** `ExpedienteSecciones`, `ExpedienteEtapaAvances`, `ExpedienteTramites`, `TramiteRequisitos`, `FlujoNodos`, `FundamentosLegales`, `DocumentosSolicitados`, `DocumentosInternos`, `InfraPerfiles`, `InfraCondiciones`, `InfraChecklist`
- **Hijos de Reunion:** `Asistentes`, `AcuerdosReunion`
- **Hijos de Ticket:** `TicketComentarios`, `TicketAdjuntos`, `TicketTramites`, `TemasTicket`, `CategoriasTicket`, `UsuarioTemas`
- **Seguridad:** `RolModuloAccesos`

---

## 10. Convenciones y Patrones de Código (Reglas Estrictas)

### Entidades de Dominio
- **Constructor privado + Factory Method estático:** `Crear(...)` es el único punto de entrada. Nunca usar `new Entidad()` desde fuera del dominio.
- **Sin setters públicos** en datos controlados por lógica de negocio. Usar métodos descriptivos (`Renombrar()`, `Activar()`, `EstablecerAsignacion()`).
- Las colecciones hijas son `private readonly List<T>` expuestas como `IReadOnlyCollection<T>`.
- Los tipos de PK varían: `Institucion/Area/Unidad` → `string`, `Usuario/AsignacionUsuario` → `Guid`, el resto → `int`.

### CQRS Estricto
- **Command**: muta estado (usa `IUnitOfWork` + `SaveChangesAsync`). Devuelve solo el ID del registro creado o `Unit`. Nunca devuelve datos de negocio completos.
- **Query**: devuelve datos. **Obligatoriamente** usa `.AsNoTracking()`. Nunca llama a `SaveChangesAsync`.

### Repositorios
6 repositorios concretos en `src/Infrastructure/Persistence/Repositories/Repositories.cs` (un solo archivo):
`ExpedienteRepository`, `InstitucionRepository`, `ContactoRepository`, `ReunionRepository`, `TicketRepository`, `UsuarioRepository`.

Los repositorios pueden usar `.IgnoreQueryFilters()` cuando necesitan datos globales (ej. para importación idempotente).

### Manejo de Archivos Adjuntos
`AdjuntoStorage.cs` en `Web/Common/`: guarda archivos en `wwwroot/uploads/` con nombre UUID. **Limitación conocida:** no compatible con load balancing horizontal (evaluar `IFileStorageService` + Azure Blob para futuro).

---

## 11. Enums del Sistema (referencia rápida)

| Enum | Valores |
|------|---------|
| `EstadoExpediente` | EnExploracion, EnLevantamiento, EnModelado, EnValidacion, Cerrado |
| `EstadoLevantamientoExp` | EnProceso, Completo, PendienteDeValidar, RequiereRevisita |
| `EstadoSeccion` | Pendiente, EnProgreso, Completo, Validado |
| `AccionRequisito` | Mantener, Simplificar, Digitalizar, Eliminar |
| `FaseFlujo` | Actual, Propuesto |
| `TipoNodoFlujo` | Inicio, Paso, Decision, Fin |
| `OrigenContacto` | Manual, Reunion |
| `VisibilidadReunion` | Publica, Privada |
| `EstadoCompromiso` | Pendiente, EnProgreso, Cumplido, Reprogramado, Cancelado |
| `EstadoTicket` | Abierto, EnProgreso, Resuelto, Cerrado |
| `PrioridadTicket` | Baja, Media, Alta, Critica |
| `TipoComentarioTicket` | Comentario, CambioEstado, Asignacion |
| `InfraStatus` | Pendiente, Cumple, NoCumple, Parcial, NoAplica |
| `TipoDocumento` | Acta, Informe, Instructivo, Presentacion, Memorando, VideoTutorial, Resolucion, Formato, Otro |

---

## 12. Estado de Implementación y Deuda Técnica

### Implementado y estable
- Clean Architecture + CQRS con MediatR completo
- RLS (Row-Level Security) con Global Query Filters por jerarquía Inst/Area/Unidad
- Soft-Delete (borrado logico) en las 4 entidades transaccionales
- Pipeline de Validacion centralizada (FluentValidation + ValidationBehavior)
- Pipeline de Cache (CachingBehavior + ICacheableQuery) para catalogos estaticos
- Auditoria automatica (CreatedAt/By, UpdatedAt/By) en SaveChangesAsync
- `.AsNoTracking()` en todos los Queries y Dashboards de solo lectura
- Security Headers HTTP (CSP, X-Frame-Options, X-Content-Type-Options)
- jQuery + dependencias JS alojadas localmente (sin CDN externo)
- Meta tag Anti-CSRF para peticiones AJAX futuras
- Control de acceso a modulos por URL (ModuloAccesoMiddleware + tabla RolModuloAcceso)
- Jerarquia Institucion → Area → Unidad en BD y entidades de dominio
- Combobox de cambio de contexto jerarquico en Navbar
- Importacion idempotente desde Supabase (Expedientes y Reuniones)

### Pendiente de implementar (proximas fases)
- **`NotificationHandlers` para Domain Events:** Los eventos estan declarados pero sin handlers. Se deben crear para logica de notificacion/email desacoplada del Command.
- **`IFileStorageService`:** Los adjuntos se guardan en `wwwroot/uploads/`. Para soportar multiples instancias, mover a Azure Blob Storage / NAS.
- **Endurecer CSP:** Reemplazar `'unsafe-inline'` con nonces cuando los formularios migren a Fetch/HTMX.
- **UI completa de Escalabilidad:** Falta el modulo de UI para gestion de Areas/Unidades, asignacion multiple de usuarios, y el rol `Consultor` con restricciones en UI (la estructura de BD ya esta).
