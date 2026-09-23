# Carga de 29 instituciones al catálogo — 2026-09-23

Agrega al catálogo las veintinueve instituciones de la lista. **Se puede correr las veces que
haga falta**: cada una se busca antes por sus siglas y por su nombre completo, y sólo se crea
la que no aparezca.

## Cómo correrlo

```powershell
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-23_catalogo_instituciones.sql
```

- **`-b`** detiene en el primer error. El script además trae `SET XACT_ABORT ON`: o entran las
  que falten o no entra ninguna.
- **`-I`** activa `QUOTED_IDENTIFIER`, que exige el índice filtrado de `Proyectos.Codigo`.
- **`-f 65001`** lee el archivo como UTF-8, sin lo cual las tildes llegan corrompidas.

## Qué escribe

| Campo | Contenido |
|---|---|
| `Id` | Las siglas |
| `Nombre` | Las siglas |
| `NombreCorto` | Las siglas |
| `Descripcion` | El nombre completo |
| `Activo` | Sí |
| `LogoUrl`, `Color`, `Tipo` | Vacíos, a propósito |

Las siglas van en `Nombre` porque así se pidió ver el catálogo y así están las que ya había
—`UNAH`, `IHTT`, `INA`—. El nombre completo no se pierde: queda en `Descripcion`.

## Cómo decide si ya está

El catálogo trae los nombres escritos de todas las formas imaginables: unos son las puras
siglas, otros el nombre corrido en mayúsculas y sin tildes, y otros el nombre con las siglas
pegadas al final. Comparar tal cual no serviría, así que antes de comparar todo se lleva a una
forma común —sin espacios ni puntuación, la Ñ como N, y sin distinguir mayúsculas ni tildes— y
se da por encontrada la institución cuando su llave, su nombre o su nombre corto **coincide con
las siglas, coincide con el nombre completo, o empieza por el nombre completo**.

Esa última regla es la que reconoce a `INSTITUTOHONDURENODEMERCADEOAGRICOLAIHMA` como el IHMA
que ya estaba, en vez de crearlo de nuevo.

## Las siglas

Verificadas contra el sitio oficial de cada institución o el Portal Único de Transparencia.
Donde conviven dos formas se tomó la que usa la propia institución:

| Institución | Siglas | Por qué esa |
|---|---|---|
| Secretaría de Gestión de Riesgos y Contingencias Nacionales | **COPECO** | Es como se nombra ella misma: `copeco.gob.hn`, no SEGRCN |
| Cuerpo de Bomberos de Honduras | **HBCBH** | El nombre de ley es «Heroico y Benemérito…», y así estaba ya en el catálogo |
| Secretaría de la Presidencia | **SEP** | `sep.gob.hn` y el Portal de Transparencia; SDP es la forma vieja |
| Secretaría de Vivienda y Asentamientos Humanos | **SEVIAH** | Su propia cuenta y el portal la escriben así, no SEVAH |

Las demás son las de uso corriente: IHSS, UNAH, UNAG, UNACIFOR, UPNFM, INFOP, SGJD, DNBE, ENAG,
AHAC, DGMM, IHTT, BANADESA, DICTA, ICF, IHMA, INA, IPM, INPREUNAH, INJUPEMP, INPREMA, SANAA,
SAPP, CONSUCOOP y BANHPROVI.

## Comprobación

El script imprime al final cuántas creó y una fila por institución diciendo si la creó o ya
estaba, con la llave bajo la cual quedó:

```
Creadas en esta corrida: 15 de 29

Pedida   Institucion                               Resultado  EnCatalogo
IHSS     Instituto Hondureño de Seguridad Social   creada     IHSS
COPECO   Secretaría de Gestión de Riesgos…         ya estaba  COPECO
IHMA     Instituto Hondureño de Mercadeo Agrícola  ya estaba  INSTITUTOHONDURENODEMERCADEOAGRICOLAIHMA
```

Correrlo dos veces seguidas tiene que decir **0 de 29** la segunda vez.

## Dónde se corrió ya

| Base | Servidor | Creadas | Total |
|---|---|---|---|
| `GestionGD_Sandbox` | `LP-GD-JAGM\SQL2025` | 15 | 73 |
| `GestionGD_TEST` | `LP-GD-JAGM\SQL2025` | 15 | 73 |
| `DigerTramitesEstado_Unificada_Sandbox` | `LP-GD-JAGM\SQLEXPRESS` | 21 | 59 |

`GestionGD_Sandbox` es a la que apunta hoy `appsettings.Development.json`, así que es la que se
ve al levantar el portal. El viejo sandbox de SQLEXPRESS se dejó al día por si alguien todavía
lo usa; tenía menos instituciones de entrada, de ahí las 21.

**No se tocó `GestionGD_Unificada`**, que es la que va a producción.

## Lo que este script no hace

Las catorce que ya estaban quedaron **como estaban**: no se les cambió el nombre ni se les
rellenó el nombre corto. Varias arrastran la llave deforme de una importación vieja
—`INSTITUTOHONDURENODEMERCADEOAGRICOLAIHMA`, `ADMINISTRACIONNACIONALDESERVICIOCIVILANSEC`— y el
catálogo trae además duplicados de antes: `IHADFA` junto a
`INSTITUTOHONDURENOPARALAPREVENCIONDELALCOHOLISMO…`, `IHT` junto a `CANATURHIHT`, `SRECI` junto
a `SRECICANCILLERIA`. Normalizar eso es una limpieza aparte, y conviene hacerla desde la
pantalla de catálogos para que quede en la bitácora.
