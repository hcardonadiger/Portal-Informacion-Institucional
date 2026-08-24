# Despliegue en IIS — PortalDigital, API v1 y HondurasÁgil

Los tres sistemas conviven en el mismo servidor, **cada uno en su propio sitio de IIS y su
propio grupo de aplicaciones**. Separados a propósito: la API es pública y el portal interno
no debe compartir proceso con ella. Si uno se recicla o se cae, los otros dos siguen en pie.

| Sistema | Proyecto | Marco | Sitio | Grupo de aplicaciones |
|---|---|---|---|---|
| PortalDigital (interno) | `src/Web` | .NET 9 | `DIGER - PortalDigital` | `DIGER_PortalDigital` |
| API pública v1 | `src/Presentation` | .NET 9 | `DIGER - API v1` | `DIGER_ApiPublica` |
| HondurasÁgil (ciudadano) | `src/Web` de `VentanillaDigital.Net` | .NET 10 | `DIGER - HondurasAgil` | `DIGER_HondurasAgil` |

---

## Antes de nada: lo que casi siempre se pasa por alto

**Que los 1.057 trámites ya estén en producción no significa que el esquema esté al día.**
Son cosas distintas, y confundirlas cuesta una tarde:

- **Los datos** viven en la base y **no viajan por git**.
- **El esquema** sí viaja por git, como migraciones de EF, y hay que **aplicarlo**.

En producción faltan —salvo que alguien ya las haya aplicado— las nueve columnas nuevas de
`TramitesSiger`, la tabla `CategoriasTramite` con sus ocho categorías, las cinco columnas de
contacto institucional y la corrección de colación que hace que la búsqueda ignore las
tildes. Sin eso, la API arranca y falla en la primera consulta.

El script lo hace por usted. Solo hay que saber que ese paso existe y por qué.

---

## Requisitos en el servidor

| Requisito | Por qué |
|---|---|
| IIS con herramientas de administración | El script usa el módulo `WebAdministration` |
| **Hosting Bundle de ASP.NET Core 9** | PortalDigital y la API |
| **Hosting Bundle de ASP.NET Core 10** | HondurasÁgil |
| SDK de .NET 9 y de .NET 10 | Para publicar. Si publica en otra máquina, en el servidor bastan los Hosting Bundle |
| PowerShell **elevado** | Crea sitios, grupos y permisos |
| Acceso a SQL Server | Con autenticación de Windows desde la cuenta que ejecuta |

**El orden importa:** si instala el Hosting Bundle *antes* que IIS, el módulo
`AspNetCoreModuleV2` no queda registrado y **todo responde 500.30** sin decir por qué. Si le
pasa, reinstale el Hosting Bundle después de IIS.

---

## Cómo se usa

### Primero, verificar sin tocar nada

```powershell
.\Desplegar.ps1 -SoloVerificar -ServidorSql 'SRV-SQL\INSTANCIA'
```

Revisa los requisitos uno por uno y dice exactamente qué falta. **No despliega.**
Ejecútelo así la primera vez, en el servidor real.

### Después, el despliegue

```powershell
.\Desplegar.ps1 `
    -ServidorSql     'SRV-SQL\INSTANCIA' `
    -ClaveApi        (Read-Host 'Clave de la API' -AsSecureString) `
    -RepoVentanilla  'C:\DIGER\Aplicativos\VentanillaDigital.Net' `
    -HostPortal      'tramites.diger.gob.hn' `
    -HostApi         'api.diger.gob.hn' `
    -HostVentanilla  'hondurasagil.gob.hn'
```

`-ClaveApi` se pide con `Read-Host -AsSecureString` a propósito: así **la clave no queda en
el historial de PowerShell**. No la escriba en la línea de comandos.

Genere una clave de verdad, larga y al azar:

```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Max 256 }))
```

---

## Qué hace el script, en orden

1. **Verifica** los requisitos. Si falta uno, se detiene **sin desplegar nada**.
2. **Publica** los tres proyectos a carpetas temporales. Si uno no compila, se entera antes
   de haber parado ningún sitio.
