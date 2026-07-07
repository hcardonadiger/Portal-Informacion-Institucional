# Mejoras y Optimizaciones a Funcionalidades Existentes

Tras analizar la arquitectura y el flujo del sistema, se proponen las siguientes mejoras enfocadas en optimizar el rendimiento, refactorizar deuda técnica y mejorar sustancialmente la Experiencia de Usuario (UX). Estas mejoras aplican a lo que el sistema *ya hace actualmente*.

## 1. Mejoras en Interfaz y Experiencia de Usuario (UI/UX)

*   **Autoguardado (Auto-save) en el Wizard de Expedientes:**
    *   **Problema Actual:** El expediente consta de 7 secciones densas. Si la sesión expira o el navegador se cierra, el usuario pierde el trabajo no guardado del paso actual.
    *   **Mejora:** Implementar un guardado en segundo plano (vía un endpoint AJAX/Fetch ligero) cada 'N' minutos o al detectar inactividad, guardando el progreso temporalmente o como un borrador (Draft).
*   **Modernización de la Subida de Evidencias:**
    *   **Problema Actual:** La subida de fotos (en Reuniones) se guarda en la carpeta local `wwwroot` y usa un `<input type="file">` tradicional.
    *   **Mejora:** Integrar un área de *Drag & Drop* (arrastrar y soltar) usando Vanilla JS o librerías ligeras. Además, arquitectónicamente, migrar el almacenamiento físico de `wwwroot` a un servicio en la nube (Azure Blob Storage o Amazon S3) y almacenar solo la URL en SQL Server. Esto facilita escalar la aplicación web en múltiples servidores (web farms) sin perder archivos.
*   **Guardado Asíncrono en Formularios Complejos (Reuniones):**
    *   **Problema Actual:** El Razor Page de Reuniones utiliza un model-binding estándar. Si el usuario agrega 20 asistentes y hay un error de validación en el servidor al enviar (submit), la página debe recargarse completa.
    *   **Mejora:** Cambiar las interacciones de las tablas dinámicas (Alta/Baja de asistentes y acuerdos) a peticiones HTMX o Fetch/JSON, validando cada fila sin recargar la página entera, haciendo la UI mucho más fluida.

## 2. Refactorización Arquitectónica y Backend

*   **Implementación de Caché (Distributed/Memory Cache) para Catálogos:**
    *   **Problema Actual:** Si se consulta el Catálogo TGR de SEFIN constantemente, o el propio catálogo de Instituciones, se están haciendo llamadas a la base de datos repetitivas para datos que casi nunca cambian.
    *   **Mejora:** Implementar `IMemoryCache` o Redis (`IDistributedCache`) en la capa de `Infrastructure` o `Application` para guardar en memoria las Instituciones Activas y los Catálogos. Esto reducirá drásticamente la carga sobre SQL Server y acelerará los tiempos de carga de los formularios.
*   **Eventos de Dominio (Domain Events) para Desacoplar Lógica:**
    *   **Problema Actual:** Cuando un ticket cambia de estado o se asigna, la lógica de registro (historial/auditoría) probablemente esté acoplada en el mismo Command Handler. Si mañana se desea enviar un correo cuando un ticket se asigne, el Handler principal se volverá lento.
    *   **Mejora:** Aprovechar que el proyecto ya tiene **MediatR**. Cuando un `Ticket` sea asignado, la Entidad debe emitir un evento `TicketAsignadoEvent`. Un manejador en segundo plano (Notification Handler) atrapará este evento e insertará el registro histórico, preparando el terreno para el envío de correos asíncronos.
*   **Separación de API vs UI (Preparación para el futuro):**
    *   **Mejora:** El README sugiere exponer datos en una API. Crear un proyecto nuevo `Diger.TramitesEstado.WebApi` en la capa de Presentación. Debido a que se usa CQRS/MediatR, los controladores de la API simplemente inyectarán `IMediator` y reutilizarán exactamente la misma lógica de negocio (`Commands`/`Queries`) que hoy usan los Razor Pages, logrando reusabilidad total sin duplicar código.

## 3. Seguridad y Mantenimiento

*   **Soft-Delete (Borrado Lógico) Generalizado:**
    *   Actualmente el README indica que una institución no puede borrarse si tiene referencias (buena práctica relacional). Sin embargo, para tablas como `Expedientes` o `Reuniones`, sugeriría implementar una interfaz `ISoftDeletable` (que agregue un campo `IsDeleted`). Así, al eliminar un registro, solo se oculta mediante el filtro global (Global Query Filter), previniendo pérdida de información sensible.
