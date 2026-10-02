# Normalización de marca del portal según el Manual de Marca DIGER

Fecha: 2026-10-02
Estado: aprobado, pendiente de plan de implementación
Fuente normativa: `MANUAL DE MARCA DIGER.pdf` (18 páginas)

## 1. Por qué

El portal dice cumplir el Manual de Marca y no lo cumple. El encabezado de
`tokens.css` afirma estar *"alineado al Manual de Marca DIGER 2026 (azul
gobierno + acento dorado + cian)"*, y `diger.css` repite la afirmación en
comentarios de componente: *"(Manual de Marca: navy + subrayado dorado)"* y
*"Avatar dorado en el header (Manual de Marca)"*.

El manual real no contiene ningún dorado. Su paleta son tres azules y nada
más. El dorado `#ad8411` está a **147° de tono** del color de marca más
cercano, y el manual, en *Usos Incorrectos* punto C, prohíbe expresamente
sustituir el color de marca por tonos fuera de la línea gráfica establecida.

Los comentarios son parte del problema, no ruido alrededor de él: mientras
sigan afirmando que el dorado viene del manual, el siguiente desarrollador lo
reintroduce citándolos de buena fe. Corregirlos es alcance, no cortesía.

## 2. Qué dice el manual

### Paleta (pág. 09) — tres colores, sin más

| Nombre del manual | HEX | RGB | CMYK |
|---|---|---|---|
| Celeste (azul claro) | `#7acbdd` | 122, 203, 221 | C53 M0 Y14 K0 |
| Azul oscuro (azul marino) | `#02395e` | 2, 57, 94 | C100 M76 Y38 K29 |
| Azul medio (corporativo) | `#1a65a3` | 26, 101, 163 | C89 M56 Y10 K1 |

### Tipografía (pág. 08)

Poppins ExtraBold y Poppins Medium.

### Reglas que aplican a medios digitales

- Tamaño mínimo digital: 85 px (pág. 07).
- Área de seguridad: el largo de la primera barra de la "E" (pág. 06).
- Modulación 15X × 5X (pág. 05).
- Tres variantes de logo: completo, abreviada, isotipo. El isotipo es la
  variante que el manual asigna explícitamente a favicons (pág. 12).

## 3. Decisiones tomadas

Las tomó Hércules Cardona el 2026-10-02 durante el diseño:

1. **El dorado se elimina y el celeste ocupa su lugar.** La paleta queda en
   exactamente los tres colores del manual.
2. **Barrido total, por etapas verificables.** Los cinco azules fuera de
   manual colapsan y los ~1,258 literales hex pasan a token, con revisión
   entre etapas en vez de una sola pasada.
3. **Esta pasada es solo color y tipografía.** Los logos quedan fuera: ver
   §9.
4. **Los pesos tipográficos 600 y 700 se conservan**, apartándose de la
   lectura literal del manual. Justificación en §7.

## 4. Arquitectura de la paleta: tres capas

El defecto de origen es que hoy no existe frontera entre color de marca y
color de interfaz. Por eso el dorado pudo instalarse en el header invocando
al manual sin que nadie pudiera contradecirlo con una regla. La paleta nueva
declara tres capas, cada una con su propia regla:

**Capa marca.** Tres valores, literales del manual, inmodificables. Se
escriben una sola vez, con la página del manual en el comentario. No se
derivan ni se ajustan.

**Capa derivada.** Sombras y tintes de los tres anteriores para hover,
degradados, pills y bordes. Regla objetiva de pertenencia: **tono dentro de
±8° de un color de marca**. Es una prueba que se puede correr, no un
criterio que se discute.

**Capa función.** Neutros de texto y borde, más verde/ámbar/rojo de estado.
Están fuera del manual a propósito: el manual norma una identidad gráfica,
no una interfaz de datos, y un error no se puede señalar en azul. Van
documentados como función y quedan prohibidos en cualquier elemento que
represente al logo.

## 5. Mapa de colapso de color

Cada fila trae la evidencia que la justifica. El tono explica *por qué* se
reemplaza; el contraste demuestra que el reemplazo *no degrada* la
accesibilidad por debajo de AA.

| Hoy | Usos | Tono | Pasa a | Contraste s/ blanco | Veredicto |
|---|---|---|---|---|---|
| `#0a2d6e` | 96 | 219° | `#02395e` | 13.02 → 12.00 | AAA · tono ajeno por 12° |
| `#1455a4` | 56 | 213° | `#1a65a3` | 7.33 → 6.11 | AA · reclamaba ser el azul corporativo |
| `#0c447c` | 4 | 210° | `#1a65a3` | 9.84 → 6.11 | AA · se reancla al hex del manual |
| `#1d4ed8` | 15 | 224° | `#1a65a3` | 6.70 → 6.11 | AA · índigo, ajeno por 17° |
| `#022a45` | 1 | 204° | se conserva | — | sombra legítima de `#02395e` |
| `#ad8411` | 8 usos vía `var()` | 44° | se elimina | — | ajeno por 147° |

