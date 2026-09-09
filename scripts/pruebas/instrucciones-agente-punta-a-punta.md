# Encargo: correr el plan de pruebas de punta a punta

Corré el recorrido de 22 pasos que va de **GestionGD** (portal interno) hasta **Honduras
Simple** (portal ciudadano), pasando por la **API v1**. El documento del recorrido está en
`C:\Users\jgarcia\Documents\Portal-Informacion-Institucional\docs\flujo-de-pruebas-punta-a-punta.html`
— leelo, pero este archivo manda: acá está el **método** de cada paso, que el documento no
trae porque está escrito para una persona con un navegador.

Al final entregás una tabla de 22 filas y una ficha por cada falla. Nada más.

---

## 1 · Lo único que no podés hacer vos

**Entrar como el operador en GestionGD.** No tecleás contraseñas en formularios de acceso, y
esa regla no se negocia ni aunque el usuario te dé la contraseña.

Antes de empezar, pedile al usuario esto y esperá:

> Entre en `https://localhost:49175` como `operador.cowork@diger.gob.hn`, contraseña
> `Pruebas#2026`, y **deje la pestaña abierta**. Avíseme cuando esté.

Con esa pestaña abierta ya podés manejarla vos: pulsar, llenar los campos de una ficha,
publicar y despublicar **no son credenciales**, son usar una sesión que ya abrió otra persona.

Si en algún paso rebota por permisos, pedí el cambio a `admin.cowork@diger.gob.hn` — mismo
trato, lo abre el usuario.

**Lo que NO tenés que pedir:** la clave de la API. No hace falta el `Authorize` de Swagger:
todos los pasos de API los hacés por HTTP desde la terminal (§4).

---

## 2 · Reglas que no se negocian

1. **No tecleás contraseñas ni claves de API en ningún campo**, ni en Swagger ni en un login.
2. **Nunca publiques ni despubliques por SQL.** Tiene que ser por la pantalla de GestionGD.
   Un `UPDATE` directo **no toca `UpdatedAt`**, y `/api/v1/cambios` se apoya en esa marca: el
   cambio se volvería invisible para la sincronización y estarías probando otra cosa. Es
   justamente el punto ciego que el recorrido busca. SQL solo para **leer y comprobar**.
3. **Solo tocás las bases `_Sandbox`.** `DigerTramitesEstado_Unificada` y
   `VentanillaDigital_Net` son las de verdad; ni un `SELECT` hace falta contra ellas.
4. **No arregles nada.** Si algo falla, lo anotás con evidencia y seguís. No toques código, ni
   `appsettings.json`, ni migraciones. La causa no es tu trabajo acá.
5. **Al matar procesos, siempre por PID**, nunca por nombre de imagen. Un `taskkill /IM
   dotnet.exe` mata también lo que el usuario tenga abierto. Ya pasó una vez.
6. **No `git push`.** Si comprometés algo, commit y nada más.

---

## 3 · Levantar el entorno

Todo desde `C:\Users\jgarcia\Documents\Portal-Informacion-Institucional`.

```powershell
# 1 · El sandbox. SIN -Rehacer: así no pisa nada y no borra los usuarios de prueba.
& "C:\Users\jgarcia\Documents\Portal-Informacion-Institucional\scripts\pruebas\Refrescar-Sandbox.ps1"

# 2 · Los tres sistemas. Lanzalo con run_in_background: true en la herramienta.
& "C:\Users\jgarcia\Documents\Portal-Informacion-Institucional\scripts\Iniciar-Todo.ps1" -SinNavegador

# 3 · Esperar a que respondan.
$objetivo = @{49175='Portal interno'; 7199='API'; 7180='Portal ciudadano'}
foreach ($p in $objetivo.Keys) {
  $arriba = $false
  for ($i=0; $i -lt 90; $i++) {
    if (Test-NetConnection -ComputerName localhost -Port $p -InformationLevel Quiet -WarningAction SilentlyContinue) { $arriba=$true; break }
    Start-Sleep -Seconds 1
  }
  "$($objetivo[$p]) ($p) = $arriba tras $i s"
}
```

**Puertos.** Portal interno `49175` (https) · API `7199` https / **`5199` http** · Portal
ciudadano `7180` https / **`5180` http**.

**Usá siempre los puertos http (`5199`, `5180`) para las llamadas desde PowerShell.** Los
certificados son autofirmados y `Invoke-WebRequest` de PowerShell 5.1 no tiene
`-SkipCertificateCheck`. Por https vas a pelear con el certificado para nada.

### Los tres se caen solos, y sin avisar

**Ya me pasó en la preparación:** los tres estuvieron arriba varias llamadas seguidas y a la
siguiente los cinco puertos estaban muertos, sin que nadie los tocara. No lo diagnostiqués;
convivís con ello.

