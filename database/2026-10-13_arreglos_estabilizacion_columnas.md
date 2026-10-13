# Las dos columnas que pide `feature/arreglos-estabilizacion` — 2026-10-13

**Script:** `2026-10-13_arreglos_estabilizacion_columnas.sql`

```
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-13_arreglos_estabilizacion_columnas.sql
```

Saque copia de seguridad antes: borra la columna `Reuniones.Duracion`.

## El síntoma

Al correr la rama contra una base salida de TEST, el portal no deja ni iniciar sesión. El
error habla de un objeto que no existe, pero **no falta ninguna tabla**: faltan dos columnas.

| Falta | La usa | Error |
|---|---|---|
| `Usuarios.CalendarioToken` (+ índice único) | el feed de calendario (ICS) | `Invalid column name 'CalendarioToken'` — al leer `Usuarios`, o sea, al entrar |
| `Reuniones.DuracionMinutos` | Mi Día y las reuniones | `Invalid column name 'DuracionMinutos'` |

Son las dos migraciones propias de la rama, que TEST no tiene:
`20260902010828_DuracionReunionEnMinutos` y `20260902013548_TokenCalendarioUsuario`.

## Qué hace el script

Lo mismo que esas dos migraciones, y las anota en `__EFMigrationsHistory` para que
`dotnet ef database update` no intente repetirlas. Se puede correr las veces que haga falta.

**Una diferencia, a propósito.** La migración convierte el texto de la duración a minutos y
después borra el texto. Su regla lee solo el primer número, así que las duraciones compuestas
salen mal, y como el texto se borra ya no hay de dónde corregirlas:

| Texto | La migración da | El script da |
|---|---|---|
| «1 hora y 30 minutos» | 1 | **90** |
| «2 horas 1 minuto» | 2 | **121** |
| «1:30» | 60 | **90** |

Todo lo demás sigue la regla de la migración al pie de la letra («2 horas» → 120, «30min» →
30, «1» → 60).

**Ojo si se despliega la rama en TEST o en Producción con `dotnet ef database update`:** la
migración hará la conversión mala. Corra antes este script; al quedar anotadas las dos
migraciones, EF ya no las aplica.

## Dónde se corrió

| Base | Resultado |
|---|---|
| Copia de `GestionGD` | Dos corridas, salida 0; la segunda no hace nada |
| `GestionGD` en `LP-GD-JAGM\SQL2025` | Aplicado el 2026-10-13. 21 duraciones convertidas, las 21 correctas |

Respaldo previo: `GestionGD_antes_20261013.bak`, en la carpeta `Backup` de la instancia SQL2025.

Verificado con el `AppDbContext` de la rama leyendo todas las entidades: antes fallaban
`Reunion` y `Usuario`; después, 81 de 81 sin falla, y EF no ve migraciones pendientes.

## Consecuencia para otras ramas

`Reuniones.Duracion` ya no existe en esa base. Una rama que todavía la use —las que no traen
«Mi Día»— fallará al abrir reuniones contra `GestionGD`. Para volver atrás, restaure el respaldo.
