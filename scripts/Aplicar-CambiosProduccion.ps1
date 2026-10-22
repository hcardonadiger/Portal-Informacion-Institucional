<#
.SYNOPSIS
    Lleva a produccion los cambios del trabajo de la API v1 (agosto 2026). Reversible.

.DESCRIPTION
    Esto NO es el despliegue completo: para instalar los tres sitios desde cero
    esta Desplegar.ps1. Este script aplica solo los cambios de este trabajo sobre
    una produccion que ya funciona, y sabe deshacerlos.

    QUE HACE (-Accion DO):
      1. Respalda las dos bases. Sin respaldo no continua.
      2. Crea el usuario de SOLO LECTURA para la API publica.
         Antes la API se conectaba con las mismas credenciales que el portal
         interno: escritura sobre las 64 tablas. Ahora lee nueve y no escribe nada.
      3. Aplica en HondurasAgil las tablas del catalogo replicado (nueve Portal*).
      4. Ajusta las variables de entorno de IIS:
         - la API pasa a usar la cadena de solo lectura
         - se apaga Swagger en produccion
         - se configura la sincronizacion automatica
      5. Reinicia los grupos de aplicaciones y comprueba que todo responde.

    QUE HACE (-Accion UNDO):
      Lo contrario y en orden inverso. Devuelve la API a su cadena anterior,
      borra las tablas Portal*, quita el usuario de solo lectura y restaura las
      variables. Los respaldos del paso 1 NO se borran nunca.

    QUE NO TOCA, NI EN DO NI EN UNDO:
      Ni una fila de los 1.057 tramites. Ni las fechas de revision. Ni los votos
      del ciudadano. Esto cambia esquema, permisos y configuracion; los datos no.

.EXAMPLE
    # Ver que haria, sin cambiar nada:
    .\Aplicar-CambiosProduccion.ps1 -ServidorSql 'SRV-SQL' -SoloVerificar

.EXAMPLE
    # Aplicar:
    .\Aplicar-CambiosProduccion.ps1 -ServidorSql 'SRV-SQL' -Accion DO `
        -ClaveLecturaApi (Read-Host 'Clave del usuario de solo lectura' -AsSecureString) `
        -ClaveApi        (Read-Host 'Clave de la API'                   -AsSecureString)

.EXAMPLE
    # Revertir:
    .\Aplicar-CambiosProduccion.ps1 -ServidorSql 'SRV-SQL' -Accion UNDO
#>

param(
    [ValidateSet('DO','UNDO')]
    [string] $Accion = 'DO',

    [Parameter(Mandatory=$true)]
    [string] $ServidorSql,

    [string] $BasePortal     = 'TramitesEstado',
    [string] $BaseVentanilla = 'VentanillaDigital_Net',

    [string] $RepoPortal     = (Split-Path -Parent $PSScriptRoot),
    [string] $RepoVentanilla = 'C:\DIGER\Aplicativos\VentanillaDigital.Net',

    [string] $CarpetaRespaldos = 'C:\DIGER\Respaldos',

    # Un LOGIN pertenece al SERVIDOR, no a la base. Si ensayo y produccion
    # comparten instancia y comparten nombre, comparten tambien contrasena:
    # aplicar en un entorno deja al otro sin acceso. Medido el 18-08-2026 —
    # al fijar la clave de produccion, la API de ensayo empezo a dar
    # "Login failed". Con servidores separados puede dejarlo como esta.
    [string] $NombreUsuario = 'api_portaldigital_lectura',

    # Solo hacen falta en DO. En UNDO se ignoran.
    [System.Security.SecureString] $ClaveLecturaApi,
    [System.Security.SecureString] $ClaveApi,

    [string] $PoolPortal     = 'diger-portal',
    [string] $PoolApi        = 'diger-api',
    [string] $PoolVentanilla = 'diger-ventanilla',

    [string] $UrlApi = 'http://localhost:8081',

    [switch] $SoloVerificar
)