- Lanzá `Iniciar-Todo.ps1` con **`run_in_background: true`** en la herramienta.
- **Comprobá los tres puertos al empezar cada tramo** (A, B, C, …). Son dos segundos y te
  ahorra atribuirle a la aplicación una caída que fue del entorno.
- Si alguno se cayó, relanzalo y **anotá en qué paso pasó**. Un paso que falla con un sistema
  caído no es FALLA: es **NO PROBADO**, y hay que repetirlo. Confundir las dos cosas es la
  forma más fácil de reportar un fallo que no existe.

```powershell
function Vivos {
  $r = @{}
  foreach ($p in 49175,5199,5180) {
    $r[$p] = Test-NetConnection -ComputerName localhost -Port $p -InformationLevel Quiet -WarningAction SilentlyContinue
  }
  $r.GetEnumerator() | ForEach-Object { "$($_.Key) = $($_.Value)" }
}
```

Ojo: **relanzar no devuelve la sesión del operador.** La cookie sigue en el navegador y el
portal interno la vuelve a aceptar, pero si el usuario cerró la pestaña hay que pedirle que
entre otra vez.

---

## 4 · La clave de la API, sin verla ni escribirla

La clave que está escrita en `src/Api/appsettings.Development.json` —
`clave-de-pruebas-local` — **no sirve**: da 401. La efectiva vive en *user-secrets* y se carga
después, así que gana. Cargala en una variable y **no la imprimas nunca**:

```powershell
$s = "$env:APPDATA\Microsoft\UserSecrets\diger-tramites-estado-api\secrets.json"
$h = @{'X-Api-Key' = (Get-Content $s -Raw | ConvertFrom-Json).'PortalDigitalApi:ApiKey'}
```

Leé **ese archivo concreto**. Si intentás recorrer todos los `secrets.json` con un comodín, el
clasificador lo bloquea, y con razón.

Función que vas a usar todo el rato:

```powershell
function Est($codigo) {
  try { (Invoke-WebRequest "http://localhost:5199/api/v1/tramites/$codigo" -Headers $h -UseBasicParsing -TimeoutSec 15).StatusCode }
  catch { [int]$_.Exception.Response.StatusCode }
}
```

---

## 5 · Las cinco fichas del recorrido

Comprobadas en este estado el 8 de septiembre de 2026. **Si alguna no está así al empezar,
paralo y avisá**: alguien la movió y el recorrido ya no mide lo que dice medir.

| Código | Institución | Estado de partida | Para qué |
|---|---|---|---|
| `506-001` | CONSUCOOP | sin publicar · **le falta la modalidad** | pasos 4-14, 18, 19 |
| `123-008` | IHTT | sin publicar · **le falta el tiempo** | paso 15 |
| `506-008` | CONSUCOOP | **ya publicada** · sin costo capturado | paso 16 |
| `400-001` | ADUANAS | sin publicar · **fuera del piloto** | paso 17 |
| `603-001` | INPREMA | sin publicar | paso 21 |

Comprobación de un vistazo:

```powershell
sqlcmd -S "LP-GD-JAGM\SQLEXPRESS" -E -C -h -1 -W -d DigerTramitesEstado_Unificada_Sandbox -Q `
"SET NOCOUNT ON; SELECT Codigo + ' | ' + Sigla + ' | pub=' + CAST(Publicado AS varchar) + ' | mod=' + ISNULL(CAST(Modalidad AS varchar),'-') + ' | tiempo=' + ISNULL(TiempoTexto,'-') + ' | gratis=' + ISNULL(CAST(CostoEsGratuito AS varchar),'-') FROM TramitesSiger WHERE Codigo IN ('506-001','123-008','506-008','400-001','603-001') ORDER BY Codigo;"
```

**Nombres de columnas que se equivocan solos** (ya me pasó): la tabla es `TramitesSiger`; el
correo del usuario es `Correo`, no `Email`; el tiempo es `TiempoTexto`, no `TiempoEstimado`; en
el portal ciudadano la tabla es `PortalTramites`, no `Tramites`.

---

## 6 · El recorrido

Anotá para cada paso: **OK / FALLA / NO PROBADO**, y donde se pida, el número medido.

### A. Que la cadena esté de pie

**1 · Los tres arriba y sobre el sandbox.** Puertos arriba (§3). Después probá que la API lee
de verdad el sandbox: el total que devuelve tiene que coincidir con el conteo en la base.

```powershell
$api = ((Invoke-WebRequest "http://localhost:5199/api/v1/tramites?tamano=1" -Headers $h -UseBasicParsing).Content | ConvertFrom-Json).total
sqlcmd -S "LP-GD-JAGM\SQLEXPRESS" -E -C -h -1 -W -d DigerTramitesEstado_Unificada_Sandbox -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM TramitesSiger WHERE Publicado=1;"
"API dice: $api"
```
Y que el portal ciudadano diga que está en pruebas: `GET http://localhost:5180/` y buscá en el
HTML `Entorno de pruebas` y `VentanillaDigital_Net_Sandbox`.

