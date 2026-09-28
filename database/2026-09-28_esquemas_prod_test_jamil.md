# Esquemas · Producción → TEST → Jamil · 2026-09-28

Dos scripts, cada uno para una base:

| # | Script | Corre en | Lleva la base a |
|---|---|---|---|
| A | `2026-09-28_A_produccion_al_esquema_de_test.sql` | `GestionGD` (Producción) | el esquema de `GestionGD_TEST` |
| B | `2026-09-28_B_test_al_esquema_de_jamil.sql` | `GestionGD_TEST` | el esquema de la rama `Jamil` |

```
sqlcmd -S <servidor> -d GestionGD      -b -I -f 65001 -i 2026-09-28_A_produccion_al_esquema_de_test.sql
sqlcmd -S <servidor> -d GestionGD_TEST -b -I -f 65001 -i 2026-09-28_B_test_al_esquema_de_jamil.sql
```

**Saque copia de seguridad antes de cada uno.** Las banderas no son adorno: `-b` detiene
en el primer error (sin ella sqlcmd sigue de largo y confirma lo que alcanzó a hacer), `-I`
lo exigen los índices filtrados y `-f 65001` las tildes. Los dos se pueden correr las
veces que haga falta: cada paso comprueba su propia condición contra la base, y la segunda
corrida no hace nada.

---

## Los respaldos de partida

`GestionGD.bak` trae **cinco** respaldos dentro, tres de ellos de TEST. El de Producción
de hoy es el de la **posición 5**:

| Pos. | Base | Fecha |
|---|---|---|
| 1 | GestionGD_TEST | 02-09 |
| 2 | GestionGD | 02-09 |
| 3 | GestionGD_TEST | 21-09 |
| 4 | GestionGD_TEST | 24-09 |
| **5** | **GestionGD** | **28-09 09:10** |

`GestionGD_TEST.bak` trae uno solo, del 28-09 09:09. Todo lo de abajo se probó sobre
esos dos: posición 5 y posición 1.

---

## A · Producción al esquema de TEST

Producción es un prefijo exacto de TEST: le faltan cinco migraciones y nada más —ni una
columna hecha a mano de diferencia—.

- **Catálogos de prioridades y categorías** (cuatro migraciones del 18-09). Convierten
  `Proyectos.Prioridad` y `Tickets.Prioridad`, que eran texto, en una llave a su catálogo.
  Cada fila conserva su prioridad: las 40 de proyectos (22 Alta, 16 Media, 2 Baja) y la
  del ticket quedaron igual, ahora por Id.
- **`Reuniones.EncuestaActiva`** (`bit NOT NULL DEFAULT 0`), del 23-09.

Es la unión, sin cambios, de dos scripts ya probados: `2026-09-18_poner_al_dia.sql`, que
está en Jamil, y `2026-09-24_poner_al_dia.sql`, que está en las ramas de los compañeros
(`fix/orden-reuniones-ultima-creacion` y otras) y no en Jamil.

**Qué se comprobó**

- Dos corridas seguidas, salida 0 las dos.
- La huella del esquema —columnas, tipos, nulabilidad, defaults, índices con sus filtros,
  llaves foráneas, checks y migraciones, 1.294 líneas— queda **idéntica** a la de TEST.
- Ningún dato se pierde: las únicas tablas que cambian de conteo son los tres catálogos
  nuevos (10 filas) y el historial de migraciones (+5).
- El código de `dev` y el de `fix/orden-reuniones-ultima-creacion` leen las 81 entidades
  de su modelo sin una falla. Contra la Producción sin actualizar fallan en 5 y 6.

## B · TEST al esquema de Jamil

A TEST le faltan diez migraciones de Jamil, las de SIGER de finales de agosto. No forman
un bloque: están intercaladas entre migraciones que TEST ya tiene, por eso el script va por
tramos. Es, sin cambios, `2026-09-21_poner_al_dia_siger.sql`, revalidado contra el TEST de
hoy, que ya trae una migración más que cuando se escribió.

TEST tiene además **tres migraciones que Jamil no trae**, y el script **no las quita**:

| Migración | De dónde viene | Qué deja | ¿Le estorba a Jamil? |
|---|---|---|---|
| `AgregarModuloAmbito` | sólo `rescate/modulo-ambito` | tabla `ModuloAmbitos` | No: Jamil no la usa |
| `AddReunionEncuestaActiva` | `fix/orden-reuniones…`, `kirby_opt`, `pichu_opt`, `worktree-afiche-*` | `Reuniones.EncuestaActiva` con default 0 | No: los `INSERT` de Jamil toman el default |
| `ActividadEspejoPorEntregable` | ninguna rama: se aplicó desde código local | sólo datos | No: no toca esquema |

Quitarlas rompería el código de las otras ramas que corre sobre TEST.

**Qué se comprobó**

- Dos corridas seguidas, salida 0 las dos.
- Contra una base construida desde cero con las migraciones de Jamil, a TEST **no le falta
  nada**. Le sobra lo de la tabla de arriba y tres columnas que ninguna migración crea
  (`Asistentes.EsPreregistro`, `Asistentes.Confirmado`, `Instituciones.Color`), que todas
  las bases en uso ya tienen.
- Ningún dato se pierde: se crean cuatro tablas —dos se llenan con 16 entregables y 75
  lugares derivados de los expedientes existentes— y el historial pasa de 74 a 84.
- El código de Jamil lee sus 85 entidades sin una falla. **Contra el TEST sin actualizar
  falla en 8** (columnas y tablas de SIGER), lo que confirma que la prueba detecta.

## Cómo se probó "que corra"

Una comparación de esquema no ejecuta código, así que además se compiló cada rama tal como
está en git y se abrió su `AppDbContext` real contra la base, leyendo **todas** las
entidades del modelo: si falta una columna que el modelo mapea o un tipo no cuadra, EF
falla ahí. No es un recorrido por las pantallas: no escribe, no prueba permisos, no inicia
sesión.

Una advertencia honesta: de seis corridas de esa sonda sobre Producción actualizada, **una**
dio 21 fallas sin que quedaran los mensajes. Repetida cinco veces —en la misma base, en dos
copias nuevas desde cero y con la invocación idéntica— dio 81 de 81 todas las veces, y el
log de SQL Server no muestra nada en ese momento. No sé qué fue. La huella del esquema, que
no depende de la sonda, es idéntica a TEST.

## Y después: Jamil a Producción

Probado también el camino completo: **Producción + A + B** deja una base donde el código
de Jamil lee sus 85 entidades sin falla. Cuando toque llevar Jamil a Producción, son estos
dos scripts, en ese orden.

El guion de Honduras Ágil (`2026-09-28_pry_2026_25_puesta_al_dia.sql`) da el mismo 93 % en
los tres estados: Producción sin tocar, con A, y con A y B. No importa el orden.