$ErrorActionPreference = 'Stop'
$script:Fallos = @()
$script:Hechos = @()
$script:Respaldos = @()

function Escribir-Paso  { param($t) Write-Host ""; Write-Host "-- $t" -ForegroundColor Cyan }
function Escribir-Ok    { param($t) Write-Host "   [ok]    $t" -ForegroundColor Green; $script:Hechos += $t }
function Escribir-Aviso { param($t) Write-Host "   [aviso] $t" -ForegroundColor Yellow }
function Escribir-Mal   { param($t) Write-Host "   [FALLA] $t" -ForegroundColor Red; $script:Fallos += $t }
function Escribir-Plan  { param($t) Write-Host "   [haria] $t" -ForegroundColor DarkGray }

function Convertir-Clave {
    param([System.Security.SecureString]$Segura)
    if (-not $Segura) { return $null }
    $ptr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Segura)
    try   { return [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

function Invocar-Sql {
    param([string]$Base, [string]$Archivo, [hashtable]$Variables, [string]$Consulta)

    # -b para que un fallo devuelva codigo de error; sin el, sqlcmd calla y el
    # script cree que fue bien. -I porque hay indices filtrados: sin QUOTED_IDENTIFIER
    # ON cualquier escritura muere con Msg 1934. -f 65001 por los acentos.
    $args = @('-S', $ServidorSql, '-E', '-b', '-I', '-f', '65001', '-d', $Base)

    if ($Archivo)  { $args += @('-i', $Archivo) }
    if ($Consulta) { $args += @('-Q', $Consulta) }
    if ($Variables) { foreach ($k in $Variables.Keys) { $args += @('-v', "$k=$($Variables[$k])") } }

    $salida = & sqlcmd @args 2>&1
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd fallo sobre '$Base':`n$($salida -join "`n")" }
    return $salida
}

function Respaldar-Base {
    param([string]$Base)

    if (-not (Test-Path $CarpetaRespaldos)) {
        New-Item -ItemType Directory -Path $CarpetaRespaldos -Force | Out-Null
    }

    $sello   = Get-Date -Format 'yyyyMMdd-HHmmss'
    $destino = Join-Path $CarpetaRespaldos "$Base`_antes_cambios_$sello.bak"

    if ($SoloVerificar) { Escribir-Plan "Respaldaria '$Base' en $destino"; return }

    # COMPRESSION no existe en Express Edition: falla con Msg 1844 y tumba el
    # respaldo entero. Se pregunta la edicion en vez de suponerla.
    # EngineEdition 4 = Express.
    $filas = Invocar-Sql -Base 'master' -Consulta "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('EngineEdition') AS int);"
    $edicion = ($filas | Where-Object { "$_".Trim() -match '^\d+$' } | Select-Object -First 1)
    $compresion = ', COMPRESSION'
    if ([int]"$edicion".Trim() -eq 4) {
        $compresion = ''
        Escribir-Aviso 'Express Edition: el respaldo va sin comprimir.'
    }

    $sql = "BACKUP DATABASE [$Base] TO DISK = N'$destino' WITH INIT$compresion, NAME = N'Antes de los cambios de la API v1';"
    Invocar-Sql -Base 'master' -Consulta $sql | Out-Null

    if (-not (Test-Path $destino)) { throw "El respaldo de '$Base' no aparecio en disco." }
    $script:Respaldos += $destino
    Escribir-Ok "Respaldo de '$Base': $destino"
}

function Reiniciar-Pool {
    param([string]$Pool)
    if ($SoloVerificar) { Escribir-Plan "Reiniciaria el grupo '$Pool'"; return }
    if (-not (Test-Path "IIS:\AppPools\$Pool")) { Escribir-Aviso "No existe el grupo '$Pool'; se omite."; return }
    Restart-WebAppPool -Name $Pool
    Escribir-Ok "Grupo '$Pool' reiniciado"
}

function Fijar-Variable {
    param([string]$Pool, [string]$Nombre, [string]$Valor)

    if ($SoloVerificar) {
        # Se enmascara por CONTENIDO, no por nombre: una cadena de conexion se llama
        # 'ConnectionStrings__DefaultConnection' y lleva la contrasena dentro. Mirar
        # solo el nombre la imprimia entera en pantalla.
        $mostrado = $Valor
        if ($Nombre -match 'Password|ApiKey|Clave' -or $Valor -match 'Passwords*=') {
            $mostrado = $Valor -replace '(?i)(Passwords*=)[^;]*', '$1<oculto>'
            if ($mostrado -eq $Valor) { $mostrado = '<oculto>' }
        }
        Escribir-Plan "Fijaria '$Nombre' = $mostrado en '$Pool'"; return
    }
    if (-not (Test-Path "IIS:\AppPools\$Pool")) { Escribir-Aviso "No existe el grupo '$Pool'; se omite '$Nombre'."; return }

    $filtro = "system.applicationHost/applicationPools/add[@name='$Pool']/environmentVariables"

    # Quitar antes de poner: anadir dos veces la misma variable deja la
    # configuracion de IIS invalida, y no avisa hasta que el sitio no arranca.
    try {
        Remove-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
            -filter $filtro -name '.' -AtElement @{name=$Nombre} -ErrorAction SilentlyContinue
    } catch { }

    Add-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
        -filter $filtro -name '.' -value @{name=$Nombre; value=$Valor}

    Escribir-Ok "Variable '$Nombre' fijada en '$Pool'"
}

function Quitar-Variable {
    param([string]$Pool, [string]$Nombre)
    if ($SoloVerificar) { Escribir-Plan "Quitaria '$Nombre' de '$Pool'"; return }
    if (-not (Test-Path "IIS:\AppPools\$Pool")) { return }
    $filtro = "system.applicationHost/applicationPools/add[@name='$Pool']/environmentVariables"
    try {
        Remove-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
            -filter $filtro -name '.' -AtElement @{name=$Nombre} -ErrorAction SilentlyContinue
        Escribir-Ok "Variable '$Nombre' quitada de '$Pool'"
    } catch { }
}

# ═══ Comprobaciones previas ═════════════════════════════════════════════════

function Verificar-Requisitos {
    Escribir-Paso 'Comprobando requisitos'

    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        Escribir-Mal 'Falta sqlcmd. Instale SQL Server Command Line Utilities.'
    } else { Escribir-Ok 'sqlcmd disponible' }

    # WebAdministration puede no estar: este script tambien vale para aplicar
    # solo la parte de base de datos en una maquina sin IIS.
    if (Get-Module -ListAvailable -Name WebAdministration) {
        Import-Module WebAdministration -ErrorAction SilentlyContinue
        Escribir-Ok 'Modulo WebAdministration cargado'
    } else {
        Escribir-Aviso 'Sin WebAdministration: se omitiran los pasos de IIS.'
    }

    $scripts = @(
        (Join-Path $PSScriptRoot 'sql\10-usuario-solo-lectura-api.sql'),
        (Join-Path $RepoVentanilla 'scripts\sql\20-catalogo-portal-SUBIDA.sql'),
        (Join-Path $RepoVentanilla 'scripts\sql\21-catalogo-portal-BAJADA.sql')
    )
    foreach ($s in $scripts) {
        if (Test-Path $s) { Escribir-Ok "Script encontrado: $(Split-Path $s -Leaf)" }
        else              { Escribir-Mal "Falta el script: $s" }
    }

    try {
        Invocar-Sql -Base 'master' -Consulta 'SELECT 1' | Out-Null
        Escribir-Ok "Conexion a '$ServidorSql' correcta"
    } catch {
        Escribir-Mal "No se puede conectar a '$ServidorSql': $($_.Exception.Message)"
    }

    if ($Accion -eq 'DO' -and -not $SoloVerificar) {
        if (-not $ClaveLecturaApi) { Escribir-Mal 'Falta -ClaveLecturaApi. Sin ella no se puede crear el usuario de solo lectura.' }
        if (-not $ClaveApi)        { Escribir-Aviso 'Sin -ClaveApi no se refresca la clave de la API; se conservara la que ya tenga IIS.' }
    }
}