**2 · `/api/v1/salud` sin clave → 200.** `Invoke-WebRequest "http://localhost:5199/api/v1/salud" -UseBasicParsing`, **sin** `$h`.

**3 · `/api/v1/tramites` sin clave → 401.** Igual, sin `$h`. Si devuelve la lista, es el fallo
más grave del recorrido.

### B. La ficha se prepara

**4 · Abrir `506-001` en GestionGD** (navegador, pestaña del operador) y comprobar que dice
que le falta la **modalidad**, y solo eso.

**5 · `Est '506-001'` → 404.** Es la foto de partida; sin ella el paso 9 no demuestra nada.

**6 · Ponerle la modalidad en el Editor** y guardar. Por la pantalla, **no por SQL**.
Comprobá después con la consulta de §5 que `mod` ya no es `-`.

**7 · Completitud sube en uno.** Contá antes y después:
```sql
SELECT COUNT(*) FROM TramitesSiger WHERE Sigla='CONSUCOOP'
  AND CategoriaId IS NOT NULL AND Modalidad IS NOT NULL
  AND TiempoTexto IS NOT NULL AND CostoEsGratuito IS NOT NULL;
```

### C. Publicar

**8 · Publicar `506-001`** desde *Honduras Simple → Publicado*. Por la pantalla.
**Guardá la hora exacta**: `$t0 = Get-Date`.

**9 · `Est '506-001'` → 200**, y los cuatro campos son los que escribiste.

**10 · `506-001` está en `/api/v1/codigos-publicados`.**

### D. Llega sola al ciudadano

**11 · Medí cuánto tarda en aparecer.** Acá sos mejor que una persona: sondeá cada 2 s.

```powershell
$t0 = Get-Date; $visto = $null
for ($i=0; $i -lt 300; $i++) {
  try { if ((Invoke-WebRequest "http://localhost:5180/Tramites/506-001" -UseBasicParsing -TimeoutSec 10).StatusCode -eq 200) { $visto = (Get-Date) - $t0; break } } catch {}
  Start-Sleep -Seconds 2
}
"aparecio tras: $($visto.TotalSeconds) s"
```
Esperado: **menos de 60 s**. Anotá el número aunque pase. Si a los 10 min no aparece, FALLA.

**12 · El ciudadano lee lo mismo.** Traé el detalle de la API y la página del portal, y
compará los seis: nombre, institución, categoría, modalidad, tiempo, costo. **Las tildes
cuentan.**

**13 · Sale en su institución y en su categoría.** `GET /Instituciones/...` y `/Categorias`, y
que el trámite esté en el HTML.

**14 · `http://localhost:5180/Tramites/506-001` → 200.**

### E. Lo que falta no se inventa

**15 · Publicar `123-008` sin completar.** Le falta el **tiempo**. Esperá a que llegue y
comprobá que donde iba el tiempo hay un **guion**, y que el nombre, la institución y el costo
siguen ahí.

**16 · `http://localhost:5180/Tramites/506-008`.** El costo tiene que salir como guion. Buscá
en el HTML y confirmá que **no** aparece `Gratuito`, `L 0.00` ni `Sin costo`. (Comprobado el
8-09-2026: sirve `Costo: -`.) Si aparece alguna, es grave.

### F. El corte del piloto

**17 · Publicar `400-001` (ADUANAS).** Esperá 2 min. Tienen que cumplirse **las dos mitades**:
`Est '400-001'` → **200**, y `http://localhost:5180/Tramites/400-001` → **404**. Si la API
tampoco lo sirve, el problema es otro.

### G. Quitar también viaja

**18 · Despublicar `506-001`** desde la pantalla. `$t1 = Get-Date`. Después: `Est '506-001'` →
**404**, y su código ya no está en `/codigos-publicados`.

**19 · Medí cuánto tarda en desaparecer del ciudadano.** Mismo bucle del paso 11, pero
esperando el 404. Anotá el número. Si a los 10 min sigue visible, FALLA.

> Nota: publicar y despublicar sellan `UpdatedAt`, así que los dos deberían viajar por el
> ciclo ligero (~10 s). Si la baja tarda ~5 min, la trajo la reconciliación del ciclo pesado
> y no el ligero — eso es un dato que vale la pena anotar, no un fallo por sí solo.

### H. Cuando la API se cae

**20 · Apagar la API — por PID, nunca por nombre.**

