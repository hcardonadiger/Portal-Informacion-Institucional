# Plan de implementación — Promover un trámite del expediente a SIGER

> **Para quien ejecute esto con agentes:** usar `superpowers:subagent-driven-development`
> (recomendado) o `superpowers:executing-plans`, tarea por tarea. Los pasos llevan
> casilla (`- [ ]`) para ir marcándolos.

**Objetivo:** un botón que crea una ficha SIGER a partir de un trámite del
expediente, y otro que la actualiza cuando el expediente cambia, sin tocar lo que
SIGER decide.

**Diseño de referencia:** [`diseno.md`](./diseno.md). Las decisiones PR-01 a PR-07
están cerradas ahí; este plan no las revisa, las ejecuta.

**Arquitectura:** la lógica pura (normalizar modalidad, generar código, calcular el
diff, decidir si se publica) vive en `src/Application/Siger/Promocion/` y se prueba
sin base de datos. Las páginas Razor solo orquestan. El expediente gana cuatro
campos y dos colecciones hijas, siguiendo el patrón de reemplazo en bloque que ya
usa el agregado.

**Tecnología:** .NET 9, EF Core sobre SQL Server, Razor Pages, xUnit con
FluentAssertions.

## Restricciones globales

Estas valen para **todas** las tareas. Copiadas del proyecto, no inventadas:

- **Nunca abrir una transacción explícita de EF.** El `DbContext` está registrado
  con `EnableRetryOnFailure`, que es incompatible con `BeginTransaction`. Un solo
  `SaveChangesAsync()` ya es atómico: se arma todo en el rastreador de cambios y se
  guarda una vez.
- **Las migraciones se generan con `--output-dir Persistence\Migrations`**, siempre.
  Sin ese parámetro van a `src/Infrastructure/Migrations/`, que es la carpeta vieja.
  Comando exacto:
  `dotnet ef migrations add <Nombre> --project src\Infrastructure --startup-project src\Web --output-dir Persistence\Migrations`
- **`ExpedienteTramite.Id` no es estable.** `ExpedienteMapper.Aplicar` llama a
  `LimpiarHijos()` y recrea los trámites en cada guardado. La identidad de un
  trámite es **`(ExpedienteId, TramiteIndex)`**. Ninguna tarea puede guardar una
  referencia al `Id`.
- **Dónde va cada prueba:** `Application.Tests` usa EF In-Memory, que **no aplica
  CHECK constraints ni índices únicos**. Toda prueba de restricción de base va en
  `Web.Tests`, que levanta SQLite con `EnsureCreated`.
- **Colecciones hijas del expediente:** siempre reemplazo en bloque —
  `LimpiarHijos()` y luego `Agregar(...)` por cada elemento. No editar en sitio.
- **Permisos:** cada handler nuevo lleva `[Permission(modulo, accion)]`. Un handler
  sin atributo se registra como advertencia al arrancar.
- **Pruebas:** `dotnet test Diger.TramitesEstado.sln`. Antes de empezar hay 76
  verdes; ninguna tarea puede bajar ese número.
- **Compilación:** `dotnet build Diger.TramitesEstado.sln`. Si falla solo con
  `MSB3027`/`MSB3021`, hay una instancia de la app corriendo que retiene las DLL.

---

## Estructura de archivos

**Nuevos, capa Application** — lógica pura, sin base de datos:

| Archivo | Responsabilidad |
|---|---|
| `src/Application/Siger/Promocion/ModalidadNormalizador.cs` | Texto libre → catálogo cerrado |
| `src/Application/Siger/Promocion/CodigoPromovido.cs` | Genera `400-P01` |
| `src/Application/Siger/Promocion/ReglaPublicacion.cs` | `EstadoSiger` → `Publicado` |
| `src/Application/Siger/Promocion/PromocionMapeo.cs` | Trámite de expediente → ficha SIGER |
| `src/Application/Siger/Promocion/DiferenciaFicha.cs` | Qué cambiaría al actualizar |

**Nuevos, capa Domain:**

| Archivo | Responsabilidad |
|---|---|
| `src/Domain/Entities/ExpedienteTramiteHijos.cs` | `ExpedienteTramiteEntregable` y `ExpedienteTramiteLugar` |

**Modificados:**

| Archivo | Qué cambia |
|---|---|
| `src/Domain/Entities/ExpedienteTramite.cs` | 4 campos nuevos |
| `src/Domain/Entities/Expediente.cs` | 2 colecciones, `LimpiarHijos`, 2 `Agregar` |
| `src/Infrastructure/Persistence/AppDbContext.cs` | `IdSiger` opcional + 3 configuraciones |
| `src/Application/Common/Interfaces/IRepositories.cs` | 2 `DbSet` |
| `src/Application/Expedientes/Common/ExpedienteDtos.cs` | 4 campos en `TramiteInput`, 2 records |
| `src/Application/Expedientes/Common/ExpedienteMapper.cs` | Ida y vuelta de lo anterior |
| `src/Web/Pages/Expedientes/OriginalShapeMapper.cs` | Ida y vuelta de la forma JSON |
| `src/Web/Pages/Expedientes/Editor.cshtml(.cs)` | Campos, diálogos y handlers |
| `src/Web/wwwroot/js/expediente.js` | Campos, insignias, llamadas |
| `src/Web/Pages/Siger/Editor.cshtml.cs` | Usa `ReglaPublicacion` |
| `src/Web/Pages/Siger/Detalle.cshtml(.cs)` | Aviso de ficha promovida |
| `src/Web/Pages/Siger/Index.cshtml(.cs)` | Marca en el inventario |

---

# Fase 1 — Lógica pura y esquema base

Sin interfaz. Al terminar la fase, la base admite fichas sin `IdSiger` y existen
las tres piezas de lógica que la promoción necesita, probadas.

## Tarea 1: Normalizador de modalidad

**Archivos:**
- Crear: `src/Application/Siger/Promocion/ModalidadNormalizador.cs`
- Probar: `tests/Application.Tests/Siger/Promocion/ModalidadNormalizadorTests.cs`

**Interfaces:**
- Consume: `ModalidadPublica` de `Application/Siger/Publico/PublicoDtos.cs`
  (constantes `Virtual`, `Presencial`, `Hibrido`).
- Produce: `ModalidadNormalizador.Normalizar(string? texto) -> string?`. Devuelve
  una de las tres constantes o `null`.
- **Dónde se usa:** en `ExpedienteMapper` (tarea 5, paso 6) como red de seguridad.
  Después de la migración de la tarea 6 el expediente ya guarda el catálogo
  cerrado, pero el texto libre puede volver a entrar por dos puertas: el importador
  de expedientes desde Supabase y un formulario viejo en caché de un navegador. Sin
  la red, cualquiera de las dos revienta contra el CHECK al guardar. La lógica de
  conversión también se replica en SQL en la migración de la tarea 6, con el mismo
  criterio.

- [ ] **Paso 1: Escribir la prueba que falla**

`tests/Application.Tests/Siger/Promocion/ModalidadNormalizadorTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Siger.Promocion;
using Diger.TramitesEstado.Application.Siger.Publico;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Promocion;

/// <summary>
/// El expediente guarda la modalidad como texto libre; SIGER tiene un CHECK cerrado de tres
/// valores. Los casos de abajo no son inventados: son las diez variantes que existen hoy en
/// ExpedienteTramites, con su conteo real. Si esta conversión falla, la promoción revienta
/// contra CK_TramitesSiger_Modalidad en vez de guardar.
/// </summary>
public sealed class ModalidadNormalizadorTests
{
    [Theory]
    [InlineData("En línea")]                    // 166 filas
    [InlineData("En linea")]                    // 1  — sin tilde
    [InlineData("En línea (total)")]            // 12
    [InlineData("Trámite en línea")]            // 3
    [InlineData("En línea Tipo de solicitud")]  // 1  — dato sucio, pero es en línea
    public void Texto_de_en_linea_da_Virtual(string texto) =>
        ModalidadNormalizador.Normalizar(texto).Should().Be(ModalidadPublica.Virtual);

    [Theory]
    [InlineData("En línea / Presencial")]  // 2
    [InlineData("En línea, Presencial")]   // 14
    public void Texto_con_ambas_da_Hibrido(string texto) =>
        ModalidadNormalizador.Normalizar(texto).Should().Be(ModalidadPublica.Hibrido);

    [Fact]
    public void Presencial_da_Presencial() =>
        ModalidadNormalizador.Normalizar("Presencial").Should().Be(ModalidadPublica.Presencial);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Vacio_no_inventa_modalidad(string? texto) =>
        ModalidadNormalizador.Normalizar(texto).Should().BeNull();

    [Fact]
    public void Texto_que_no_dice_nada_no_inventa_modalidad() =>
        ModalidadNormalizador.Normalizar("Tipo de solicitud").Should().BeNull();

    /// <summary>
    /// El caso que más fácil se rompe: «Hibrido» no contiene ni «linea» ni «presencial», así que
    /// una conversión que solo mire palabras clave lo convertiría en null. Como el normalizador
    /// corre en cada guardado, eso borraría la modalidad de los trámites híbridos cada vez que
    /// alguien tocara el expediente.
    /// </summary>
    [Theory]
    [InlineData(ModalidadPublica.Virtual)]
    [InlineData(ModalidadPublica.Presencial)]
    [InlineData(ModalidadPublica.Hibrido)]
    public void Un_valor_que_ya_es_del_catalogo_pasa_intacto(string valor) =>
        ModalidadNormalizador.Normalizar(valor).Should().Be(valor);
}
```

- [ ] **Paso 2: Correr la prueba y confirmar que falla**

```powershell
dotnet test tests\Application.Tests --filter "FullyQualifiedName~ModalidadNormalizador"
```

Esperado: no compila — `ModalidadNormalizador` no existe.

- [ ] **Paso 3: Escribir la implementación mínima**

`src/Application/Siger/Promocion/ModalidadNormalizador.cs`:

```csharp
using System.Globalization;
using System.Text;
using Diger.TramitesEstado.Application.Siger.Publico;

namespace Diger.TramitesEstado.Application.Siger.Promocion;

/// <summary>
/// Convierte la modalidad de texto libre del expediente al catálogo cerrado de SIGER.
/// </summary>
/// <remarks>
/// Compara sin tildes y en minúsculas a propósito: en la base conviven «En línea» y
/// «En linea», y tratarlas distinto dejaría una ficha sin modalidad por una tilde.
/// Cuando el texto no dice nada reconocible devuelve null en vez de adivinar — una ficha
/// sin modalidad se declara incompleta y alguien la revisa, que es mejor que publicar
/// una modalidad equivocada.
/// </remarks>
public static class ModalidadNormalizador
{
    public static string? Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        // Un valor que ya es del catálogo sale intacto. Sin esta salida temprana, «Hibrido»
        // caería a null —no contiene ni «linea» ni «presencial»— y como este método corre en
        // cada guardado, borraría la modalidad de los híbridos cada vez que alguien editara.
        var exacto = texto.Trim();
        if (exacto is ModalidadPublica.Virtual or ModalidadPublica.Presencial or ModalidadPublica.Hibrido)
            return exacto;

        var t = SinTildes(texto).ToLowerInvariant();
        var enLinea    = t.Contains("linea") || t.Contains("virtual") || t.Contains("online");
        var presencial = t.Contains("presencial");

        return (enLinea, presencial) switch
        {
            (true,  true)  => ModalidadPublica.Hibrido,
            (true,  false) => ModalidadPublica.Virtual,
            (false, true)  => ModalidadPublica.Presencial,
            _              => null
        };
    }

    private static string SinTildes(string s) =>
        new(s.Normalize(NormalizationForm.FormD)
             .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
             .ToArray());
}
```

- [ ] **Paso 4: Correr la prueba y confirmar que pasa**

```powershell
dotnet test tests\Application.Tests --filter "FullyQualifiedName~ModalidadNormalizador"
```

Esperado: 15 pruebas en verde.

- [ ] **Paso 5: Comprometer**

```powershell
git add src\Application\Siger\Promocion\ModalidadNormalizador.cs tests\Application.Tests\Siger\Promocion\ModalidadNormalizadorTests.cs
git commit -m "Normalizar la modalidad del expediente al catalogo cerrado de SIGER"
```

---

## Tarea 2: Generador del código de ficha promovida

**Archivos:**
- Crear: `src/Application/Siger/Promocion/CodigoPromovido.cs`
- Probar: `tests/Application.Tests/Siger/Promocion/CodigoPromovidoTests.cs`

**Interfaces:**
- Produce:
  - `CodigoPromovido.PrefijoDe(string codigoSiger) -> string` — de `400-001` saca `400`.
  - `CodigoPromovido.Siguiente(string? prefijo, IEnumerable<string> codigosExistentes) -> string`
    — devuelve `400-P01`. Con `prefijo` nulo o vacío usa `DGR`.
  - Lo usa la tarea 10.

- [ ] **Paso 1: Escribir la prueba que falla**

`tests/Application.Tests/Siger/Promocion/CodigoPromovidoTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Siger.Promocion;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Promocion;

/// <summary>
/// El código de una ficha es único y de 20 caracteres como máximo. Una ficha nacida en el
/// portal no tiene código de SIGER, así que se genera uno con el prefijo que esa institución
/// ya usa (400 = Aduanas) más una P de portal, para que se distinga a simple vista de las
/// 1.057 importadas.
/// </summary>
public sealed class CodigoPromovidoTests
{
    [Theory]
    [InlineData("400-001", "400")]
    [InlineData("24-104",  "24")]
    [InlineData("950-66",  "950")]
    public void Saca_el_prefijo_del_codigo_de_SIGER(string codigo, string esperado) =>
        CodigoPromovido.PrefijoDe(codigo).Should().Be(esperado);

    [Fact]
    public void Codigo_sin_guion_se_toma_entero_como_prefijo() =>
        CodigoPromovido.PrefijoDe("400").Should().Be("400");

    [Fact]
    public void Primera_ficha_promovida_de_la_institucion_es_P01() =>
        CodigoPromovido.Siguiente("400", []).Should().Be("400-P01");

    [Fact]
    public void Correlativo_continua_desde_el_mayor_existente() =>
        CodigoPromovido.Siguiente("400", ["400-P01", "400-P03", "400-012"])
            .Should().Be("400-P04");

    [Fact]
    public void El_correlativo_es_por_institucion_no_global() =>
        CodigoPromovido.Siguiente("24", ["400-P07", "400-P08"]).Should().Be("24-P01");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Institucion_sin_fichas_en_SIGER_usa_el_prefijo_DGR(string? prefijo) =>
        CodigoPromovido.Siguiente(prefijo, []).Should().Be("DGR-P01");

    [Fact]
    public void El_codigo_generado_cabe_en_la_columna()
    {
        // Codigo es nvarchar(20). Con el prefijo más largo que existe hoy y tres cifras
        // de correlativo sigue sobrando espacio; la prueba lo deja fijado.
        var codigo = CodigoPromovido.Siguiente("9999", ["9999-P998"]);
        codigo.Should().Be("9999-P999");
        codigo.Length.Should().BeLessThanOrEqualTo(20);
    }
}
```

