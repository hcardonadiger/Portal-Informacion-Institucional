# Lengua de casa DIGER

La doctrina visual del portal. Se decidió el 2026-10-13 comparando lo que el portal
mostraba contra las alternativas, apartado por apartado, sobre mediciones de
`src/Web/wwwroot/css/diger.css` y `tokens.css` — no sobre gusto.

**Para qué existe.** Un diseño deja de ser genérico cuando su especificidad viene de
algún lugar concreto. Aquí viene de este documento: se decidió una vez y se impone en
cada pantalla, en vez de volver a decidirse —distinto cada vez— en cada una. Eso es
lo que hace que el portal se reconozca a sí mismo.

**La tesis en una línea.** Esto es un **registro institucional**, no un tablero
decorado: el color significa excepción, la jerarquía la lleva el peso, y nada flota
sin estar de verdad encima de algo.

Cada regla de abajo es **falsable**: se puede comprobar con un `grep` o mirando la
pantalla. Si una regla no se puede comprobar, no pertenece a este documento.

---

## 1. Geometría

`--diger-radius-sm` se queda en **8px**. Fue una decisión explícita: endurecer la
superficie grande, no la geometría entera.

`--diger-radius-lg` es **8px** (bajó de 16px). Viste `.card`, que es la superficie de
70 de las 105 páginas; ese redondeo generoso repetido en casi todo el portal era una
de las dos cosas que lo hacían leerse como plantilla.

`--diger-radius-pill` está **reservado a lo que es un conteo**: `.chip-conteo`,
`.ficha-tabs .n`, `.dep-mas`. Nada más. Un estado nunca es píldora.

Las **barras de dato llevan 2px**, no `radius-sm`: un radio de 8px sobre una barra de
10px de alto vuelve a ser casi una píldora.

Los **avatares usan `border-radius: 50%`**, no el token. Lo circular ahí es la forma
del dato, y se queda.

> Comprobación: `grep -c radius-pill diger.css` → **3**.

## 2. Estado y color

**El color significa excepción.** Lo normal no lleva color.

Un estado se lee por **tipografía**: versalitas, `--diger-fs-2xs`, peso 700,
`letter-spacing: .07em`, sin fondo y sin caja. `.badge-secondary` y `.badge-light`
—lo normal— se apagan en `--diger-text-muted` y bajan a peso 500.

El motivo no es estético. Un fondo pastel hace que lo normal llame tanto la atención
como la excepción, de modo que el color deja de informar; no sobrevive a una
impresión en blanco y negro, y estos expedientes se imprimen; y una fila con tres
badges de colores distintos se vuelve ruido justo donde hace falta comparar.

La paleta es la de `tokens.css`, con sus tres capas (marca / derivada / función) y su
regla de pertenencia de ±8° de tono. **No se discute aquí**: la gobierna el Manual de
Marca y la vigila `scripts/verificar_marca.py`. Un color nuevo que no quepa en una de
las tres capas es un incumplimiento, no una propuesta.

## 3. Superficies y profundidad

**La profundidad significa estar encima.** Si algo lleva sombra, está encima de algo.

- `--diger-shadow-sm` / `-md` / `-lg` forman una escala **monótona**: más nivel, más
  profundidad. Antes no lo eran (`lg` reducía el desenfoque pero subía al 30%: no era
  más profunda que `md`, era más oscura).
- `--diger-shadow-btn` existe porque el 7% de `sm` es invisible sobre el relleno
  `#02395e` del botón primario.
- `--diger-shadow-float` es para lo que de verdad flota. Hoy su único consumidor es
  `.dep-dialog`.

Una **tabla de registro no flota**: sin radio, sin sombra, sin fondo. Es el contenido
de la página, no un objeto encima de ella. El oficio de separar lo hacen las reglas
horizontales, y el encabezado se sostiene con una regla inferior más firme que las de
las filas.

**Cero gradientes.** Un gradiente mete colores que nadie eligió: aparecen por
interpolación. Si hace falta color, es uno de la paleta, plano.

> Comprobación: `grep -c gradient diger.css` → **0**.

## 4. Movimiento

**Nada brinca bajo el cursor.** Una lista de expedientes se recorre comparando, y un
elemento que se mueve estorba justo en ese momento.

Al pasar el ratón se **marca el borde** (`--diger-blue-mid`, o `--diger-blue-pale` en
`.card`). Tres excepciones por la forma del elemento, no por capricho:

- un **botón relleno** no tiene borde que marcar → oscurece el relleno
  (`--diger-blue-dark`);
- un **envoltorio sin borde** marca el del elemento que envuelve
  (`.kpi-link:hover .kpi-card`);
- una **fila de tabla** tiñe el fondo (`.seg-table tr:hover td`), que es lo que hace
  cualquier hoja de cálculo, y por una razón.

La animación de **aparición** de un modal (`transform: scale`) no es movimiento de
hover y no se toca.

> Comprobación: `grep -c translateY diger.css` → **0**.

## 5. Cifras y códigos

`--diger-font-mono` (`IBM Plex Mono`) es **obligatorio en el numeral de KPI**, con
`font-variant-numeric: tabular-nums`, para que las cifras alineen entre tarjetas —que
es lo que uno hace con ellas: compararlas en columna.

En **tablas sigue siendo opcional** por decisión del 2026-10-13, con una advertencia
registrada: la tabla quedó sin caja ni fondo, y es justo donde la falta de
monoespaciado más pesa, porque lo único que separa las columnas es que los glifos
alineen. Si alguna vez se revisa un apartado de esta lengua de casa, éste es el
candidato.

El numeral de KPI va a **1.4rem y peso 600**, no a 30px y peso 800. Era el acento
visual más fuerte del portal puesto sobre un dato que rara vez es el más importante
de su pantalla.

## 6. Fuentes: lo que es verdad hoy

- `--diger-font-ui` es **Manrope** y es la que de verdad se ve.
- `--diger-font-marca` declara **Poppins**, pero **la CSP bloquea las fuentes
  externas**: el portal nunca la ha renderizado. El CSS calculado miente sobre esto —
  para saber qué fuente se está usando hay que **medir el ancho del texto**, no leer
  `font-family`.
- `var(--token)` **no resuelve** dentro de SVG ni en Chart.js. Ahí va el valor.

---

## Lo que esta lengua de casa NO decide

- La **paleta**: la gobierna el Manual de Marca y `scripts/verificar_marca.py`.
- La **composición** de una pantalla (qué región manda, qué va primero): eso es
  trabajo de la skill `office-web-ui-system`.
- El **modo oscuro**: no existe. Se eliminó el 2026-09-24 y el portal es claro por
  construcción, porque el registro público de asistencia dependía de que corriera
  JavaScript para no salir oscuro.

## Dónde está el registro de por qué

La comparación que produjo estas decisiones, con las muestras lado a lado:
https://claude.ai/code/artifact/abb95180-3eef-46bc-98e4-8b9fe4b2ea8b