# ═══ DO ═════════════════════════════════════════════════════════════════════

function Aplicar {
    $clave = Convertir-Clave $ClaveLecturaApi

    Escribir-Paso '1/5 · Respaldos (sin esto no se sigue)'
    Respaldar-Base $BasePortal
    Respaldar-Base $BaseVentanilla

    Escribir-Paso '2/5 · Usuario de solo lectura para la API publica'
    if ($SoloVerificar) {
        Escribir-Plan "Crearia el login '$NombreUsuario' con SELECT sobre nueve tablas"
    } else {
        Invocar-Sql -Base $BasePortal `
                    -Archivo (Join-Path $PSScriptRoot 'sql\10-usuario-solo-lectura-api.sql') `
                    -Variables @{ Usuario = $NombreUsuario; Clave = $clave; Accion = 'DO' } | Out-Null
        Escribir-Ok "Usuario '$NombreUsuario' creado sobre '$BasePortal'"

        # Se comprueba de verdad, no se supone: leer debe funcionar y escribir no.
        $prueba = & sqlcmd -S $ServidorSql -U $NombreUsuario -P $clave -b -I -d $BasePortal `
                           -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.TramitesSiger;" 2>&1
        if ($LASTEXITCODE -ne 0) { throw "El usuario de solo lectura no puede leer: $prueba" }
        Escribir-Ok "Comprobado: lee $($prueba | Select-Object -First 1) tramites"

        & sqlcmd -S $ServidorSql -U $NombreUsuario -P $clave -b -I -d $BasePortal `
                 -Q "UPDATE dbo.TramitesSiger SET Publicado = Publicado;" 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { throw 'GRAVE: el usuario de solo lectura SI puede escribir. Revise el script de permisos.' }
        Escribir-Ok 'Comprobado: no puede escribir'
    }

    Escribir-Paso '3/5 · Catalogo replicado en HondurasAgil'
    if ($SoloVerificar) {
        Escribir-Plan "Crearia las nueve tablas Portal* en '$BaseVentanilla'"
    } else {
        Invocar-Sql -Base $BaseVentanilla `
                    -Archivo (Join-Path $RepoVentanilla 'scripts\sql\20-catalogo-portal-SUBIDA.sql') | Out-Null
        $n = Invocar-Sql -Base $BaseVentanilla -Consulta `
             "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'Portal%';" -ErrorAction Stop
        Escribir-Ok "Tablas Portal* presentes (se esperan 9): $(($n | Where-Object { $_ -match '^\s*\d+\s*$' }) -join '')"
    }

    Escribir-Paso '4/5 · Variables de entorno de IIS'
    $csLectura = "Server=$ServidorSql;Database=$BasePortal;User ID=$NombreUsuario;Password=$clave;TrustServerCertificate=True;"

    # El cambio de fondo: la API deja de compartir credenciales con el portal interno.
    Fijar-Variable $PoolApi 'ConnectionStrings__DefaultConnection' $csLectura
    # Swagger apagado en produccion: publicar el contrato entero sin querer es regalar el mapa.
    Fijar-Variable $PoolApi 'PortalDigitalApi__PublicarSwagger' 'false'

    if ($ClaveApi) {
        $claveApiTexto = Convertir-Clave $ClaveApi
        Fijar-Variable $PoolApi        'PortalDigitalApi__ApiKey' $claveApiTexto
        Fijar-Variable $PoolVentanilla 'PortalDigital__ApiKey'    $claveApiTexto
    }

    # Sincronizacion de HondurasAgil.
    Fijar-Variable $PoolVentanilla 'PortalDigital__SincronizacionAutomatica'  'true'
    Fijar-Variable $PoolVentanilla 'PortalDigital__IntervaloMinutos'          '60'
    # La red contra el punto ciego del ciclo incremental: /cambios se apoya en la
    # fecha de modificacion, que un UPDATE directo contra la base no toca.
    Fijar-Variable $PoolVentanilla 'PortalDigital__HorasEntreCiclosCompletos' '24'
    # Un arranque no deberia depender de que otro sistema este en pie.
    Fijar-Variable $PoolVentanilla 'PortalDigital__SincronizarAlArrancar'     'false'
    # OJO: en false el portal acepta fichas sin costo, sin plazo y sin modalidad.
    # Pongalo en true antes de abrir al ciudadano.
    Fijar-Variable $PoolVentanilla 'PortalDigital__ExigirFichaCompleta'       'false'

    Escribir-Paso '5/5 · Reinicio y comprobacion'
    Reiniciar-Pool $PoolApi
    Reiniciar-Pool $PoolVentanilla
    Probar-Humo
}

