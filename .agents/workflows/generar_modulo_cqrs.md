# Flujo de Trabajo: Generar Módulo CQRS

Este flujo te guiará para generar un nuevo caso de uso (Command o Query) siguiendo la arquitectura estricta del proyecto (Clean Architecture + CQRS con MediatR).

## Pre-requisitos
1. Lee `Contextos/analisis_general.md` para entender las reglas arquitectónicas (especialmente las secciones 6 y 10).
2. Asegúrate de tener claro:
   - El nombre de la Entidad de Dominio afectada (ej. `Expediente`).
   - La acción a realizar (ej. `CrearExpediente`, `GetExpedientesQuery`).
   - Si es un Command (muta estado) o un Query (solo lectura, requiere `.AsNoTracking()`).

## Pasos

### Paso 1: DTO y/o Record
Crea el archivo correspondiente en la carpeta `src/Application/[Modulo]/Commands/[Accion]/` o `src/Application/[Modulo]/Queries/[Accion]/`.
- Si es un Command, define un `record` que implemente `IRequest<TResponse>`.
- Si es un Query y debe ser cacheable, asegúrate de que implemente `ICacheableQuery`.

### Paso 2: Handler
Crea la clase `Handler` en el mismo archivo (o en uno separado `[Accion]CommandHandler.cs`).
- Inyecta `IApplicationDbContext` u otras interfaces desde `src/Application/Common/Interfaces/IRepositories.cs`.
- Usa el Factory Method estático de la entidad (`Entidad.Crear(...)`) y nunca `new Entidad()`.
- **Importante:** Un Command siempre debe llamar a `SaveChangesAsync(cancellationToken)` al final. Un Query nunca lo hace.

### Paso 3: FluentValidation
Si el caso de uso requiere validación, crea una clase `[Accion]CommandValidator` que herede de `AbstractValidator<TCommand>`.
- Las reglas de validación se ejecutarán automáticamente gracias al `ValidationBehavior` del pipeline.

### Paso 4: Pruebas y Revisión
Verifica que las referencias sean correctas, que no estés mutando estado en un Query, y que el DTO de respuesta no exponga entidades del dominio directamente.
