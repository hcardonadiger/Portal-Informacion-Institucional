# Separar Honduras Simple del inventario SIGER

**Diseño aprobado el 3 de septiembre de 2026.** Sólo afecta a PortalDigital.

## Qué se pide

Que el inventario SIGER pueda abrirse a gente de fuera sin abrirles, de paso, el trabajo
operativo de Honduras Simple (antes Honduras Ágil): la conciliación, la publicación, la
edición de fichas y el llenado.

Palabras del encargo: *«esa sección otra gente puede llegar a tener acceso más adelante, así
que necesito que todo lo de HondurasSimple lo separes en otra sección»*.

## Cómo está hoy

La sección SIGER no es una cosa, son **tres** que se llaman igual y hay que partir las tres:

1. El grupo del menú, en `src/Web/Pages/Shared/_Layout.cshtml`.
2. El área «SIGER» del catálogo de permisos, en `src/Web/Common/CatalogoModulos.cs`.
3. Las claves de permiso `Siger`, `Siger.Conciliacion`, `Siger.Llenado`, `Siger.Publicacion`,
   declaradas con `[Permission]` en cada PageModel y descubiertas por reflexión al arrancar
   por `PermissionCatalogSyncService`.

Debajo cuelgan once páginas en `src/Web/Pages/Siger/`, mezcladas: cuatro son de consulta y
siete son de trabajo.

## La regla del reparto

**Si escribe, se va.** SIGER queda de sólo lectura.

| Se queda en SIGER | Se va a Honduras Simple |
|---|---|
| `Index` — Inventario | `Editor` — crear y editar fichas |
| `Tablero` — Observatorio | `Archivo` — capturar el archivo del original |
| `Detalle` — ficha de un trámite | `CapturaLote` — captura por lotes |
| `Original` — ver el original del SIGER | `Completitud` — completitud de fichas |
| | `Llenado` — llenado asistido |
| | `Conciliacion` — conciliación con expedientes |
| | `Publicacion` — publicado en Honduras Simple |

`Detalle` se queda, pero **su acción de borrar se va**: la página es de consulta, borrar no lo
es.

## Las claves de permiso

| Clave vieja | Clave nueva |
|---|---|
| `Siger.Ver` | `Siger.Ver` — se queda |
| `Siger.Editar` | `HondurasSimple.Editar` |
| `Siger.Eliminar` | `HondurasSimple.Eliminar` |
| `Siger.Conciliacion.Editar` | `HondurasSimple.Conciliacion.Editar` |
| `Siger.Publicacion.Ver` / `.Editar` | `HondurasSimple.Publicacion.Ver` / `.Editar` |
| `Siger.Llenado.Ver` / `.Editar` | `HondurasSimple.Llenado.Ver` / `.Editar` |

`Completitud` pasa de `Siger.Ver` a `HondurasSimple.Ver`.

Terminado el cambio, el módulo `Siger` **sólo conserva `Ver`**. Que no le quede ninguna acción
de escritura es la comprobación de que el reparto se hizo completo: mientras `Siger.Editar`
exista, la separación es decorativa.

La forma del módulo nuevo copia a propósito la del viejo —un módulo raíz y tres submódulos—
porque `CatalogoModulos.Obtener` decide la indentación de la pantalla de administración
mirando si el prefijo antes del punto está en el mapa. Inventar otra forma habría dejado los
cuatro módulos nuevos como hermanos sueltos.

## Nadie pierde acceso

Al cambiar la clave, las concesiones que los roles tienen en `RolPermisos` apuntarían a un
permiso que `PermissionCatalogSyncService` va a desactivar por no encontrarlo. Sin nada más,
todo el mundo perdería la publicación, la conciliación y el llenado el día del despliegue.

Un `IHostedService` nuevo copia cada concesión vieja a su clave nueva, registrado **después**
de `PermissionCatalogSyncService` —que es quien crea los permisos nuevos en el catálogo— y con
la misma guarda de «una sola vez» que usa `PermisosSeedService`.

Quien hoy publica, mañana publica. La separación sirve para lo que venga, no para quitarle
nada a nadie.

## Las rutas

Los siete archivos se mudan de `Pages/Siger/` a `Pages/HondurasSimple/`, así que
`/Siger/Publicacion` pasa a ser `/HondurasSimple/Publicacion`.

Las direcciones viejas no se rompen: un middleware con un diccionario de siete entradas
responde 301 a la nueva. Vive en su propio archivo para que se pueda borrar de un tirón el día
que ya nadie llegue por ahí.

## El menú

Grupo nuevo, después de SIGER, con seis entradas — el tope que el propio navbar se impuso y
documentó:

```
SIGER                    Honduras Simple
├─ Inventario            ├─ Conciliación
└─ Observatorio          ├─ Publicado
                         ├─ Llenado asistido
                         ├─ Completitud
                         ├─ Captura por lotes
                         └─ Archivo del original
```

El editor de fichas no entra al menú: se llega desde el Inventario y desde el Detalle, como
hoy.

### Un arreglo que va de paso

Hoy el grupo SIGER se muestra si el usuario tiene permiso de **Expedientes**, no de SIGER
(`_Layout.cshtml`, `@if (puedeExpedientes)`). Con eso, abrirle el inventario a alguien de fuera
obligaría a darle Expedientes entero. Pasa a gatearse con `Siger.Ver`, que es lo que la línea
siempre quiso decir.

## Los cruces

Ocho enlaces saltan hoy de una sección a la otra. Todos quedan tras una comprobación de
permiso, además de repuntar a la ruta nueva:

- **Inventario** → Completitud, Captura por lotes, Publicado, Archivo, «+ Nuevo trámite», y el
  «Editar» de cada fila.
- **Detalle** → «Editar», «Completar la ficha» y el botón de eliminar.
- **Original** → «Archivo del SIGER original».

Esconder el botón no es la protección; la protección es el `[Permission]` del handler. El
botón se esconde para no ofrecer una puerta que se va a cerrar en la cara.

## El renombrado

Los siete textos que aún dicen «Honduras Ágil» pasan a «Honduras Simple»: seis en la pantalla
de publicación y uno en el catálogo de módulos. El portal ciudadano ya se renombró; que el
gestor siga diciendo el nombre viejo sólo genera dudas sobre si son dos cosas distintas.

## Cómo se comprueba

Con dos usuarios, contra la copia `DigerTramitesEstado_Pruebas` y nunca contra la Unificada:

1. Uno con todos los permisos: las siete páginas abren en su ruta nueva, y las siete rutas
   viejas redirigen.
2. Uno con **sólo `Siger.Ver`**: ve el inventario y el observatorio; no ve el grupo del menú,
   no ve ninguno de los ocho botones, y las siete direcciones escritas a mano le responden que
   no.

La segunda es la que importa. La primera sólo dice que no se rompió nada.

## Lo que no se toca

PortalDigital y nada más: ni la base `Unificada`, ni la API pública, ni el repositorio de
Honduras Simple.

## Riesgo asumido

El servicio que copia las concesiones escribe en `Permisos` y `RolPermisos` al arrancar la
aplicación. Es escritura de catálogo, no de datos de trámites, y es lo mismo que ya hacen los
dos servicios de arranque que existen — pero conviene saberlo antes de que la aplicación
arranque por primera vez contra la base que va a producción.