- [ ] **Paso 2: Correr la prueba y confirmar que falla**

```powershell
dotnet test tests\Application.Tests --filter "FullyQualifiedName~CodigoPromovido"
```

Esperado: no compila — `CodigoPromovido` no existe.

- [ ] **Paso 3: Escribir la implementación mínima**

`src/Application/Siger/Promocion/CodigoPromovido.cs`:

```csharp
using System.Globalization;

namespace Diger.TramitesEstado.Application.Siger.Promocion;

/// <summary>
/// Genera el código de una ficha que nació en el portal y no en SIGER.
/// </summary>
/// <remarks>
/// El correlativo es por institución, no global: así el código sigue leyéndose como los de
/// SIGER (prefijo de institución + número) y no delata cuántas fichas ha promovido DIGER en
/// total. La «P» es la marca visible de que la ficha no viene del inventario.
/// </remarks>
public static class CodigoPromovido
{
    public const string PrefijoPorDefecto = "DGR";
    private const string Marca = "-P";

    public static string PrefijoDe(string codigoSiger)
    {
        if (string.IsNullOrWhiteSpace(codigoSiger)) return PrefijoPorDefecto;
        var guion = codigoSiger.IndexOf('-');
        return guion < 0 ? codigoSiger.Trim() : codigoSiger[..guion].Trim();
    }

    public static string Siguiente(string? prefijo, IEnumerable<string> codigosExistentes)
    {
        var p = string.IsNullOrWhiteSpace(prefijo) ? PrefijoPorDefecto : prefijo.Trim();
        var inicio = p + Marca;

        var mayor = codigosExistentes
            .Where(c => c is not null && c.StartsWith(inicio, StringComparison.OrdinalIgnoreCase))
            .Select(c => int.TryParse(c[inicio.Length..], NumberStyles.None,
                                      CultureInfo.InvariantCulture, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{p}{Marca}{mayor + 1:00}";
    }
}
```

- [ ] **Paso 4: Correr la prueba y confirmar que pasa**

```powershell
dotnet test tests\Application.Tests --filter "FullyQualifiedName~CodigoPromovido"
```

Esperado: 10 pruebas en verde.

- [ ] **Paso 5: Comprometer**

```powershell
git add src\Application\Siger\Promocion\CodigoPromovido.cs tests\Application.Tests\Siger\Promocion\CodigoPromovidoTests.cs
git commit -m "Generar el codigo de una ficha SIGER creada desde el portal"
```

---

## Tarea 3: Sacar la regla de publicación del formulario

Hoy `CalcularPublicado` es un método privado dentro de `Siger/Editor.cshtml.cs`. La
promoción necesita exactamente la misma regla; si se copia, el día que cambie una
va a discrepar de la otra. Se extrae junto a `FichaPublicaCompletitud`, que ya vive
en Application por la misma razón.

**Archivos:**
- Crear: `src/Application/Siger/Promocion/ReglaPublicacion.cs`
- Modificar: `src/Web/Pages/Siger/Editor.cshtml.cs` (borrar el método privado y usar el nuevo)
- Probar: `tests/Application.Tests/Siger/Promocion/ReglaPublicacionTests.cs`

**Interfaces:**
- Produce: `ReglaPublicacion.SePublica(string? estadoSiger) -> bool`, y las constantes
  `ReglaPublicacion.Registrado`, `.Aprobado`, `.Completo`. Lo usan las tareas 10 y 14.

- [ ] **Paso 1: Escribir la prueba que falla**

`tests/Application.Tests/Siger/Promocion/ReglaPublicacionTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Siger.Promocion;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Promocion;

/// <summary>
/// La única regla que decide si una ficha llega al ciudadano. Vive en Application y no en la
/// página del editor porque la promoción la necesita igual: dos copias de esta regla acabarían
/// discrepando, y la discrepancia se vería en el portal público.
/// </summary>
public sealed class ReglaPublicacionTests
{
    [Theory]
    [InlineData("Aprobado")]
    [InlineData("Completo")]
    public void Aprobado_y_Completo_se_publican(string estado) =>
        ReglaPublicacion.SePublica(estado).Should().BeTrue();

    [Theory]
    [InlineData("Registrado")]
    [InlineData("En revisión")]
    [InlineData("")]
    [InlineData(null)]
    public void Cualquier_otro_estado_no_se_publica(string? estado) =>
        ReglaPublicacion.SePublica(estado).Should().BeFalse();

    [Fact]
    public void Una_ficha_promovida_nace_sin_publicar() =>
        ReglaPublicacion.SePublica(ReglaPublicacion.Registrado).Should().BeFalse();
}
```

- [ ] **Paso 2: Correr la prueba y confirmar que falla**

```powershell
dotnet test tests\Application.Tests --filter "FullyQualifiedName~ReglaPublicacion"
```

Esperado: no compila.

- [ ] **Paso 3: Escribir la implementación**

`src/Application/Siger/Promocion/ReglaPublicacion.cs`:

```csharp
namespace Diger.TramitesEstado.Application.Siger.Promocion;

/// <summary>
/// Qué estados de SIGER hacen visible una ficha en el portal del ciudadano.
/// </summary>
/// <remarks>
/// Separada de <c>FichaPublicaCompletitud</c> a propósito: una cosa es que la ficha esté
/// aprobada (esto) y otra que esté completa (aquello). Una ficha aprobada pero incompleta se
/// publica igual, con sus campos vacíos, que fue la decisión P-09 opción 1.
/// </remarks>
public static class ReglaPublicacion
{
    public const string Registrado = "Registrado";
    public const string Aprobado   = "Aprobado";
    public const string Completo   = "Completo";

    public static bool SePublica(string? estadoSiger) =>
        estadoSiger is Aprobado or Completo;
}
```

- [ ] **Paso 4: Reemplazar el método privado del editor**

En `src/Web/Pages/Siger/Editor.cshtml.cs`, borrar el método `CalcularPublicado`
(la línea `private static bool CalcularPublicado(TramiteSiger t) => t.EstadoSiger is "Aprobado" or "Completo";`
y su bloque de documentación queda: mover ese comentario a `ReglaPublicacion` si
aporta) y cambiar cada uso:

```csharp
// antes
entity.Publicado = CalcularPublicado(entity);
// después
entity.Publicado = ReglaPublicacion.SePublica(entity.EstadoSiger);
```

Agregar el `using` correspondiente:

```csharp
using Diger.TramitesEstado.Application.Siger.Promocion;
```

Buscar todos los usos antes de borrar:

```powershell
Select-String -Path src\Web\Pages\Siger\Editor.cshtml.cs -Pattern "CalcularPublicado"
```

- [ ] **Paso 5: Correr todas las pruebas**

```powershell
dotnet build Diger.TramitesEstado.sln
dotnet test Diger.TramitesEstado.sln
```

Esperado: compila sin errores, 76 + 8 pruebas en verde. Ninguna de las existentes
puede romperse: la regla es idéntica, solo cambió de sitio.

- [ ] **Paso 6: Comprometer**

```powershell
git add src\Application\Siger\Promocion\ReglaPublicacion.cs src\Web\Pages\Siger\Editor.cshtml.cs tests\Application.Tests\Siger\Promocion\ReglaPublicacionTests.cs
git commit -m "Mover la regla de publicacion de la pagina del editor a Application"
```

---

## Tarea 4: `IdSiger` admite vacío

**Archivos:**
- Modificar: `src/Domain/Entities/TramiteSiger.cs:5`
- Modificar: `src/Infrastructure/Persistence/AppDbContext.cs` (`TramiteSigerConfiguration`, el `HasIndex` de `IdSiger`)
- Crear: migración `SigerIdOpcional`
- Probar: `tests/Web.Tests/FichaSinIdSigerTests.cs`

**Interfaces:**
- Produce: `TramiteSiger.IdSiger` pasa de `int` a `int?`. Todo el código que lo lea
  tiene que tolerar el vacío. Lo usan las tareas 10, 16 y 17.

- [ ] **Paso 1: Escribir la prueba que falla**

Va en `Web.Tests` y no en `Application.Tests` porque el índice único **no existe**
en el proveedor In-Memory; con SQLite sí.

`tests/Web.Tests/FichaSinIdSigerTests.cs`:

```csharp
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Una ficha nacida en el portal no tiene identificador de SIGER, y el vacío es justamente la
/// marca de que no lo tiene. El índice único sobre IdSiger tiene que estar filtrado: SQL Server
/// solo admite un nulo en un índice único sin filtro, así que sin el filtro la segunda ficha
/// promovida fallaría al guardar. Esta prueba corre sobre SQLite, que sí aplica índices.
/// </summary>
public sealed class FichaSinIdSigerTests(PortalFactory factory) : IClassFixture<PortalFactory>
{
    [Fact]
    public async Task Dos_fichas_sin_IdSiger_conviven()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.TramitesSiger.Add(new TramiteSiger
        {
            IdSiger = null, Codigo = "400-P01", Nombre = "Primera promovida",
            Institucion = "Aduanas", EstadoSiger = "Registrado"
        });
        db.TramitesSiger.Add(new TramiteSiger
        {
            IdSiger = null, Codigo = "400-P02", Nombre = "Segunda promovida",
            Institucion = "Aduanas", EstadoSiger = "Registrado"
        });

        var guardar = async () => await db.SaveChangesAsync();

        await guardar.Should().NotThrowAsync(
            "el índice único de IdSiger debe estar filtrado por IS NOT NULL");
    }

    [Fact]
    public async Task Dos_fichas_con_el_mismo_IdSiger_siguen_prohibidas()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.TramitesSiger.Add(new TramiteSiger
        {
            IdSiger = 7001, Codigo = "24-900", Nombre = "Importada A",
            Institucion = "Propiedad", EstadoSiger = "Registrado"
        });
        db.TramitesSiger.Add(new TramiteSiger
        {
            IdSiger = 7001, Codigo = "24-901", Nombre = "Importada B",
            Institucion = "Propiedad", EstadoSiger = "Registrado"
        });

        var guardar = async () => await db.SaveChangesAsync();

        await guardar.Should().ThrowAsync<Exception>(
            "el filtro solo debe relajar los nulos, no permitir IdSiger repetidos");
    }
}
```

- [ ] **Paso 2: Correr la prueba y confirmar que falla**

```powershell
dotnet test tests\Web.Tests --filter "FullyQualifiedName~FichaSinIdSiger"
```

Esperado: no compila — `IdSiger` es `int` y no admite `null`.

- [ ] **Paso 3: Hacer opcional la propiedad**

En `src/Domain/Entities/TramiteSiger.cs`, cambiar la línea 5:

```csharp
// antes
public int IdSiger { get; set; }
// después
/// <summary>Identificador en el sistema SIGER. Vacío cuando la ficha nació en este portal
/// (promovida desde un expediente) y por tanto no existe en SIGER.</summary>
public int? IdSiger { get; set; }
```

- [ ] **Paso 4: Filtrar el índice único**

En `src/Infrastructure/Persistence/AppDbContext.cs`, dentro de
`TramiteSigerConfiguration`:

```csharp
// antes
b.HasIndex(x => x.IdSiger).IsUnique();
// después
// Filtrado a propósito: SQL Server solo admite UN nulo en un índice único sin filtro, y
// las fichas promovidas desde un expediente son todas de IdSiger nulo. Sin el filtro, la
// segunda promoción falla con violación de índice.
b.HasIndex(x => x.IdSiger).IsUnique().HasFilter("[IdSiger] IS NOT NULL");
```

- [ ] **Paso 5: Arreglar lo que asumía que nunca era nulo**

```powershell
dotnet build Diger.TramitesEstado.sln
```

Compilar y corregir cada error que salga. Los sitios conocidos son
`src/Web/Pages/Siger/Editor.cshtml.cs` (el formulario tiene `public int IdSiger`,
que pasa a `int?`), `Index.cshtml.cs` y `Detalle.cshtml`. Regla: donde se muestre,
usar `t.IdSiger?.ToString() ?? "—"`; donde se compare, comparar contra `int?`.

- [ ] **Paso 6: Generar la migración**

```powershell
dotnet ef migrations add SigerIdOpcional --project src\Infrastructure --startup-project src\Web --output-dir Persistence\Migrations
```

Abrir el archivo generado y confirmar que hace tres cosas: `DropIndex` del índice
viejo, `AlterColumn` de `IdSiger` a `nullable: true`, y `CreateIndex` con
`filter: "[IdSiger] IS NOT NULL"`. Si falta el filtro, agregarlo a mano.

- [ ] **Paso 7: Correr todas las pruebas**

```powershell
dotnet test Diger.TramitesEstado.sln
```

Esperado: todo en verde, incluidas las 2 nuevas.

- [ ] **Paso 8: Comprometer**

```powershell
git add src\Domain\Entities\TramiteSiger.cs src\Infrastructure src\Web tests\Web.Tests\FichaSinIdSigerTests.cs
git commit -m "Permitir fichas SIGER sin IdSiger, con indice unico filtrado"
```

---

# Fase 2 — El expediente gana lo que la ficha necesita

Al terminar la fase, un trámite del expediente puede llevar categoría, modalidad
del catálogo cerrado, costo, entregables y lugares de atención, y los datos que ya
existían están convertidos.

## Tarea 5: Cuatro campos nuevos en el trámite del expediente

**Archivos:**
- Modificar: `src/Domain/Entities/ExpedienteTramite.cs`
- Modificar: `src/Infrastructure/Persistence/AppDbContext.cs` (`ExpedienteTramiteConfiguration`)
- Modificar: `src/Application/Expedientes/Common/ExpedienteDtos.cs` (`TramiteInput`)
- Modificar: `src/Application/Expedientes/Common/ExpedienteMapper.cs` (las dos direcciones)
- Modificar: `src/Web/Pages/Expedientes/OriginalShapeMapper.cs` (las dos direcciones)
- Crear: migración `CamposFichaEnTramiteExpediente`
- Probar: `tests/Application.Tests/Expedientes/CamposFichaTramiteTests.cs`

**Interfaces:**
- Produce: `ExpedienteTramite.CategoriaId (int?)`, `.Modalidad (string?)` ahora
  catálogo cerrado, `.ModalidadDetalle (string?)`, `.EsGratuito (bool?)`.
  `TramiteInput` gana los mismos cuatro al final, con valor por defecto para no
  romper las llamadas existentes. Claves JSON: `categoria_id`, `modalidad`,
  `modalidad_detalle`, `es_gratuito`. Lo usan las tareas 6, 9 y 10.

- [ ] **Paso 0: Extraer el armado de un `ExpedienteInputDto` mínimo**

Las pruebas de esta tarea y de la 7 necesitan un `ExpedienteInputDto` válido con
todo vacío salvo lo que cada una prueba. Revisar primero si
`tests/Application.Tests/Expedientes/ExpedienteHandlerTests.cs` ya arma uno; si es
así, mover ese armado a un archivo nuevo y usarlo también allí, sin dejar dos
copias:

`tests/Application.Tests/Expedientes/ExpedienteInputFactory.cs`:

