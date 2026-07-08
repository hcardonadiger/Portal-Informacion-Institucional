# Prompt de Refactorización Arquitectónica y Preparación para Escalabilidad

## Contexto del Proyecto
Eres un desarrollador de software Senior y Arquitecto en .NET 9. Estás trabajando en el proyecto "DIGER Trámites Estado", el cual utiliza **Clean Architecture**, el patrón **CQRS (con MediatR)**, **Entity Framework Core**, y **Razor Pages** para su interfaz.
Para entender a la perfección la base funcional y los módulos del sistema, tu primer paso será leer el archivo: `Contextos/analisis_general.md`.

## Objetivo Principal
El objetivo de esta sesión es ejecutar una **refactorización arquitectónica profunda** para solventar toda la deuda técnica, problemas de seguridad y falta de optimización detectados. **Es estrictamente necesario que arregles la base arquitectónica antes de que intentemos escalar el programa.**

Debes basar tu trabajo en los hallazgos de `Contextos/diagnostico_arquitectura.md`. Además, debes asegurarte de que cada decisión arquitectónica que tomes ahora sea **100% compatible** y prepare el terreno para el plan de escalabilidad futuro detallado en `Contextos/prompt_escalabilidad.md` (jerarquía multi-institución).

---

## Tareas a Ejecutar (Por Fases)

### Fase 1: Front-end, Resiliencia y Seguridad Web
1. **Localización de Dependencias JS/CSS:**
   * Revisa `src\Web\Pages\Shared\_ValidationScriptsPartial.cshtml` y `_Layout.cshtml`.
   * Descarga cualquier librería (como `jquery`, `jquery.validate`, etc.) que dependa de un CDN externo (ej. `cdnjs`).
   * Guárdalos en `wwwroot/lib` y actualiza las referencias para que el aplicativo funcione offline o en intranets estrictas.
2. **Cabeceras de Seguridad (Security Headers):**
   * Configura en el pipeline de `Program.cs` las cabeceras de seguridad necesarias: `Content-Security-Policy (CSP)`, `X-Frame-Options` y `X-Content-Type-Options`.
   * Revisa que la protección Anti-CSRF esté correctamente configurada para el futuro uso intensivo de APIs/AJAX.

### Fase 2: Motor de Base de Datos (Entity Framework Core)
1. **Soft-Delete (Borrado Lógico) Universal:**
   * Crea una interfaz `ISoftDeletable` (`bool IsDeleted { get; set; }`).
   * Configura `AppDbContext` (en el método `OnModelCreating`) para agregar **Global Query Filters** a todas las entidades que implementen la interfaz (`.HasQueryFilter(e => !e.IsDeleted)`).
   * **COMPATIBILIDAD CON ESCALABILIDAD:** Asegúrate de implementar este filtro de una forma flexible. En el futuro cercano (ver *prompt_escalabilidad*), a este filtro se le anexará una condición de lectura por Institución/Área/Unidad (Row-Level Security).
   * Sobrescribe `SaveChangesAsync()` para interceptar operaciones `EntityState.Deleted`, cambiando su estado a `Modified` e igualando `IsDeleted = true`.
2. **Lecturas Optimizadas:**
   * Modifica los manejadores (Handlers) de los *Queries* (consultas de solo lectura) para asegurar que la llamada a EF Core utilice obligatoriamente `.AsNoTracking()`, mejorando dramáticamente el consumo de memoria.

### Fase 3: Modernización de Pipelines (MediatR) y Desacoplamiento
1. **Validación Centralizada (Fail-Fast):**
   * Configura FluentValidation.
   * Crea e inyecta un `ValidationBehavior` en el pipeline de MediatR para interceptar *Commands*. Si hay errores, la petición debe rebotar antes de llegar a la lógica de negocio.
2. **Caché en Memoria (`IMemoryCache`):**
   * Crea un `CachingBehavior` en MediatR que intercepte peticiones basadas en una interfaz `ICacheableQuery`. Úsalo para cachear los catálogos estáticos y evitar golpear la base de datos innecesariamente.
3. **Eventos de Dominio (Domain Events):**
   * Revisa que los `Commands` actuales no estén ejecutando tareas asíncronas pesadas (como envíos de correo) directamente en su Handler.
   * Mueve esas lógicas secundarias hacia `NotificationHandlers` escuchando eventos de dominio emitidos por la entidad base.

---

## Reglas de Ejecución y Restricciones
* **No implementes la Escalabilidad todavía:** Aunque debes leer `prompt_escalabilidad.md` para entender el futuro del código, **TU TRABAJO AHORA MISMO NO ES** crear las tablas de Institución/Área/Unidad. Tu trabajo es arreglar el código actual para que esté prístino y optimizado antes de recibir la carga estructural de la jerarquía.
* **Clean Code:** Prioriza la legibilidad. Usa constructores privados, métodos estáticos de creación (Factory Methods) y no uses setters públicos en las entidades de dominio.
* Aplica el patrón CQRS estrictamente: un *Command* muta estado, un *Query* devuelve estado. No los mezcles.