Referencia de tono de la capa marca: `#02395e` = 204°, `#1a65a3` = 207°,
`#7acbdd` = 191°.

Los cinco colapsos pasan AA o AAA. Esto importa de forma específica: §3 de
`DESIGN.md` documenta que una pasada anterior decidió **no** fusionar estos
hex *"sin poder verificar el contraste visualmente"*. El bloqueo era real y
queda levantado con medición, no con opinión. Ese levantamiento se escribe
en `DESIGN.md` para que nadie vuelva a frenarse por la misma razón.

## 6. Regla de acento: una regla, dos superficies

El dorado hace hoy dos trabajos sobre fondos opuestos, y un solo color no
puede reemplazarlo: el celeste rinde **6.52** sobre el navy del header pero
cae a **1.84** sobre blanco, donde se vería lavado.

- **Sobre navy** (header, footer, avatar, hover del footer): celeste `#7acbdd`.
- **Sobre claro** (subrayado de `.page-title`, bordes de énfasis): azul medio `#1a65a3`.

Ambos son colores del manual. El avatar además invierte el texto: fondo
celeste con texto navy da 6.52, mientras que texto blanco sobre celeste daría
1.84 y fallaría.

Lugares concretos donde vive el dorado hoy (`diger.css`): líneas 49, 1207,
1455, 1875, 1899, 4363, 4517 y 4534.

## 7. Tipografía

El manual especifica dos cortes, ExtraBold y Medium. La realidad del portal
son 367 declaraciones de `font-weight` dominadas por 600 (155) y 700 (212),
con 800 en 76 y 500 en apenas 15.

**Se cumple lo que es inequívoco:**

- `body` no declara `font-weight` y por tanto corre en 400 (Regular). Pasa a
  **500 (Medium)**, que es lo que el manual nombra. Es una línea y es el
  cambio más fiel del conjunto.
- Titulares de primer nivel a **800 (ExtraBold)**. Son exactamente dos
  selectores: `.page-title` y `.pg-title` en `diger.css` (hoy en 700), que
  son el título de página de todo el portal. No se tocan `h2`…`h6` ni los
  títulos de tarjeta: el manual norma el titular, no toda la jerarquía.
- `expediente.css` pisa el `body` con `'Segoe UI'` y saca de marca al módulo
  de Expedientes completo. Esa declaración se elimina y el módulo hereda
  Poppins.

**Se conserva, apartándose de la lectura literal:**

- Los pesos **600 y 700** siguen disponibles como escalones intermedios de
  interfaz, documentados como tales. La página de tipografía del manual
  define la tipografía *de marca* —los cortes del logotipo y los materiales
  institucionales—, no una escala de interfaz. Reducir un portal denso de
  tablas y badges a dos pesos eliminaría la jerarquía visual sin ganar
  fidelidad real.
- `afiche-qr.css` conserva Segoe UI. Está documentado como decisión
  deliberada para impresión sin conexión y es correcta: el afiche se imprime
  en máquinas que pueden no tener Poppins.

Poppins ya se carga en `_Layout.cshtml` con los pesos 400;500;600;700;800 y
los cinco se usan, así que la petición a Google Fonts no cambia.

## 8. Garantía estructural

El usuario pidió explícitamente no cambiar la estructura. Este trabajo es un
cambio de valores, no de forma.

**No se toca:** ninguna etiqueta HTML, ningún nombre de clase CSS, ningún
`asp-page`, la rejilla, los breakpoints, ningún PageModel, ni nada en
Application, Domain o Infrastructure.

**Se toca:** valores de color, el `font-weight` del `body`, la declaración
`font-family` de `expediente.css`, los comentarios que afirman falsedades
sobre el manual, y literales hex que pasan a token.

El bloque `<style>` inline de `_Layout.cshtml` que inyecta `--diger-blue`,
`--diger-blue-dark` y `--diger-blue-mid` por institución **queda intacto**.
La marca DIGER es el valor por defecto y el branding por institución lo sigue
pisando igual que hoy. `InstitucionBrandingService` y la columna
`Instituciones.Color` no se modifican.

No hay cambios de base de datos en esta pasada, así que no aplica la
convención de `database/<fecha>_poner_al_dia.sql`.

### Mecánica de migración: repuntar primero, borrar al final

Los tokens `--diger-blue-title` (`#0a2d6e`) y `--diger-blue-strong`
(`#0c447c`) **no se borran en la etapa 1**. Se repuntan a
`var(--diger-blue)` y `var(--diger-blue-mid)` respectivamente, y se marcan
como obsoletos. Recién se eliminan en la etapa 6, cuando ya no quede ninguna
referencia.

El motivo es que así la etapa 1, por sí sola, deja el portal conforme al
manual sin depender de haber encontrado todas las referencias. Las etapas 3 y
4 pasan a ser limpieza, no corrección: si alguna se escapa, el color que
pinta ya es el correcto.

`--diger-cyan` se renombra a `--diger-celeste`, que es el término del propio
manual. El renombre es gratis porque hoy tiene **cero referencias**.