```csharp
using Diger.TramitesEstado.Application.Expedientes.Common;

namespace Diger.TramitesEstado.Application.Tests.Expedientes;

/// <summary>
/// Un <see cref="ExpedienteInputDto"/> válido con todo vacío. Cada prueba llena solo lo suyo
/// con <c>with</c>, para que se lea qué está probando y no quede sepultado bajo treinta campos
/// que no importan.
/// </summary>
internal static class ExpedienteInputFactory
{
    public static ExpedienteInputDto Minimo() => new(
        InstitucionId: "ADUANAS",
        // El resto de escalares obligatorios se completa copiando la firma real del record;
        // las diez colecciones hijas van como listas vacías.
        Tramites: [], Requisitos: [], Flujos: [], Legal: [],
        DocsSolicitados: [], DocsInternos: [], Perfiles: [],
        Condiciones: [], ChecklistInfra: [], Secciones: []);
}
```

> El orden y los nombres exactos de los parámetros salen de
> `src/Application/Expedientes/Common/ExpedienteDtos.cs`. Abrirlo y copiar la firma
> real — este bloque muestra la forma, no la lista completa, porque el record tiene
> más de treinta campos y transcribirlos aquí solo crearía una copia que envejece.

- [ ] **Paso 1: Escribir la prueba que falla**

La prueba comprueba lo que de verdad se puede romper: que los cuatro campos
sobrevivan el viaje completo de ida y vuelta por los dos mapeadores.

`tests/Application.Tests/Expedientes/CamposFichaTramiteTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Expedientes.Common;
using Diger.TramitesEstado.Application.Siger.Publico;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Expedientes;

/// <summary>
/// Los campos que la ficha pública necesita viajan por dos mapeadores (forma JSON del editor →
/// DTO de aplicación → entidad) y vuelven por los mismos dos. Un campo que se agrega al DTO
/// pero se olvida en un mapeador se pierde en silencio al guardar: no falla nada, simplemente
/// el dato desaparece. Esta prueba fija el viaje completo.
/// </summary>
public sealed class CamposFichaTramiteTests
{
    [Fact]
    public void Los_campos_de_ficha_sobreviven_el_mapeo_a_entidad()
    {
        var entrada = TramiteConFicha();
        var e = Diger.TramitesEstado.Domain.Entities.Expediente.Crear(
            "EXP-001", "ADUANAS", null, null, "Aduanas", "Analista");

        ExpedienteMapper.Aplicar(e, ConUnTramite(entrada));

        var t = e.Tramites.Single();
        t.CategoriaId.Should().Be(3);
        t.Modalidad.Should().Be(ModalidadPublica.Hibrido);
        t.ModalidadDetalle.Should().Be("En línea, Presencial");
        t.EsGratuito.Should().BeFalse();
    }

    [Fact]
    public void El_costo_sin_capturar_se_distingue_de_gratuito()
    {
        var e = Diger.TramitesEstado.Domain.Entities.Expediente.Crear(
            "EXP-002", "ADUANAS", null, null, "Aduanas", "Analista");

        ExpedienteMapper.Aplicar(e, ConUnTramite(TramiteConFicha() with { EsGratuito = null }));

        e.Tramites.Single().EsGratuito.Should().BeNull(
            "sin capturar y «tiene costo» son estados distintos; el segundo completa la ficha");
    }

    private static TramiteInput TramiteConFicha() => new(
        TramiteIndex: 0, NombreTramite: "Permiso de importación",
        NombreCorto: null, AreaResponsable: null,
        Modalidad: ModalidadPublica.Hibrido, PlazoLegal: null, Tercero: null, TiempoReal: null,
        MetodoPago: null, PagoBanco: null, PagoCuenta: null,
        TgrInst: null, TgrRubro: null, TgrMonto: null,
        DocEntregado: null, Objetivo: null, Alcance: null, AlcanceObs: null,
        Descripcion: null, Dirigido: null, Horario: null, Telefono: null,
        EmailTramite: null, SitioWeb: null)
    {
        CategoriaId = 3,
        ModalidadDetalle = "En línea, Presencial",
        EsGratuito = false
    };

    private static ExpedienteInputDto ConUnTramite(TramiteInput t) =>
        ExpedienteInputFactory.Minimo() with { Tramites = [t] };
}
```

- [ ] **Paso 2: Correr la prueba y confirmar que falla**

```powershell
dotnet test tests\Application.Tests --filter "FullyQualifiedName~CamposFichaTramite"
```

Esperado: no compila — los cuatro campos no existen.

- [ ] **Paso 3: Agregar las propiedades a la entidad**

En `src/Domain/Entities/ExpedienteTramite.cs`, después de `public string? SitioWeb`:

```csharp
    // ── Campos que alimentan la ficha pública al promover a SIGER ──────────
    /// <summary>Categoría del catálogo público. Obligatoria para publicar la ficha.</summary>
    public int? CategoriaId { get; set; }

    /// <summary>Texto libre original de la modalidad, anterior al catálogo cerrado.
    /// Se conserva porque «En línea (total)» lleva un matiz que «Virtual» pierde.</summary>
    public string? ModalidadDetalle { get; set; }

    /// <summary>Tres estados: sin capturar, tiene costo, es gratuito. Nunca se deduce de los
    /// campos de pago — «sin monto» no significa «gratis».</summary>
    public bool? EsGratuito { get; set; }
```

`Modalidad` ya existe; no se agrega, se le pone catálogo cerrado en el paso
siguiente.

- [ ] **Paso 4: Configurar EF**

En `ExpedienteTramiteConfiguration` de `AppDbContext.cs`:

```csharp
        b.Property(x => x.Modalidad).HasMaxLength(20);          // era 60, ahora catálogo cerrado
        b.Property(x => x.ModalidadDetalle).HasMaxLength(60);   // hereda el largo viejo

        b.HasOne<CategoriaTramite>().WithMany()
            .HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.CategoriaId).HasFilter("[CategoriaId] IS NOT NULL");

        b.ToTable(t => t.HasCheckConstraint("CK_ExpedienteTramites_Modalidad",
            "[Modalidad] IS NULL OR [Modalidad] IN ('Virtual', 'Presencial', 'Hibrido')"));
```

Sin prefijo `N'` en el literal, igual que el CHECK de `TramitesSiger`: los valores
son ASCII y SQLite —que usan los `Web.Tests`— no entiende `N'...'`.

- [ ] **Paso 5: Agregar los campos al DTO**

En `src/Application/Expedientes/Common/ExpedienteDtos.cs`, al final de
`TramiteInput`, después de `EstadoTramite? EstadoTramite = null`:

```csharp
    int?      CategoriaId = null,
    string?   ModalidadDetalle = null,
    bool?     EsGratuito = null);
```

Van al final y con valor por defecto para no romper las construcciones
posicionales que ya existen.

- [ ] **Paso 6: Mapear en las dos direcciones**

En `ExpedienteMapper.Aplicar`, dentro del `foreach` de trámites, cambiar la
asignación de `Modalidad` y agregar los tres campos nuevos:

```csharp
                // Normalizado a la entrada y no solo en la migración: el texto libre puede
                // volver a entrar por el importador de Supabase o por un formulario viejo en
                // caché. Sin esto, cualquiera de los dos revienta contra el CHECK al guardar.
                Modalidad = ModalidadNormalizador.Normalizar(t.Modalidad),
                ModalidadDetalle = t.ModalidadDetalle ?? t.Modalidad,
                TramiteSigerId = t.TramiteSigerId,
                CategoriaId = t.CategoriaId,
                EsGratuito = t.EsGratuito
```

con el `using Diger.TramitesEstado.Application.Siger.Promocion;` correspondiente.
Nótese que `Normalizar` devuelve tal cual los tres valores del catálogo, así que un
guardado normal desde el desplegable pasa sin tocarse.

En el sentido inverso del mismo archivo (la proyección a `TramiteInput`, alrededor
de la línea 183), agregar los tres argumentos en el mismo orden.

En `OriginalShapeMapper.ToInput`, dentro del `for` de trámites:

```csharp
            int? categoriaId = int.TryParse(G("categoria_id"), out var cid) ? cid : null;
            bool? esGratuito = G("es_gratuito") switch
            {
                "1" or "true"  => true,
                "0" or "false" => false,
                _              => null
            };
```

y pasarlos al `new TramiteInput(...)` en las tres posiciones nuevas, junto con
`G("modalidad_detalle")`.

En la dirección de salida del mismo archivo, dentro del diccionario del trámite:

```csharp
                ["categoria_id"] = t.CategoriaId?.ToString(),
                ["modalidad_detalle"] = t.ModalidadDetalle,
                ["es_gratuito"] = t.EsGratuito switch { true => "1", false => "0", null => null },
```

- [ ] **Paso 7: Generar la migración**

```powershell
dotnet ef migrations add CamposFichaEnTramiteExpediente --project src\Infrastructure --startup-project src\Web --output-dir Persistence\Migrations
```

- [ ] **Paso 8: Correr las pruebas**

```powershell
dotnet build Diger.TramitesEstado.sln
dotnet test Diger.TramitesEstado.sln
```

Esperado: todo verde. Si `ExpedienteHandlerTests` falla, es porque construye
`TramiteInput` posicionalmente y el orden cambió — revisar que los campos nuevos
hayan quedado **al final**.

- [ ] **Paso 9: Comprometer**

```powershell
git add src tests
git commit -m "Agregar categoria, modalidad cerrada y costo al tramite del expediente"
```

---

## Tarea 6: Convertir las 240 modalidades que ya existen

La migración anterior puso un CHECK sobre `Modalidad`, pero en la base hay diez
variantes de texto libre que no lo cumplen. Esta migración convierte los datos y
guarda el texto original.

**Archivos:**
- Crear: migración `ConvertirModalidadesExistentes` (SQL a mano, sin cambios de modelo)
- Crear: `scripts/sql/20-verificar-modalidades.sql`

**Interfaces:**
- No produce código. Deja `Modalidad` cumpliendo el CHECK y `ModalidadDetalle` con
  el texto anterior.

> **Orden importante:** esta migración tiene que correr **antes** de que el CHECK
> de la tarea 5 se aplique sobre datos existentes. Como EF aplica las migraciones
> en orden de nombre, y `CamposFichaEnTramiteExpediente` ya creó el CHECK, hay que
> revisar el archivo de la tarea 5 y **mover la creación del CHECK a esta
> migración**, después del `UPDATE`. Si no, la migración anterior falla al aplicarse
> sobre la base de ensayo.

- [ ] **Paso 1: Medir el punto de partida**

Crear `scripts/sql/20-verificar-modalidades.sql`:

```sql
-- Reparto de modalidades en ExpedienteTramites. Antes de convertir debe dar diez
-- variantes; después, solo Virtual / Presencial / Hibrido / NULL.
SELECT ISNULL(Modalidad, '(nulo)') AS Modalidad, COUNT(*) AS Filas
FROM   ExpedienteTramites
GROUP  BY Modalidad
ORDER  BY COUNT(*) DESC;

-- Ninguna fila debe quedar fuera del catálogo cerrado.
SELECT COUNT(*) AS FueraDelCatalogo
FROM   ExpedienteTramites
WHERE  Modalidad IS NOT NULL
  AND  Modalidad NOT IN ('Virtual', 'Presencial', 'Hibrido');
```

Correrlo contra la base de ensayo y anotar el resultado.

- [ ] **Paso 2: Crear la migración vacía**

```powershell
dotnet ef migrations add ConvertirModalidadesExistentes --project src\Infrastructure --startup-project src\Web --output-dir Persistence\Migrations
```

Como no hay cambios de modelo, el archivo sale con `Up` y `Down` vacíos. Es lo
esperado.

- [ ] **Paso 3: Escribir la conversión**

