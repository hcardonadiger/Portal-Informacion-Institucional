---
trigger: glob
globs: src/**/*.cs
---

# Convenciones de Arquitectura y CQRS (.NET 9)

## Entidades de Dominio
- Usar constructor privado y Factory Method estático `Crear(...)` obligatoriamente.
- Prohibidos los setters públicos en propiedades de negocio.
- Colecciones hijas deben ser `private readonly List<T>` expuestas como `IReadOnlyCollection<T>`.

## CQRS con MediatR
- **Commands:** Mutan estado (`IUnitOfWork` + `SaveChangesAsync`). Retornan SOLO el ID generado (`int`/`Guid`) o `Unit`. JAMÁS devuelven entidades o DTOs completos.
- **Queries:** Solo lectura. Es OBLIGATORIO incluir `.AsNoTracking()` en todas las consultas EF Core. NUNCA ejecutan `SaveChangesAsync`.

## Persistencia y Seguridad
- Respetar `ISoftDeletable`. NUNCA generar comandos `DELETE` físicos para estas entidades.
- No agregar `.Where()` manuales para filtros de organización (Institución/Área) a menos que se use `.IgnoreQueryFilters()`; RLS ya está configurado globalmente en `AppDbContext`.