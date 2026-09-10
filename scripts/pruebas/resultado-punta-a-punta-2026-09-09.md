# Resultado del recorrido punta a punta — 9 de septiembre de 2026

Entorno: sandbox (`DigerTramitesEstado_Unificada_Sandbox` / `VentanillaDigital_Net_Sandbox`).
Sesion de GestionGD abierta por el usuario como `admin.cowork@diger.gob.hn` (Cowork - Administrador).

## Tabla de los 22 pasos

| Paso | Resultado | Medida |
|---|---|---|
| 1  | OK | API total 457 = BD 457; portal marca `Entorno de pruebas` y `VentanillaDigital_Net_Sandbox` |
| 2  | OK | `/api/v1/salud` sin clave -> 200 |
| 3  | OK | `/api/v1/tramites` sin clave -> 401 |
| 4  | OK | la ficha dice "Falta capturar: modalidad", y solo eso |
| 5  | OK | `Est 506-001` -> 404 |
| 6  | OK | modalidad = Presencial por el Editor; `UpdatedAt` sellado 15:32 UTC; incompletas 355 -> 354 |
| 7  | OK | completitud CONSUCOOP 7 -> 8 (sube en uno) |
| 8  | OK | publicada por pantalla; publicadas 457 -> 458 |
| 9  | OK | `Est 506-001` -> 200 con los cuatro campos escritos |
| 10 | OK | presente en `/api/v1/codigos-publicados` (458) |
| 11 | OK | aparecio en <= 32 s (esperado < 60 s) |
| 12 | OK | los seis campos coinciden API vs portal, tildes incluidas |
| 13 | OK | sale en `/Instituciones/CONSUCOOP` y en `/Tramites?CategoriaId=5` |
| 14 | OK | `/Tramites/506-001` -> 200 |
| 15 | OK | `123-008` publicada incompleta: `Tiempo: -`; nombre, institucion y costo intactos |
| 16 | OK | `506-008` muestra `Costo: -`; no aparece `Gratuito`, `L 0.00` ni `Sin costo` |
| 17 | OK | `400-001`: API 200 **y** ciudadano 404 (las dos mitades) |
| 18 | OK | despublicada: `Est` -> 404 y fuera de `codigos-publicados` (460 -> 459) |
| 19 | OK | desaparecio en 4 s (ciclo ligero, no la reconciliacion pesada) |
| 20 | OK | API abajo (PID 17572, `Diger.TramitesEstado.Api`); las 5 rutas del ciudadano dan 200 con datos |
| 21 | OK | API reencendida; `603-001` aparecio en < 2 s |
| 22 | OK | Virtual 188 (96+92), Hibrido 92, Mixto 200 con 0 resultados |

**Fallas: ninguna. No probados: ninguno.**

## Observacion menor (no es un paso del recorrido)

En `/Tramites/123-008` el `meta name="description"` y el `og:description` escriben el costo en
minuscula (`Costo: l. 600.00`) mientras que en el cuerpo de la pagina sale bien (`L. 600.00`).
Solo afecta a lo que veria un buscador o una vista previa al compartir el enlace.

## Que quedo tocado en el sandbox

| Codigo | Antes | Despues |
|---|---|---|
| `506-001` | sin publicar, sin modalidad | **sin publicar**, ahora con `Modalidad = Presencial` (el cambio del paso 6 no se revirtio) |
| `123-008` | sin publicar | **publicada** (sigue sin tiempo) |
| `506-008` | publicada | sin cambios |
| `400-001` | sin publicar | **publicada** (no visible al ciudadano, esta fuera del piloto) |
| `603-001` | sin publicar | **publicada** |

Publicados: 457 -> 460. Para volver al punto de partida hay que despublicar `123-008`,
`400-001` y `603-001`, y quitarle la modalidad a `506-001`; o correr
`Refrescar-Sandbox.ps1 -Rehacer` (ojo: borra los usuarios de prueba).