En el `Up` de la migración generada:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // El texto libre se conserva íntegro antes de normalizar: «En línea (total)» y
    // «En línea Tipo de solicitud» llevan matiz que el catálogo cerrado pierde, y no hay
    // forma de recuperarlo después.
    migrationBuilder.Sql(@"
        UPDATE ExpedienteTramites
        SET    ModalidadDetalle = Modalidad
        WHERE  Modalidad IS NOT NULL AND LTRIM(RTRIM(Modalidad)) <> '';");

    // Mismo criterio que ModalidadNormalizador: se compara sin tildes y en minúsculas,
    // porque en la base conviven «En línea» y «En linea».
    migrationBuilder.Sql(@"
        UPDATE ExpedienteTramites
        SET    Modalidad = CASE
                 WHEN LOWER(Modalidad) COLLATE Latin1_General_CI_AI LIKE '%linea%'
                  AND LOWER(Modalidad) COLLATE Latin1_General_CI_AI LIKE '%presencial%'
                      THEN 'Hibrido'
                 WHEN LOWER(Modalidad) COLLATE Latin1_General_CI_AI LIKE '%linea%'
                      THEN 'Virtual'
                 WHEN LOWER(Modalidad) COLLATE Latin1_General_CI_AI LIKE '%presencial%'
                      THEN 'Presencial'
                 ELSE NULL
               END
        WHERE  Modalidad IS NOT NULL;");

    // El CHECK se crea aquí y no en la migración anterior: sobre los datos sin convertir
    // habría fallado al aplicarse.
    migrationBuilder.Sql(@"
        ALTER TABLE ExpedienteTramites
        ADD CONSTRAINT CK_ExpedienteTramites_Modalidad
        CHECK ([Modalidad] IS NULL OR [Modalidad] IN ('Virtual', 'Presencial', 'Hibrido'));");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(
        "ALTER TABLE ExpedienteTramites DROP CONSTRAINT CK_ExpedienteTramites_Modalidad;");
    migrationBuilder.Sql(@"
        UPDATE ExpedienteTramites
        SET    Modalidad = ModalidadDetalle
        WHERE  ModalidadDetalle IS NOT NULL;");
}
```

- [ ] **Paso 4: Quitar el CHECK de la migración anterior**

Abrir la migración `CamposFichaEnTramiteExpediente` y borrar de su `Up` la creación
de `CK_ExpedienteTramites_Modalidad` (y de su `Down`, el drop). La configuración en
`AppDbContext` se queda: es la que hace que `EnsureCreated` lo cree en los
`Web.Tests`.

- [ ] **Paso 5: Aplicar y verificar contra la base de ensayo**

```powershell
dotnet ef database update --project src\Infrastructure --startup-project src\Web
```

Después correr `scripts/sql/20-verificar-modalidades.sql`. Esperado:

| Modalidad | Filas |
|---|---|
| Virtual | 183 |
| (nulo) | 38 |
| Hibrido | 16 |
| Presencial | 3 |

y `FueraDelCatalogo` = 0. Si los números no cuadran con 240, **parar** y revisar
antes de seguir.

- [ ] **Paso 6: Comprometer**

```powershell
git add src\Infrastructure scripts\sql\20-verificar-modalidades.sql
git commit -m "Convertir las modalidades de texto libre al catalogo cerrado"
```

---

## Tarea 7: Entregables y lugares de atención en el expediente

**Archivos:**
- Crear: `src/Domain/Entities/ExpedienteTramiteHijos.cs`
- Modificar: `src/Domain/Entities/Expediente.cs` (2 colecciones, `LimpiarHijos`, 2 `Agregar`)
- Modificar: `src/Infrastructure/Persistence/AppDbContext.cs` (2 `DbSet` + 2 configuraciones)
- Modificar: `src/Application/Common/Interfaces/IRepositories.cs` (2 `DbSet`)
- Modificar: `src/Application/Expedientes/Common/ExpedienteDtos.cs` (2 records + 2 listas)
- Modificar: `src/Application/Expedientes/Common/ExpedienteMapper.cs` (2 direcciones)
- Modificar: `src/Web/Pages/Expedientes/OriginalShapeMapper.cs` (2 direcciones)
- Crear: migración `HijosDeTramiteExpediente`
- Probar: `tests/Application.Tests/Expedientes/HijosTramiteTests.cs`

**Interfaces:**
- Produce: `ExpedienteTramiteEntregable` (`ExpedienteId`, `TramiteIndex`, `Orden`,
  `Entregable`, `Formato`, `Presentacion`) y `ExpedienteTramiteLugar`
  (`ExpedienteId`, `TramiteIndex`, `Orden`, `Lugar`, `Ciudad`, `Direccion`,
  `Telefonos`). En el DTO: `EntregableInput` y `LugarInput` con la misma forma, y
  `ExpedienteInputDto.Entregables` / `.Lugares`. Claves JSON: `entregables_tram` y
  `lugares_tram`, listas por trámite igual que `reqs_tram`. Lo usan las tareas 8, 9 y 10.

- [ ] **Paso 1: Escribir la prueba que falla**

`tests/Application.Tests/Expedientes/HijosTramiteTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Expedientes.Common;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Expedientes;

/// <summary>
/// Entregables y lugares de atención son colecciones hijas nuevas del expediente y siguen la
/// misma regla que las diez que ya existen: se reemplazan en bloque. La prueba de reemplazo no
/// es un detalle — si LimpiarHijos() se olvida de una colección, cada guardado la duplica.
/// </summary>
public sealed class HijosTramiteTests
{
    [Fact]
    public void Los_entregables_y_lugares_llegan_a_la_entidad()
    {
        var e = Expediente.Crear("EXP-010", "ADUANAS", null, null, "Aduanas", "Analista");

        ExpedienteMapper.Aplicar(e, ConHijos(
            [new EntregableInput(0, 0, "Permiso sellado", "PDF", "Digital")],
            [new LugarInput(0, 0, "Ventanilla central", "Tegucigalpa", "Bo. El Centro", "2222-0000")]));

        e.Entregables.Should().ContainSingle()
            .Which.Entregable.Should().Be("Permiso sellado");
        e.Lugares.Should().ContainSingle()
            .Which.Lugar.Should().Be("Ventanilla central");
    }

    [Fact]
    public void Guardar_dos_veces_no_duplica_los_hijos()
    {
        var e = Expediente.Crear("EXP-011", "ADUANAS", null, null, "Aduanas", "Analista");
        var entrada = ConHijos(
            [new EntregableInput(0, 0, "Constancia", null, null)],
            [new LugarInput(0, 0, "Sede", null, null, null)]);

        ExpedienteMapper.Aplicar(e, entrada);
        ExpedienteMapper.Aplicar(e, entrada);

        e.Entregables.Should().HaveCount(1, "LimpiarHijos() debe vaciar también estas dos");
        e.Lugares.Should().HaveCount(1);
    }

    [Fact]
    public void Un_entregable_sin_texto_no_se_guarda()
    {
        var e = Expediente.Crear("EXP-012", "ADUANAS", null, null, "Aduanas", "Analista");

        ExpedienteMapper.Aplicar(e, ConHijos(
            [new EntregableInput(0, 0, "   ", null, null)], []));

        e.Entregables.Should().BeEmpty("es la misma regla que ya aplica a requisitos");
    }

    private static ExpedienteInputDto ConHijos(
        List<EntregableInput> entregables, List<LugarInput> lugares) =>
        ExpedienteInputFactory.Minimo() with { Entregables = entregables, Lugares = lugares };
}
```

- [ ] **Paso 2: Correr la prueba y confirmar que falla**

```powershell
dotnet test tests\Application.Tests --filter "FullyQualifiedName~HijosTramite"
```

Esperado: no compila.

- [ ] **Paso 3: Crear las entidades**

`src/Domain/Entities/ExpedienteTramiteHijos.cs`:

```csharp
namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Documento que el trámite entrega al ciudadano. Los largos son los de
/// <see cref="EntregableSiger"/>, su destino al promover, para que nunca haya que truncar.</summary>
public sealed class ExpedienteTramiteEntregable : BaseEntity
{
    public int     ExpedienteId { get; set; }
    public int     TramiteIndex { get; set; }
    public int     Orden        { get; set; }
    public string  Entregable   { get; set; } = default!;
    public string? Formato      { get; set; }
    public string? Presentacion { get; set; }
}

/// <summary>Sede donde se atiende el trámite. Los largos son los de
/// <see cref="LugarAtencionSiger"/>.</summary>
public sealed class ExpedienteTramiteLugar : BaseEntity
{
    public int     ExpedienteId { get; set; }
    public int     TramiteIndex { get; set; }
    public int     Orden        { get; set; }
    public string  Lugar        { get; set; } = default!;
    public string? Ciudad       { get; set; }
    public string? Direccion    { get; set; }
    public string? Telefonos    { get; set; }
}
```

- [ ] **Paso 4: Sumarlas al agregado**

En `src/Domain/Entities/Expediente.cs`, junto a las otras colecciones:

```csharp
    private readonly List<ExpedienteTramiteEntregable> _entregables = [];
    private readonly List<ExpedienteTramiteLugar>      _lugares     = [];

    public IReadOnlyCollection<ExpedienteTramiteEntregable> Entregables => _entregables.AsReadOnly();
    public IReadOnlyCollection<ExpedienteTramiteLugar>      Lugares     => _lugares.AsReadOnly();
```

En `LimpiarHijos()`, agregar al final de la línea de `_secciones.Clear()`:

```csharp
        _entregables.Clear(); _lugares.Clear();
```

Y las dos sobrecargas:

```csharp
    public void Agregar(ExpedienteTramiteEntregable x) => _entregables.Add(x);
    public void Agregar(ExpedienteTramiteLugar x)      => _lugares.Add(x);
```

- [ ] **Paso 5: Configurar EF**

En `AppDbContext.cs`, dos `DbSet`:

```csharp
    public DbSet<ExpedienteTramiteEntregable> TramiteEntregables { get; init; } = default!;
    public DbSet<ExpedienteTramiteLugar>      TramiteLugares     { get; init; } = default!;
```

y dos configuraciones, siguiendo la forma de `TramiteRequisitoConfiguration`:

```csharp
public sealed class ExpedienteTramiteEntregableConfiguration
    : IEntityTypeConfiguration<ExpedienteTramiteEntregable>
{
    public void Configure(EntityTypeBuilder<ExpedienteTramiteEntregable> b)
    {
        b.ToTable("ExpedienteTramiteEntregables");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Entregable).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Formato).HasMaxLength(2000);
        b.Property(x => x.Presentacion).HasMaxLength(600);
        b.HasIndex(x => new { x.ExpedienteId, x.TramiteIndex, x.Orden });
    }
}

public sealed class ExpedienteTramiteLugarConfiguration
    : IEntityTypeConfiguration<ExpedienteTramiteLugar>
{
    public void Configure(EntityTypeBuilder<ExpedienteTramiteLugar> b)
    {
        b.ToTable("ExpedienteTramiteLugares");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Lugar).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Ciudad).HasMaxLength(2000);
        b.Property(x => x.Direccion).HasMaxLength(2000);
        b.Property(x => x.Telefonos).HasMaxLength(1000);
        b.HasIndex(x => new { x.ExpedienteId, x.TramiteIndex, x.Orden });
    }
}
```

Revisar cómo `TramiteRequisitos` declara su relación con `Expediente` y copiar ese
mismo patrón para las dos nuevas, para que el borrado en cascada se comporte igual.

Agregar los dos `DbSet` también a `IApplicationDbContext` en
`src/Application/Common/Interfaces/IRepositories.cs`.

- [ ] **Paso 6: Agregar los DTO**

En `ExpedienteDtos.cs`:

```csharp
public sealed record EntregableInput(
    int TramiteIndex, int Orden, string Entregable, string? Formato, string? Presentacion);

public sealed record LugarInput(
    int TramiteIndex, int Orden, string Lugar, string? Ciudad, string? Direccion, string? Telefonos);
```

y las dos listas en `ExpedienteInputDto`, al final y con valor por defecto:

```csharp
    List<EntregableInput>? Entregables = null,
    List<LugarInput>? Lugares = null);
```

- [ ] **Paso 7: Mapear en las dos direcciones**

En `ExpedienteMapper.Aplicar`, después del bloque de requisitos:

```csharp
        foreach (var x in (d.Entregables ?? []).Where(z => !string.IsNullOrWhiteSpace(z.Entregable)))
            e.Agregar(new ExpedienteTramiteEntregable
            {
                TramiteIndex = x.TramiteIndex, Orden = x.Orden,
                Entregable = x.Entregable.Trim(), Formato = x.Formato, Presentacion = x.Presentacion
            });

        foreach (var x in (d.Lugares ?? []).Where(z => !string.IsNullOrWhiteSpace(z.Lugar)))
            e.Agregar(new ExpedienteTramiteLugar
            {
                TramiteIndex = x.TramiteIndex, Orden = x.Orden,
                Lugar = x.Lugar.Trim(), Ciudad = x.Ciudad,
                Direccion = x.Direccion, Telefonos = x.Telefonos
            });
```

Agregar las dos a `ConteosHijos` para que el registro de cambios las cuente, y la
proyección inversa junto a la de requisitos.

En `OriginalShapeMapper`, replicar exactamente la forma de `ReqsTram`: listas por
trámite, con las claves `entregables_tram` y `lugares_tram`, en las dos
direcciones. Agregar los dos campos correspondientes a `OriginalExpedienteDto`.

- [ ] **Paso 8: Generar la migración**

```powershell
dotnet ef migrations add HijosDeTramiteExpediente --project src\Infrastructure --startup-project src\Web --output-dir Persistence\Migrations
```

- [ ] **Paso 9: Correr las pruebas**

```powershell
dotnet build Diger.TramitesEstado.sln
dotnet test Diger.TramitesEstado.sln
```

Esperado: todo verde, incluidas las 3 nuevas.

- [ ] **Paso 10: Comprometer**

```powershell
git add src tests
git commit -m "Agregar entregables y lugares de atencion al tramite del expediente"
```

---

## Tarea 8: Sembrar las dos tablas con lo que ya existe

**Archivos:**
- Crear: migración `SembrarEntregablesYLugares`
- Crear: `scripts/sql/21-verificar-siembra.sql`

**Interfaces:**
- No produce código.

- [ ] **Paso 1: Crear la migración vacía**

```powershell
dotnet ef migrations add SembrarEntregablesYLugares --project src\Infrastructure --startup-project src\Web --output-dir Persistence\Migrations
```

- [ ] **Paso 2: Escribir la siembra**

En el `Up`:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // DocEntregado es un texto suelto por trámite (202 de 240 lo tienen). Pasa a ser el
    // primer entregable de la lista, para que nadie tenga que volver a teclearlo.
    migrationBuilder.Sql(@"
        INSERT INTO ExpedienteTramiteEntregables (ExpedienteId, TramiteIndex, Orden, Entregable)
        SELECT ExpedienteId, TramiteIndex, 0, LTRIM(RTRIM(DocEntregado))
        FROM   ExpedienteTramites
        WHERE  DocEntregado IS NOT NULL AND LTRIM(RTRIM(DocEntregado)) <> '';");

    // El expediente no tenía lista de sedes, pero sí horario y teléfono por trámite y una
    // dirección de sede. Se arma un lugar único con eso. El nombre sale de la institución,
    // porque no hay un campo que nombre la sede.
    migrationBuilder.Sql(@"
        INSERT INTO ExpedienteTramiteLugares
               (ExpedienteId, TramiteIndex, Orden, Lugar, Direccion, Telefonos)
        SELECT t.ExpedienteId, t.TramiteIndex, 0,
               e.Institucion, e.DirSede, t.Telefono
        FROM   ExpedienteTramites t
        JOIN   Expedientes e ON e.Id = t.ExpedienteId
        WHERE  (t.Telefono IS NOT NULL AND LTRIM(RTRIM(t.Telefono)) <> '')
           OR  (e.DirSede  IS NOT NULL AND LTRIM(RTRIM(e.DirSede))  <> '');");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // Solo borra lo sembrado (Orden = 0 y sin nada capturado después). Es deliberadamente
    // conservador: si alguien ya editó la lista, revertir no debe borrarle el trabajo.
    migrationBuilder.Sql("DELETE FROM ExpedienteTramiteEntregables WHERE Orden = 0;");
    migrationBuilder.Sql("DELETE FROM ExpedienteTramiteLugares WHERE Orden = 0;");
}
```

- [ ] **Paso 3: Escribir la verificación**

`scripts/sql/21-verificar-siembra.sql`:

```sql
-- Entregables sembrados: debe coincidir con los trámites que tenían DocEntregado.
SELECT (SELECT COUNT(*) FROM ExpedienteTramiteEntregables)            AS EntregablesSembrados,
       (SELECT COUNT(*) FROM ExpedienteTramites
        WHERE DocEntregado IS NOT NULL AND LTRIM(RTRIM(DocEntregado)) <> '') AS ConDocEntregado;

-- Ningún entregable ni lugar puede quedar sin texto: la columna es obligatoria.
SELECT (SELECT COUNT(*) FROM ExpedienteTramiteEntregables
        WHERE Entregable IS NULL OR LTRIM(RTRIM(Entregable)) = '') AS EntregablesVacios,
       (SELECT COUNT(*) FROM ExpedienteTramiteLugares
        WHERE Lugar IS NULL OR LTRIM(RTRIM(Lugar)) = '')           AS LugaresVacios;
```

- [ ] **Paso 4: Aplicar y verificar**

```powershell
dotnet ef database update --project src\Infrastructure --startup-project src\Web
```

Correr `scripts/sql/21-verificar-siembra.sql`. Esperado: `EntregablesSembrados` =
`ConDocEntregado` = 202, y los dos conteos de vacíos en 0.

- [ ] **Paso 5: Comprometer**

```powershell
git add src\Infrastructure scripts\sql\21-verificar-siembra.sql
git commit -m "Sembrar entregables y lugares con los datos sueltos que ya existian"
```

---

## Tarea 9: Los campos nuevos en la pantalla del expediente

**Archivos:**
- Modificar: `src/Web/Pages/Expedientes/Editor.cshtml` (ficha del trámite)
- Modificar: `src/Web/Pages/Expedientes/Editor.cshtml.cs` (cargar categorías)
- Modificar: `src/Web/wwwroot/js/expediente.js` (`FICHA_FIELDS`, listas nuevas)

