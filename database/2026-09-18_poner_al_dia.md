# Poner al día la base — 2026-09-18

**Reemplaza a los cuatro scripts sueltos de ese día.** Si ya corrió alguno, no importa: este
converge al estado final desde donde sea que esté.

## Por qué hubo que rehacerlos

Los cuatro anteriores decidían qué hacer mirando `__EFMigrationsHistory`, y esa fila se escribe
**al final**. Si la corrida moría a la mitad quedaban cambios hechos y ninguna marca de que se
hicieron, así que volver a correr el script lo intentaba todo de nuevo y fallaba de otra manera
—por ejemplo, buscando la columna `Prioridad` que ya se había borrado—.

Éste **no pregunta por el historial**. Cada paso comprueba su propia condición contra el catálogo
de la base: ¿existe la tabla?, ¿existe la columna?, ¿ya está esa fila? Da igual el punto de
partida:

| Estado de la base | Qué hace |
|---|---|
| Sin empezar | Lo aplica todo |
| A medias | Retoma donde quedó, sin repetir ni duplicar |
| Ya completa | Nada |

Al terminar escribe las cuatro filas de `__EFMigrationsHistory` que falten, para que `dotnet ef`
sepa que ya están.

## Cómo correrlo

```powershell
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-18_poner_al_dia.sql
```

O péguelo en SSMS y ejecute.

Las banderas no son adorno:

- **`-b`** detiene en el primer error. **Sin ella, `sqlcmd` sigue de largo, llega al `COMMIT` y
  confirma lo que alcanzó a hacer** — así es como una base termina a medias. El script además trae
  `SET XACT_ABORT ON`, que hace lo mismo desde adentro: cualquier error deshace todo. O entra
  completo o no entra nada.
- **`-I`** activa `QUOTED_IDENTIFIER`, que exige el índice filtrado de `Proyectos.Codigo`.
- **`-f 65001`** lee el archivo como UTF-8, para que las tildes no lleguen corrompidas.

## Qué deja

| Catálogo | Contenido |
|---|---|
| Prioridades de proyecto | Alta, Media, Baja |
| Prioridades de ticket | Critica, Alta, Media, Baja |
| Categorías de proyecto | Q3, Q2, Q1 |

`Proyectos.Prioridad` y `Tickets.Prioridad` pasan de texto a llave foránea, y aparece
`Proyectos.CategoriaId`, que **admite nulo**: los proyectos que ya existen quedan sin clasificar.

Lo que no case con ninguna prioridad —nulo, vacío o un texto que nadie reconoce— toma la
predeterminada (`Media`), y queda dicho acá para que no sorprenda.

`Critica` va sin tilde porque así está guardado en los tickets que ya existen. Se puede renombrar
desde la pantalla apenas termine.

## Comprobación

El script la imprime solo al final. Tiene que decir:

```
Proyectos sin prioridad válida (debe ser 0)          0
Tickets sin prioridad válida (debe ser 0)            0
Columna Proyectos.Prioridad (debe decir eliminada)   eliminada
Columna Tickets.Prioridad (debe decir eliminada)     eliminada
Migraciones registradas (deben ser 4)                4
```

Si alguna prioridad `Q3` quedara en el catálogo de prioridades, es que algún proyecto la tiene
asignada: reasígnelos y vuelva a correrlo.

## Cómo se probó

Sobre una base desechable llevada al mismo punto de partida que producción, con proyectos y
tickets sembrados a propósito —uno por cada prioridad, uno con un valor que no casa y uno borrado
lógicamente—, en cuatro escenarios:

1. **Desde cero.** Aplica todo; los datos viajan y no queda ningún huérfano.
2. **Con los cambios hechos y el historial borrado** — el caso que falló en producción. Corre
   limpio y repone las marcas.
3. **Sobre una base ya completa.** No hace nada.
4. **A medias de verdad**: tabla creada y sembrada, columna agregada pero vacía, columna vieja
   todavía presente. Retoma donde quedó, mueve los datos y no duplica la semilla.

## Después

Regenere la plantilla de Excel, porque sus listas salen de la base:

```powershell
cd database\plantillas
.\Nueva-Plantilla-Proyectos.ps1 -Servidor <servidor> -BaseDatos <base>
```

Y la matriz de permisos va aparte, desde la pantalla: ver
[`2026-09-18_permisos_catalogos.md`](2026-09-18_permisos_catalogos.md).
