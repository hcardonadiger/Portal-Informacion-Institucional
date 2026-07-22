# Flujo de Trabajo: Auditoría y Actualización de Contexto

Este flujo automatiza el descubrimiento de funcionalidades no documentadas en el proyecto y sugiere actualizaciones al documento base.

## Cuándo usar este flujo
Ejecuta este flujo periódicamente (ej. cada dos semanas) o después de un ciclo de desarrollo intensivo donde múltiples agentes o desarrolladores hayan modificado el código de forma paralela.

## Pasos del Agente

### Paso 1: Revisar el Historial de Git
- Ejecuta `git log -n 20 --stat` para revisar los últimos commits e identificar qué archivos (especialmente entidades de dominio y páginas web) han sido añadidos o modificados significativamente.

### Paso 2: Escanear Entidades de Dominio
- Lista el directorio `src/Domain/Entities`.
- Compara la lista de archivos con las entidades detalladas en `Contextos/analisis_general.md` (Sección 4).
- Si hay nuevas entidades, abre y lee su código fuente para entender su propósito.

### Paso 3: Escanear Módulos Web
- Lista los directorios `src/Application` y `src/Web/Pages`.
- Busca carpetas o módulos que no estén listados en la Sección 3 de `Contextos/analisis_general.md`.

### Paso 4: Proponer Actualizaciones
- Crea un `implementation_plan.md` listando las nuevas características, entidades o flujos descubiertos.
- Presenta tus hallazgos al usuario para que te aclare dudas de negocio utilizando `/grill-me` (si es necesario).
- Actualiza `Contextos/analisis_general.md` incorporando las adiciones aprobadas por el usuario.
