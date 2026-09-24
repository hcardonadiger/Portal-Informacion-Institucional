# Segunda lista de instituciones — 2026-09-24

Treinta instituciones más, **ninguna de las del 23 de septiembre**. Mismo motor que aquel
script: cada una se busca antes por sus siglas y por su nombre completo, y sólo se crea la que
no aparezca. Se puede correr las veces que haga falta.

## Cómo correrlo

```powershell
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-24_catalogo_instituciones_segunda_lista.sql
```

Las banderas y el criterio de búsqueda son los mismos del
[script del 23](2026-09-23_catalogo_instituciones.md): `-b` para detenerse en el primer error,
`-I` por el índice filtrado de `Proyectos.Codigo`, `-f 65001` para que las tildes no lleguen
corrompidas.

## Qué escribe

`Nombre` y `NombreCorto` llevan las siglas; el nombre completo queda en `Descripcion`. Logo,
color y tipo quedan vacíos a propósito.

**Dos no tienen siglas** —el Centro de la Cultura Garinagu de Honduras y el Programa Ciudad
Mujer—. A esas el `Nombre` les queda completo, `NombreCorto` vacío, y la llave se arma con el
nombre sin espacios y en mayúsculas: `PROGRAMACIUDADMUJER`. Las mayúsculas no son capricho: el
dominio sólo admite letras mayúsculas, dígitos, guion y guion bajo en la llave, y sin
convertirlas la institución no pasaba su propia validación al editarla.

## Las siglas

Verificadas contra el sitio oficial de cada institución o el Portal Único de Transparencia.
Las que tenían más de una forma:

| Institución | Siglas | Por qué esa |
|---|---|---|
| Instituto Nacional de la Juventud | **INJUVE** | Así se nombra en el Gabinete Social; INJ no lo usa nadie |
| Secretaría de Turismo | **SETUR** | Portal de Transparencia. Comparte sitio con el IHT pero es otra entidad, y ambas van por separado |
| Programa Nacional de Prevención, Rehabilitación y Reinserción Social | **PNPRRS** | Como lo escribe el propio programa; el portal lo lista como PNRRS |
| Comisión Nacional del Deporte, Educación Física y Recreación | **CONDEPOR** | `condepor.gob.hn`. No confundir con CONDEPAH, que es la Confederación Deportiva y va aparte |

Las demás: IHAH, SECAPPH, CICESCT, CONAPREV, INAMI, SEDH, CDPC, DIDADPOL, HONDUCOR, INP,
INHGEOMIN, IHADFA, SENAF, CENET, ZOLITUR, IHT, CONDEPAH, PANI, STSS, SEMUJER, PRONADERS,
BANASUPRO, EDUCREDITO y ERSAPS.

## Las siete que ya estaban

No se tocaron: el Centro de la Cultura Garinagu, el IHAH, SECAPPH, SEDH, DIDADPOL, IHADFA
e IHT. Cuatro de ellas estaban guardadas con el nombre corrido como llave —
`INSTITUTOHONDURENODEANTROPOLOGIAEHISTORIA`, `CENTRODELACULTURAGARINAGUDEHONDURAS`— y aun así
el script las reconoció, porque compara sobre el nombre normalizado y no sobre el texto crudo.

## Dónde se corrió ya

| Base | Servidor | Creadas | Total |
|---|---|---|---|
| `GestionGD_Sandbox` | `LP-GD-JAGM\SQL2025` | 23 | 96 |
| `GestionGD_TEST` | `LP-GD-JAGM\SQL2025` | 23 | 96 |
| `DigerTramitesEstado_Unificada_Sandbox` | `LP-GD-JAGM\SQLEXPRESS` | 27 | 86 |

`GestionGD_Sandbox` es a la que apunta hoy `appsettings.Development.json`. El sandbox viejo de
SQLEXPRESS venía con menos instituciones, de ahí las 27.

**No se tocó `GestionGD_Unificada`**, que es la que va a producción.

## Comprobación

Correrlo dos veces seguidas tiene que decir **0 de 30** la segunda vez. Antes de las bases
reales se probó sobre una copia desechable del catálogo: 23 creadas, 7 reconocidas, todas las
llaves válidas para el dominio y ninguna descripción vacía.