3. **Migra** las dos bases de datos.
4. **Crea o actualiza** los tres grupos, con «sin código administrado».
5. **Fija las variables de entorno**, que es donde viven los secretos.
6. **Copia los archivos**, deteniendo antes cada grupo para que no haya DLL en uso.
7. **Crea o actualiza** los tres sitios y concede permisos a cada identidad.
8. **Arranca** y hace **pruebas de humo**.

Es **idempotente**: puede volver a ejecutarlo cuantas veces quiera.

---

## Dónde viven los secretos, y por qué ahí

Ésta es la respuesta a **P-03**, abierta desde la Fase 0.

Los secretos van en **variables de entorno del grupo de aplicaciones de IIS**, no en
`appsettings.json`.

**El motivo no es teórico.** `appsettings.json` está versionado en git: un secreto que entra
ahí queda en la historia del repositorio para siempre. Cambiarlo después no lo borra —hay que
reescribir la historia o dar el secreto por comprometido— y quien tenga acceso de lectura al
repositorio ya lo tiene.

Las variables del grupo de aplicaciones, en cambio, no están en la carpeta de publicación,
así que no se van en un despliegue ni acaban en el repositorio, y solo las lee un
administrador del servidor.

El código no cambia: ya lee de `IConfiguration`, y las variables de entorno entran por ahí
solas. La equivalencia es el doble guion bajo:

| Configuración | Variable de entorno |
|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` |
| `PortalDigitalApi:ApiKey` | `PortalDigitalApi__ApiKey` |

Si prefiere fijarlas a mano, sin el script:

```powershell
%windir%\system32\inetsrv\appcmd.exe set config -section:system.applicationHost/applicationPools ^
  /+"[name='DIGER_ApiPublica'].environmentVariables.[name='PortalDigitalApi__ApiKey',value='<clave>']" ^
  /commit:apphost
```

### Y de paso, dos secretos que hay que rotar

En `src/Web/appsettings.Development.json`, versionado, están hoy en texto plano **la
contraseña de `sa`** de la base y **una `AnonKey` de Supabase**. Hay que **rotarlas, no solo
moverlas**: quien tenga acceso de lectura al repositorio ya las tiene.

---

## Si producción YA está funcionando: `Aplicar-CambiosProduccion.ps1`

`Desplegar.ps1` instala los tres sitios desde cero. Si producción ya funciona y solo
hay que llevarle los cambios de este trabajo, use el otro script — **y sabe deshacerlos**.

```powershell
# 1. Ver qué haría, sin tocar nada:
.\Aplicar-CambiosProduccion.ps1 -ServidorSql 'SRV-SQL' -SoloVerificar

# 2. Aplicar:
.\Aplicar-CambiosProduccion.ps1 -ServidorSql 'SRV-SQL' -Accion DO `
    -ClaveLecturaApi (Read-Host 'Clave del usuario de solo lectura' -AsSecureString) `
    -ClaveApi        (Read-Host 'Clave de la API'                   -AsSecureString)

