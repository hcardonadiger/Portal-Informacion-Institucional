# Poner al día una base desplegada — 2026-09-04

Este es el **primero** de la cadena de cuatro scripts. Existe porque faltaba: las cinco
migraciones del 2 al 4 de septiembre no las cubría ninguno, y sin ellas el portal nuevo **no
arranca contra la base desplegada**.

## Qué se descubrió comparando con la base real

El 2026-10-02 se restauró el respaldo de producción (`GestionGD_TEST`, nombre lógico
`TramitesEstado_Prod`) y se comparó objeto por objeto contra el esquema que el código espera.
Producción estaba **más atrasada de lo que los scripts suponían**:

| Qué | Cuánto |
|---|---|
| Migraciones del código que la base no tenía | **13** |
| Tablas que faltaban por completo | 6 |
| Columnas que faltaban en tablas existentes | 10 |
| Índices que faltaban | 20 |
| Claves foráneas que faltaban | 5 |

De esas 13 migraciones, **cinco no las cubría ningún script** — las del 2 al 4 de septiembre.
Este archivo las cubre. Las otras ocho ya tenían dueño: cuatro en
`2026-09-18_poner_al_dia.sql`, una en `2026-09-24_poner_al_dia.sql` y tres en
`2026-10-02_poner_al_dia.sql`.

## La cadena completa, en orden

| # | Archivo | Qué hace | ¿Modifica? |
|---|---|---|---|
| 1 | `00_diagnostico.sql` | Dice en qué estado está la base y **qué hay que correr** | No |
| 2 | `2026-09-04_poner_al_dia.sql` | **Este.** Seis columnas en cinco tablas | Sí |
| 3 | `2026-09-18_poner_al_dia.sql` | Catálogos de prioridades y categorías | Sí |
| 4 | `2026-09-24_poner_al_dia.sql` | Encuesta en el cierre + tres columnas fuera de migración | Sí |
| 5 | `2026-10-02_poner_al_dia.sql` | Ámbito de módulos, tokens MCP y adjuntos de reunión | Sí |

**Corra siempre el 1 primero.** Su última sección dice, con nombre y apellido, cuáles de los
otros cuatro hacen falta y en qué orden. Si dice «Nada que hacer», terminó.

```
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 00_diagnostico.sql -o salida.txt
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-04_poner_al_dia.sql
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-18_poner_al_dia.sql
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-24_poner_al_dia.sql
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-02_poner_al_dia.sql
```

- `-b` detiene en el primer error. **Sin ella** sqlcmd sigue de largo, llega al `COMMIT` y
  confirma lo que alcanzó a hacer.
- `-I` activa `QUOTED_IDENTIFIER`.
- `-f 65001` es UTF-8; sin ella los acentos salen rotos.

## Lo urgente: `Usuarios.IsDeleted`

De las seis columnas, ésta es la que convierte el despliegue en una emergencia si se olvida.

El modelo la mapea, así que **EF la pide en cada consulta de `Usuarios`**. Si la columna no
está, la consulta falla con `Invalid column name 'IsDeleted'` — y sin consultar `Usuarios` no
hay inicio de sesión. Una base sin esta columna no es un portal con una función de menos: es un
portal al que **nadie entra**.

Viene de `20260904173357_AgregarBorradoLogicoUsuario`, y es borrado lógico: dar de baja a alguien
no borra la fila, la marca. Así la auditoría conserva quién hizo qué y los expedientes siguen
sabiendo quién fue su analista. Entra en `0` para todos — **nadie queda dado de baja** por correr
el script.

## Las seis columnas

| Columna | Tipo | Migración |
|---|---|---|
| `Roles.EsJefeDeArea` | `bit NOT NULL DEFAULT 0` | `20260902215804_AgregarJefeDeAreaYPmo` |
| `Roles.EsPmo` | `bit NOT NULL DEFAULT 0` | `20260902215804_AgregarJefeDeAreaYPmo` |
| `Proyectos.Accion` | `nvarchar(20) NULL` | `20260902215902_AgregarAccionProyecto` |
| `ProyectoInteresados.Automatico` | `bit NOT NULL DEFAULT 0` | `20260902221131_AgregarInteresadoAutomatico` |
| `ExpedienteTramites.Accion` | `nvarchar(60) NULL` | `20260903175629_AgregarAccionTramite` |
| `Usuarios.IsDeleted` | `bit NOT NULL DEFAULT 0` | `20260904173357_AgregarBorradoLogicoUsuario` |