```powershell
$c = Get-NetTCPConnection -LocalPort 5199 -State Listen -ErrorAction SilentlyContinue
if (-not $c) { throw "nada escucha en 5199: la API ya estaba caida, no sigas" }
$procId = ($c.OwningProcess | Select-Object -Unique)
$nombre = (Get-Process -Id $procId).ProcessName
"PID $procId -> $nombre"
# Salvaguarda: solo se mata si es la API. Comprobado, se llama Diger.TramitesEstado.Api.
if ($nombre -ne 'Diger.TramitesEstado.Api') { throw "ese proceso no es la API: $nombre. No lo mates." }
Stop-Process -Id $procId -Force
```

**Nunca `Stop-Process -Name dotnet` ni `taskkill /IM`.** En esta máquina corre además el
portal interno, el portal ciudadano y a veces cosas del usuario. Matar por nombre se lleva
todo por delante — ya pasó una vez.

Comprobá que `http://localhost:5199/api/v1/salud` ya no responde. Después recorré el portal
ciudadano entero: `/`, `/Tramites`, una ficha, `/Instituciones`, `/Categorias`. **Los cinco
tienen que dar 200 con datos.** Es la prueba que más vale del recorrido.

**21 · Encender y comprobar que se recupera solo.**
```powershell
& "C:\Users\jgarcia\Documents\Portal-Informacion-Institucional\scripts\Iniciar-Todo.ps1" -Solo Api -SinNavegador
```
Cuando responda, publicá `603-001` (INPREMA) por la pantalla y medí con el bucle del paso 11.
Puede tardar más que el 11: tras una caída el enlace espera antes de volver a confiar. Dale
hasta 5 min.

### I. Los bordes

**22 · La modalidad.** `?modalidad=Virtual` trae virtuales **e** híbridos; `?modalidad=Hibrido`
solo híbridos; `?modalidad=Mixto` devuelve **200 con cero resultados**, no un error. Compará
los tres `total`.

---

## 7 · Cómo entregar

Primero la tabla de los 22, **con los que pasaron también**, y con los tiempos:

```
Paso 1   OK
Paso 11  OK   (apareció en 24 s)
Paso 19  OK   (desapareció en 3 min 10 s)
Paso 17  FALLA
Paso 20  NO PROBADO  (no pude matar el proceso)
```

Después, una ficha por cada FALLA:

```
PASO:       17
SISTEMA:    Portal ciudadano
CÓDIGO:     400-001
DIRECCIÓN:  http://localhost:5180/Tramites/400-001
ESPERABA:   404 — está fuera del corte del piloto.
PASÓ:       Devolvió 200 y la ficha se ve completa.
EVIDENCIA:  [el HTML relevante o el JSON de la API]
```

Y al final, **qué dejaste tocado**: qué fichas quedaron publicadas o despublicadas y cuáles no
volviste a dejar como estaban. No hace falta limpiar —el sandbox es desechable— pero sí hace
falta decirlo, porque la próxima corrida parte de ahí.

---

## 8 · Trampas que ya me tragué

- **La clave del repositorio no vale.** Da 401. La buena está en user-secrets (§4). Si te da
  401 en todo, no vayas a buscarla a `appsettings.Development.json`.
- **Solo hay seis fichas sin publicar** en las tres instituciones del piloto. Si te salís de
  las cinco de §5, es probable que te quedes sin material a mitad del recorrido.
- **Al ciudadano solo llegan INPREMA, IHTT y CONSUCOOP.** Publicar otra y no verla es lo
  correcto, no un fallo. Es el paso 17.
- **La primera sincronización tras `Refrescar-Sandbox.ps1` vacía la réplica** del portal
  ciudadano y la reconstruye desde la API. Dale un minuto antes de dar nada por perdido.
- **`Refrescar-Sandbox.ps1` sin `-Rehacer` no pisa nada.** Con `-Rehacer` sí, y **borra los
  usuarios de prueba**. No lo uses salvo que el usuario lo pida.
- **PowerShell 5.1**: no hay `&&`, ni `??`, ni ternario, ni `-SkipCertificateCheck`. Usá los
  puertos http.
- **Chrome puede reportar la ventana en 0×0** y entonces no se pueden tomar capturas. Si pasa,
  decilo y apoyate en el HTML y en los códigos de estado, que para casi todo alcanzan.
- **Los tres procesos se mueren solos entre llamadas.** Comprobá los puertos al empezar cada
  tramo. Un paso corrido contra un sistema caído es NO PROBADO, no FALLA.
- **`.cshtml` no se recarga en caliente.** Si alguien edita una vista, hay que reiniciar el
  sitio. No debería pasar durante una corrida, pero si ves algo imposible, es candidato.