**Interfaces:**
- Consume: las claves JSON de las tareas 5 y 7.
- Produce: la pantalla escribe y lee esas claves. Sin esto, los campos existen en
  la base pero nadie puede llenarlos.

- [ ] **Paso 1: Leer cómo está armada la ficha hoy**

```powershell
Select-String -Path src\Web\wwwroot\js\expediente.js -Pattern "FICHA_FIELDS" -Context 0,6
Select-String -Path src\Web\wwwroot\js\expediente.js -Pattern "renderFichasPanels" -Context 0,25
```

Los campos de la ficha se declaran en `FICHA_FIELDS` y se dibujan en
`renderFichasPanels`. Los campos nuevos siguen ese mismo camino; las listas siguen
el de los requisitos, que ya son una lista por trámite.

- [ ] **Paso 2: Declarar los campos nuevos**

En `expediente.js`, agregar a `FICHA_FIELDS`:

```javascript
var FICHA_FIELDS = ['nombre_corto','modalidad','modalidad_detalle','categoria_id','es_gratuito',
  'plazo_legal','tercero','tiempo_real','metodo_pago',
  'pago_banco','pago_cuenta','tgr_inst','tgr_rubro','tgr_monto','doc_entregado','objetivo',
  'alcance_obs','descripcion','dirigido','horario','telefono','email_tramite','sitio_web'];
```

- [ ] **Paso 3: Dibujar los controles**

En `renderFichasPanels`, donde hoy se dibuja el campo de texto de `modalidad`,
reemplazarlo por un desplegable y agregar los otros dos:

```javascript
  + '<div class="f"><label>Modalidad</label>'
  + '<select id="modalidad_'+i+'">'
  +   '<option value="">— Sin capturar —</option>'
  +   '<option value="Virtual">Virtual (en línea)</option>'
  +   '<option value="Presencial">Presencial</option>'
  +   '<option value="Hibrido">Híbrido (ambas)</option>'
  + '</select></div>'
  + '<div class="f"><label>Categoría</label>'
  + '<select id="categoria_id_'+i+'">' + opcionesCategoria() + '</select></div>'
  + '<div class="f"><label>Costo</label>'
  + '<select id="es_gratuito_'+i+'">'
  +   '<option value="">— Sin capturar —</option>'
  +   '<option value="1">Es gratuito</option>'
  +   '<option value="0">Tiene costo</option>'
  + '</select></div>'
```

`opcionesCategoria()` se arma con el catálogo que el PageModel deja en
`window.__EXPMETA__.categorias`, igual que ya hace con `plantillas`.

El texto libre anterior se conserva y se muestra como solo lectura, para que el
analista vea qué decía:

```javascript
  + '<div class="f"><label>Modalidad (texto original del levantamiento)</label>'
  + '<input type="text" id="modalidad_detalle_'+i+'" readonly></div>'
```

- [ ] **Paso 4: Publicar el catálogo de categorías**

En `Editor.cshtml.cs`, cargar las categorías y pasarlas al `__EXPMETA__` de la
vista, siguiendo el mismo patrón que ya usa para `plantillas`:

```csharp
    public IReadOnlyList<CategoriaTramite> Categorias { get; private set; } = [];
```

y en `OnGetAsync`:

```csharp
        Categorias = await db.CategoriasTramite.AsNoTracking()
            .OrderBy(c => c.Nombre).ToListAsync(ct);
```

- [ ] **Paso 5: Las dos listas nuevas**

Entregables y lugares se dibujan como los requisitos: una tabla por trámite con
botón de agregar y de quitar fila. Localizar la función que hace eso para
requisitos y replicarla con los campos de cada una:

```powershell
Select-String -Path src\Web\wwwroot\js\expediente.js -Pattern "reqs_tram|renderRequisitos|agregarRequisito"
```

Al serializar (la función que arma el JSON de guardado, donde hoy aparece
`ft.tramite_siger_id`), agregar `entregables_tram` y `lugares_tram` con la misma
forma que `reqs_tram`.

- [ ] **Paso 6: Probar a mano**

```powershell
dotnet build Diger.TramitesEstado.sln
dotnet run --project src\Web
```

Entrar con `admin@diger.gob.hn` / `Admin#2026`, abrir un expediente, y comprobar
las cuatro cosas: que la modalidad muestre el valor convertido, que el texto
original se vea al lado, que se pueda agregar un entregable y un lugar, y que todo
siga ahí después de guardar y recargar.

- [ ] **Paso 7: Comprometer**

```powershell
git add src\Web
git commit -m "Capturar categoria, modalidad, costo, entregables y lugares en el expediente"
```

---

# Fase 3 — Promover

## Tarea 10: El mapeo de promoción

**Archivos:**
- Crear: `src/Application/Siger/Promocion/PromocionMapeo.cs`
- Probar: `tests/Application.Tests/Siger/Promocion/PromocionMapeoTests.cs`

**Interfaces:**
- Consume: `ModalidadNormalizador`, `CodigoPromovido`, `ReglaPublicacion`.
- Produce:
  - `PromocionMapeo.CrearFicha(ExpedienteTramite t, Expediente e, string codigo) -> TramiteSiger`
  - `PromocionMapeo.CostoTexto(ExpedienteTramite t) -> string?`
  - `PromocionMapeo.Requisitos(IEnumerable<TramiteRequisito>) -> List<RequisitoSiger>`
  - `PromocionMapeo.Entregables(IEnumerable<ExpedienteTramiteEntregable>) -> List<EntregableSiger>`
  - `PromocionMapeo.Lugares(IEnumerable<ExpedienteTramiteLugar>) -> List<LugarAtencionSiger>`
  - `PromocionMapeo.CamposDelExpediente(TramiteSiger destino, ExpedienteTramite t, Expediente e)`
    — reescribe **solo** las columnas que el expediente manda. Lo usa la tarea 14.

- [ ] **Paso 1: Escribir la prueba que falla**

`tests/Application.Tests/Siger/Promocion/PromocionMapeoTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Siger.Promocion;
using Diger.TramitesEstado.Application.Siger.Publico;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Promocion;

/// <summary>
/// El mapeo de un trámite de expediente a una ficha SIGER. Lo que más importa aquí no es que
/// copie —eso es mecánico— sino que NO copie: una ficha promovida nace sin publicar y sin
/// IdSiger, y el reparto de propiedad impide que actualizar desde el expediente borre lo que
/// SIGER decidió.
/// </summary>
public sealed class PromocionMapeoTests
{
    [Fact]
    public void La_ficha_nace_sin_IdSiger_y_sin_publicar()
    {
        var ficha = PromocionMapeo.CrearFicha(Tramite(), Expedienteo(), "400-P01");

        ficha.IdSiger.Should().BeNull("es la marca de que no existe en SIGER");
        ficha.Codigo.Should().Be("400-P01");
        ficha.EstadoSiger.Should().Be(ReglaPublicacion.Registrado);
        ficha.Publicado.Should().BeFalse("promover y publicar son dos actos distintos");
    }

    [Fact]
    public void Copia_la_cabecera_del_tramite_y_la_institucion_del_expediente()
    {
        var ficha = PromocionMapeo.CrearFicha(Tramite(), Expedienteo(), "400-P01");

        ficha.Nombre.Should().Be("Permiso de importación");
        ficha.Institucion.Should().Be("Aduanas");
        ficha.InstitucionId.Should().Be("ADUANAS");
        ficha.Dependencia.Should().Be("Dirección de Operaciones");
        ficha.Objetivo.Should().Be("Autorizar la importación");
        ficha.DirigidoA.Should().Be("Importadores");
        ficha.EnlacePrincipal.Should().Be("https://aduanas.gob.hn/permiso");
        ficha.CategoriaId.Should().Be(3);
        ficha.Modalidad.Should().Be(ModalidadPublica.Hibrido);
    }

    [Fact]
    public void El_tiempo_sale_del_real_y_si_no_del_plazo_legal()
    {
        PromocionMapeo.CrearFicha(Tramite(), Expedienteo(), "x").TiempoTexto
            .Should().Be("5 días hábiles");

        var sinReal = Tramite();
        sinReal.TiempoReal = null;
        PromocionMapeo.CrearFicha(sinReal, Expedienteo(), "x").TiempoTexto
            .Should().Be("10 días por ley");
    }

    [Fact]
    public void Un_tramite_gratuito_no_lleva_texto_de_costo()
    {
        var t = Tramite();
        t.EsGratuito = true;

        var ficha = PromocionMapeo.CrearFicha(t, Expedienteo(), "x");

        ficha.CostoEsGratuito.Should().BeTrue();
        ficha.CostoTexto.Should().BeNull("«es gratuito» ya es una respuesta completa");
    }

    [Fact]
    public void Un_tramite_con_costo_arma_el_texto_con_monto_y_metodo()
    {
        var t = Tramite();
        t.EsGratuito = false;

        PromocionMapeo.CrearFicha(t, Expedienteo(), "x").CostoTexto
            .Should().Be("L. 250.00 — Depósito bancario");
    }

    [Fact]
    public void Un_costo_sin_capturar_deja_la_ficha_incompleta_sin_inventar()
    {
        var t = Tramite();
        t.EsGratuito = null;

        var ficha = PromocionMapeo.CrearFicha(t, Expedienteo(), "x");

        ficha.CostoEsGratuito.Should().BeNull();
        ficha.CostoTexto.Should().BeNull("no se infiere el costo de un texto de pago");
        FichaPublicaCompletitud.CamposFaltantes(
            ficha.CategoriaId, ficha.Modalidad, ficha.TiempoTexto,
            ficha.CostoEsGratuito, ficha.EstaEnSol, ficha.SolUrl)
            .Should().Contain("costo");
    }

    [Fact]
    public void Actualizar_no_toca_lo_que_SIGER_decide()
    {
        var ficha = PromocionMapeo.CrearFicha(Tramite(), Expedienteo(), "400-P01");
        // Alguien la aprobó, la marcó como popular y le puso el enlace a SOL.
        ficha.EstadoSiger = ReglaPublicacion.Aprobado;
        ficha.Publicado   = true;
        ficha.EsPopular   = true;
        ficha.EstaEnSol   = true;
        ficha.SolUrl      = "https://sol.gob.hn/permiso";

        var t = Tramite();
        t.NombreTramite = "Permiso de importación (corregido)";
        PromocionMapeo.CamposDelExpediente(ficha, t, Expedienteo());

        ficha.Nombre.Should().Be("Permiso de importación (corregido)", "el expediente manda el contenido");
        ficha.Codigo.Should().Be("400-P01", "el código se genera una vez y no cambia");
        ficha.EstadoSiger.Should().Be(ReglaPublicacion.Aprobado);
        ficha.Publicado.Should().BeTrue("actualizar no puede sacar una ficha del portal");
        ficha.EsPopular.Should().BeTrue();
        ficha.EstaEnSol.Should().BeTrue();
        ficha.SolUrl.Should().Be("https://sol.gob.hn/permiso");
    }

    [Fact]
    public void Los_requisitos_se_numeran_desde_uno_en_el_orden_del_expediente()
    {
        var reqs = PromocionMapeo.Requisitos([
            new TramiteRequisito { Orden = 1, Requisito = "Segundo" },
            new TramiteRequisito { Orden = 0, Requisito = "Primero" }
        ]);

        reqs.Select(r => (r.Numero, r.Requisito))
            .Should().Equal((1, "Primero"), (2, "Segundo"));
    }

    private static ExpedienteTramite Tramite() => new()
    {
        TramiteIndex = 0,
        NombreTramite = "Permiso de importación",
        AreaResponsable = "Dirección de Operaciones",
        Objetivo = "Autorizar la importación",
        Descripcion = "Permite ingresar mercadería",
        Dirigido = "Importadores",
        SitioWeb = "https://aduanas.gob.hn/permiso",
        TiempoReal = "5 días hábiles",
        PlazoLegal = "10 días por ley",
        CategoriaId = 3,
        Modalidad = ModalidadPublica.Hibrido,
        TgrMonto = "L. 250.00",
        MetodoPago = "Depósito bancario"
    };

    private static Expediente Expedienteo() =>
        Expediente.Crear("EXP-100", "ADUANAS", null, null, "Aduanas", "Analista");
}
```

- [ ] **Paso 2: Correr la prueba y confirmar que falla**

```powershell
dotnet test tests\Application.Tests --filter "FullyQualifiedName~PromocionMapeo"
```

Esperado: no compila.

- [ ] **Paso 3: Escribir la implementación**

`src/Application/Siger/Promocion/PromocionMapeo.cs`:

```csharp
using Diger.TramitesEstado.Domain.Entities;

namespace Diger.TramitesEstado.Application.Siger.Promocion;

/// <summary>
/// Convierte un trámite de expediente en una ficha SIGER, y reaplica sobre una ficha existente
/// solo las columnas que el expediente manda.
/// </summary>
/// <remarks>
/// El reparto de propiedad está en <see cref="CamposDelExpediente"/> y es la pieza que hace
/// seguro el botón de actualizar: todo lo que SIGER sabe y el expediente no —si está en SOL y
/// dónde, si es destacado, si está aprobado— se queda fuera. Sin esa separación, un analista
/// que pulsa «actualizar» sacaría del portal una ficha ya publicada sin enterarse.
/// </remarks>
public static class PromocionMapeo
{
    public static TramiteSiger CrearFicha(ExpedienteTramite t, Expediente e, string codigo)
    {
        var ficha = new TramiteSiger
        {
            IdSiger     = null,                       // la marca de «no existe en SIGER»
            Codigo      = codigo,
            EstadoSiger = ReglaPublicacion.Registrado,
            Nombre      = t.NombreTramite,            // se sobreescribe abajo, pero Nombre es requerido
            Institucion = e.Institucion
        };
        ficha.Publicado = ReglaPublicacion.SePublica(ficha.EstadoSiger);
        CamposDelExpediente(ficha, t, e);
        return ficha;
    }

    /// <summary>Reescribe únicamente lo que el expediente manda. Todo lo demás se deja intacto.</summary>
    public static void CamposDelExpediente(TramiteSiger destino, ExpedienteTramite t, Expediente e)
    {
        destino.Nombre          = t.NombreTramite;
        destino.Institucion     = e.Institucion;
        destino.InstitucionId   = e.InstitucionId;
        destino.Dependencia     = t.AreaResponsable;
        destino.Descripcion     = t.Descripcion;
        destino.Objetivo        = t.Objetivo;
        destino.DirigidoA       = t.Dirigido;
        destino.EnlacePrincipal = t.SitioWeb;
        destino.CategoriaId     = t.CategoriaId;
        destino.Modalidad       = t.Modalidad;
        destino.TiempoTexto     = Recortar(t.TiempoReal ?? t.PlazoLegal, 120);
        destino.CostoEsGratuito = t.EsGratuito;
        destino.CostoTexto      = CostoTexto(t);
    }

    /// <summary>
    /// Gratuito no lleva texto: «es gratuito» ya es una respuesta completa y no hay monto que
    /// escribir. Sin capturar tampoco lleva texto — inventarlo desde los campos de pago haría
    /// que la ficha pareciera completa sin serlo.
    /// </summary>
    public static string? CostoTexto(ExpedienteTramite t)
    {
        if (t.EsGratuito != false) return null;

        var partes = new[] { t.TgrMonto, t.MetodoPago }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim());

        var texto = string.Join(" — ", partes);
        return string.IsNullOrWhiteSpace(texto) ? null : Recortar(texto, 250);
    }

    public static List<RequisitoSiger> Requisitos(IEnumerable<TramiteRequisito> origen) =>
        origen.OrderBy(r => r.Orden)
              .Select((r, i) => new RequisitoSiger { Numero = i + 1, Requisito = r.Requisito })
              .ToList();

    public static List<EntregableSiger> Entregables(IEnumerable<ExpedienteTramiteEntregable> origen) =>
        origen.OrderBy(x => x.Orden)
              .Select((x, i) => new EntregableSiger
              {
                  Numero = i + 1, Entregable = x.Entregable,
                  Formato = x.Formato, Presentacion = x.Presentacion
              })
              .ToList();

    public static List<LugarAtencionSiger> Lugares(IEnumerable<ExpedienteTramiteLugar> origen) =>
        origen.OrderBy(x => x.Orden)
              .Select((x, i) => new LugarAtencionSiger
              {
                  Numero = i + 1, Lugar = x.Lugar, Ciudad = x.Ciudad,
                  Direccion = x.Direccion, Telefonos = x.Telefonos
              })
              .ToList();

    private static string? Recortar(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null
        : s.Length <= max ? s.Trim()
        : s.Trim()[..max];
}
```