`--diger-gold` y `--diger-gold-light` se eliminan en la etapa 1, después de
reemplazar sus ocho usos.

El fallback de `:focus-visible` en `diger.css` es
`var(--diger-blue, #1d4ed8)` — el índigo ajeno. Pasa a `#02395e`.

## 9. Fuera de alcance

**Los logos, completos.** El manual exige tres variantes y el portal tiene
una y media: `logo_diger.png` es un raster de 11827×2312 px y 1 MB, no existe
la versión abreviada, y el isotipo del header es un SVG inline con `#BFE3EE`
y `#FFFFFF`, colores que no son del manual, dibujado a 27×20 px cuando el
manual fija 85 px como mínimo digital.

No se aborda porque falta el arte oficial. El archivo `logos.pptx` que se
subió a OneDrive el 2026-10-02 contiene **32 logos de instituciones del
Estado** (Finanzas, CNBS, CONATEL, SAR, SAG, Salud, RNP, ARSA, Aduanas,
BANHPROVI, ENEE, INPREMA, Bomberos, Ihcine y otros), pero **ninguna variante
de DIGER**. Esos 32 son insumo valioso para `Instituciones.LogoUrl`, que es
un campo editable ya existente y hoy vacío, pero son otro trabajo.

Los logos del manual son vectoriales: todo el PDF de 18 páginas contiene una
sola imagen rasterizada, así que el arte bueno existe en vector y conviene
pedirlo antes que reconstruirlo.

**Hallazgo registrado, no corregido aquí:**
`InstitucionOptions.Nombre` tiene como valor por defecto en código
*"Dirección General de Gobierno Digital"*, mientras que el manual y
`appsettings.json` dicen *"Dirección de Gestión por Resultados"*. Es un
cambio de una línea en `src/Application/Common/Models/InstitucionOptions.cs`
pero toca C# y no CSS; se deja señalado para decidirlo aparte.

## 10. Etapas

Cada etapa cierra con compilación, los 754 tests, revisión en pantalla y
recuento de literales hex restantes como medida objetiva de avance.

1. **Tokens.** Corregir valores, eliminar el dorado, repuntar los obsoletos,
   renombrar `--diger-cyan`, declarar las tres capas y corregir el encabezado
   falso de `tokens.css` junto con los comentarios de `diger.css`.
2. **Superficies de marca.** Header, footer, gov-bar, login, nav y avatar: es
   donde la marca se lee y donde vivía el dorado.
3. **Resto del CSS.** Los literales hex restantes de `diger.css` (174),
   `expediente.css` (41), `tramites-indicadores.css` (18) y `modal.css` (15).
   `afiche-qr.css` (10) se revisa pero su Segoe UI no se toca (§7).
4. **Vistas `.cshtml`.** Los 940 literales, por módulo, en orden de densidad:
   `Tableros/MiTablero` (52), `Siger/Conciliacion` (42), `Recursos/Index`
   (33), `Expedientes/Cronograma` (33), `Reuniones/CompromisoDetalle` (32),
   `Ayuda/Index` (31) y el resto.
5. **Tipografía.** `body` a 500 y eliminación del Segoe UI de
   `expediente.css`.
6. **Cierre.** Eliminar los tokens obsoletos ya sin referencias, reverificar
   el contraste de la paleta final completa y actualizar `DESIGN.md`:
   registrar las tres capas, la regla de ±8°, la regla de acento por
   superficie, y el levantamiento documentado del bloqueo de §3.

## 11. Verificación

- **Contraste:** cada par texto/fondo de la paleta final debe alcanzar AA
  (4.5:1) para texto normal. Los valores de §5 ya están medidos y se
  reverifican al cierre sobre la paleta completa.
- **Pertenencia de tono:** ningún color de las capas marca o derivada puede
  quedar a más de 8° de tono de un color del manual. Es comprobable con un
  script.
- **Literales restantes:** el conteo de hex debe bajar monótonamente entre
  etapas. Punto de partida: 318 en CSS y 940 en `.cshtml`.
- **Regresión:** `dotnet build` y `dotnet test` (754 pruebas) verdes en cada
  etapa.
- **Ausencia de dorado:** `grep` de `diger-gold`, `#ad8411` y `#c79621` debe
  devolver cero al cierre.

## 12. Riesgos

**`#0a2d6e` tiene 96 usos y se aclara un punto de contraste** (13.02 →
12.00). Sigue en AAA, pero el cambio será perceptible en los títulos del
portal. Es el precio de cumplir el manual y se asume conscientemente.

**`.page-title` pierde el subrayado dorado**, que es un rasgo visualmente
característico del portal hoy. Pasa a azul medio y el resultado se verá más
sobrio. Es consecuencia directa de la decisión 1 de §3.

**El riesgo de alcance** —1,258 literales es mucha superficie— se mitiga con
la mecánica de §8: la etapa 1 ya deja el portal conforme, y las etapas 3 y 4
son limpieza cuyo fallo no produce color incorrecto.
