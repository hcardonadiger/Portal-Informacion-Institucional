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
5. **Entran las cuatro superficies que la auditoría inicial no cubría**
   (§5.1): JavaScript, reportes PDF, modelos de UI en C# y los colores de
   rol. Los roles van en la última etapa, con su script idempotente en
   `database/`, para que el cambio de base quede aislado y se pueda aprobar
   o revertir por separado. Los PDF pasan de Arial a Poppins.

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
| `#c8d5ee` | 1 | 219° | `#c8ddee` | — | tinte con el mismo sesgo violeta |
| `#e8f0ff` | 1 | 219° | `#e8f5ff` | — | tinte con el mismo sesgo violeta |
| `#ad8411` | 8 usos vía `var()` | 44° | se elimina | — | ajeno por 147° |

Referencia de tono de la capa marca: `#02395e` = 204°, `#1a65a3` = 207°,
`#7acbdd` = 191°.

Los dos tintes (`--diger-blue-pale` y `--diger-blue-light`) los descubrió el
guardián de §11 al correrlo, no la lectura manual: arrastran exactamente el
mismo sesgo violeta de 219° que `#0a2d6e`. La corrección conserva saturación
y luminosidad y solo endereza el tono, así que el cambio es casi
imperceptible en pantalla pero los mete en familia.

### 5.1 Superficies más allá del CSS

La auditoría inicial cubría `wwwroot/css` y `Pages/*.cshtml`. Faltaban
cuatro superficies que también pintan marca, con ~200 literales más:

| Superficie | Literales | Archivos |
|---|---|---|
| JavaScript | 91 | `expediente.js` (69), `tramites-indicadores.js` (21), `afiche-qr.js` (1) |
| Reportes PDF (C#) | 72 | `InformeService.cs` (39), `ProyectoPdfService.cs` (22), `ActaPdfService.cs` (11) |
| Modelos de UI (C#) | 37 | `CalendarioUi.cs` (23), `CompromisoUi.cs` (14) |
| Colores de rol (BD) | 6 | sembrados en `20260813143220_AddRolesDinamicos` |

**Los reportes PDF son el caso más grave y el menos negociable.** Los tres
servicios declaran `FontFamily("Arial")`: el acta y los informes son
documentos oficiales que salen de la institución con la marca encima y hoy
no usan la tipografía de marca. Sus azules `#1a3a5c` (211°) y `#2e6da4`
(208°) sí caen en familia, pero el color de texto `#1c2333` está a 15°.

**Los colores de rol son un cambio de base de datos.** `Rol.Color` se
almacena en la tabla `Roles` y se pinta como fondo de badge en
`/Accesos/Roles` y `/Usuarios/Index`. Los seis valores sembrados
(`#3c3489`, `#0c447c`, `#1a5fa0`, `#1b5e20`, `#085041`, `#6d4c00`) están
fuera de la paleta. Tocarlos obliga al script idempotente de `database/`
que exige CLAUDE.md: la migración de EF no alcanza, porque las bases
desplegadas se actualizan corriendo SQL a mano. Dato revelador: el editor de
roles ya tiene como *placeholder* `#1A65A3`, que es exactamente el azul
medio del manual.

### 5.2 El dorado del calendario no es acento, es estado

Tres de los ocho usos del dorado no son marca: `.cal-chip.priv`,
`.cal-dia-item.priv` y `.cal-dia-item.priv .cal-dia-hora` distinguen las
**reuniones privadas**. El comentario del CSS las llama "dorado
institucional", pero funcionalmente son un indicador de estado y pertenecen
a la capa función, no a la capa marca.

Pasan a un token propio `--diger-privado`, valuado en el `#b45309` que ya
existe como `--diger-warning`. Así el hex del dorado desaparece por completo
y el color queda nombrado por su función en vez de por una afirmación de
marca falsa.

Esto además corrige un defecto real de accesibilidad: `#ad8411` como texto
sobre blanco da **3.45 y no alcanza AA**; `#b45309` da **5.02 y sí**.

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

Lugares concretos donde vive el dorado hoy, en `diger.css`, ya separados por
su naturaleza real:

**Acento de marca (5)** — se reemplazan por la regla de arriba:

| Línea | Selector | Fondo | Pasa a |
|---|---|---|---|
| 49 | `.page-title` / `.pg-title` (borde inferior) | claro | azul medio |
| 1207 | `.app-header` (borde inferior) | navy | celeste |
| 1455 | `.user-menu-btn .side-avatar` (fondo) | navy | celeste + texto navy |
| 1875 | `.diger-footer` (borde superior) | navy | celeste |
| 1899 | `.diger-footer-inner a:hover` | navy | celeste |

**Estado, no marca (3)** — pasan a `--diger-privado` según §5.2: líneas
4363 (`.cal-chip.priv`), 4517 (`.cal-dia-item.priv`) y 4534
(`.cal-dia-item.priv .cal-dia-hora`).

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

### 7.1 Los reportes PDF

`ActaPdfService`, `InformeService` y `ProyectoPdfService` declaran
`FontFamily("Arial")`. Pasan a Poppins. El motor es QuestPDF 2024.10.4 con
licencia Community, que no descarga fuentes: hay que embarcar los archivos.

- Se agregan `Poppins-Medium.ttf` y `Poppins-ExtraBold.ttf` en
  `src/Infrastructure/Reports/Fuentes/`, incluidos en el `.csproj` como
  `EmbeddedResource`. Poppins se distribuye bajo SIL Open Font License, que
  permite la redistribución, de modo que embarcarlos es legítimo.
- Se registran una sola vez con `FontManager.RegisterFont(stream)` junto al
  `Settings.License = LicenseType.Community` que los tres servicios ya
  ejecutan.
- Si el registro falla, QuestPDF cae a la fuente por defecto en vez de
  reventar, así que el peor caso es un PDF con otra tipografía, no un error
  en producción.

## 8. Garantía estructural

El usuario pidió explícitamente no cambiar la estructura. Este trabajo es un
cambio de valores, no de forma.

**No se toca:** ninguna etiqueta HTML, ningún nombre de clase CSS, ningún
`asp-page`, la rejilla, los breakpoints, ningún PageModel, ninguna entidad de
Domain, ningún handler de Application, ni el esquema de la base. Ninguna
firma de método cambia, ninguna consulta cambia, ningún comportamiento
cambia.

**Se toca:** valores de color —estén en CSS, en `.cshtml`, en `.js`, en una
constante de C# o en una fila sembrada—, el `font-weight` del `body`, la
`font-family` de `expediente.css` y de los tres servicios de PDF, los
comentarios que afirman falsedades sobre el manual, y literales hex que pasan
a token.

Las superficies de §5.1 obligan a tocar archivos `.cs`, pero solo en el
valor de constantes y literales de color: `CalendarioUi.cs` y
`CompromisoUi.cs` siguen devolviendo la misma tupla con la misma forma, y los
servicios de PDF siguen generando el mismo documento con el mismo layout.

El bloque `<style>` inline de `_Layout.cshtml` que inyecta `--diger-blue`,
`--diger-blue-dark` y `--diger-blue-mid` por institución **queda intacto**.
La marca DIGER es el valor por defecto y el branding por institución lo sigue
pisando igual que hoy. `InstitucionBrandingService` y la columna
`Instituciones.Color` no se modifican.

**Sí hay un cambio de base de datos**, acotado a la última etapa: los seis
valores de `Roles.Color` (§5.1). Aplica íntegra la convención de CLAUDE.md,
porque las bases desplegadas se actualizan corriendo SQL a mano y una
migración de EF por sí sola nunca les llegaría. El script
`database/<fecha>_poner_al_dia.sql` debe actualizar cada fila **solo si
conserva el color sembrado**, para no pisar un color que alguien haya
cambiado a mano desde el editor de roles, y debe ser idempotente: correrlo
dos veces no cambia nada la segunda vez.

Ningún otro dato se toca. El esquema no cambia: es un `UPDATE` de valores,
sin `ALTER TABLE`, así que no hay migración de EF asociada ni fila nueva en
`__EFMigrationsHistory`.

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

Cada etapa cierra con el guardián de §11 en verde para lo que esa etapa
cubre, `dotnet build`, los 754 tests y revisión en pantalla.

0. **El guardián.** Crear `scripts/verificar_marca.py` antes de cambiar nada.
   Debe fallar: ese fallo inicial es la línea base contra la que se mide todo
   lo demás.
1. **Tokens.** Corregir valores, eliminar el dorado, agregar
   `--diger-privado`, repuntar los obsoletos, renombrar `--diger-cyan`,
   enderezar los dos tintes violetas, declarar las tres capas y corregir el
   encabezado falso de `tokens.css` junto con los comentarios de `diger.css`.
2. **Superficies de marca.** Header, footer, gov-bar, login, nav, avatar y
   el calendario privado: es donde la marca se lee y donde vivía el dorado.
3. **Resto del CSS.** Los literales restantes de `diger.css`,
   `expediente.css`, `tramites-indicadores.css` y `modal.css`.
   `afiche-qr.css` se revisa pero su Segoe UI no se toca (§7).
4. **Vistas `.cshtml`.** Por módulo, en orden de densidad:
   `Tableros/MiTablero` (52), `Siger/Conciliacion` (42), `Recursos/Index`
   (33), `Expedientes/Cronograma` (33), `Reuniones/CompromisoDetalle` (32),
   `Ayuda/Index` (31) y el resto.
5. **JavaScript.** `expediente.js` (69), `tramites-indicadores.js` (21) y
   `afiche-qr.js` (1). Los colores de gráfica se leen de las variables CSS
   con `getComputedStyle` donde se pueda, y se dejan como constante
   nombrada donde no.
6. **Modelos de UI en C#.** `CalendarioUi.cs` (23) y `CompromisoUi.cs` (14).
   Misma tupla, mismos nombres, otros valores.
7. **Reportes PDF.** Los 72 literales de los tres servicios y el paso de
   Arial a Poppins (§7.1). Se verifica generando un acta y un informe y
   abriéndolos.
8. **Tipografía del portal.** `body` a 500, `.page-title`/`.pg-title` a 800
   y eliminación del Segoe UI de `expediente.css`.
9. **Colores de rol.** Los seis valores de `Roles.Color`, con el script
   idempotente de `database/`. Va al final y aislada a propósito: es el único
   cambio de datos y debe poder aprobarse o revertirse sola.
10. **Cierre.** Eliminar los tokens obsoletos ya sin referencias, correr el
    guardián completo en verde y actualizar `DESIGN.md`: registrar las tres
    capas, la regla de ±8°, la regla de acento por superficie, las cuatro
    superficies de §5.1 y el levantamiento documentado del bloqueo de §3.

## 11. Verificación

La verificación no es una lista que alguien recuerda correr: es un programa,
`scripts/verificar_marca.py`, que se crea en la etapa 0 y devuelve código de
salida 1 mientras algo incumpla. Comprueba cuatro cosas:

1. **Colores prohibidos.** Que no quede `#ad8411`, `#c79621`, `#0a2d6e`,
   `#1d4ed8`, `#1455a4` ni `#0c447c` en ningún archivo, ni los tokens
   `--diger-gold` / `--diger-gold-light`.
2. **Pertenencia de tono.** Que ningún token de marca o derivado quede a más
   de 8° de un color del manual. Los tintes muy claros se eximen por
   luminancia (>0.75), no por nombre: un tinte casi blanco pierde tono por
   saturación baja y penalizarlo sería ruido.
3. **Contraste AA.** Que cada par texto/fondo declarado alcance 4.5:1.
4. **Censo de literales.** Conteo de hex de 6 dígitos, que debe bajar
   monótonamente entre etapas.

**Línea base medida el 2026-10-02, antes de tocar nada:** 82 fallos; censo de
242 literales en CSS y 884 en vistas, 1126 en total. El censo cuenta solo hex
de seis dígitos; las formas abreviadas como `#fff` quedan fuera a propósito,
porque son casi siempre blanco o negro y no colores de marca.

A ese censo se suman las superficies de §5.1 —91 en JavaScript, 72 en
reportes, 37 en modelos de UI— que el guardián debe incorporar al ampliarse
en la etapa 5.

Complementan al guardián:

- **Regresión:** `dotnet build` y `dotnet test` (754 pruebas) verdes en cada
  etapa.
- **Revisión en pantalla:** cada etapa se mira en el navegador. El guardián
  prueba que los números cuadran, no que la página se ve bien.
- **PDF:** la etapa 7 se verifica generando un acta y un informe reales y
  abriéndolos, porque ninguna prueba automática mira un PDF.
- **Script SQL:** la etapa 9 se verifica corriéndolo dos veces contra
  `GestionGD_new` y comprobando que la segunda vez no cambia ninguna fila.

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