- [ ] **Paso 4: Correr la prueba y confirmar que pasa**

```powershell
dotnet test tests\Application.Tests --filter "FullyQualifiedName~PromocionMapeo"
```

Esperado: 8 pruebas en verde.

- [ ] **Paso 5: Comprometer**

```powershell
git add src\Application\Siger\Promocion\PromocionMapeo.cs tests\Application.Tests\Siger\Promocion\PromocionMapeoTests.cs
git commit -m "Mapear un tramite de expediente a ficha SIGER, con reparto de propiedad"
```

---

## Tarea 11: El handler que promueve

**Archivos:**
- Modificar: `src/Web/Pages/Expedientes/Editor.cshtml.cs` (handler `OnPostPromoverASigerAsync`)
- Probar: `tests/Web.Tests/PromoverASigerTests.cs`

**Interfaces:**
- Consume: `PromocionMapeo`, `CodigoPromovido`, `ReglaPublicacion`.
- Produce: `POST /Expedientes/Editor?handler=PromoverASiger` con
  `{ expedienteId, tramiteIndex }`; devuelve
  `{ sigerId, codigo, faltantes: string[] }`. Lo usa la tarea 12.

- [ ] **Paso 1: Escribir la prueba que falla**

`tests/Web.Tests/PromoverASigerTests.cs`:

```csharp
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Promover crea la ficha y deja el enlace. Las dos cosas tienen que pasar o ninguna: una ficha
/// creada sin enlace queda huérfana y el analista la vuelve a promover, duplicándola.
/// </summary>
public sealed class PromoverASigerTests(PortalFactory factory) : IClassFixture<PortalFactory>
{
    [Fact]
    public async Task Promover_crea_la_ficha_y_deja_el_enlace()
    {
        var (expedienteId, _) = await SembrarExpediente(factory);

        var cliente = factory.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync(
            "/Expedientes/Editor?handler=PromoverASiger",
            new { expedienteId, tramiteIndex = 0 });

        respuesta.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tramite = await db.Tramites.SingleAsync(
            t => t.ExpedienteId == expedienteId && t.TramiteIndex == 0);
        tramite.TramiteSigerId.Should().NotBeNull();

        var ficha = await db.TramitesSiger.SingleAsync(f => f.Id == tramite.TramiteSigerId);
        ficha.IdSiger.Should().BeNull();
        ficha.Publicado.Should().BeFalse();
        ficha.Codigo.Should().EndWith("-P01");
    }

    [Fact]
    public async Task Promover_dos_veces_el_mismo_tramite_no_crea_dos_fichas()
    {
        var (expedienteId, _) = await SembrarExpediente(factory);
        var cliente = factory.CreateClient();
        var cuerpo = new { expedienteId, tramiteIndex = 0 };

        await cliente.PostAsJsonAsync("/Expedientes/Editor?handler=PromoverASiger", cuerpo);
        var segunda = await cliente.PostAsJsonAsync("/Expedientes/Editor?handler=PromoverASiger", cuerpo);

        segunda.IsSuccessStatusCode.Should().BeFalse(
            "un trámite ya promovido se actualiza, no se vuelve a promover");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.TramitesSiger.CountAsync(f => f.IdSiger == null)).Should().Be(1);
    }

    [Fact]
    public async Task Los_requisitos_del_expediente_llegan_a_la_ficha()
    {
        var (expedienteId, _) = await SembrarExpediente(factory);

        var cliente = factory.CreateClient();
        await cliente.PostAsJsonAsync("/Expedientes/Editor?handler=PromoverASiger",
            new { expedienteId, tramiteIndex = 0 });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tramite = await db.Tramites.SingleAsync(
            t => t.ExpedienteId == expedienteId && t.TramiteIndex == 0);

        var requisitos = await db.RequisitosSiger
            .Where(r => r.TramiteSigerId == tramite.TramiteSigerId)
            .OrderBy(r => r.Numero).ToListAsync();

        requisitos.Select(r => r.Requisito).Should().Equal("Solicitud firmada", "Copia de RTN");
    }

    /// <summary>Deja en la base un expediente con un trámite y dos requisitos.</summary>
    private static async Task<(int ExpedienteId, int TramiteIndex)> SembrarExpediente(
        PortalFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var e = Expediente.Crear("EXP-900", "ADUANAS", null, null, "Aduanas", "Analista");
        e.Agregar(new ExpedienteTramite
        {
            TramiteIndex = 0, NombreTramite = "Permiso de importación",
            FechaCreacion = DateOnly.FromDateTime(DateTime.Today)
        });
        e.Agregar(new TramiteRequisito { TramiteIndex = 0, Orden = 0, Requisito = "Solicitud firmada" });
        e.Agregar(new TramiteRequisito { TramiteIndex = 0, Orden = 1, Requisito = "Copia de RTN" });

        db.Expedientes.Add(e);
        await db.SaveChangesAsync();
        return (e.Id, 0);
    }
}
```

> **Nota:** revisar cómo `GateoDePermisosTests` construye un cliente autenticado
> (`TestAuthHandler`) y usar el mismo mecanismo. Si `PostAsJsonAsync` necesita
> antiforgery, seguir el patrón que ya use ese archivo.

- [ ] **Paso 2: Correr la prueba y confirmar que falla**

```powershell
dotnet test tests\Web.Tests --filter "FullyQualifiedName~PromoverASiger"
```

Esperado: 404 — el handler no existe.

- [ ] **Paso 3: Escribir el handler**

En `src/Web/Pages/Expedientes/Editor.cshtml.cs`:

```csharp
    public sealed record PromoverRequest(int ExpedienteId, int TramiteIndex);

    /// <summary>
    /// Crea una ficha SIGER a partir de un trámite ya guardado del expediente.
    /// </summary>
    /// <remarks>
    /// Se identifica el trámite por (ExpedienteId, TramiteIndex) y no por su Id: el mapeador del
    /// expediente borra y recrea los trámites en cada guardado, así que el Id no sobrevive.
    /// <para>
    /// Todo se guarda con un solo SaveChangesAsync, que EF ya envuelve en una transacción. No se
    /// abre una explícita: el DbContext usa EnableRetryOnFailure y las dos cosas son
    /// incompatibles.
    /// </para>
    /// </remarks>
    [Permission("Siger", AccionModulo.Crear, "Promover un trámite del expediente a SIGER")]
    public async Task<IActionResult> OnPostPromoverASigerAsync(
        [FromBody] PromoverRequest req, CancellationToken ct)
    {
        var expediente = await db.Expedientes
            .FirstOrDefaultAsync(e => e.Id == req.ExpedienteId, ct);
        if (expediente is null) return NotFound();

        var tramite = await db.Tramites.FirstOrDefaultAsync(
            t => t.ExpedienteId == req.ExpedienteId && t.TramiteIndex == req.TramiteIndex, ct);
        if (tramite is null) return NotFound();

        if (tramite.TramiteSigerId is not null)
            return BadRequest(new { error = "Este trámite ya está en SIGER. Use «Actualizar ficha»." });

        // El prefijo sale de las fichas que esa institución ya tiene en SIGER.
        var codigosInstitucion = await db.TramitesSiger.AsNoTracking()
            .Where(f => f.InstitucionId == expediente.InstitucionId)
            .Select(f => f.Codigo)
            .ToListAsync(ct);

        var prefijo = codigosInstitucion
            .Select(CodigoPromovido.PrefijoDe)
            .GroupBy(p => p)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        var codigo = CodigoPromovido.Siguiente(prefijo, codigosInstitucion);
        var ficha = PromocionMapeo.CrearFicha(tramite, expediente, codigo);

        var requisitos = await db.Requisitos.AsNoTracking()
            .Where(r => r.ExpedienteId == req.ExpedienteId && r.TramiteIndex == req.TramiteIndex)
            .ToListAsync(ct);
        var entregables = await db.TramiteEntregables.AsNoTracking()
            .Where(x => x.ExpedienteId == req.ExpedienteId && x.TramiteIndex == req.TramiteIndex)
            .ToListAsync(ct);
        var lugares = await db.TramiteLugares.AsNoTracking()
            .Where(x => x.ExpedienteId == req.ExpedienteId && x.TramiteIndex == req.TramiteIndex)
            .ToListAsync(ct);

        ficha.Requisitos  = PromocionMapeo.Requisitos(requisitos);
        ficha.Entregables = PromocionMapeo.Entregables(entregables);
        ficha.LugaresAtencion = PromocionMapeo.Lugares(lugares);

        db.TramitesSiger.Add(ficha);
        tramite.TramiteSigerId = ficha.Id;   // ver nota abajo

        await db.SaveChangesAsync(ct);

        var faltantes = FichaPublicaCompletitud.CamposFaltantes(
            ficha.CategoriaId, ficha.Modalidad, ficha.TiempoTexto,
            ficha.CostoEsGratuito, ficha.EstaEnSol, ficha.SolUrl);

        return new JsonResult(new
        {
            sigerId = ficha.Id,
            codigo  = ficha.Codigo,
            faltantes,
            aviso = FichaPublicaCompletitud.Frase(faltantes)
        });
    }
```

> **Cuidado con el orden del Id.** `ficha.Id` vale 0 hasta que se guarde. Hay dos
> formas de resolverlo y las dos son correctas; elegir una:
> **(a)** usar la propiedad de navegación si `ExpedienteTramite` la tiene, para que
> EF resuelva la clave sola; **(b)** hacer dos `SaveChangesAsync` seguidos — el
> primero inserta la ficha y le asigna Id, el segundo escribe el enlace. La opción
> (b) rompe la atomicidad, así que si se elige hay que envolverla con
> `db.Database.CreateExecutionStrategy().ExecuteAsync(...)`, que **sí** es
> compatible con `EnableRetryOnFailure`, a diferencia de un `BeginTransaction`
> suelto. Preferir (a).

- [ ] **Paso 3b: Reintentar si el código chocó**

`Codigo` es único. Dos analistas promoviendo trámites de la misma institución al
mismo tiempo pueden calcular el mismo correlativo, porque entre el `SELECT` de los
códigos y el `INSERT` no hay nada que los separe. Envolver el guardado:

```csharp
        // Hasta tres intentos: entre leer los códigos existentes y guardar, otro usuario pudo
        // haber tomado el mismo correlativo. Recalcular y reintentar es más simple que
        // serializar la generación, y el choque es raro.
        for (var intento = 0; ; intento++)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException) when (intento < 2)
            {
                var tomados = await db.TramitesSiger.AsNoTracking()
                    .Where(f => f.InstitucionId == expediente.InstitucionId)
                    .Select(f => f.Codigo).ToListAsync(ct);
                ficha.Codigo = CodigoPromovido.Siguiente(prefijo, tomados);
            }
        }
```

Si al tercer intento sigue fallando, la excepción sale y el analista ve el error:
tres choques seguidos no son contención, son un defecto.

Agregar los `using`:

```csharp
using Diger.TramitesEstado.Application.Siger.Promocion;
using Diger.TramitesEstado.Application.Siger.Publico;
```

- [ ] **Paso 4: Correr las pruebas**

```powershell
dotnet test tests\Web.Tests --filter "FullyQualifiedName~PromoverASiger"
```

Esperado: 3 en verde.

- [ ] **Paso 5: Correr todo**

```powershell
dotnet test Diger.TramitesEstado.sln
```

- [ ] **Paso 6: Comprometer**

```powershell
git add src\Web tests\Web.Tests
git commit -m "Agregar el handler que promueve un tramite del expediente a SIGER"
```

---

## Tarea 12: El botón y el diálogo de promoción

**Archivos:**
- Modificar: `src/Web/Pages/Expedientes/Editor.cshtml` (diálogo)
- Modificar: `src/Web/wwwroot/js/expediente.js` (botón, llamada, refresco)

**Interfaces:**
- Consume: `POST ?handler=PromoverASiger` de la tarea 11.
- Produce: nada que otras tareas consuman.

- [ ] **Paso 1: Agregar el botón a la ficha del trámite**

En `renderFichasPanels` de `expediente.js`, en la cabecera de cada panel:

```javascript
  + (_sigerIds[i]
      ? '<a class="btn-sec" href="/Siger/Detalle?id='+_sigerIds[i]+'" target="_blank">Ver ficha en SIGER</a>'
      : '<button type="button" class="btn-sec" onclick="promoverASiger('+i+')">Promover a SIGER</button>')
```

- [ ] **Paso 2: Escribir la función**

```javascript
// ── PROMOVER A SIGER ──────────────────────────────────────────
// Guarda primero y promueve después. El editor es un formulario con estado sin guardar; si
// promoviera desde la pantalla, copiaría datos que todavía no existen en la base.
async function promoverASiger(i){
  if(!expedienteId){
    alert('Guarde el expediente antes de promover un trámite.');
    return;
  }
  if(!confirm('Se creará una ficha en SIGER con los datos de este trámite.\n\n'
            + 'La ficha nace sin publicar: alguien tiene que aprobarla después.\n\n'
            + '¿Continuar?')) return;

  await guardarExpediente();          // el guardado que ya existe

  var resp = await fetch('?handler=PromoverASiger', {
    method: 'POST',
    headers: {'Content-Type':'application/json', 'RequestVerificationToken': tokenAntiforgery()},
    body: JSON.stringify({ expedienteId: expedienteId, tramiteIndex: i })
  });

  if(!resp.ok){
    var err = await resp.json().catch(function(){ return {}; });
    alert(err.error || 'No se pudo promover el trámite.');
    return;
  }

  var r = await resp.json();

  // Sin esto, el siguiente guardado manda tramite_siger_id vacío y borra el enlace
  // recién creado: la ficha queda huérfana en SIGER.
  _sigerIds[i] = r.sigerId;
  actualizarBadgesSiger();
  renderFichasPanels();

  alert('Ficha creada con el código ' + r.codigo + '.\n\n' + r.aviso);
}
```

