---
description: 
---

# Workflow: Crear Caso de Uso CQRS en Clean Architecture

1. **Dominio (`src/Domain`)**: Verifica si se requiere un nuevo evento o método en la Entidad correspondiente.
2. **Aplicación (`src/Application`)**:
   - Crea el `Command` o `Query` (usar `record`).
   - Si es Command: Crea su `Validator` con FluentValidation.
   - Si es Query cacheada: Implementa `ICacheableQuery`.
   - Crea el `Handler` implementando `IRequestHandler`.
3. **Infraestructura (`src/Infrastructure`)**: Actualiza el repositorio en `Repositories.cs` solo si requiere un método no genérico.
4. **Web (`src/Web/Pages`)**: Crea/actualiza la Razor Page (`.cshtml` y `.cshtml.cs`) e integra MediatR (`_mediator.Send(...)`).
5. **Verificación**: Ejecuta `dotnet build` usando el Terminal Subagent para asegurar que no haya errores de compilación.