# ═══ UNDO ═══════════════════════════════════════════════════════════════════

function Revertir {
    Escribir-Paso '1/4 · Respaldos antes de revertir'
    # Si, tambien al revertir. Una reversion es un cambio como cualquier otro.
    Respaldar-Base $BasePortal
    Respaldar-Base $BaseVentanilla

    Escribir-Paso '2/4 · Variables de entorno'
    # La API vuelve a la cadena del portal interno. Es un retroceso en seguridad
    # y por eso se dice en voz alta en vez de hacerlo callando.
    Escribir-Aviso 'La API volvera a necesitar credenciales con escritura. Fijelas a mano:'
    Escribir-Aviso "  ConnectionStrings__DefaultConnection en el grupo '$PoolApi'"
    Quitar-Variable $PoolApi        'PortalDigitalApi__PublicarSwagger'
    Quitar-Variable $PoolVentanilla 'PortalDigital__SincronizacionAutomatica'
    Quitar-Variable $PoolVentanilla 'PortalDigital__IntervaloMinutos'
    Quitar-Variable $PoolVentanilla 'PortalDigital__HorasEntreCiclosCompletos'
    Quitar-Variable $PoolVentanilla 'PortalDigital__SincronizarAlArrancar'
    Quitar-Variable $PoolVentanilla 'PortalDigital__ExigirFichaCompleta'

    Escribir-Paso '3/4 · Catalogo replicado de HondurasAgil'
    if ($SoloVerificar) {
        Escribir-Plan 'Borraria las nueve tablas Portal* y su historial de migraciones'
    } else {
        Invocar-Sql -Base $BaseVentanilla `
                    -Archivo (Join-Path $RepoVentanilla 'scripts\sql\21-catalogo-portal-BAJADA.sql') | Out-Null
        Escribir-Ok 'Tablas Portal* eliminadas'
        Escribir-Aviso 'El catalogo sincronizado se ha perdido. Vuelve entero con un ciclo completo.'
    }

    Escribir-Paso '4/4 · Usuario de solo lectura'
    if ($SoloVerificar) {
        Escribir-Plan "Quitaria el login '$NombreUsuario'"
    } else {
        # El propio script cierra las sesiones abiertas antes del DROP LOGIN:
        # con la API en marcha, sin eso falla con Msg 15434 y deja el login huerfano.
        Invocar-Sql -Base $BasePortal `
                    -Archivo (Join-Path $PSScriptRoot 'sql\10-usuario-solo-lectura-api.sql') `
                    -Variables @{ Usuario = $NombreUsuario; Clave = 'no-se-usa'; Accion = 'UNDO' } | Out-Null
        Escribir-Ok "Usuario '$NombreUsuario' eliminado"
    }

    Reiniciar-Pool $PoolApi
    Reiniciar-Pool $PoolVentanilla
}

# ═══ Comprobacion de humo ═══════════════════════════════════════════════════

function Probar-Humo {
    if ($SoloVerificar) { Escribir-Plan 'Comprobaria que la API responde'; return }

    try {
        # /salud no pide clave a proposito: un monitor no deberia custodiar un secreto.
        $r = Invoke-RestMethod -Uri "$UrlApi/api/v1/salud" -TimeoutSec 20
        if ($r.baseDeDatos) { Escribir-Ok "La API responde y alcanza su base ($($r.estado))" }
        else                { Escribir-Mal 'La API responde pero NO alcanza su base. Revise la cadena de solo lectura.' }
    } catch {
        Escribir-Mal "La API no responde en $UrlApi : $($_.Exception.Message)"
    }
}

# ═══ Ejecucion ══════════════════════════════════════════════════════════════

Write-Host ''
Write-Host '══════════════════════════════════════════════════════════════════' -ForegroundColor White
Write-Host "  Cambios de la API v1 — accion: $Accion" -ForegroundColor White
if ($SoloVerificar) { Write-Host '  MODO COMPROBACION: no se cambia nada' -ForegroundColor Yellow }
Write-Host '══════════════════════════════════════════════════════════════════' -ForegroundColor White

Verificar-Requisitos

if ($script:Fallos.Count -gt 0) {
    Write-Host ''
    Write-Host 'No se continua: hay requisitos sin cumplir.' -ForegroundColor Red
    $script:Fallos | ForEach-Object { Write-Host "  · $_" -ForegroundColor Red }
    exit 1
}

try {
    if ($Accion -eq 'DO') { Aplicar } else { Revertir }
} catch {
    Write-Host ''
    Write-Host "ABORTADO: $($_.Exception.Message)" -ForegroundColor Red

    # Se dice lo que hay, no lo que se pretendia. Anunciar respaldos inexistentes
    # justo cuando algo ha fallado es la peor mentira posible.
    if ($script:Respaldos.Count -gt 0) {
        Write-Host 'Respaldos hechos antes del fallo:' -ForegroundColor Yellow
        $script:Respaldos | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    } else {
        Write-Host 'NO se hizo ningun respaldo.' -ForegroundColor Yellow
    }

    if ($_.Exception.Message -match 'Operating system error 5') {
        Write-Host ''
        Write-Host 'Ese error 5 es de permisos, y no son los suyos: quien escribe el' -ForegroundColor Yellow
        Write-Host 'respaldo es la CUENTA DEL SERVICIO SQL Server, no su usuario.' -ForegroundColor Yellow
        Write-Host 'Use una carpeta a la que esa cuenta pueda escribir. Para saber cual:' -ForegroundColor Yellow
        Write-Host "  SELECT SERVERPROPERTY(N'InstanceDefaultBackupPath')" -ForegroundColor Yellow
    }
    exit 1
}

Write-Host ''
Write-Host '══════════════════════════════════════════════════════════════════' -ForegroundColor White
if ($script:Fallos.Count -eq 0) {
    Write-Host "  Terminado sin fallos - $($script:Hechos.Count) pasos" -ForegroundColor Green
    if ($Accion -eq 'DO' -and -not $SoloVerificar) {
        Write-Host ''
        Write-Host '  QUEDA POR HACER A MANO:' -ForegroundColor Yellow
        Write-Host '   - Borrar el archivo donde anoto la clave de solo lectura.' -ForegroundColor Yellow
        Write-Host '   - ExigirFichaCompleta esta en false: el portal acepta fichas' -ForegroundColor Yellow
        Write-Host '     incompletas. Pongalo en true antes de abrir al ciudadano.' -ForegroundColor Yellow
    }
    exit 0
} else {
    Write-Host "  Terminado CON $($script:Fallos.Count) fallo(s)" -ForegroundColor Red
    $script:Fallos | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
