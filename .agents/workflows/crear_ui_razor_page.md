# Flujo de Trabajo: Crear UI (Razor Pages)

Este flujo te ayuda a crear páginas frontend (Razor Pages) siguiendo las convenciones de UI establecidas para este proyecto.

## Pre-requisitos
1. Lee `Contextos/analisis_general.md` para entender las reglas de seguridad web (Sección 8).
2. Familiarízate con `src/Web/wwwroot/css/tokens.css` y `src/Web/wwwroot/css/diger.css` para utilizar las clases base del diseño.

## Pasos

### Paso 1: Crear la Página
Crea el modelo (`PageModel.cs`) y la vista (`.cshtml`) en `src/Web/Pages/[Modulo]/`.
- El modelo debe inyectar `IMediator` para ejecutar comandos y queries.
- Utiliza `.AsNoTracking()` en las consultas si las haces directamente, o preferiblemente, utiliza un caso de uso (Query) vía MediatR.

### Paso 2: Diseño y Layout
- Asegúrate de que el `.cshtml` referencie correctamente el layout global (o el módulo asume el layout por defecto en `_ViewStart.cshtml`).
- Usa clases CSS como `.diger-card`, `.diger-btn`, `.diger-input` en lugar de estilos hardcodeados o de frameworks externos no autorizados.

### Paso 3: Seguridad Anti-CSRF
- Si la página contiene llamadas AJAX/Fetch que modifican el estado (POST/PUT/DELETE), asegúrate de capturar el token del meta tag `<meta name="csrf-token" content="..." />` presente en el layout principal, e inclúyelo en los Headers de tu petición (`RequestVerificationToken`).

### Paso 4: Revisión Funcional
- Valida que la UI se comporte de manera responsiva.
- Asegúrate de que la navegación y la experiencia de usuario (micro-animaciones) mantengan el estándar premium esperado.