# 3. Si algo va mal:
.\Aplicar-CambiosProduccion.ps1 -ServidorSql 'SRV-SQL' -Accion UNDO
```

### Qué hace

| Paso | DO | UNDO |
|---|---|---|
| 1 | Respalda las dos bases. Sin respaldo no sigue | Respalda otra vez: revertir también es un cambio |
| 2 | Crea el usuario de **solo lectura** de la API y comprueba que lee y no escribe | Quita las variables de sincronización |
| 3 | Crea las nueve tablas `Portal*` en HondurasÁgil | Borra esas nueve tablas |
| 4 | Fija las variables de entorno de IIS | Quita el usuario de solo lectura |
| 5 | Reinicia los grupos y comprueba que la API responde | Reinicia los grupos |

**Lo que no toca, ni en DO ni en UNDO:** ni una fila de los 1.057 trámites, ni las fechas de
revisión, ni los votos del ciudadano. Comprobado el 18-08-2026 con un ciclo completo
DO → UNDO → DO: los 15 trámites sembrados y los 23 votos siguieron intactos.

### El cambio de fondo: la API deja de escribir

Hasta ahora la API se conectaba **con las mismas credenciales que el portal interno**:
escritura sobre las 64 tablas, incluidas expedientes, reuniones, chat y permisos. No las
usaba, pero podía — y es la única cara expuesta a la red.

Ahora usa `api_portaldigital_lectura`, que tiene `SELECT` sobre **nueve tablas** y `DENY`
de escritura sobre todo el esquema. El script lo comprueba en cada ejecución: si el usuario
lograra escribir, aborta.

> **Salvedad honesta.** `DENY` no alcanza a `sysadmin` ni a `db_owner`. A quien tenga esos
> roles no lo detiene nada de esto. Lo que se acota es el daño de un fallo *en la API*.

### Tres cosas que aprendimos ejecutándolo, y que están dentro del script

**Respaldos comprimidos.** `WITH COMPRESSION` no existe en Express Edition: falla con
`Msg 1844` y tumba el respaldo entero. El script pregunta la edición antes.

**Quién escribe el respaldo.** No es su usuario, es la **cuenta del servicio de SQL Server**.
Un `Operating system error 5` sobre una carpeta suya es eso. Para saber cuál sirve:
`SELECT SERVERPROPERTY(N'InstanceDefaultBackupPath')`.

**`DROP LOGIN` con la API en marcha** falla con `Msg 15434` — pero el `DROP USER` previo sí
funciona, así que quedaba un login huérfano mientras el script anunciaba «revertido». Ahora
cierra las sesiones primero y **verifica** el resultado en vez de darlo por hecho.

> **Ojo con el UNDO entre entornos.** El login es del **servidor**, no de la base. Revertir
> en ensayo borra el login que también usa producción, y deja allí un usuario huérfano. Si
> tiene las dos bases en la misma instancia, vuelva a aplicar el DO sobre la otra.

### Dos trampas del login que conviene conocer antes de que le pasen

**El login es del servidor, no de la base.** Si ensayo y producción viven en la misma
instancia y comparten nombre de usuario, comparten contraseña: aplicar el script en un
entorno **deja al otro sin acceso**. Use un nombre por entorno:

```powershell
-NombreUsuario 'api_portaldigital_lectura_prod'
-NombreUsuario 'api_portaldigital_lectura_ensayo'
```

**Si la API deja de conectar de golpe, mire si está bloqueada antes que la contraseña.**
El login se crea con `CHECK_POLICY = ON`, que hereda la política de bloqueo de Windows: unos
pocos intentos fallidos —una cadena de conexión vieja en cualquier aplicación— cierran la
cuenta. El mensaje `Login failed for user` se lee como «contraseña mala» y no lo es.

```sql
SELECT name, LOGINPROPERTY(name,'IsLocked'), LOGINPROPERTY(name,'BadPasswordCount')
FROM   sys.sql_logins WHERE name LIKE 'api_portaldigital%';
```

Si `IsLocked` es 1:

```
SQLCMD -S <servidor> -E -b -I -f 65001 -d <base> ^
       -v Usuario="api_portaldigital_lectura" Clave="<la de siempre>" Accion="DESBLOQUEAR" ^
       -i scripts\sql\10-usuario-solo-lectura-api.sql
```

Y después busque **quién** está usando la clave vieja, o volverá a pasar mañana.


### Codificación: por qué estos scripts llevan BOM

Los `.ps1` se guardan en **UTF-8 con BOM**, y no es capricho. PowerShell 5.1 sin BOM lee el
archivo en ANSI, y un guión largo `—` se convierte en tres caracteres, el último de los
cuales es una comilla tipográfica de cierre… **que PowerShell acepta como delimitador de
cadena**. El resultado es un error de sintaxis en una línea que se ve perfectamente bien.

Los `.sql` van igual, y se ejecutan con `-f 65001`. Un BOM perdido *en medio* de un archivo
—por ejemplo, al anteponerle una cabecera a un script generado por EF Core— da
`Msg 102: Incorrect syntax near ''`, sin mencionar jamás la palabra BOM.


## Lo que el script NO hace, y hay que hacer a mano

Dependen de certificados y de decisiones que un script no puede tomar por usted.

### 1. Certificados y enlaces HTTPS

El script crea los sitios en HTTP. Los enlaces del 443 se añaden después, con el certificado
de cada nombre de host ya instalado:

```powershell
New-WebBinding -Name 'DIGER - API v1' -Protocol https -Port 443 `
               -HostHeader 'api.diger.gob.hn' -SslFlags 1
```

