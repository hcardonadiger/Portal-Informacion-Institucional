---
trigger: glob
globs: src/Web/**/*
---

# Convenciones de Front-end y Razor Pages

- PROHIBIDO usar CDNs externos (FontAwesome, Bootstrap CDN, jQuery CDN). Usar librerías locales ubicadas en `wwwroot/lib/`.
- Cualquier llamada Fetch/AJAX en JS debe incluir el encabezado CSRF `RequestVerificationToken` obtenido del meta tag layout.
- Mantener la separación de responsabilidades: Lógica de servidor en PageModel (`.cshtml.cs`), presentación en `.cshtml`.