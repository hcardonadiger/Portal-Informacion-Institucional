# Desplegar `ike_opt` a producción — qué correr en la base

## Vigente desde el 2026-10-26: un solo script

Medido contra el respaldo de producción `GestionGD_021026` (copia del 2026-10-26, 74 migraciones,
la última `20260923223127_AddReunionEncuestaActiva`). Esa base sigue en el estado que se midió el
2026-10-02, más abajo, y el código de `ike_opt` ya trae **18 migraciones** que no tiene.

```
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-26_poner_al_dia.sql -o salida.txt
```

`2026-10-26_poner_al_dia.sql` reúne, sin reescribirlos, los nueve scripts que van del
`2026-10-02` al `2026-10-23`. **Reemplaza** correrlos uno por uno: no hace falta correr también
el de 2026-10-02 que nombra el diagnóstico. Va después de `2026-09-24_poner_al_dia.sql`; el script
lo comprueba solo y se detiene si la base no llegó ahí. Pasos de publicación, configuración y
reversión: `docs/despliegue/2026-10-26_produccion_poner_al_dia.md`.

**Después**, `2026-10-26_equipo_institucional.sql` (migración `20261026192746_ExpedienteEquipoInstitucional`:
tabla `ExpedienteEquipo` y el contacto de cada expediente pasado a su equipo como prioridad 1). Es
idempotente y va aparte porque llegó después de armado el script único.

`00_diagnostico.sql` todavía no conoce los cambios posteriores al 2026-10-02: sobre la base de
producción va a recomendar el script de esa fecha. Úselo para confirmar que la base es la correcta
y que llegó al 2026-09-24, no para decidir qué correr.

---

Lo que sigue es lo medido el 2026-10-02. Queda como antecedente.

## Lo primero: ¿sobre cuál base?

Se midieron **dos respaldos del mismo día** y **no están en el mismo estado**. Los dos traen el
mismo nombre lógico (`TramitesEstado_Prod`), así que por dentro se parecen; pero el archivo de
datos delata que son bases distintas:

| Respaldo | Archivo de datos | Migraciones | Qué le falta |
|---|---|---|---|
| `GestionGD 2.bak` (12:20) | `GestionGD_TEST.mdf` | 63 | **13 migraciones**, 6 tablas, 10 columnas |
| `GestionGD_021026` (13:23) | `GestionGD.mdf` | 74 | **2 migraciones**, 2 tablas |

**Antes de correr nada, confirme el nombre de la base en el servidor.** No es un detalle de
forma: la cadena de `src/Web/appsettings.json` dice `Database=GestionGD_TEST`, que es la
**atrasada** de las dos. Si la base viva es `GestionGD`, esa cadena apunta al lugar equivocado y
hay que corregirla en el servidor (o la app no verá los datos que la gente usa).

Da igual cuál sea para decidir qué correr: **`00_diagnostico.sql` lo dice solo.** Córralo primero
y haga lo que diga su última sección. No hace falta adivinar ni leer el resto de esta nota.

```
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 00_diagnostico.sql -o salida.txt
```

## Si la base es la del respaldo de 13:23 (`GestionGD`)

**Un solo script**, y el diagnóstico lo nombra solo:

```
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-02_poner_al_dia.sql
```

Crea `TokensAccesoMcp` y `ReunionAdjuntos`. `ModuloAmbitos` ya está allá y el script la deja en
paz («ya estaba»). No toca nada más.

## Si la base es la del respaldo de 12:20 (`GestionGD_TEST`)

**Cuatro scripts, en este orden.** El diagnóstico los nombra en orden y numerados:

```
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-04_poner_al_dia.sql
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-18_poner_al_dia.sql
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-24_poner_al_dia.sql
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-02_poner_al_dia.sql
```

El primero incluye **`Usuarios.IsDeleted`**, sin la cual **nadie inicia sesión**: el modelo la
mapea, EF la pide en cada consulta de `Usuarios` y falla con «Invalid column name».

Correr los cuatro sobre una base que ya está al día **no hace nada**: cada paso comprueba su
propia condición. No hay riesgo en correr uno que no hacía falta.

## Las banderas no son adorno

- `-b` detiene en el primer error. **Sin ella** `sqlcmd` sigue de largo, llega al `COMMIT` y
  confirma lo que alcanzó a hacer. Así quedó una base a medias antes.
- `-I` activa `QUOTED_IDENTIFIER`.
- `-f 65001` lee el archivo como UTF-8; sin ella los acentos salen rotos.

Cada script trae `SET XACT_ABORT ON`: cualquier error deshace **todo**. O entra completo o no
entra nada. Y al final imprime una tabla donde **todo debe decir «presente»**.