Las cuatro `bit` llevan `DEFAULT CONVERT(bit, 0)` y no `DEFAULT 0`: es la forma exacta en que EF
declara un `defaultValue: false`, y así la columna queda **idéntica** a la que crea la migración.

**`EsJefeDeArea` y `EsPmo`** son dos capacidades más del rol, al lado de las cuatro que ya había
(`EsAdministrador`, `EsSoloLectura`, `EsSupervisor`, `EsTecnicoSoporte`). Siguen el mismo
criterio: lo que el rol *puede* hacer se declara como capacidad, no se adivina del nombre. Entran
en `0` para todos los roles: **nadie gana un permiso por correr el script**. Quien deba ser jefe
de área o PMO se marca después en `/Accesos/Roles`.

**`ProyectoInteresados.Automatico`** distingue al interesado que el portal agregó solo —porque es
el responsable, el jefe del área, el PMO— del que alguien puso a mano. Importa porque los
automáticos se recalculan y los manuales no se tocan. Entra en `0`: los interesados que ya
existen quedan como puestos a mano, que es lo prudente — marcarlos como automáticos los
expondría a que un recálculo los borre.

**Las dos `Accion`** son nulables y sin valor por omisión, a propósito: un proyecto sin acción
anotada es un estado legítimo, no un dato faltante. Son de largo distinto —20 y 60— porque la del
trámite describe el paso y la del proyecto es una etiqueta corta.

## La comprobación distingue tres cosas, no dos

Al final el script no pregunta sólo «¿está la columna?». Pregunta si está **con el tipo y la
nulabilidad correctos**, y puede responder `PRESENTE PERO DISTINTA`. Una columna que exista con
otro tipo es peor que una que falte: el diagnóstico la daría por buena y el error aparecería
después, en producción, con un dato que no entra.

## Qué cambió en `00_diagnostico.sql`

- La sección 3 vigila ahora las **seis columnas nuevas**, cada una con la migración de la que
  viene. `Usuarios.IsDeleted` sale marcada como «sin ella no hay login».
- Hay una **recomendación nueva**, la número 3, que nombra este script y **avisa del login** si
  falta esa columna.
- El resumen numérico trae una columna más, `ColumnasDel0904`.
- **El «PARE» dejó de dispararse en falso.** Ese aviso —«faltan módulos enteros, ningún script
  los arregla»— contaba las seis tablas faltantes sin fijarse en que tres las crea el script del
  09-18 y tres el del 10-02. Contra el respaldo real de producción daba un `PARE: faltan 6
  tablas` cuando en realidad los scripts cubrían las seis. Ahora se descuentan en
  `@tablasFaltan0918` y `@tablasFaltan1002`, y el PARE sólo mira lo que queda fuera. Un PARE
  falso enseña a no creerle al diagnóstico, que es justo lo contrario de para qué existe.

## Antes de correrlo en el servidor

Seis `ALTER TABLE ... ADD`. **No borra ni modifica una sola fila**; las cuatro `bit` entran en 0
y las dos de texto en NULL, así que el comportamiento de hoy no cambia por aplicarlo.

Sí toma un bloqueo de esquema por tabla. `Usuarios`, `Roles` y `ProyectoInteresados` son chicas;
`Proyectos` y `ExpedienteTramites` también en esta base. Aun así conviene correrlo sin gente
usando el portal.

Se detiene solo, con `THROW`, si falta alguna de las cinco tablas, y no deja nada aplicado.
`THROW` y no `RAISERROR` por la razón que ya costó una vez: `RAISERROR` deja el mensaje pero **no
corta el lote**, así que el script seguiría y moriría más abajo con un error que no explica nada.

**Para deshacerlo** (sólo si hiciera falta):

```sql
/* SQL Server nombró solo las restricciones de valor por omisión, así que hay que buscarlas. */
DECLARE @sql nvarchar(max) = N'';
SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(OBJECT_NAME(dc.parent_object_id))
            + N' DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';' + CHAR(10)
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE (OBJECT_NAME(dc.parent_object_id) = N'Roles'               AND c.name IN (N'EsJefeDeArea', N'EsPmo'))
   OR (OBJECT_NAME(dc.parent_object_id) = N'ProyectoInteresados' AND c.name = N'Automatico')
   OR (OBJECT_NAME(dc.parent_object_id) = N'Usuarios'            AND c.name = N'IsDeleted');
EXEC sp_executesql @sql;

ALTER TABLE [Roles]               DROP COLUMN [EsJefeDeArea], [EsPmo];
ALTER TABLE [Proyectos]           DROP COLUMN [Accion];
ALTER TABLE [ProyectoInteresados] DROP COLUMN [Automatico];
ALTER TABLE [ExpedienteTramites]  DROP COLUMN [Accion];
ALTER TABLE [Usuarios]            DROP COLUMN [IsDeleted];
DELETE FROM __EFMigrationsHistory WHERE MigrationId IN (
    N'20260902215804_AgregarJefeDeAreaYPmo', N'20260902215902_AgregarAccionProyecto',
    N'20260902221131_AgregarInteresadoAutomatico', N'20260903175629_AgregarAccionTramite',
    N'20260904173357_AgregarBorradoLogicoUsuario');
```

