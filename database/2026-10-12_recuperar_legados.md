# 2026-10-12 — Recuperar los expedientes legados

Acompaña a `2026-10-12_recuperar_legados.sql`. **Va después de**
`2026-10-12_poner_al_dia.sql`.

## Qué pasó

`GestionGD_new` se creó el 2026-09-22 y los expedientes anteriores al corte de
legado (2026-04-01) no llegaron. La base quedó con 12 expedientes, **todos**
posteriores a abril. En el portal, el filtro «Legados» no devolvía nada —no
porque los ocultara, sino porque no había ninguno.

Siguen existiendo en el respaldo de producción
`.backups/TramitesEstado_Prod_antes_migraciones_faltantes.bak` (14 de agosto):
**29 expedientes de 29 instituciones con 188 trámites**, el catálogo del
levantamiento inicial — ADUANAS, ARSA, CNBS, CONATEL, IHT, INFOP, BANHPROVI,
Bomberos y otras.

## Sobre la fecha 01-01-2026

Las 29 tienen exactamente esa fecha, sin una sola variación, y se crearon en
una **ventana de 7 segundos** el 2026-08-06 (19:41:46 → 19:41:53), sin usuario
creador. Es una carga masiva por script, y `2026-01-01` es un relleno: la
fecha de apertura real no se conocía.

Se conserva tal cual, por decisión explícita, para que la base local quede
idéntica a producción. Conviene saberlo: **esa fecha no significa nada** y no
debería citarse como apertura real. El modelo de dominio esperaba `NULL` para
este caso —`CorteLegado` documenta que «un expediente sin FechaApertura se
trata como legado por defecto, ya que no hay forma de saber cuándo se abrió»—
pero la carga puso una fecha en su lugar.

## Cómo correrlo

**Paso 1 — restaurar el respaldo como base de apoyo.** El script lee de ahí;
no lleva los datos dentro.

```sql
RESTORE DATABASE [TramitesEstado_Recuperacion]
FROM DISK = N'<ruta>\TramitesEstado_Prod_antes_migraciones_faltantes.bak'
WITH MOVE N'TramitesEstado_Prod'     TO N'<carpeta DATA>\TramitesEstado_Recuperacion.mdf',
     MOVE N'TramitesEstado_Prod_log' TO N'<carpeta DATA>\TramitesEstado_Recuperacion_log.ldf',
     RECOVERY, STATS = 50;
```

El archivo `.bak` tiene que estar donde la **cuenta de servicio de SQL Server**
pueda leerlo. La carpeta de usuario no sirve: da `Operating system error 5`.
Una carpeta bajo `C:\Users\Public\` funciona.

**Paso 2 — correr el script.**

```
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-12_recuperar_legados.sql
```

**Paso 3 — soltar la base de apoyo** cuando la verificación esté conforme.

```sql
DROP DATABASE [TramitesEstado_Recuperacion];
```

## Qué hace, en orden

1. **Instituciones que falten.** Los expedientes legados apuntan a
   instituciones que la base nueva no tiene; sin ellas la clave foránea
   rechaza la inserción. En la corrida contra `GestionGD_new` fueron 5:
   ADUANAS, ALCALDIACOMAYAGUA, SAR, SECSEGURIDAD y STSS.
2. **Expedientes**, con su `InstitucionId` traducido por el mapa de alias.
3. **Sus trámites**, enlazados por el mapa de identificadores nuevos.

## Dos casos que el script resuelve solo

**Alias de institución.** La misma institución puede existir en ambas bases con
distinto `Id`: el respaldo la llama `INM` y la base nueva
`INSTITUTONACIONALDEMIGRACION`, con el mismo `Nombre`. Como
`IX_Instituciones_Nombre` es único, insertarla otra vez falla. El script
empareja por nombre, imprime el reenrutamiento y manda los expedientes al `Id`
que la base ya usa. Es el mismo criterio del importador de Supabase.

**Colisión de código.** `EXP-IHTT-2026-01` existe en ambos lados como **dos
expedientes distintos** que comparten código: el local es de mayo, está
borrado lógicamente y tiene 1 trámite; el del respaldo es de enero, activo, con
3. Como `IX_Expedientes_Codigo` es único, uno no entra. La regla de
idempotencia lo resuelve sin lógica especial —el código ya está, así que no se
inserta— y la verificación lo marca `omitido (codigo ya usado)` en vez de
`FALTA`. **Si quiere recuperar también ese, hay que decidir antes qué pasa con
el local.**

## Resultado esperado

Primera corrida:

```
recuperados: 5 institucion(es), 28 expediente(s) y 185 tramite(s)
```

Segunda:

```
ya estaban recuperados: no se inserto ninguna fila
```

En ambas, la verificación final lista los 29 códigos del respaldo con
`Tramites_origen` y `Tramites_destino` iguales, y ningún `FALTA`. El único que
no dice `presente` es `EXP-IHTT-2026-01`, que dice `omitido (codigo ya usado)`.

## Por qué el script es dinámico

El respaldo es del 14 de agosto y la base lleva migraciones posteriores, así
que los esquemas no tienen por qué coincidir. Una lista fija de 57 columnas se
rompería contra un respaldo de otra fecha. El script copia **solo las columnas
presentes en ambos lados** y deja que las que sobran en destino tomen su valor
por omisión. Hoy la única diferencia es `ExpedienteTramites.Accion`, que admite
nulos. El conteo de columnas se imprime al arrancar, antes de tocar nada.

No se escribe fila en `__EFMigrationsHistory`: esto no es una migración de EF
—no cambia el esquema— y registrar un `MigrationId` que el código no reconoce
solo confunde.