> **Verificar antes de escribir:** los nombres `guardarExpediente`,
> `expedienteId` y `tokenAntiforgery` son los que este plan asume. Buscar los
> reales y usar esos:
> ```powershell
> Select-String -Path src\Web\wwwroot\js\expediente.js -Pattern "function guardar|__EXPMETA__|RequestVerificationToken"
> ```

- [ ] **Paso 3: Probar a mano**

```powershell
dotnet run --project src\Web
```

Sobre un expediente guardado: promover un trámite, comprobar que sale el código,
que la insignia aparece, y —lo que de verdad importa— **guardar el expediente otra
vez y recargar**: el enlace tiene que seguir ahí. Si se pierde, el paso 2 no
actualizó `_sigerIds`.

Después abrir `/Siger/Detalle?id=<el que devolvió>` y confirmar que la ficha está,
sin publicar, con sus requisitos.

- [ ] **Paso 4: Comprometer**

```powershell
git add src\Web
git commit -m "Agregar el boton de promover a SIGER en la ficha del tramite"
```

---

# Fase 4 — Actualizar

## Tarea 13: Calcular qué cambiaría

**Archivos:**
- Crear: `src/Application/Siger/Promocion/DiferenciaFicha.cs`
- Probar: `tests/Application.Tests/Siger/Promocion/DiferenciaFichaTests.cs`

**Interfaces:**
- Produce: `DiferenciaFicha.Calcular(TramiteSiger actual, TramiteSiger propuesta, int reqActuales, int reqPropuestos, int entActuales, int entPropuestos, int lugActuales, int lugPropuestos) -> IReadOnlyList<CambioFicha>`
  y `record CambioFicha(string Campo, string? Antes, string? Despues)`. Lo usa la tarea 14.

- [ ] **Paso 1: Escribir la prueba que falla**

`tests/Application.Tests/Siger/Promocion/DiferenciaFichaTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Siger.Promocion;
using Diger.TramitesEstado.Application.Siger.Publico;
using Diger.TramitesEstado.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Diger.TramitesEstado.Application.Tests.Siger.Promocion;

/// <summary>
/// El aviso que ve el analista antes de aplicar una actualización. Solo debe listar lo que de
/// verdad cambia: un diálogo que enumera diez campos idénticos entrena a la gente a pulsar
/// «aceptar» sin leer, que es justo lo que este aviso existe para evitar.
/// </summary>
public sealed class DiferenciaFichaTests
{
    [Fact]
    public void Sin_cambios_la_lista_queda_vacia()
    {
        var a = Ficha();
        var b = Ficha();

        DiferenciaFicha.Calcular(a, b, 3, 3, 1, 1, 1, 1).Should().BeEmpty();
    }

    [Fact]
    public void Reporta_el_campo_que_cambio_con_su_antes_y_despues()
    {
        var actual = Ficha();
        var propuesta = Ficha();
        propuesta.Modalidad = ModalidadPublica.Hibrido;

        DiferenciaFicha.Calcular(actual, propuesta, 3, 3, 1, 1, 1, 1)
            .Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new CambioFicha("Modalidad", ModalidadPublica.Virtual, ModalidadPublica.Hibrido));
    }

    [Fact]
    public void Reporta_el_conteo_de_las_colecciones_que_cambian()
    {
        var cambios = DiferenciaFicha.Calcular(Ficha(), Ficha(), 3, 5, 1, 1, 1, 2);

        cambios.Should().HaveCount(2);
        cambios.Should().ContainEquivalentOf(new CambioFicha("Requisitos", "3", "5"));
        cambios.Should().ContainEquivalentOf(new CambioFicha("Lugares de atención", "1", "2"));
    }

    [Fact]
    public void Un_campo_que_pasa_a_vacio_se_reporta_como_tal()
    {
        var propuesta = Ficha();
        propuesta.TiempoTexto = null;

        DiferenciaFicha.Calcular(Ficha(), propuesta, 3, 3, 1, 1, 1, 1)
            .Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new CambioFicha("Tiempo", "5 días hábiles", null));
    }

    private static TramiteSiger Ficha() => new()
    {
        Nombre = "Permiso de importación",
        Dependencia = "Operaciones",
        Descripcion = "Permite ingresar mercadería",
        Objetivo = "Autorizar la importación",
        DirigidoA = "Importadores",
        EnlacePrincipal = "https://aduanas.gob.hn",
        CategoriaId = 3,
        Modalidad = ModalidadPublica.Virtual,
        TiempoTexto = "5 días hábiles",
        CostoEsGratuito = false,
        CostoTexto = "L. 250.00",
        Institucion = "Aduanas"
    };
}
```

- [ ] **Paso 2: Correr la prueba y confirmar que falla**

```powershell
dotnet test tests\Application.Tests --filter "FullyQualifiedName~DiferenciaFicha"
```

- [ ] **Paso 3: Escribir la implementación**

`src/Application/Siger/Promocion/DiferenciaFicha.cs`:

```csharp
using Diger.TramitesEstado.Domain.Entities;

namespace Diger.TramitesEstado.Application.Siger.Promocion;

/// <summary>Un campo que cambiaría al actualizar. <c>null</c> significa vacío, no «sin cambio».</summary>
public sealed record CambioFicha(string Campo, string? Antes, string? Despues);

/// <summary>
/// Compara la ficha que hay con la que saldría del expediente, y devuelve solo las diferencias.
/// </summary>
/// <remarks>
/// Los nombres de campo son los que el analista ve en pantalla, no los de las columnas: quien
/// lee el aviso va a buscar el campo en la ficha, no en la base. Es el mismo criterio que usa
/// <c>FichaPublicaCompletitud</c>.
/// </remarks>
public static class DiferenciaFicha
{
    public static IReadOnlyList<CambioFicha> Calcular(
        TramiteSiger actual, TramiteSiger propuesta,
        int reqActuales, int reqPropuestos,
        int entActuales, int entPropuestos,
        int lugActuales, int lugPropuestos)
    {
        var cambios = new List<CambioFicha>();

        void Comparar(string campo, string? antes, string? despues)
        {
            if (!string.Equals(antes, despues, StringComparison.Ordinal))
                cambios.Add(new CambioFicha(campo, antes, despues));
        }

        Comparar("Nombre",           actual.Nombre,          propuesta.Nombre);
        Comparar("Dependencia",      actual.Dependencia,     propuesta.Dependencia);
        Comparar("Descripción",      actual.Descripcion,     propuesta.Descripcion);
        Comparar("Objetivo",         actual.Objetivo,        propuesta.Objetivo);
        Comparar("Dirigido a",       actual.DirigidoA,       propuesta.DirigidoA);
        Comparar("Enlace principal", actual.EnlacePrincipal, propuesta.EnlacePrincipal);
        Comparar("Categoría",        actual.CategoriaId?.ToString(), propuesta.CategoriaId?.ToString());
        Comparar("Modalidad",        actual.Modalidad,       propuesta.Modalidad);
        Comparar("Tiempo",           actual.TiempoTexto,     propuesta.TiempoTexto);
        Comparar("Costo",            TextoCosto(actual),     TextoCosto(propuesta));

        Comparar("Requisitos",          Conteo(reqActuales), Conteo(reqPropuestos));
        Comparar("Entregables",         Conteo(entActuales), Conteo(entPropuestos));
        Comparar("Lugares de atención", Conteo(lugActuales), Conteo(lugPropuestos));

        return cambios;
    }

    private static string Conteo(int n) => n.ToString();

    private static string? TextoCosto(TramiteSiger f) => f.CostoEsGratuito switch
    {
        true  => "Gratuito",
        false => f.CostoTexto ?? "Tiene costo",
        null  => null
    };
}
```

- [ ] **Paso 4: Correr la prueba y confirmar que pasa**

```powershell
dotnet test tests\Application.Tests --filter "FullyQualifiedName~DiferenciaFicha"
```

Esperado: 4 en verde.

- [ ] **Paso 5: Comprometer**

```powershell
git add src\Application\Siger\Promocion\DiferenciaFicha.cs tests\Application.Tests\Siger\Promocion\DiferenciaFichaTests.cs
git commit -m "Calcular que cambiaria al actualizar una ficha desde el expediente"
```

---

## Tarea 14: Los handlers de vista previa y aplicación

**Archivos:**
- Modificar: `src/Web/Pages/Expedientes/Editor.cshtml.cs`
- Probar: `tests/Web.Tests/ActualizarFichaSigerTests.cs`

**Interfaces:**
- Produce:
  - `GET ?handler=DiferenciaSiger&expedienteId=&tramiteIndex=` → `{ cambios: [{campo, antes, despues}] }`
  - `POST ?handler=ActualizarSiger` con `{ expedienteId, tramiteIndex }` → `{ aplicados }`
  - Lo usa la tarea 15.

- [ ] **Paso 1: Escribir la prueba que falla**

`tests/Web.Tests/ActualizarFichaSigerTests.cs`:

```csharp
using Diger.TramitesEstado.Application.Siger.Promocion;
using Diger.TramitesEstado.Domain.Entities;
using Diger.TramitesEstado.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diger.TramitesEstado.Web.Tests;

/// <summary>
/// Actualizar reescribe lo que el expediente manda. La prueba que de verdad importa es la
/// segunda: una ficha ya publicada no puede salirse del portal porque alguien pulsó
/// «actualizar» — eso sería un cambio invisible en la cara pública del Estado.
/// </summary>
public sealed class ActualizarFichaSigerTests(PortalFactory factory) : IClassFixture<PortalFactory>
{
    [Fact]
    public async Task Actualizar_reescribe_lo_que_manda_el_expediente()
    {
        var (expedienteId, sigerId) = await SembrarPromovido(factory);
        await CambiarNombreDelTramite(factory, expedienteId, "Permiso de importación (corregido)");

        var cliente = factory.CreateClient();
        var resp = await cliente.PostAsJsonAsync("/Expedientes/Editor?handler=ActualizarSiger",
            new { expedienteId, tramiteIndex = 0 });
        resp.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.TramitesSiger.SingleAsync(f => f.Id == sigerId)).Nombre
            .Should().Be("Permiso de importación (corregido)");
    }

    [Fact]
    public async Task Actualizar_no_saca_del_portal_una_ficha_publicada()
    {
        var (expedienteId, sigerId) = await SembrarPromovido(factory);
        await AprobarYDestacar(factory, sigerId);
        await CambiarNombreDelTramite(factory, expedienteId, "Otro nombre");

        var cliente = factory.CreateClient();
        await cliente.PostAsJsonAsync("/Expedientes/Editor?handler=ActualizarSiger",
            new { expedienteId, tramiteIndex = 0 });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ficha = await db.TramitesSiger.SingleAsync(f => f.Id == sigerId);

        ficha.Nombre.Should().Be("Otro nombre");
        ficha.Publicado.Should().BeTrue();
        ficha.EstadoSiger.Should().Be(ReglaPublicacion.Aprobado);
        ficha.EsPopular.Should().BeTrue();
        ficha.SolUrl.Should().Be("https://sol.gob.hn/permiso");
    }

    [Fact]
    public async Task La_vista_previa_lista_solo_lo_que_cambia()
    {
        var (expedienteId, _) = await SembrarPromovido(factory);
        await CambiarNombreDelTramite(factory, expedienteId, "Permiso corregido");

        var cliente = factory.CreateClient();
        var resp = await cliente.GetFromJsonAsync<RespuestaDiferencia>(
            $"/Expedientes/Editor?handler=DiferenciaSiger&expedienteId={expedienteId}&tramiteIndex=0");

        resp!.Cambios.Should().ContainSingle()
            .Which.Campo.Should().Be("Nombre");
    }

    [Fact]
    public async Task Un_tramite_sin_promover_no_se_puede_actualizar()
    {
        var (expedienteId, _) = await SembrarSinPromover(factory);

        var cliente = factory.CreateClient();
        var resp = await cliente.PostAsJsonAsync("/Expedientes/Editor?handler=ActualizarSiger",
            new { expedienteId, tramiteIndex = 0 });

        resp.IsSuccessStatusCode.Should().BeFalse();
    }

    private sealed record RespuestaDiferencia(List<CambioFicha> Cambios);

    /// <summary>Siembra un expediente con un trámite y lo promueve. Devuelve los dos ids.</summary>
    private static async Task<(int ExpedienteId, int SigerId)> SembrarPromovido(PortalFactory f)
    {
        var (expedienteId, _) = await SembrarSinPromover(f);

        var cliente = f.CreateClient();
        var resp = await cliente.PostAsJsonAsync("/Expedientes/Editor?handler=PromoverASiger",
            new { expedienteId, tramiteIndex = 0 });
        resp.EnsureSuccessStatusCode();

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tramite = await db.Tramites.SingleAsync(
            t => t.ExpedienteId == expedienteId && t.TramiteIndex == 0);

        return (expedienteId, tramite.TramiteSigerId!.Value);
    }

    private static async Task<(int ExpedienteId, int SigerId)> SembrarSinPromover(PortalFactory f)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Código único por siembra: estas pruebas comparten la base del fixture.
        var codigo = $"EXP-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";
        var e = Expediente.Crear(codigo, "ADUANAS", null, null, "Aduanas", "Analista");
        e.Agregar(new ExpedienteTramite
        {
            TramiteIndex = 0, NombreTramite = "Permiso de importación",
            FechaCreacion = DateOnly.FromDateTime(DateTime.Today)
        });
        e.Agregar(new TramiteRequisito { TramiteIndex = 0, Orden = 0, Requisito = "Solicitud firmada" });

        db.Expedientes.Add(e);
        await db.SaveChangesAsync();
        return (e.Id, 0);
    }

    private static async Task CambiarNombreDelTramite(PortalFactory f, int expedienteId, string nombre)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t = await db.Tramites.SingleAsync(
            x => x.ExpedienteId == expedienteId && x.TramiteIndex == 0);
        t.NombreTramite = nombre;
        await db.SaveChangesAsync();
    }

    /// <summary>Pone la ficha en el estado que el expediente NO debe poder tocar.</summary>
    private static async Task AprobarYDestacar(PortalFactory f, int sigerId)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ficha = await db.TramitesSiger.SingleAsync(x => x.Id == sigerId);
        ficha.EstadoSiger = ReglaPublicacion.Aprobado;
        ficha.Publicado   = true;
        ficha.EsPopular   = true;
        ficha.EstaEnSol   = true;
        ficha.SolUrl      = "https://sol.gob.hn/permiso";
        await db.SaveChangesAsync();
    }
}
```