Esto es para volver atrás un despliegue completo, no para «probar»: el portal nuevo no funciona
sin esas columnas.

## Cómo se probó: contra el respaldo real de producción

No contra una base de juguete. Se restauró `GestionGD 2.bak` —el respaldo de producción del
2026-10-02— en SQL Server 2025 y se corrió la cadena entera sobre él.

**1. El diagnóstico, antes.** Reportó `TablasFaltantes 6 · ColumnasFaltantes 1 ·
ColumnasDel0904 6 · MigracionesSinRegistrar 13`, y nombró los cuatro scripts en orden,
**sin dar el «PARE» falso**.

**2. La cadena completa.** Los cuatro scripts corrieron en orden, los cuatro con salida 0, y
ningún control quedó en `FALTA` ni en `PRESENTE PERO DISTINTA`.

**3. El esquema quedó idéntico al que espera el código.** Se volcaron tablas, columnas (con tipo,
longitud y nulabilidad), índices (con unicidad), claves foráneas y objetos de programación de
ambas bases y se compararon línea por línea: **1335 objetos en cada una, cero diferencias**.

**4. No se perdió ni una fila.** Se restauró el respaldo **otra vez**, intacto, y se contaron las
filas de cada tabla a ambos lados:

| | Tablas con filas | Filas en total |
|---|---|---|
| Antes | 56 | 38 956 |
| Después | 59 | 38 979 |

Las **23 filas de diferencia son exactamente las que se esperaban**, y ninguna sale de una tabla
de datos:

| Tabla | Antes | Después |
|---|---|---|
| `__EFMigrationsHistory` | 63 | 76 (+13 migraciones registradas) |
| `PrioridadesProyecto` | — | 3 (catálogo nuevo) |
| `PrioridadesTicket` | — | 4 (catálogo nuevo) |
| `CategoriasProyecto` | — | 3 (catálogo nuevo) |

Las otras **55 tablas de datos quedaron con el mismo conteo exacto**.

**5. Los datos de prioridad se trasladaron bien.** El script del 09-18 reemplaza
`Proyectos.Prioridad`/`Tickets.Prioridad` —texto— por una clave al catálogo, y eso sí mueve
datos. Sobre los 26 proyectos reales de producción quedó: **16 Alta, 8 Media, 2 Baja, y cero
huérfanos** (ningún `PrioridadId` apuntando a una fila que no existe).

**6. El diagnóstico, después.** `0 · 0 · 0 · 0` → «Nada que hacer: esta base ya corre el código
de hoy».

**7. Idempotencia de toda la cadena.** Los cuatro scripts corridos una segunda vez: ninguno creó
ni agregó nada, ninguno dio error, y el historial se quedó en 76 filas sin duplicar.

## Una cosa que conviene saber del historial

Producción tiene **tres filas en `__EFMigrationsHistory` que el código no declara como
migración**:

| MigrationId | Qué es |
|---|---|
| `20260719000000_AddPreregistroAsistente` | archivo sin `[Migration]`; se registró a mano |
| `20260719100000_AddInstitucionBranding` | archivo sin `[Migration]`; se registró a mano |
| `20260827174307_ActividadEspejoPorEntregable` | no existe como archivo en el código |

Las dos primeras están explicadas en `2026-09-24_poner_al_dia.md`. La tercera **no tiene archivo
en el repositorio** y, comprobado contra el esquema, **no dejó rastro estructural**: tras aplicar
la cadena, producción y el modelo coinciden en los 1335 objetos. No estorba y no hay que hacer
nada con ella — pero si alguien se pregunta de dónde sale esa fila, ésta es la respuesta: quedó
de un cambio que después se consolidó de otra forma. `EXCEPT`-ar el historial no es el camino:
`dotnet ef` ignora las filas que no reconoce.
