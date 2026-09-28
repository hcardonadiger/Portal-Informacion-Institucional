# Poner al día una base desplegada — 2026-09-24

Para llevar la base de **otro servidor** al código de hoy sin sacarle copia y sin tocar los
datos que ya subieron otras personas. Todo se hace con scripts que se pueden correr las veces
que haga falta: la segunda corrida no hace nada.

## Los tres archivos, en orden

| # | Archivo | Qué hace | ¿Modifica? |
|---|---|---|---|
| 1 | `00_diagnostico.sql` | Dice en qué estado está la base y **qué hay que correr** | No |
| 2 | `2026-09-18_poner_al_dia.sql` | Catálogos de prioridades y categorías | Sí |
| 3 | `2026-09-24_poner_al_dia.sql` | Encuesta en el cierre + tres columnas que ninguna migración crea | Sí |

**Corra siempre el 1 primero.** Su última sección dice, con nombre y apellido, cuáles de los
otros dos hacen falta. Si dice «Nada que hacer», terminó.

## Cómo correrlos

```
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 00_diagnostico.sql -o salida.txt
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-24_poner_al_dia.sql
```

O péguelos en SSMS y ejecute. Las banderas no son decorativas:

- `-b` detiene en el primer error. **Sin ella** sqlcmd sigue de largo, llega al `COMMIT` y
  confirma lo que alcanzó a hacer. Así fue como una base quedó a medias antes.
- `-I` activa `QUOTED_IDENTIFIER`.
- `-f 65001` es UTF-8; sin ella los acentos de los mensajes salen rotos.

Adentro, `SET XACT_ABORT ON` hace lo mismo: cualquier error deshace **todo**. O entra completo
o no entra nada.

## Qué cambia el script del 2026-09-24

Son dos cosas de distinta naturaleza y conviene no confundirlas.

**1. `Reuniones.EncuestaActiva`** (`bit NOT NULL DEFAULT 0`) — es la migración
`20260923223127_AddReunionEncuestaActiva`, la única de EF posterior al 2026-09-18. Decide si el
cierre de la reunión pide la segunda firma. Al final el script la registra en
`__EFMigrationsHistory` para que `dotnet ef database update` no intente aplicarla de nuevo.

**2. Tres columnas que ninguna migración crea nunca:**

| Columna | Tipo |
|---|---|
| `Asistentes.EsPreregistro` | `bit NOT NULL DEFAULT 0` (+ índice `IX_Asistentes_EsPreregistro`) |
| `Asistentes.Confirmado` | `bit NULL` |
| `Instituciones.Color` | `nvarchar(max) NULL` |

Sus dos archivos —`20260719000000_AddPreregistroAsistente` y `20260719100000_AddInstitucionBranding`—
quedaron sin el atributo `[Migration]` y sin Designer, así que **EF no los ve**: por mucho que
se corra `database update`, esas columnas no aparecen. El modelo sí las usa, y
`Instituciones.Color` es la que lee `IInstitucionBrandingService`.

Las bases que ya estaban en uso las tienen porque alguien las agregó a mano en su momento. Una
base creada desde cero con las migraciones **no**. Por eso van en este script y **no** se
registran en el historial: anotar un `MigrationId` que el código no reconoce solo confundiría a
quien lo lea después.

## Por qué los scripts no consultan `__EFMigrationsHistory`

Porque esa fila se escribe **al final**. Si la corrida muere a la mitad quedan cambios hechos y
ninguna marca de que se hicieron, y volver a correr el script falla de otra manera. Cada paso
comprueba **su propia** condición contra el catálogo de la base —¿existe la columna?, ¿existe
el índice?—, así que converge al estado final venga de donde venga:

| Estado de la base | Qué hace |
|---|---|
| Sin empezar | Lo aplica todo |
| A medias | Retoma donde quedó |
| Ya completa | Nada |

## Si la base está muy atrasada

El script del 2026-09-24 **se detiene solo** si no encuentra `Reuniones`, `Asistentes` o
`Instituciones`, con un mensaje que lo dice, y no deja nada aplicado. Agregar columnas no
reconstruye módulos enteros: si el diagnóstico reporta tablas faltantes en su sección 1, eso es
otra conversación y hay que verlo antes de correr nada.

## Antes de correrlo en el servidor

Los cambios son **aditivos** —agregan columnas e índice, no borran ni modifican filas—, pero
`ALTER TABLE` toma un bloqueo de esquema. `Reuniones`, `Asistentes` e `Instituciones` son tablas
chicas y la transacción dura poco, aun así conviene correrlo en un momento sin gente usando el
portal.

**Para deshacerlo** (solo si hiciera falta), el inverso es:

```sql
DROP INDEX [IX_Asistentes_EsPreregistro] ON [Asistentes];
ALTER TABLE [Asistentes]    DROP COLUMN [EsPreregistro], [Confirmado];
ALTER TABLE [Instituciones] DROP COLUMN [Color];
ALTER TABLE [Reuniones]     DROP COLUMN [EncuestaActiva];
DELETE FROM __EFMigrationsHistory WHERE MigrationId = N'20260923223127_AddReunionEncuestaActiva';
```

Ojo: hay que quitar antes las restricciones de valor por omisión de las columnas `bit`, que SQL
Server nombró solo. Y el portal deja de funcionar sin esas columnas: esto es para volver atrás
un despliegue completo, no para «probar».

## Cómo se probaron

Contra SQL Server 2025 real, no a ojo:

1. Base nueva + `dotnet ef database update` con las 70 migraciones. El diagnóstico reportó
   **81/81 tablas, 70/70 migraciones y las 3 columnas faltando** — que es exactamente lo que
   esta nota afirma sobre las migraciones inertes.
2. Script aplicado: agregó las tres columnas y el índice, y dejó los tipos idénticos a los del
   modelo (`bit NOT NULL DEFAULT 0`, `bit NULL`, `nvarchar(max) NULL`).
3. Corrido dos veces más: no hizo nada, salida 0.
4. Simulando una base sin la migración de la encuesta (columna e historial borrados): la
   detectó, la agregó y registró el historial.
5. Contra una base vacía: se detuvo en la comprobación previa y dejó **0 objetos**.

La primera versión usaba `RAISERROR` en esa comprobación previa y **no servía**: `RAISERROR` no
corta el lote, así que el script seguía y moría más abajo con «Cannot find the object
"Reuniones"». Se cambió a `THROW`, que sí lo corta.