`-SslFlags 1` es **SNI**, imprescindible cuando varios sitios comparten el 443 — que es
exactamente este caso, con tres.

### 2. El puerto de autenticación por certificado de PortalDigital

PortalDigital tiene inicio de sesión con certificado digital, y eso necesita un enlace HTTPS
**aparte** que pida el certificado de cliente. No se puede mezclar con el enlace normal: si
el sitio principal pidiera certificado, todo el mundo vería el diálogo al entrar.

En desarrollo son dos puertos (49175 el normal, 49176 el de certificado). En producción hay
que replicar esa separación con dos enlaces, y activar la negociación de certificado de
cliente **solo en el segundo**.

### 3. Encender la sincronización de HondurasÁgil

HondurasÁgil trae el interruptor de origen **apagado**. Enciéndalo cuando la API sirva fichas
completas. **Antes no**: hoy `?soloFichasCompletas=true` devuelve muy pocas, y encenderlo
demasiado pronto deja el portal más vacío que ahora.

### 4. Un cortafuegos delante de la API

La API lleva clave y límite de peticiones, pero está pensada para **un solo consumidor
conocido**. Si HondurasÁgil vive en el mismo servidor, lo más sencillo y lo más seguro es no
publicar la API a Internet en absoluto y dejarla accesible solo desde el propio servidor.

---

## Comprobación después de desplegar

El script ya hace estas pruebas. Conviene saber qué significa cada una:

| Prueba | Debe dar | Si no |
|---|---|---|
| `GET /api/v1/salud` | **200** con `"baseDeDatos": true` | Si da **503** con `false`, el sitio vive pero no llega a la base |
| `GET /api/v1/tramites` **sin clave** | **401** | Si da 200, **la autenticación no está puesta**. Pare y revise |
| `GET /api/v1/tramites` con clave | **200** | |
| `GET /swagger/v1/swagger.json` | **404** | Si da 200, Swagger quedó publicado en producción |

---

## Si algo sale mal

| Síntoma | Causa casi segura |
|---|---|
| **500.30** en todo | Falta el Hosting Bundle, o el grupo no está en «sin código administrado», o se instaló el bundle antes que IIS |
| **500.19** | La identidad del grupo no puede leer la carpeta |
| La API arranca pero **todo da 401** | Falta `PortalDigitalApi__ApiKey`. Falla cerrado a propósito: sin clave en el servidor no entra nadie |
| **Error de columna inexistente** en la primera consulta | Las migraciones no se aplicaron. Es lo del principio: datos y esquema son cosas distintas |
| El registro no aparece en `logs\` | La identidad del grupo no tiene permiso de escritura ahí |

El registro de arranque de cada aplicación queda en `logs\` dentro de su propia carpeta de
publicación. Es lo primero que hay que mirar ante un 500.30: el mensaje que IIS enseña en el
navegador no dice nada útil.

---

## Dónde debe vivir este archivo

Estos dos archivos —`Desplegar.ps1` y `DESPLIEGUE.md`— pertenecen a
`scripts\` del repositorio `Portal-Informacion-Institucional`. Se generaron fuera por una
restricción de la herramienta que los escribió. Cópielos con:

```powershell
Copy-Item C:\Users\jgarcia\Documents\despliegue-diger\Desplegar.ps1, `
          C:\Users\jgarcia\Documents\despliegue-diger\DESPLIEGUE.md `
          C:\Users\jgarcia\Documents\Portal-Informacion-Institucional\scripts\
```