> **Nota:** `SembrarSinPromover` devuelve `(ExpedienteId, 0)` — el segundo valor es
> el `TramiteIndex`, no un id de SIGER. La prueba que lo usa solo necesita el
> primero. `PromoverASigerTests` tiene un sembrado casi idéntico: extraer el común a
> un helper compartido de `tests/Web.Tests/` en lugar de mantener dos copias.

- [ ] **Paso 2: Correr la prueba y confirmar que falla**

```powershell
dotnet test tests\Web.Tests --filter "FullyQualifiedName~ActualizarFichaSiger"
```

- [ ] **Paso 3: Escribir los dos handlers**

En `Editor.cshtml.cs`:

```csharp
    [Permission("Siger", AccionModulo.Editar, "Ver qué cambiaría al actualizar una ficha SIGER")]
    public async Task<IActionResult> OnGetDiferenciaSigerAsync(
        int expedienteId, int tramiteIndex, CancellationToken ct)
    {
        var ctx = await CargarParaActualizar(expedienteId, tramiteIndex, ct);
        if (ctx is null) return NotFound();

        var propuesta = new TramiteSiger
        {
            Codigo = ctx.Ficha.Codigo,
            Nombre = ctx.Tramite.NombreTramite,
            Institucion = ctx.Expediente.Institucion
        };
        PromocionMapeo.CamposDelExpediente(propuesta, ctx.Tramite, ctx.Expediente);

        var cambios = DiferenciaFicha.Calcular(
            ctx.Ficha, propuesta,
            ctx.Ficha.Requisitos.Count,       ctx.Requisitos.Count,
            ctx.Ficha.Entregables.Count,      ctx.Entregables.Count,
            ctx.Ficha.LugaresAtencion.Count,  ctx.Lugares.Count);

        return new JsonResult(new { cambios });
    }

    /// <summary>
    /// Reescribe la ficha con lo que manda el expediente.
    /// </summary>
    /// <remarks>
    /// Solo se tocan las columnas de <see cref="PromocionMapeo.CamposDelExpediente"/>. Estado,
    /// publicación, SOL, destacado y tareas de digitalización se quedan como estaban: son lo que
    /// SIGER sabe y el expediente no. Las colecciones se reemplazan en bloque, que es seguro
    /// porque ninguna pantalla del sistema las edita.
    /// </remarks>
    [Permission("Siger", AccionModulo.Editar, "Actualizar una ficha SIGER desde el expediente")]
    public async Task<IActionResult> OnPostActualizarSigerAsync(
        [FromBody] PromoverRequest req, CancellationToken ct)
    {
        var ctx = await CargarParaActualizar(req.ExpedienteId, req.TramiteIndex, ct);
        if (ctx is null) return NotFound();

        PromocionMapeo.CamposDelExpediente(ctx.Ficha, ctx.Tramite, ctx.Expediente);

        ctx.Ficha.Requisitos.Clear();
        foreach (var r in PromocionMapeo.Requisitos(ctx.Requisitos)) ctx.Ficha.Requisitos.Add(r);

        ctx.Ficha.Entregables.Clear();
        foreach (var x in PromocionMapeo.Entregables(ctx.Entregables)) ctx.Ficha.Entregables.Add(x);

        ctx.Ficha.LugaresAtencion.Clear();
        foreach (var x in PromocionMapeo.Lugares(ctx.Lugares)) ctx.Ficha.LugaresAtencion.Add(x);

        await db.SaveChangesAsync(ct);
        return new JsonResult(new { aplicados = true });
    }
```

y el ayudante privado que carga todo de una vez:

```csharp
    private sealed record ContextoActualizacion(
        Expediente Expediente, ExpedienteTramite Tramite, TramiteSiger Ficha,
        List<TramiteRequisito> Requisitos,
        List<ExpedienteTramiteEntregable> Entregables,
        List<ExpedienteTramiteLugar> Lugares);

    private async Task<ContextoActualizacion?> CargarParaActualizar(
        int expedienteId, int tramiteIndex, CancellationToken ct)
    {
        var expediente = await db.Expedientes.FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (expediente is null) return null;

        var tramite = await db.Tramites.FirstOrDefaultAsync(
            t => t.ExpedienteId == expedienteId && t.TramiteIndex == tramiteIndex, ct);
        if (tramite?.TramiteSigerId is null) return null;

        var ficha = await db.TramitesSiger
            .Include(f => f.Requisitos)
            .Include(f => f.Entregables)
            .Include(f => f.LugaresAtencion)
            .FirstOrDefaultAsync(f => f.Id == tramite.TramiteSigerId, ct);
        if (ficha is null) return null;

        return new ContextoActualizacion(expediente, tramite, ficha,
            await db.Requisitos.Where(r => r.ExpedienteId == expedienteId && r.TramiteIndex == tramiteIndex).ToListAsync(ct),
            await db.TramiteEntregables.Where(x => x.ExpedienteId == expedienteId && x.TramiteIndex == tramiteIndex).ToListAsync(ct),
            await db.TramiteLugares.Where(x => x.ExpedienteId == expedienteId && x.TramiteIndex == tramiteIndex).ToListAsync(ct));
    }
```

- [ ] **Paso 4: Correr las pruebas**

```powershell
dotnet test tests\Web.Tests --filter "FullyQualifiedName~ActualizarFichaSiger"
dotnet test Diger.TramitesEstado.sln
```

- [ ] **Paso 5: Comprometer**

```powershell
git add src\Web tests\Web.Tests
git commit -m "Agregar los handlers de vista previa y actualizacion de una ficha promovida"
```

---

## Tarea 15: El diálogo de actualización

**Archivos:**
- Modificar: `src/Web/wwwroot/js/expediente.js`
- Modificar: `src/Web/Pages/Expedientes/Editor.cshtml` (contenedor del diálogo)

- [ ] **Paso 1: Agregar el botón**

En el panel de un trámite ya promovido, junto a «Ver ficha en SIGER»:

```javascript
  + '<button type="button" class="btn-sec" onclick="actualizarFichaSiger('+i+')">Actualizar ficha</button>'
```

- [ ] **Paso 2: Escribir la función**

```javascript
// ── ACTUALIZAR LA FICHA SIGER ────────────────────────────────
// Muestra qué va a cambiar antes de aplicar. Se cambia igual — el expediente manda — pero
// nadie pierde una corrección sin darse cuenta de que la perdió.
async function actualizarFichaSiger(i){
  await guardarExpediente();

  var resp = await fetch('?handler=DiferenciaSiger&expedienteId='+expedienteId+'&tramiteIndex='+i);
  if(!resp.ok){ alert('No se pudo comparar con la ficha.'); return; }

  var cambios = (await resp.json()).cambios || [];
  if(cambios.length === 0){
    alert('La ficha ya coincide con el expediente. No hay nada que actualizar.');
    return;
  }

  var texto = cambios.map(function(c){
    return '• ' + c.campo + ': ' + (c.antes || '(vacío)') + ' → ' + (c.despues || '(vacío)');
  }).join('\n');

  if(!confirm('Esto va a cambiar en la ficha de SIGER:\n\n' + texto
            + '\n\nEl estado de publicación no cambia. ¿Aplicar?')) return;

  var aplicar = await fetch('?handler=ActualizarSiger', {
    method: 'POST',
    headers: {'Content-Type':'application/json', 'RequestVerificationToken': tokenAntiforgery()},
    body: JSON.stringify({ expedienteId: expedienteId, tramiteIndex: i })
  });

  alert(aplicar.ok ? 'Ficha actualizada.' : 'No se pudo actualizar la ficha.');
}
```

- [ ] **Paso 3: Probar a mano**

```powershell
dotnet run --project src\Web
```

Promover un trámite, cambiarle el nombre y un requisito, pulsar «Actualizar ficha»
y comprobar que el diálogo lista exactamente esos dos cambios y ninguno más.
Después aprobar la ficha desde `/Siger/Editor`, volver al expediente, cambiar algo
y actualizar: la ficha tiene que seguir aprobada.

- [ ] **Paso 4: Comprometer**

```powershell
git add src\Web
git commit -m "Agregar el dialogo que muestra los cambios antes de actualizar la ficha"
```

---

# Fase 5 — Que se vea dónde está

## Tarea 16: Insignias que distinguen y llevan a la ficha

**Archivos:**
- Modificar: `src/Web/Pages/Expedientes/Editor.cshtml.cs` (publicar qué fichas son promovidas)
- Modificar: `src/Web/wwwroot/js/expediente.js` (`actualizarBadgesSiger`, `tramRowHTML`)

**Interfaces:**
- Consume: `TramiteSiger.IdSiger` nulo = ficha promovida (tarea 4).

- [ ] **Paso 1: Publicar el dato a la vista**

En `Editor.cshtml.cs`, al cargar el expediente, resolver para cada trámite enlazado
si su ficha nació aquí, y dejarlo en el `__EXPMETA__` como un diccionario
`sigerPromovido: { "<sigerId>": true }`:

```csharp
        var sigerIds = expediente.Tramites
            .Where(t => t.TramiteSigerId is not null)
            .Select(t => t.TramiteSigerId!.Value).ToList();

        SigerPromovido = await db.TramitesSiger.AsNoTracking()
            .Where(f => sigerIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.IdSiger == null, ct);
```

- [ ] **Paso 2: Distinguir las dos insignias**

En `actualizarBadgesSiger` y en `tramRowHTML` de `expediente.js`, sustituir la
insignia única por las dos, envueltas en un enlace:

```javascript
// Azul: el trámite se trajo del inventario. Verde: la ficha la creamos nosotros desde aquí.
// La diferencia sale de IdSiger — vacío significa que no existe en SIGER.
function badgeSigerHTML(sigerId){
  if(!sigerId) return '';
  var promovido = !!(window.__EXPMETA__ && window.__EXPMETA__.sigerPromovido
                     && window.__EXPMETA__.sigerPromovido[sigerId]);
  var estilo = promovido
    ? 'background:#dcfce7;color:#166534'
    : 'background:#dbeafe;color:#1455a4';
  return ' <a class="siger-badge" href="/Siger/Detalle?id='+sigerId+'" target="_blank"'
       + ' title="' + (promovido ? 'Ficha creada desde este expediente' : 'Trámite importado del inventario SIGER') + '"'
       + ' style="font-size:10px;font-weight:700;'+estilo+';padding:1px 6px;border-radius:4px;'
       + 'vertical-align:middle;margin-left:4px;text-decoration:none">'
       + (promovido ? 'EN SIGER' : 'SIGER') + '</a>';
}
```

y usarla en los dos sitios donde hoy se arma la insignia a mano.

- [ ] **Paso 3: Probar a mano**

Abrir un expediente que tenga un trámite importado de SIGER y otro promovido:
tienen que verse de distinto color, con distinto texto, y los dos llevar a su
ficha.

- [ ] **Paso 4: Comprometer**

```powershell
git add src\Web
git commit -m "Distinguir en el expediente la ficha importada de la creada desde aqui"
```

---

## Tarea 17: La marca en el lado de SIGER

**Archivos:**
- Modificar: `src/Web/Pages/Siger/Detalle.cshtml` (aviso de ficha promovida)
- Modificar: `src/Web/Pages/Siger/Index.cshtml(.cs)` (marca y filtro en el inventario)

- [ ] **Paso 1: El aviso en el detalle**

`Siger/Detalle.cshtml.cs` ya carga `ExpedientesVinculados`. En `Detalle.cshtml`,
arriba de la ficha:

```razor
@if (Model.Tramite.IdSiger is null)
{
    <div class="aviso-info">
        <strong>Ficha creada desde un expediente.</strong>
        Este trámite no existe en el inventario SIGER: lo levantó DIGER y se promovió desde
        @if (Model.ExpedientesVinculados.Count > 0)
        {
            var v = Model.ExpedientesVinculados[0];
            <a href="/Expedientes/Editor?id=@v.ExpedienteId">el expediente @v.Codigo</a>
        }
        else
        {
            <text>un expediente</text>
        }.
        Su contenido se mantiene desde ahí; aquí se decide la categoría y la publicación.
    </div>
}
```

Revisar el nombre real de las propiedades de `ExpedienteVinculadoRow` antes de
escribirlo:

```powershell
Select-String -Path src\Web\Pages\Siger\Detalle.cshtml.cs -Pattern "record ExpedienteVinculadoRow"
```

- [ ] **Paso 2: La sección vacía de tareas de digitalización**

En la misma vista, donde se listan las tareas, cuando la ficha es promovida y no
tiene ninguna:

```razor
@if (Model.Tramite.IdSiger is null && Model.Tramite.TareasDigitalizacion.Count == 0)
{
    <p class="nota-vacia">
        Las tareas de digitalización son el plan interno de DIGER y no se copian desde el
        expediente: en una ficha promovida, el expediente <em>es</em> ese plan.
    </p>
}
```

- [ ] **Paso 3: La marca en el inventario**

En `Siger/Index.cshtml`, en la fila de cada trámite, junto al código:

```razor
@if (t.IdSiger is null)
{
    <span class="badge-promovida" title="Ficha creada desde un expediente">PORTAL</span>
}
```

Y en `Index.cshtml.cs`, agregar `IdSiger` a la proyección de `TramiteSigerRow` si
no está, más una casilla de filtro «Solo fichas creadas desde expedientes» que
aplique `.Where(t => t.IdSiger == null)`, siguiendo la forma de los filtros que ya
tiene la página.

- [ ] **Paso 4: Probar a mano y correr todo**

```powershell
dotnet build Diger.TramitesEstado.sln
dotnet test Diger.TramitesEstado.sln
dotnet run --project src\Web
```

Abrir `/Siger` y comprobar la marca y el filtro; abrir el detalle de una ficha
promovida y comprobar los dos avisos.

- [ ] **Paso 5: Comprometer**

```powershell
git add src\Web
git commit -m "Marcar en SIGER las fichas creadas desde un expediente"
```

---

## Cierre

- [ ] **Verificación final**

```powershell
dotnet build Diger.TramitesEstado.sln
dotnet test Diger.TramitesEstado.sln
```

Esperado: compilación limpia y **todas** las pruebas en verde — las 76 que había
más las ~40 nuevas.

- [ ] **Verificación de datos contra la base de ensayo**

```powershell
sqlcmd -S 'LP-GD-JAGM\SQLEXPRESS' -U sa -P admin123 -C -d TramitesEstado_Ensayo -i scripts\sql\20-verificar-modalidades.sql
sqlcmd -S 'LP-GD-JAGM\SQLEXPRESS' -U sa -P admin123 -C -d TramitesEstado_Ensayo -i scripts\sql\21-verificar-siembra.sql
```

- [ ] **Prueba de extremo a extremo, a mano**

Con la app corriendo: crear un expediente nuevo, llenar un trámite completo
(categoría, modalidad, costo, un requisito, un entregable, un lugar), promoverlo,
comprobar que el aviso dice que la ficha está completa, aprobarla desde
`/Siger/Editor`, y confirmar que aparece en `GET /api/v1/tramites`.

Ese último paso es el que cierra el círculo: es el trabajo del expediente llegando
al ciudadano, que era el problema con el que empezó todo esto.
