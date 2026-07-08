# Diagnóstico Arquitectónico y Plan de Optimización - Portal DIGER

Este documento engloba un análisis profundo del código actual (Program.cs, Views, Arquitectura CQRS) y expande las propuestas previas, definiendo las mejores prácticas necesarias para convertir este aplicativo en una base robusta, segura y altamente escalable.

---

## 1. Análisis de Dependencias Externas (UI/Frontend)

Para que el sistema sea estable, resiliente y cumpla con estándares de redes restringidas o gubernamentales, no debe depender del acceso a internet externo para funcionar.

*   **Hallazgo Crítico:** En el archivo `src\Web\Pages\Shared\_ValidationScriptsPartial.cshtml`, se están consumiendo librerías desde CDNs externos:
    *   `jquery.min.js` (cdnjs.cloudflare.com)
    *   `jquery.validate.min.js`
    *   `jquery.validate.unobtrusive.min.js`
*   **Acción Requerida:** 
    1.  Descargar estos tres archivos JS.
    2.  Alojar los archivos localmente dentro de `src\Web\wwwroot\lib\jquery\`.
    3.  Cambiar las referencias en las vistas para apuntar a los archivos locales (ej: `~/lib/jquery/jquery.min.js`).
*   **Modernización Asíncrona:** Tal como se planteó, se debe reemplazar el envío sincrónico de formularios (Postback) por **HTMX** o **Fetch API** (Vanilla JS). Esto evitará las recargas completas de página (Full Page Reloads) especialmente en los "Wizards" (Paso a Paso) y agregará la capacidad de **Autoguardado en Borrador**.

---

## 2. Optimizaciones de Rendimiento (Backend y EF Core)

Al usar SQL Server y Entity Framework Core como motores del sistema, la eficiencia en las consultas dictará la velocidad del portal.

*   **`.AsNoTracking()` por Defecto en Consultas (Queries):** 
    *   Al usar CQRS, todas las operaciones de lectura (Queries) que no van a modificar datos (ej: cargar el Tablero, mostrar el listado de Expedientes o Instituciones) deben incluir explícitamente `.AsNoTracking()` en EF Core. Esto reduce el consumo de memoria del servidor web a más de la mitad, ya que EF no crea instantáneas para rastrear cambios.
*   **Caché Distribuida o en Memoria (`IMemoryCache`):**
    *   Las listas que rara vez cambian (Instituciones, Catálogos externos, Tipos de Documentos, Temas de Tickets) no deben golpear la base de datos en cada *request*. Se debe inyectar `IMemoryCache` para almacenar estos catálogos en memoria por horas o días.
    *   *Súper Mejora:* Implementar esto de forma invisible creando un **Pipeline Behavior en MediatR** (Ej: `CachingBehavior`). Esto permite cachear cualquier Query simplemente agregándole una interfaz `ICacheableQuery`.
*   **Almacenamiento de Archivos Fuera del Servidor Web:**
    *   Guardar archivos adjuntos o fotos de perfil en la carpeta `wwwroot` limita la capacidad de balancear la carga (Load Balancing / Escalabilidad Horizontal). Si se despliegan 2 servidores, un archivo guardado en el servidor A no será visto por el servidor B.
    *   *Solución:* Implementar un `IFileStorageService` en la capa *Infrastructure* que suba los archivos a un servicio Cloud (Azure Blob Storage, S3) o a un servidor de archivos en red (NAS), y en base de datos guardar solo el URL o Ruta.

---

## 3. Seguridad, Auditoría y Resiliencia

*   **Cabeceras HTTP de Seguridad (Security Headers):**
    *   Actualmente `Program.cs` no configura cabeceras de defensa. Se debe crear o instalar un Middleware (como `NetEscapades.AspNetCore.SecurityHeaders`) para inyectar:
        *   `Content-Security-Policy (CSP)`: Previene ataques Cross-Site Scripting (XSS).
        *   `X-Frame-Options: DENY`: Evita ataques de Clickjacking (que el portal sea embebido en un iframe malicioso).
        *   `X-Content-Type-Options: nosniff`.
*   **Tokens Anti-CSRF en Peticiones AJAX:**
    *   Razor Pages protege los `<form>` nativos inyectando un token oculto. Cuando migremos las funciones de guardado a peticiones asíncronas (HTMX/Fetch), es vital configurar el Javascript para que intercepte y envíe la cabecera `RequestVerificationToken` al servidor, evitando vulnerabilidades CSRF.
*   **Soft-Delete (Borrado Lógico) Obligatorio:**
    *   Para evitar la pérdida de información crítica por errores humanos, **ningún registro transaccional debe borrarse usando `DELETE` en SQL**. 
    *   Se debe implementar una interfaz `ISoftDeletable` (propiedad `IsDeleted`).
    *   Se debe configurar Entity Framework Core (`OnModelCreating`) para agregar automáticamente un **Global Query Filter** (`.HasQueryFilter(e => !e.IsDeleted)`).
    *   Se sobrescribe el método `SaveChangesAsync()` para que, si detecta un borrado (`EntityState.Deleted`), cambie el estado a `Modified` y ponga `IsDeleted = true`.

---

## 4. Modernización de Arquitectura (Patrones y Buenas Prácticas)

*   **Pipelines de Validación Centralizada:**
    *   En lugar de escribir `if (string.IsNullOrEmpty(...))` dentro de los manejadores (Handlers), se debe usar **FluentValidation**.
    *   Configurar un `ValidationBehavior` en MediatR que intercepte cualquier *Command* entrante, corra las validaciones de FluentValidation, y si algo falla, devuelva los errores inmediatamente sin llegar a ejecutar el Command. Esto mantiene el código de negocio sumamente limpio.
*   **Desacoplamiento Absoluto mediante Eventos de Dominio:**
    *   El código base ya tiene infraestructura para eventos (`BaseEntity.AddDomainEvent`). Es hora de sacarle provecho.
    *   *Caso de uso:* Cuando un Ticket es creado, en lugar de poner el código que envía el email directamente en el `CrearTicketCommandHandler`, el comando solo guarda el ticket y emite un `TicketCreatedEvent`. Un manejador separado asíncrono (`TicketCreatedEventHandler`) capturará el evento y mandará el email. Si el correo falla, la base de datos no sufre un rollback y la aplicación no se cuelga.
*   **Strongly-Typed IDs (IDs Fuertemente Tipados):**
    *   *Prevención de errores humanos:* Al cambiar los IDs de las tablas a `VARCHAR` (como acordamos para Institución, Área, etc.), es muy fácil equivocarse de orden al llamar un método `Crear(string institucionId, string areaId)`. 
    *   *Recomendación:* Se puede evaluar el uso de ValueObjects o Records en C# para envolver los IDs (`public record InstitucionId(string Value)`). De esta forma, el compilador arroja un error si intentas pasar un Área donde se requiere una Institución.
