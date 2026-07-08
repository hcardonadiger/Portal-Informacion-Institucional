# Nuevas Funcionalidades y Valor Agregado (Roadmap Propuesto)

Estas son propuestas de módulos o características **completamente nuevas** que actualmente no existen en el sistema, pero que aportarían un inmenso valor a la operativa diaria de la institución (DIGER), facilitando la vida de analistas y administradores.

## 1. Buscador Global Inteligente (Tipo "Omnibox")

*   **Concepto:** Una barra de búsqueda siempre visible en el menú superior de la aplicación (accesible también mediante un atajo de teclado como `Ctrl + K` o `Cmd + K`).
*   **Funcionamiento:** Al escribir, el sistema realiza una búsqueda global asíncrona que retorna resultados categorizados:
    *   *Tickets:* Búsqueda por número de ticket (ej. "TCK-0012") o problema ("Error de acceso").
    *   *Expedientes:* Búsqueda por el nombre del trámite o la institución.
    *   *Contactos:* Búsqueda por nombre de un funcionario o su correo.
*   **Valor Agregado:** Permite a los técnicos y coordinadores navegar instantáneamente al registro que buscan sin tener que navegar por múltiples pantallas o usar filtros en tablas complejas.

## 2. Sistema de Notificaciones In-App y Recordatorios por Correo

*   **Concepto:** Un centro de notificaciones proactivo para el usuario.
*   **Funcionalidad In-App:** Un ícono de campana (🔔) en la cabecera que informe en tiempo real (usando *SignalR*) de eventos clave:
    *   "Te han asignado el Ticket TCK-2026-0004".
    *   "El acuerdo 'Entregar documentos legales' vence mañana".
    *   "Un analista ha modificado el estado del expediente a 'En Validación'".
*   **Funcionalidad por Correo:** Un servicio en segundo plano (Background Worker o Hangfire) que envíe correos electrónicos automáticos (usando plantillas HTML generadas con Razor) notificando asignaciones críticas o resúmenes semanales a los Coordinadores.

## 3. Módulo de Auditoría Avanzada (Activity Tracker / Timeline)

*   **Concepto:** Aunque actualmente hay trazabilidad de "Creado Por/Modificado Por", en sistemas gubernamentales suele ser necesario saber **qué** se modificó exactamente.
*   **Funcionamiento:** Implementar un *Entity Framework Interceptor* que, de forma transparente, registre en una tabla de `AuditoriaLogs` cada cambio (UPDATE/INSERT/DELETE) sobre tablas clave (Expedientes, Tickets). El log debe guardar:
    *   Usuario que hizo el cambio.
    *   Fecha y Hora.
    *   Campo modificado: Ej. "Cambió Estado de 'En Progreso' a 'Resuelto'".
    *   Valor anterior vs. Valor nuevo (en formato JSON).
*   **Interfaz (Timeline):** Mostrar este log en la vista de detalle del Ticket o Expediente como una "Línea de tiempo" visual (estilo GitHub o Jira), brindando contexto histórico total a cualquier analista que tome el caso.

## 4. Exportación Dinámica y Reportería (Business Intelligence)

*   **Concepto:** Expandir los Dashboards actuales permitiendo la descarga y análisis externo.
*   **Funcionamiento:**
    *   Agregar un botón **"Exportar a Excel (.xlsx)"** o CSV en todos los listados (Tickets, Expedientes, Contactos). Usando librerías como *ClosedXML* o *EPPlus*, se genera el archivo sin necesidad de instalar Office en el servidor.
    *   **Reportes Consolidados:** Un módulo nuevo en "Tableros" para generar reportes en PDF multi-página de la productividad general del mes, listos para presentar a la gerencia.

## 5. Módulo de Autocompletado Cruzado Inteligente

*   **Concepto:** Actualmente, el sistema alimenta el "Directorio de Contactos" cuando se crean reuniones, pero según el README no se retroalimenta al crear nuevas.
*   **Funcionamiento:** En el formulario de *Reuniones* y en *Tickets*, cuando un usuario empiece a teclear el nombre de un Asistente, Reportante o "Enlace Institucional", sugerir automáticamente perfiles desde la tabla `Contacto`.
*   **Valor Agregado:** Previene errores de tipeo, reduce drásticamente el tiempo de ingreso de datos y evita duplicidad de personas en la base de datos (con diferentes correos o nombres mal escritos).
