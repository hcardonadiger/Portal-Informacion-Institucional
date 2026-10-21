# Fase 0 — Decisiones (lado PortalDigital)

Cierre de la Fase 0 del plan `2026-08-13-plan-api-portaldigital-hondurasagil.html`,
**solo del lado de PortalDigital** (Diger.TramitesEstado). El trabajo del lado de
HondurasÁgil (Fase 1b, Fase 5, Fase 6) queda fuera de este documento.

## Entorno de ensayo (P-08)

**Resuelto.** En vez de restaurar un respaldo aparte, se usa la base local ya
existente en esta máquina (LocalDB, `TramitesEstado_Prod`), confirmada por
DIGER como copia de desarrollo, no la base de producción real. La Fase 1 se
ensaya ahí antes de tocar cualquier entorno compartido.

## Preguntas cerradas

| # | Pregunta | Decisión | Dueño |
|---|---|---|---|
| P-01 | Instituciones del piloto (corte 1) | **INPREMA, IHTT, CONSUCOOP** — propuesta técnica del plan, aceptada. | DIGER |
| P-02 | Autenticación de la API | **Clave estática por cliente** (`X-Api-Key`), sobre el hueco ya existente en `PortalDigitalOptions.ApiKey`. | Infraestructura + DIGER |
| P-05 | Categoría: ¿1:N o N:N? | **1:N** (`TramitesSiger.CategoriaId`), igual a como ya la modela el sistema consumidor. Más barato de mantener; cambiarlo después implica tabla puente y reescribir el filtro. | DIGER + técnico |
| P-07 | Columnas de `?busqueda=` | **Nombre + Descripcion + Objetivo.** Las tres necesitan la corrección de colación del script F — no dejar el bug de tildes a medias. | Técnico |
| P-08 | Entorno de ensayo | Base local existente, confirmada como copia de desarrollo (ver arriba). | Infraestructura |

## Preguntas todavía abiertas (no bloquean la Fase 1)

| # | Pregunta | Bloquea | Nota |
|---|---|---|---|
| P-03 | ¿Dónde viven los secretos de la API (la `ApiKey`)? | Fase 4 | Ver M-07: hoy hay credenciales en texto plano en `appsettings.json`; la clave de la API no puede nacer igual. |
| P-04 | Política de fecha de última revisión cuando `UpdatedAt` es viejo | Fase 7 | Se diluye con el piloto (todas las fichas del corte 1 se tocan), pero hay que fijarla antes del corte 2. |
| P-06 | ¿Quién clasifica los 1.057 trámites y quién aprueba una ficha completada? | Fase 3 | Es trabajo humano continuo, no una decisión de una sola vez. |

## Contrato congelado

[`openapi-v1.yaml`](./openapi-v1.yaml) — siete rutas, `TramiteResumenDto` /
`TramiteDetalleDto` construidos contra los campos reales de
`src/Domain/Entities/TramiteSiger.cs` y `Institucion.cs` (más los campos que
agrega la Fase 1: `CategoriaId`, `Modalidad`, `EstaEnSol`, `SolUrl`,
`SolVerificadoEl`, `CostoTexto`, `CostoEsGratuito`, `TiempoTexto`,
`EsPopular`, y el contacto institucional). Cambios de forma requieren v2, no
edición silenciosa de esta v1.

## Qué falta para dar la Fase 0 por cerrada del todo

- P-03, P-04, P-06 — no bloquean el inicio de la Fase 1, pero sí bloquean
  Fase 4, Fase 7 y Fase 3 respectivamente. Resolverlas antes de llegar ahí.
- Casos límite en el contrato (nombre de 600 caracteres, campos en `NULL`)
  para pruebas de la Fase 4/5 — pendientes de redactar cuando arranque esa fase.
