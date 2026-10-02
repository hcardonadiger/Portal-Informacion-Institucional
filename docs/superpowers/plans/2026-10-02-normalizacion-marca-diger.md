# Normalización de marca DIGER — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que el portal cumpla el Manual de Marca DIGER en color y tipografía, sin cambiar su estructura.

**Architecture:** La paleta se reorganiza en tres capas con reglas distintas (marca / derivada / función) dentro de `tokens.css`. Los tokens obsoletos se repuntan a la capa marca antes de migrar sus usos, de modo que la etapa 1 por sí sola ya deja el portal conforme y todo lo posterior es limpieza. Un programa, `scripts/verificar_marca.py`, convierte el cumplimiento en algo medible y falla mientras algo incumpla.

**Tech Stack:** CSS con custom properties, Razor Pages (.NET 9), JavaScript sin framework, QuestPDF 2024.10.4, SQL Server, Python 3 para el guardián.

**Spec:** `docs/superpowers/specs/2026-10-02-normalizacion-marca-diger-design.md`

## Global Constraints

Estas reglas aplican a **todas** las tareas:

- **Los tres colores del manual son literales exactos y no se tocan:** `#02395e` (azul oscuro), `#1a65a3` (azul medio), `#7acbdd` (celeste).
- **Colores prohibidos en todo el repositorio:** `#ad8411`, `#c79621`, `#0a2d6e`, `#1d4ed8`, `#1455a4`, `#0c447c`.
- **Mapa de sustitución.** Vale para CSS, `.cshtml`, `.js` y C#. Las tareas 4, 5, 6, 7 y 8 lo aplican tal cual:

  | Literal | Pasa a (CSS) | Pasa a (JS / C#) |
  |---|---|---|
  | `#0a2d6e` | `var(--diger-blue)` | `#02395e` |
  | `#1455a4` | `var(--diger-blue-mid)` | `#1a65a3` |
  | `#0c447c` | `var(--diger-blue-mid)` | `#1a65a3` |
  | `#1d4ed8` | `var(--diger-blue-mid)` | `#1a65a3` |
  | `#022a45` | `var(--diger-blue-dark)` | `#022a45` |
  | `#c8d5ee` | `var(--diger-blue-pale)` | `#c8ddee` |
  | `#e8f0ff` | `var(--diger-blue-light)` | `#e8f5ff` |
  | `#ad8411` (acento) | según regla de acento | — |
  | `#ad8411` (calendario) | `var(--diger-privado)` | `#b45309` |
  | `#e2e8f0` | `var(--diger-border)` | — |
  | `#6b7fa3` | `var(--diger-text-muted)` | — |

  **Para cualquier literal fuera del mapa:** si está a ≤8° de tono de un color del manual, usar el token derivado más cercano. Si está a más de 8°, **dejarlo y anotarlo** — es candidato a token nuevo de la capa función, no a colapso silencioso. Colapsar un color de estado contra un azul de marca rompe la distinción que ese color existía para hacer.
- **Regla de pertenencia:** ningún color de marca o derivado puede quedar a más de 8° de tono de un color del manual.
- **Regla de acento:** celeste sobre fondo navy, azul medio sobre fondo claro. Nunca al revés.
- **Contraste mínimo:** WCAG AA, 4.5:1 para texto normal.
- **No se cambia estructura:** ninguna etiqueta HTML, nombre de clase, `asp-page`, rejilla, breakpoint, PageModel, entidad, handler ni esquema de base. Solo valores.
- **`afiche-qr.css` conserva Segoe UI.** Es una decisión documentada para impresión sin conexión. No se toca.
- **Cada tarea cierra con commit propio.** Mensaje en español, sin prefijos tipo `feat:`, describiendo el porqué y no el qué.
- **Verificación obligatoria antes de cada commit:** `python scripts/verificar_marca.py` y `dotnet build Diger.TramitesEstado.sln`.

---

### Task 1: El guardián de marca

Convierte el cumplimiento en un programa. Debe existir y **fallar** antes de cambiar nada: ese fallo es la línea base.

**Files:**
- Ya creado: `scripts/verificar_marca.py` (commit `26f8663`)

**Interfaces:**
- Produce: `python scripts/verificar_marca.py` → código de salida 0 si todo pasa, 1 si algo falla. `--censo` imprime solo el conteo de literales.

- [ ] **Step 1: Confirmar que el guardián existe y falla**

```bash
python scripts/verificar_marca.py
```

Esperado: código de salida 1, con una línea base de **82 fallos** y censo `css: 242  vistas: 884  total: 1126`.

Si el número de fallos no es 82, **detenerse**: alguien cambió color desde el 2026-10-02 y la línea base de este plan ya no aplica. Anotar el número real y continuar con él.

- [ ] **Step 2: Guardar la línea base**

```bash
python scripts/verificar_marca.py > docs/superpowers/plans/linea-base-marca.txt
git add docs/superpowers/plans/linea-base-marca.txt
git commit -m "La linea base del guardian de marca: 82 fallos, 1126 literales"
```

---

### Task 2: La capa de tokens

El corazón del cambio. Al terminar esta tarea el portal **ya cumple el manual**, aunque queden literales sueltos: los tokens obsoletos apuntan a la capa marca, así que pintan el color correcto incluso donde nadie los migró.

**Files:**
- Modify: `src/Web/wwwroot/css/tokens.css:1-20` (encabezado y bloque de color de marca)
- Modify: `src/Web/wwwroot/css/tokens.css:52-56` (los tokens `blue-title` / `blue-strong`)
- Modify: `src/Web/wwwroot/css/tokens.css:188-192` (el segundo `:root`, que repite esos valores)

**Interfaces:**
- Produce: los tokens `--diger-blue`, `--diger-blue-mid`, `--diger-celeste`, `--diger-blue-dark`, `--diger-blue-light`, `--diger-blue-pale`, `--diger-privado`. Los obsoletos `--diger-blue-title` y `--diger-blue-strong` siguen existiendo pero como alias.
- Deja de existir: `--diger-gold`, `--diger-gold-light`, `--diger-cyan`.

- [ ] **Step 1: Reemplazar el encabezado mentiroso de `tokens.css`**

El encabezado actual afirma estar alineado al manual "azul gobierno + acento dorado + cian". Es falso. Sustituirlo por:

```css
/* ══════════════════════════════════════════════════════════════════
   DIGER — Design tokens

   Alineados al Manual de Marca DIGER. La paleta del manual (pág. 09)
   son TRES colores y nada más. No hay dorado: el que había (#ad8411)
   estaba a 147° de tono del color de marca más cercano, y el manual,
   en Usos Incorrectos punto C, prohíbe sustituir el color de marca
   por tonos fuera de la línea gráfica.

   Tres capas, con reglas distintas:
     marca    — literales del manual, inmodificables
     derivada — mismo tono, otra luminosidad (±8°)
     función  — neutros y estados, fuera del manual a propósito

   Lo comprueba scripts/verificar_marca.py, que falla si algo se sale.
   Ver DESIGN.md para la referencia completa.
   ══════════════════════════════════════════════════════════════════ */
```

- [ ] **Step 2: Reescribir el bloque de color de marca**

Sustituir las líneas que hoy declaran `--diger-blue*`, `--diger-gold*` y `--diger-cyan` por:

```css
    /* ── CAPA MARCA — Manual de Marca DIGER, pág. 09 ──────────────────
       Tres valores, literales del manual. No se derivan ni se ajustan.
       Cualquier otro azul que pretenda ser marca es un incumplimiento. */
    --diger-blue:       #02395e;   /* azul oscuro (marino)      — tono 204° */
    --diger-blue-mid:   #1a65a3;   /* azul medio (corporativo)  — tono 207° */
    --diger-celeste:    #7acbdd;   /* celeste (azul claro)      — tono 191° */

    /* ── CAPA DERIVADA — mismo tono, otra luminosidad ─────────────────
       Regla de pertenencia: ±8° de tono respecto a la capa marca. */
    --diger-blue-dark:  #022a45;   /* sombra de --diger-blue    — tono 204° */
    --diger-blue-light: #e8f5ff;   /* tinte  — antes #e8f0ff, violáceo a 219° */
    --diger-blue-pale:  #c8ddee;   /* borde  — antes #c8d5ee, violáceo a 219° */

    /* Obsoletos: repuntados a la capa marca para que pinten el color
       correcto incluso donde todavía nadie migró sus usos. Se eliminan
       en la tarea 12, cuando el guardián confirme que no queda ninguna
       referencia. No agregar usos nuevos. */
    --diger-blue-title:  var(--diger-blue);
    --diger-blue-strong: var(--diger-blue-mid);
```

- [ ] **Step 3: Agregar el token de estado del calendario**

En el bloque de color semántico, junto a `--diger-warning`:

```css
    /* Reunión privada en el calendario. Antes usaba el dorado y lo
       llamaba "institucional", pero esto es un estado, no marca: vive
       en la capa función. El cambio además corrige accesibilidad —
       #ad8411 como texto daba 3.45 y no alcanzaba AA; este da 5.02. */
    --diger-privado: #b45309;
```

- [ ] **Step 4: Repetir los cambios en el segundo `:root`**

`tokens.css` tiene un segundo bloque `:root` (el que antes era `[data-theme="light"]`) que **repite** `--diger-blue-title` y `--diger-blue-strong` con los hex viejos y pisa al primero. Si no se cambia, el trabajo del paso 2 queda anulado. Reemplazar ahí también por los `var()`.

- [ ] **Step 5: Verificar**

```bash
python scripts/verificar_marca.py
```

Esperado: los fallos de `tokens.css` desaparecen. Los de `diger.css`, vistas y demás siguen. El bloque `[2] Pertenencia de tono` debe salir **sin ningún FALLA**.

- [ ] **Step 6: Compilar y mirar la página**

```bash
dotnet build Diger.TramitesEstado.sln
dotnet run --project src\Web
```

Abrir `https://localhost:49176`. El header ya no debe tener la línea dorada. Los títulos se ven un punto más claros: es lo esperado (§12 del spec).

- [ ] **Step 7: Commit**

```bash
git add src/Web/wwwroot/css/tokens.css
git commit -m "La paleta eran tres azules del manual, no dos azules y un dorado"
```

---

### Task 3: El dorado desaparece de `diger.css`

Ocho usos, de dos naturalezas distintas. Cinco son acento de marca; tres son indicador de reunión privada y pertenecen a la capa función.

**Files:**
- Modify: `src/Web/wwwroot/css/diger.css` líneas 49, 1207, 1455, 1875, 1899 (acento) y 4363, 4517, 4534 (estado)
- Modify: `src/Web/wwwroot/css/diger.css:19` (fallback de `:focus-visible`)

**Interfaces:**
- Consume: `--diger-blue-mid`, `--diger-celeste`, `--diger-privado` de la tarea 2.

- [ ] **Step 1: Acento sobre fondo claro — el subrayado del título**

Línea 49, en `.page-title, .pg-title`. El comentario de encima dice "(Manual de Marca: navy + subrayado dorado)" y es falso: corregirlo también.

```css
/* ── ENCABEZADO DE PÁGINA ─────────────────────────────────────────────
   El subrayado iba en dorado y el comentario lo atribuía al Manual de
   Marca. El manual no tiene dorado. Sobre fondo claro el acento es el
   azul medio: el celeste ahí da 1.84 y se vería lavado. */
    border-bottom: 3px solid var(--diger-blue-mid);
```

- [ ] **Step 2: Acento sobre navy — header y footer**

Línea 1207 (`.app-header`) y 1875 (`.diger-footer`):

```css
    border-bottom: 3px solid var(--diger-celeste);
```

```css
    border-top: 3px solid var(--diger-celeste);
```

Y línea 1899, el hover del enlace del footer:

```css
.diger-footer-inner a:hover {
    color: var(--diger-celeste);
}
```

- [ ] **Step 3: El avatar invierte**

Línea 1455. Fondo celeste con texto blanco daría 1.84 y fallaría; con texto navy da 6.52.

```css
/* Avatar del header: fondo celeste con texto navy. Texto blanco sobre
   celeste daría 1.84 y no alcanza AA. */
.user-menu-btn .side-avatar {
    background: var(--diger-celeste);
    color: var(--diger-blue);
}
```

- [ ] **Step 4: El calendario privado pasa a la capa función**

Líneas 4363, 4517 y 4534. El comentario "Reunión privada: se distingue con el dorado institucional" también se corrige:

```css
/* Reunión privada: estado, no marca. Vive en la capa función. */
.cal-chip.priv {
    border-left-color: var(--diger-privado);
}
```

```css
.cal-dia-item.priv {
    border-left-color: var(--diger-privado);
}
```

```css
.cal-dia-item.priv .cal-dia-hora {
    color: var(--diger-privado);
}
```

- [ ] **Step 5: El fallback del foco**

Línea 19. El fallback es el índigo ajeno:

```css
:focus-visible {
    outline: 2px solid var(--diger-blue, #02395e);
    outline-offset: 2px;
}
```

- [ ] **Step 6: Verificar que no queda dorado**

```bash
grep -rn "diger-gold\|#ad8411\|#c79621" src/
```

Esperado: sin resultados.

```bash
python scripts/verificar_marca.py
```

Esperado: el bloque `[1] Colores prohibidos` ya no reporta dorado.

- [ ] **Step 7: Mirar el calendario y el header**

```bash
dotnet build Diger.TramitesEstado.sln
dotnet run --project src\Web
```

Revisar `/Calendario` (las reuniones privadas deben seguir distinguiéndose) y el header y pie de cualquier página.

- [ ] **Step 8: Commit**

```bash
git add src/Web/wwwroot/css/diger.css
git commit -m "El dorado del calendario nunca fue marca: era el estado de reunion privada"
```

---

### Task 4: Los literales restantes de `diger.css`

**Files:**
- Modify: `src/Web/wwwroot/css/diger.css` (174 literales hex)

- [ ] **Step 1: Listar lo que hay**

```bash
grep -onE "#[0-9a-fA-F]{6}" src/Web/wwwroot/css/diger.css | sort -t: -k2 | uniq -c -f0 | head -40
```

- [ ] **Step 2: Sustituir en orden de frecuencia**

Mapa obligatorio (de las Global Constraints):

| Literal | Token |
|---|---|
| `#0a2d6e` | `var(--diger-blue)` |
| `#1455a4`, `#0c447c`, `#1d4ed8` | `var(--diger-blue-mid)` |
| `#022a45` | `var(--diger-blue-dark)` |
| `#e2e8f0` | `var(--diger-border)` |
| `#6b7fa3` | `var(--diger-text-muted)` |
| `#fff`, `#ffffff` | `var(--diger-surface)` salvo en fondos de página |

Para cualquier literal que **no** esté en el mapa: si está a ≤8° de un color del manual, usar el token derivado más cercano; si no, dejarlo y anotarlo — es candidato a token nuevo de la capa función, no a colapso silencioso.

- [ ] **Step 3: Verificar el censo**

```bash
python scripts/verificar_marca.py --censo
```

Esperado: el conteo de CSS baja respecto a la línea base. Debe bajar, no quedarse igual.

- [ ] **Step 4: Compilar, mirar y commitear**

```bash
dotnet build Diger.TramitesEstado.sln
git add src/Web/wwwroot/css/diger.css
git commit -m "Los literales de diger.css pasan a token"
```

---

### Task 5: El resto de las hojas de estilo

**Files:**
- Modify: `src/Web/wwwroot/css/expediente.css` (41 literales)
- Modify: `src/Web/wwwroot/css/modal.css` (15)
- Modify: `src/Web/wwwroot/css/tramites-indicadores.css` (18)

`afiche-qr.css` **no se toca**.

- [ ] **Step 1: Aplicar el mismo mapa de la tarea 4 a los tres archivos**

- [ ] **Step 2: Verificar**

```bash
python scripts/verificar_marca.py
```

Esperado: `[1] Colores prohibidos` ya no menciona `expediente.css` ni `modal.css`.

- [ ] **Step 3: Commit**

```bash
git add src/Web/wwwroot/css/expediente.css src/Web/wwwroot/css/modal.css src/Web/wwwroot/css/tramites-indicadores.css
git commit -m "Las hojas de modal, expediente e indicadores tambien tenian literales"
```

---

### Task 6: Las vistas Razor, por módulo

884 literales. Se hace por módulo, con commit por grupo, para que un fallo se aísle solo.

**Files:**
- Modify: `src/Web/Pages/**/*.cshtml`

- [ ] **Step 1: Grupo denso — Tableros y Siger**

`Tableros/MiTablero.cshtml` (52), `Siger/Conciliacion.cshtml` (42), `Tableros/Reuniones.cshtml` (21), `Siger/Tablero.cshtml` (16), `Tableros/Digitalizacion.cshtml` (17), `Tableros/Tickets.cshtml` (14) y el resto de `Tableros/` y `Siger/`.

Mismo mapa de la tarea 4.

```bash
python scripts/verificar_marca.py --censo
git add src/Web/Pages/Tableros src/Web/Pages/Siger
git commit -m "Tableros y Siger pasan a token"
```

- [ ] **Step 2: Grupo Reuniones y Expedientes**

`Reuniones/CompromisoDetalle.cshtml` (32), `Reuniones/Asistencia.cshtml` (25), `Reuniones/Acta.cshtml` (21), `Expedientes/Cronograma.cshtml` (33), `Expedientes/Editor.cshtml` (21) y el resto de ambas carpetas.

```bash
git add src/Web/Pages/Reuniones src/Web/Pages/Expedientes
git commit -m "Reuniones y Expedientes pasan a token"
```

- [ ] **Step 3: Grupo Cuenta, Ayuda y Recursos**

`Ayuda/Index.cshtml` (31), `Recursos/Index.cshtml` (33), `Cuenta/Login.cshtml` y el resto de `Cuenta/`.

`Login.cshtml` es la página más visible de la marca: revisarla en pantalla antes de commitear.

```bash
git add src/Web/Pages/Cuenta src/Web/Pages/Ayuda src/Web/Pages/Recursos
git commit -m "El login y las paginas de cuenta pasan a token"
```

- [ ] **Step 4: El resto**

Todo lo que quede: `Contactos/`, `Instituciones/`, `Usuarios/`, `Areas/`, `Unidades/`, `Admin/`, `Tickets/`, `PlanTrabajo/`, `Asistencia/`, `Accesos/`, `Proyectos/`, `Error.cshtml`.

```bash
python scripts/verificar_marca.py
git add src/Web/Pages
git commit -m "El resto de las vistas pasa a token"
```

Esperado al final del paso 4: `[1] Colores prohibidos` sin ningún hallazgo en `.cshtml`.

---

### Task 7: El JavaScript

91 literales que el CSS no alcanza. Donde el color sea de interfaz, se lee del CSS; donde sea de dato (series de gráfica), se nombra como constante.

**Files:**
- Modify: `src/Web/wwwroot/js/expediente.js` (69)
- Modify: `src/Web/wwwroot/js/tramites-indicadores.js` (21)
- Modify: `src/Web/wwwroot/js/afiche-qr.js` (1)

- [ ] **Step 1: Agregar el lector de tokens**

Al inicio de `expediente.js` y `tramites-indicadores.js`:

```javascript
// Lee un token de color del CSS, para no duplicar la paleta en JS.
// Si el token no existe devuelve el respaldo, de modo que un token mal
// escrito degrada a un color razonable en vez de pintar transparente.
function tokenColor(nombre, respaldo) {
    const v = getComputedStyle(document.documentElement)
        .getPropertyValue(nombre).trim();
    return v || respaldo;
}
```

- [ ] **Step 2: Sustituir los colores de interfaz**

```javascript
const AZUL      = tokenColor('--diger-blue',     '#02395e');
const AZUL_MED  = tokenColor('--diger-blue-mid', '#1a65a3');
const CELESTE   = tokenColor('--diger-celeste',  '#7acbdd');
```

Reemplazar los usos de `#1455a4` por `AZUL_MED` y los de `#0a2d6e` por `AZUL`.

- [ ] **Step 3: Nombrar las paletas de serie**

Los colores que distinguen series de una gráfica no son marca: no pueden colapsar a tres azules o las series dejarían de distinguirse. Se agrupan en una constante nombrada, con el azul medio del manual al frente:

```javascript
// Paleta de series. No es marca: su trabajo es distinguir, no representar.
// El primero sí es el azul del manual, para que la serie principal lea
// como institucional.
const SERIES = ['#1a65a3', '#0f8a6a', '#b45309', '#7c3aed', '#64748b'];
```

- [ ] **Step 4: Verificar en pantalla**

Las gráficas no tienen prueba automática. Abrir `/Expedientes` y `/Tableros/Digitalizacion` y confirmar que las series siguen distinguiéndose.

- [ ] **Step 5: Commit**

```bash
git add src/Web/wwwroot/js
git commit -m "El JavaScript duplicaba la paleta; ahora la lee del CSS"
```

---

### Task 8: Los modelos de UI en C#

Devuelven hex desde C#, donde ningún token CSS llega. La forma de la tupla no cambia: solo los valores.

**Files:**
- Modify: `src/Web/Models/CalendarioUi.cs:18-24` (23 literales)
- Modify: `src/Web/Models/CompromisoUi.cs:25-33` (14 literales)

**Interfaces:**
- Consume: nada de tareas previas (son literales C#, no tokens CSS).
- Produce: las mismas tuplas `(string bg, string fg)` y el mismo record de `CalendarioUi`. Ninguna firma cambia.

- [ ] **Step 1: `CalendarioUi.cs` — el azul del evento de reunión**

La línea 18 usa `#185fa5` como color de reunión y `#0c447c` como texto. `#0c447c` está prohibido. Sustituir por el azul medio del manual:

```csharp
TipoEventoCalendario.Reunion => new("i-reunion", "#1a65a3", "#e6f1fb", "#02395e", "Reunión"),
```

Los demás tipos de evento (ticket, expediente, asignación) son **estados**, no marca: conservan su familia de color. Solo se revisa que alcancen AA contra su propio fondo.

- [ ] **Step 2: `CompromisoUi.cs` — el estado pendiente**

Línea 25, el mismo `#0c447c`:

```csharp
EstadoCompromiso.Pendiente => ("#e6f1fb", "#02395e"),
```

- [ ] **Step 3: Verificar contraste de cada par**

Cada tupla es `(fondo, texto)`. Comprobar que ninguna baje de 4.5:1 — agregarlas a `PARES_AA` en `scripts/verificar_marca.py` para que queden vigiladas de aquí en adelante.

- [ ] **Step 4: Compilar, correr tests y mirar el calendario**

```bash
dotnet build Diger.TramitesEstado.sln
dotnet test Diger.TramitesEstado.sln
```

Esperado: 754 pruebas verdes.

- [ ] **Step 5: Commit**

```bash
git add src/Web/Models scripts/verificar_marca.py
git commit -m "El calendario y los compromisos pintaban color desde C# sin pasar por la paleta"
```

---

### Task 9: Los reportes PDF

El caso menos negociable: son documentos oficiales que salen de la institución con la marca encima, y hoy salen en Arial.

**Files:**
- Create: `src/Infrastructure/Reports/Fuentes/Poppins-Medium.ttf`
- Create: `src/Infrastructure/Reports/Fuentes/Poppins-ExtraBold.ttf`
- Create: `src/Infrastructure/Reports/FuentesMarca.cs`
- Modify: `src/Infrastructure/Diger.TramitesEstado.Infrastructure.csproj`
- Modify: `src/Infrastructure/Reports/ActaPdfService.cs` (11 literales + fuente)
- Modify: `src/Infrastructure/Reports/InformeService.cs` (39 + fuente)
- Modify: `src/Infrastructure/Reports/ProyectoPdfService.cs` (22 + fuente)

**Interfaces:**
- Produce: `FuentesMarca.Registrar()` — idempotente, seguro de llamar muchas veces.

- [ ] **Step 1: Conseguir los archivos de fuente**

Descargar Poppins de Google Fonts (licencia SIL Open Font License, que permite redistribución) y colocar `Poppins-Medium.ttf` y `Poppins-ExtraBold.ttf` en `src/Infrastructure/Reports/Fuentes/`.

- [ ] **Step 2: Incluirlos en el `.csproj`**

```xml
  <ItemGroup>
    <EmbeddedResource Include="Reports\Fuentes\Poppins-Medium.ttf" />
    <EmbeddedResource Include="Reports\Fuentes\Poppins-ExtraBold.ttf" />
  </ItemGroup>
```

- [ ] **Step 3: Crear el registrador**

```csharp
using System.Reflection;
using QuestPDF.Drawing;

namespace Diger.TramitesEstado.Infrastructure.Reports;

/// <summary>
/// Registra Poppins —la tipografía del Manual de Marca— en QuestPDF.
/// Los PDF salían en Arial, que no es tipografía de marca, y el acta y los
/// informes son documentos oficiales que salen de la institución.
/// Si el registro falla, QuestPDF cae a su fuente por defecto: el peor caso
/// es un PDF con otra letra, nunca un error en producción.
/// </summary>
public static class FuentesMarca
{
    private static bool _registradas;
    private static readonly object _candado = new();

    public const string Familia = "Poppins";

    public static void Registrar()
    {
        if (_registradas) return;
        lock (_candado)
        {
            if (_registradas) return;
            var ensamblado = Assembly.GetExecutingAssembly();
            foreach (var recurso in ensamblado.GetManifestResourceNames())
            {
                if (!recurso.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)) continue;
                using var flujo = ensamblado.GetManifestResourceStream(recurso);
                if (flujo is null) continue;
                try { FontManager.RegisterFont(flujo); }
                catch { /* se queda con la fuente por defecto */ }
            }
            _registradas = true;
        }
    }
}
```

- [ ] **Step 4: Llamarlo en los tres servicios**

En cada uno, junto al `Settings.License = LicenseType.Community;` que ya existe:

```csharp
        Settings.License = LicenseType.Community;
        FuentesMarca.Registrar();
```

Y cambiar cada `FontFamily("Arial")` por `FontFamily(FuentesMarca.Familia)`.

- [ ] **Step 5: Normalizar los colores de los tres servicios**

En `ActaPdfService.cs`, las constantes de las líneas 17-21:

```csharp
    // Paleta del Manual de Marca DIGER (pág. 09). Antes eran azules
    // propios del reporte, distintos de los del portal.
    private const string Azul      = "#02395e";   // azul oscuro
    private const string AzulMed   = "#1a65a3";   // azul medio
    private const string Gris      = "#666666";
    private const string GrisTenue = "#888888";
    private const string Linea     = "#d7e0ea";
```

Y el color de texto de la línea 39, que estaba a 15° del manual:

```csharp
page.DefaultTextStyle(x => x.FontSize(10).FontFamily(FuentesMarca.Familia)
    .FontColor("#1a1a1a").LineHeight(1.35f));
```

Aplicar el mismo criterio a `InformeService.cs` y `ProyectoPdfService.cs`.

- [ ] **Step 6: Generar un PDF de verdad y abrirlo**

Ninguna prueba automática mira un PDF. Hay que generarlo:

```bash
dotnet build Diger.TramitesEstado.sln
dotnet run --project src\Web
```

Abrir una reunión cerrada y descargar su acta; abrir un proyecto y descargar su informe. Confirmar: tipografía Poppins, azules del manual, y que el documento no perdió ninguna sección.

- [ ] **Step 7: Correr los tests**

```bash
dotnet test Diger.TramitesEstado.sln
```

Esperado: 754 verdes.

- [ ] **Step 8: Commit**

```bash
git add src/Infrastructure/Reports src/Infrastructure/Diger.TramitesEstado.Infrastructure.csproj
git commit -m "El acta y los informes salian en Arial: son documentos oficiales con la marca encima"
```

---

### Task 10: La tipografía del portal

**Files:**
- Modify: `src/Web/wwwroot/css/diger.css:9-15` (el `body`)
- Modify: `src/Web/wwwroot/css/diger.css:44-52` (`.page-title` / `.pg-title`)
- Modify: `src/Web/wwwroot/css/expediente.css:3` (quitar Segoe UI)

- [ ] **Step 1: El `body` pasa a Medium**

El manual pide Poppins Medium para cuerpo. El `body` no declara peso, así que corre en 400 (Regular).

```css
body {
    font-family: 'Poppins', system-ui, 'Helvetica Neue', Helvetica, Arial, sans-serif;
    font-size: var(--diger-text-base);
    font-weight: var(--diger-weight-medium);   /* 500 — Manual de Marca, pág. 08 */
    color: var(--diger-text);
    background: var(--diger-bg);
    min-height: 100vh;
}
```

- [ ] **Step 2: El titular pasa a ExtraBold**

En `.page-title, .pg-title`, cambiar `font-weight: 700` por:

```css
    font-weight: var(--diger-weight-extrabold);   /* 800 — Manual de Marca, pág. 08 */
```

No tocar `h2`…`h6` ni los títulos de tarjeta: el manual norma el titular, no toda la jerarquía.

- [ ] **Step 3: Expedientes deja de salirse de la marca**

`expediente.css` línea 3 pisa el `body` con Segoe UI y saca de marca al módulo completo. Quitar la declaración `font-family` de esa regla y dejar que herede:

```css
body{font-size:14px;color:var(--diger-text);background:var(--diger-bg-soft);min-height:100vh}
```

- [ ] **Step 4: Confirmar que `afiche-qr.css` sigue en Segoe UI**

```bash
grep -n "Segoe" src/Web/wwwroot/css/
```

Esperado: solo `afiche-qr.css`. Si aparece `expediente.css`, el paso 3 no se aplicó.

- [ ] **Step 5: Mirar el peso en pantalla**

El cambio de 400 a 500 afecta **todo el texto del portal**. Revisar al menos: una tabla densa (`/Expedientes`), un tablero (`/Tableros/Index`) y el login. Si el texto se siente pesado en las tablas, es señal de que la densidad necesita ajuste — anotarlo, no revertir el peso.

- [ ] **Step 6: Commit**

```bash
git add src/Web/wwwroot/css/diger.css src/Web/wwwroot/css/expediente.css
git commit -m "El cuerpo corria en Regular y el manual pide Medium; Expedientes ni siquiera estaba en Poppins"
```

---

### Task 11: Los colores de rol, con su script de base

Aislada y al final a propósito: es el único cambio de datos del plan y debe poder aprobarse o revertirse sola.

**Files:**
- Create: `database/2026-10-02_poner_al_dia.sql`
- Modify: `src/Infrastructure/Persistence/Migrations/20260813143220_AddRolesDinamicos.cs:70-75` (solo los literales de color del sembrado)

**Interfaces:**
- Consume: nada. Es el único cambio de datos.

- [ ] **Step 1: Decidir el color de cada rol**

Los seis sembrados están fuera de la paleta. El mapa, respetando que los roles deben **distinguirse entre sí** (son etiquetas, no marca) pero anclando los institucionales en el manual:

| Rol | Hoy | Pasa a | Por qué |
|---|---|---|---|
| Administrador | `#3c3489` | `#02395e` | el rol de máxima autoridad lleva el azul institucional |
| JefeInstitucion | `#0c447c` | `#1a65a3` | azul medio del manual |
| JefeArea | `#1a5fa0` | `#1a65a3` | ya era casi el azul medio; se ancla al hex exacto |
| JefeUnidad | `#1b5e20` | `#15803d` | verde, se alinea al `--diger-success` existente |
| Empleado | `#085041` | `#0f6e56` | teal, se alinea al que ya usa el calendario |
| Consultor | `#6d4c00` | `#b45309` | ámbar, se alinea al `--diger-warning` existente |

Verificar que cada uno alcance 4.5:1 contra el texto blanco con que se pinta el badge (`color:#fff` en `Roles.cshtml:40`).

- [ ] **Step 2: Corregir el sembrado de la migración**

Cambiar solo las cadenas de color en las líneas 70-75. **No** tocar ninguna otra columna ni la estructura de la migración: una migración ya aplicada no se re-ejecuta, así que esto solo afecta a bases creadas desde cero.

- [ ] **Step 3: Escribir el script idempotente**

`database/2026-10-02_poner_al_dia.sql`, siguiendo la forma de `database/2026-09-24_poner_al_dia.sql`:

```sql
/* ╔══════════════════════════════════════════════════════════════════╗
   ║  2026-10-02 — Colores de rol al Manual de Marca DIGER            ║
   ║                                                                  ║
   ║  QUÉ TRAE: actualiza los seis colores sembrados de la tabla      ║
   ║  Roles a la paleta del Manual de Marca. Son los colores de       ║
   ║  fondo de los badges de rol en /Accesos/Roles y /Usuarios.       ║
   ║                                                                  ║
   ║  NO es una migración de EF: no cambia el esquema, solo valores.  ║
   ║  No se escribe fila en __EFMigrationsHistory.                    ║
   ║                                                                  ║
   ║  VA DESPUÉS DE: database/2026-09-24_poner_al_dia.sql             ║
   ║                                                                  ║
   ║  CÓMO CORRERLO:                                                  ║
   ║  sqlcmd -S <servidor> -d <base> -b -I -f 65001 \                 ║
   ║         -i 2026-10-02_poner_al_dia.sql                           ║
   ╚══════════════════════════════════════════════════════════════════╝ */

SET NOCOUNT ON;

IF OBJECT_ID('dbo.Roles', 'U') IS NULL
BEGIN
    THROW 50000, 'No existe la tabla Roles. Corra primero database/00_diagnostico.sql.', 1;
END

/* Cada UPDATE exige el color sembrado en el WHERE. Si alguien ya cambió
   el color a mano desde el editor de roles, su elección se respeta y la
   fila no se toca. Eso es además lo que hace idempotente al script:
   la segunda corrida no encuentra filas que cambiar. */

DECLARE @cambios TABLE (Rol NVARCHAR(100), Nuevo NVARCHAR(20));

UPDATE dbo.Roles SET Color = '#02395e'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'Administrador'   AND Color = '#3c3489';

UPDATE dbo.Roles SET Color = '#1a65a3'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'JefeInstitucion' AND Color = '#0c447c';

UPDATE dbo.Roles SET Color = '#1a65a3'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'JefeArea'        AND Color = '#1a5fa0';

UPDATE dbo.Roles SET Color = '#15803d'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'JefeUnidad'      AND Color = '#1b5e20';

UPDATE dbo.Roles SET Color = '#0f6e56'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'Empleado'        AND Color = '#085041';

UPDATE dbo.Roles SET Color = '#b45309'
OUTPUT inserted.Id, inserted.Color INTO @cambios
WHERE Id = 'Consultor'       AND Color = '#6d4c00';

IF EXISTS (SELECT 1 FROM @cambios)
    PRINT CONCAT('actualizados: ', (SELECT COUNT(*) FROM @cambios), ' rol(es)');
ELSE
    PRINT 'ya estaban al dia (o fueron cambiados a mano): no se toco ninguna fila';

/* ── Verificación: todo control debe leer 'presente' ──────────────── */
SELECT
    Id,
    Color,
    CASE WHEN Color IN ('#02395e','#1a65a3','#15803d','#0f6e56','#b45309')
         THEN 'presente' ELSE 'FALTA' END AS Control
FROM dbo.Roles
WHERE Id IN ('Administrador','JefeInstitucion','JefeArea',
             'JefeUnidad','Empleado','Consultor')
ORDER BY Id;
```

- [ ] **Step 4: Correrlo dos veces**

```bash
sqlcmd -S localhost\SQL2025 -d GestionGD_new -b -I -f 65001 -i database/2026-10-02_poner_al_dia.sql
sqlcmd -S localhost\SQL2025 -d GestionGD_new -b -I -f 65001 -i database/2026-10-02_poner_al_dia.sql
```

Esperado: la primera corrida imprime `actualizados: 6 rol(es)`; la segunda imprime `ya estaban al dia`. En ambas, la verificación final debe leer `presente` en las seis filas y ningún `FALTA`.

- [ ] **Step 5: Mirar los badges**

Abrir `/Accesos/Roles` y `/Usuarios`. Los seis badges deben seguir distinguiéndose entre sí y el texto blanco debe leerse sobre todos.

- [ ] **Step 6: Commit**

```bash
git add database/2026-10-02_poner_al_dia.sql src/Infrastructure/Persistence/Migrations/20260813143220_AddRolesDinamicos.cs
git commit -m "Los badges de rol tenian seis colores que no eran de la paleta"
```

---

### Task 12: Cierre

**Files:**
- Modify: `src/Web/wwwroot/css/tokens.css` (eliminar los obsoletos)
- Modify: `DESIGN.md`
- Modify: `scripts/verificar_marca.py` (ampliar el censo a JS y C#)

- [ ] **Step 1: Confirmar que los obsoletos no tienen referencias**

```bash
grep -rn "diger-blue-title\|diger-blue-strong\|diger-cyan" src/
```

Esperado: solo las declaraciones en `tokens.css`. Si aparece algún uso, migrarlo antes de seguir.

- [ ] **Step 2: Eliminarlos de `tokens.css`**

Borrar `--diger-blue-title` y `--diger-blue-strong` de los dos bloques `:root`.

- [ ] **Step 3: Ampliar el censo del guardián**

En `scripts/verificar_marca.py`, agregar a `censo()` las superficies de JS y C#, para que el conteo cubra todo lo que el plan tocó:

```python
JS = RAIZ / "src" / "Web" / "wwwroot" / "js"
MODELOS = RAIZ / "src" / "Web" / "Models"
REPORTES = RAIZ / "src" / "Infrastructure" / "Reports"
```

- [ ] **Step 4: Correr el guardián completo**

```bash
python scripts/verificar_marca.py
```

Esperado: **código de salida 0** y `RESULTADO: el portal respeta el Manual de Marca`. Si no sale en verde, el plan no está terminado: el fallo dice exactamente qué falta.

- [ ] **Step 5: Correr todo**

```bash
dotnet build Diger.TramitesEstado.sln
dotnet test Diger.TramitesEstado.sln
```

Esperado: 754 pruebas verdes.

- [ ] **Step 6: Actualizar `DESIGN.md`**

Registrar, en §1 y §3:

- Las tres capas de la paleta y la regla de cada una.
- La regla de pertenencia de ±8° de tono.
- La regla de acento: celeste sobre navy, azul medio sobre claro.
- Que `scripts/verificar_marca.py` es el que lo vigila.
- **El levantamiento explícito del bloqueo de §3:** la deuda decía que los hex casi iguales no se fusionaban "sin poder verificar el contraste visualmente". Ya está verificado y medido; escribirlo ahí para que nadie vuelva a frenarse por la misma razón.
- Las cuatro superficies más allá del CSS, para que la próxima auditoría no las vuelva a pasar por alto.

- [ ] **Step 7: Commit**

```bash
git add src/Web/wwwroot/css/tokens.css DESIGN.md scripts/verificar_marca.py
git commit -m "DESIGN.md decia que el contraste no se podia verificar; ya se verifico"
```

---

## Qué queda pendiente después de este plan

Registrado a propósito, no olvidado:

1. **Los logos.** Las tres variantes del manual siguen sin arte oficial. `logo_diger.png` sigue siendo un raster de 1 MB y 11827 px, no existe la versión abreviada, y el isotipo del header corre a 27×20 px cuando el manual fija 85 px como mínimo digital.
2. **Los 32 logos institucionales** de `logos.pptx`, que alimentan `Instituciones.LogoUrl` —campo editable ya existente y hoy vacío.
3. **`InstitucionOptions.Nombre`** tiene como valor por defecto *"Dirección General de Gobierno Digital"* cuando el manual dice *"Dirección de Gestión por Resultados"*.