## Qué se verificó, y cómo

Sobre el respaldo de 13:23 restaurado en SQL Server 2025:

| Verificación | Resultado |
|---|---|
| Diagnóstico antes | Nombró **sólo** el script del 10-02; sin «PARE» |
| El script | `ModuloAmbitos` «ya estaba»; creó las 2 tablas; ningún control en FALTA |
| Esquema contra el que espera el código | **1335 objetos a cada lado, cero diferencias** |
| Filas | 43 338 → 43 340. **Las 69 tablas de datos, conteo exacto**; las 2 nuevas son del historial |
| Diagnóstico después | `0 · 0 · 0 · 0` → «Nada que hacer» |
| Segunda corrida | No creó nada; historial sin duplicar |
| **La app arrancada contra esa base** | Catálogo de roles cargado (7 activos), `/Cuenta/Login` **HTTP 200**, **cero errores de SQL** |

Sobre el respaldo de 12:20, con los cuatro scripts: mismo resultado — **1335 objetos idénticos**,
38 956 → 38 979 filas (las 23 de diferencia son el historial y los tres catálogos nuevos), y los
26 proyectos quedaron 16 Alta / 8 Media / 2 Baja **sin un solo huérfano**. Los números completos
están en `2026-09-04_poner_al_dia.md`.

## Lo que el script no hace y hay que hacer a mano

**Otorgar las claves `TokensMcp.*`** en `/Accesos/Permisos`.

El arranque de la app siembra las cinco claves solo, por reflexión — comprobado: la tabla
`Permisos` pasó de 70 a 75 filas al levantar la app contra el respaldo. Lo que ningún arranque
hace es *otorgarlas*: quedaron **0 filas en `RolPermisos`**. El Administrador no las necesita
—aprueba todo por código—; cualquier otro rol sí, y hasta que alguien marque esas casillas, nadie
más puede emitir un token MCP. Es a propósito.

## Dos cosas del servidor, no de la base

1. **La carpeta de subidas.** Los adjuntos de reunión guardados como archivo van a
   `wwwroot/uploads/reuniones`. La identidad del *app pool* necesita **escritura** ahí, igual que
   ya la necesita en `uploads/tickets`. Sin eso el portal arranca y falla al guardar el primero.

2. **El máximo de subida es 5 MB**, no 20 — lo fija `Uploads:ReunionesMaxMb`. Es deliberado: el
   portal responde *«pesa más de 5 MB. Súbalo a OneDrive y pegue acá el enlace»*, que es el flujo
   para el que existe `Clase = 'Enlace'`. Subirlo es cambiar esa línea; no se toca código ni base.

## Una observación sobre los roles, para que la vea alguien

No la causa el despliegue y ningún script la toca, pero el código nuevo **sí empieza a usar** las
capacidades `EsJefeDeArea` y `EsPmo` (las agregó la migración `20260902215804`, y ambos respaldos
ya las traen pobladas). En el respaldo de 13:23 están así:

| Rol (`Roles.Id`) | `NivelAlcance` | `EsJefeDeArea` | `EsPmo` |
|---|---|---|---|
| `JefeArea` | Area | **0** | 0 |
| `JefeUnidad` | Unidad | 0 | **1** |
| `PMO` | Area | 0 | **0** |

Se lee cruzado: el jefe de área **no** tiene la capacidad de jefe de área, el jefe de unidad
**sí** tiene la de PMO, y el rol PMO **no**. `InteresadosAutomaticosSync` usa esas dos
capacidades para decidir a quién agrega como interesado automático de un proyecto, así que con
estos valores agregará al jefe de unidad donde se esperaría al PMO.

Puede ser deliberado. Pero conviene mirarlo en `/Accesos/Roles` **antes** del despliegue, porque
después va a parecer que lo rompió el despliegue. Las seis asignaciones de usuario sí resuelven
bien contra `Roles.Id` (`Empleado` 51, `JefeUnidad` 14, `JefeArea` 3, `PMO` 2, `Administrador` 1,
`Consultor` 1) — ahí no hay nada que arreglar.

## Las tres filas de más en el historial

Los dos respaldos traen tres `MigrationId` que el código no declara como migración:
`20260719000000_AddPreregistroAsistente`, `20260719100000_AddInstitucionBranding` y
`20260827174307_ActividadEspejoPorEntregable`. Las dos primeras son los archivos inertes del repo
registrados a mano; la tercera no existe como archivo en ninguna rama.

**No estorban y no hay que hacer nada**: comprobado contra el esquema, ninguna dejó rastro
estructural, y `dotnet ef` ignora las filas que no reconoce.
