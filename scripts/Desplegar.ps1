<#
.SYNOPSIS
    Despliega en IIS los tres sistemas de DIGER, cada uno en su propio sitio.

.DESCRIPTION
    PortalDigital (portal interno), la API publica v1 y HondurasAgil (portal ciudadano)
    conviven en el mismo servidor, en tres sitios y tres grupos de aplicaciones separados.

    Separados a proposito: la API es publica y el portal interno no debe compartir proceso
    con ella. Si uno se cae o se recicla, los otros dos siguen en pie.

    IDEMPOTENTE: se puede volver a ejecutar tantas veces como haga falta.
    Y falla ruidosamente: cualquier paso que salga mal detiene el despliegue en vez de
    dejar un sistema a medias que parece funcionar.

.PARAMETER SoloVerificar
    No despliega: solo comprueba los requisitos y dice que falta. Ejecutelo asi la primera
    vez, en el servidor real, antes de tocar nada.

.EXAMPLE
    .\Desplegar.ps1 -SoloVerificar -ServidorSql 'SRV-SQL\INSTANCIA'

.EXAMPLE
    .\Desplegar.ps1 -ServidorSql 'SRV-SQL\INSTANCIA' `
                    -ClaveApi (Read-Host 'Clave de la API' -AsSecureString) `
                    -HostPortal 'tramites.diger.gob.hn' `
                    -HostApi    'api.diger.gob.hn' `
                    -HostVentanilla 'hondurasagil.gob.hn'

.NOTES
    Requiere PowerShell ELEVADO. Documentacion completa: DESPLIEGUE.md, misma carpeta.
    Ubicacion prevista: scripts\ del repositorio Portal-Informacion-Institucional.
#>

[CmdletBinding()]
param(
    [string] $RepoPortal      = (Split-Path -Parent $PSScriptRoot),
    [string] $RepoVentanilla  = 'C:\DIGER\Aplicativos\VentanillaDigital.Net',
    [string] $RaizPublicacion = 'C:\inetpub\diger',

    [Parameter(Mandatory = $true)]
    [string] $ServidorSql,
    [string] $BasePortal      = 'TramitesEstado',
    [string] $BaseVentanilla  = 'VentanillaDigital_Net',

    # SecureString a proposito: asi la clave no queda en el historial de PowerShell.
    [System.Security.SecureString] $ClaveApi,

    [string] $HostPortal       = '',
    [string] $HostApi          = '',
    [string] $HostVentanilla   = '',
    [int]    $PuertoPortal     = 8080,
    [int]    $PuertoApi        = 8081,
    [int]    $PuertoVentanilla = 8082,

    [switch] $SoloVerificar
)

$ErrorActionPreference = 'Stop'
$ProgressPreference    = 'SilentlyContinue'

$script:Fallos = @()

function Escribir-Paso  { param($t) Write-Host ""; Write-Host "-- $t" -ForegroundColor Cyan }
function Escribir-Ok    { param($t) Write-Host "   [ok]    $t" -ForegroundColor Green }
function Escribir-Aviso { param($t) Write-Host "   [aviso] $t" -ForegroundColor Yellow }
function Escribir-Mal   { param($t) Write-Host "   [FALLA] $t" -ForegroundColor Red; $script:Fallos += $t }

function Convertir-Clave {
    param([System.Security.SecureString] $Segura)
    if (-not $Segura) { return $null }
    $p = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Segura)
    try   { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($p) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($p) }
}

# =============================================================================
#  1 - Requisitos previos
# =============================================================================

function Verificar-Requisitos {
    Escribir-Paso 'Requisitos previos'

    $esAdmin = ([Security.Principal.WindowsPrincipal] `
        [Security.Principal.WindowsIdentity]::GetCurrent()
        ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if ($esAdmin) { Escribir-Ok 'PowerShell elevado' }
    else          { Escribir-Mal 'PowerShell NO esta elevado. Abralo como administrador.' }

    if (Get-Module -ListAvailable -Name WebAdministration) {
        Import-Module WebAdministration -ErrorAction Stop
        Escribir-Ok 'Modulo WebAdministration disponible'
    } else {
        Escribir-Mal 'Falta WebAdministration. Instale IIS con las herramientas de administracion.'
    }

    # Hacen falta LOS DOS SDK: PortalDigital es .NET 9 y HondurasAgil .NET 10.
    # Es el error mas facil de cometer en este despliegue.
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        $sdks = & dotnet --list-sdks
        foreach ($v in @('9', '10')) {
            if ($sdks -match "^$v\.") { Escribir-Ok "SDK de .NET $v presente" }
            else { Escribir-Mal "Falta el SDK de .NET $v - no se podra publicar uno de los tres proyectos." }
        }
    } else {
        Escribir-Mal 'No se encontro dotnet. Instale el SDK de .NET 9 y el de .NET 10.'
    }

    # Sin el Hosting Bundle, IIS devuelve 500.30 y el navegador no dice que falta.
    $runtimes = if (Get-Command dotnet -ErrorAction SilentlyContinue) { & dotnet --list-runtimes } else { @() }
    foreach ($v in @('9', '10')) {
        if ($runtimes -match "^Microsoft\.AspNetCore\.App $v\.") {
            Escribir-Ok "Runtime de ASP.NET Core $v presente"
        } else {
            Escribir-Mal "Falta el Hosting Bundle de ASP.NET Core $v. Sin el IIS responde 500.30."
        }
    }

    # Get-WebGlobalModule solo existe si IIS esta instalado. Un comando inexistente lanza
    # CommandNotFoundException, que -ErrorAction SilentlyContinue NO atrapa y que con
    # $ErrorActionPreference = 'Stop' aborta el script entero. Hay que preguntar antes.
    if (Get-Command Get-WebGlobalModule -ErrorAction SilentlyContinue) {
        $ancm = Get-WebGlobalModule | Where-Object { $_.Name -eq 'AspNetCoreModuleV2' }
        if ($ancm) { Escribir-Ok 'AspNetCoreModuleV2 registrado en IIS' }
        else       { Escribir-Mal 'Falta AspNetCoreModuleV2. Reinstale el Hosting Bundle DESPUES de instalar IIS.' }
    } else {
        Escribir-Mal 'No se puede comprobar AspNetCoreModuleV2: IIS no esta instalado en esta maquina.'
    }

    foreach ($r in @(@{n='PortalDigital'; p=$RepoPortal}, @{n='HondurasAgil'; p=$RepoVentanilla})) {
        if (Test-Path $r.p) { Escribir-Ok "Repositorio de $($r.n): $($r.p)" }
        else { Escribir-Mal "No existe el repositorio de $($r.n) en $($r.p)." }
    }

    foreach ($b in @($BasePortal, $BaseVentanilla)) {
        $cs = "Server=$ServidorSql;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=10;"
        try {
            $cn = New-Object System.Data.SqlClient.SqlConnection $cs
            $cn.Open()
            $cmd = $cn.CreateCommand()
            $cmd.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @n"
            [void]$cmd.Parameters.AddWithValue('@n', $b)
            $existe = [int]$cmd.ExecuteScalar()
            $cn.Close()
            if ($existe -gt 0) { Escribir-Ok "Base '$b' accesible en $ServidorSql" }
            else { Escribir-Aviso "La base '$b' NO existe todavia. Creela antes de migrar." }
        } catch {
            Escribir-Mal "No se pudo conectar a $ServidorSql : $($_.Exception.Message)"
        }
    }

    # P-03: la clave va en variable de entorno del grupo, NUNCA en appsettings.json,
    # que esta versionado en git.
    if (-not $SoloVerificar) {
        if ($ClaveApi) {
            $texto = Convertir-Clave $ClaveApi
            if ($texto.Length -lt 32) {
                Escribir-Mal "La clave tiene $($texto.Length) caracteres. Use al menos 32, al azar."
            } else { Escribir-Ok "Clave de la API recibida ($($texto.Length) caracteres)" }
        } else {
            Escribir-Mal 'Falta -ClaveApi. La API publica no puede desplegarse sin clave.'
        }
    }

    if ($script:Fallos.Count -gt 0) {
        Write-Host ""
        Write-Host "Hay $($script:Fallos.Count) requisito(s) sin cumplir. No se despliega nada." -ForegroundColor Red
        exit 1
    }
    Write-Host ""
    Escribir-Ok 'Todos los requisitos se cumplen.'
}

# =============================================================================
#  2 - Publicacion
# =============================================================================

function Publicar-Aplicacion {
    param([string]$Proyecto, [string]$Nombre)

    Escribir-Paso "Publicando $Nombre"
    if (-not (Test-Path $Proyecto)) { throw "No existe el proyecto $Proyecto" }

    # Temporal y luego copia: si publish falla a mitad, el sitio en produccion sigue
    # con la version anterior intacta.
    $temporal = Join-Path $env:TEMP "diger-publish-$Nombre-$PID"
    if (Test-Path $temporal) { Remove-Item $temporal -Recurse -Force }

    & dotnet publish $Proyecto -c Release -o $temporal --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "La publicacion de $Nombre fallo con codigo $LASTEXITCODE" }

    Escribir-Ok "Compilado en $temporal"
    return $temporal
}

function Instalar-Publicacion {
    param([string]$Origen, [string]$Destino, [string]$Pool, [string]$Nombre)

    # Detener el grupo antes de copiar: con las DLL abiertas la copia falla y el
    # despliegue queda a medias.
    if (Test-Path "IIS:\AppPools\$Pool") {
        if ((Get-WebAppPoolState -Name $Pool).Value -eq 'Started') {
            Stop-WebAppPool -Name $Pool
            Start-Sleep -Seconds 3
        }
    }

    if (-not (Test-Path $Destino)) { New-Item -ItemType Directory -Path $Destino -Force | Out-Null }

    # appsettings.Production.json NO se sobrescribe: borrarlo en cada despliegue es una
    # averia silenciosa que aparece semanas despues.
    $preservar = Join-Path $Destino 'appsettings.Production.json'
    $respaldo  = $null
    if (Test-Path $preservar) {
        $respaldo = Join-Path $env:TEMP "appsettings-prod-$Nombre-$PID.json"
        Copy-Item $preservar $respaldo -Force
    }

    Copy-Item (Join-Path $Origen '*') $Destino -Recurse -Force
    if ($respaldo) { Copy-Item $respaldo $preservar -Force; Remove-Item $respaldo -Force }

    Remove-Item $Origen -Recurse -Force
    Escribir-Ok "$Nombre instalado en $Destino"
}

# =============================================================================
#  3 - Base de datos
# =============================================================================

function Migrar-Base {
    param([string]$Repo, [string]$ProyectoInfra, [string]$ProyectoArranque,
          [string]$Base, [string]$Nombre)

    Escribir-Paso "Migrando la base de $Nombre"

    # ATENCION: esto no es opcional aunque los datos ya esten en produccion.
    # Los 1.057 tramites pueden estar y aun asi faltar las columnas nuevas, la tabla
    # CategoriasTramite y la correccion de colacion. Datos y esquema son cosas
    # distintas: git lleva las migraciones, no los datos.
    $cs = "Server=$ServidorSql;Database=$Base;Trusted_Connection=True;TrustServerCertificate=True;"

    Push-Location $Repo
    try {
        & dotnet ef database update `
            --project $ProyectoInfra --startup-project $ProyectoArranque `
            --connection $cs | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "La migracion de $Nombre fallo con codigo $LASTEXITCODE" }
        Escribir-Ok "Migraciones de $Nombre aplicadas sobre '$Base'"
    } finally { Pop-Location }
}

# =============================================================================
#  4 - IIS
# =============================================================================

function Asegurar-GrupoDeAplicaciones {
    param([string]$Pool)

    if (-not (Test-Path "IIS:\AppPools\$Pool")) {
        New-WebAppPool -Name $Pool | Out-Null
        Escribir-Ok "Grupo '$Pool' creado"
    } else {
        Escribir-Ok "Grupo '$Pool' ya existia"
    }

    # "Sin codigo administrado": ASP.NET Core corre fuera del CLR de IIS. Dejarlo en
    # v4.0 es el error clasico que produce un 500.30 sin explicacion.
    Set-ItemProperty "IIS:\AppPools\$Pool" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty "IIS:\AppPools\$Pool" -Name startMode             -Value 'AlwaysRunning'
    # Sin reciclado por horas: reciclar a las 02:00 corta una sincronizacion en curso.
    Set-ItemProperty "IIS:\AppPools\$Pool" -Name recycling.periodicRestart.time -Value '00:00:00'
}

function Asegurar-VariableDeEntorno {
    param([string]$Pool, [string]$Nombre, [string]$Valor)

    # Aqui viven los secretos (P-03). No es un archivo de la carpeta de publicacion,
    # asi que no se va en un despliegue ni acaba en el repositorio.
    $filtro = "system.applicationHost/applicationPools/add[@name='$Pool']/environmentVariables"

    # Idempotencia: quitar antes de poner. Anadir dos veces la misma variable deja la
    # configuracion de IIS invalida.
    try {
        Remove-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
            -filter $filtro -name '.' -AtElement @{name=$Nombre} -ErrorAction SilentlyContinue
    } catch { }

    Add-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
        -filter $filtro -name '.' -value @{name=$Nombre; value=$Valor}

    Escribir-Ok "Variable '$Nombre' fijada en '$Pool'"
}

function Asegurar-Sitio {
    param([string]$Sitio, [string]$Pool, [string]$Ruta, [int]$Puerto, [string]$NombreHost)

    if (-not (Test-Path "IIS:\Sites\$Sitio")) {
        New-Website -Name $Sitio -PhysicalPath $Ruta -ApplicationPool $Pool `
                    -Port $Puerto -HostHeader $NombreHost -Force | Out-Null
        Escribir-Ok "Sitio '$Sitio' creado en el puerto $Puerto"
    } else {
        Set-ItemProperty "IIS:\Sites\$Sitio" -Name physicalPath    -Value $Ruta
        Set-ItemProperty "IIS:\Sites\$Sitio" -Name applicationPool -Value $Pool
        Escribir-Ok "Sitio '$Sitio' actualizado (puerto $Puerto)"
    }

    # La identidad del grupo necesita leer la carpeta y ESCRIBIR en logs\: el registro
    # de arranque se escribe ahi, y sin permiso el diagnostico de un 500.30 se pierde
    # justo cuando mas falta hace.
    $identidad = "IIS AppPool\$Pool"
    $acl = Get-Acl $Ruta
    $acl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
        $identidad, 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
    Set-Acl $Ruta $acl

    $logs = Join-Path $Ruta 'logs'
    if (-not (Test-Path $logs)) { New-Item -ItemType Directory -Path $logs -Force | Out-Null }
    $aclLogs = Get-Acl $logs
    $aclLogs.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
        $identidad, 'Modify', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
    Set-Acl $logs $aclLogs

    Escribir-Ok "Permisos concedidos a '$identidad'"
}

# =============================================================================
#  5 - Pruebas de humo
# =============================================================================

function Probar-Humo {
    param([string]$Url, [string]$Nombre, [int]$Min = 200, [int]$Max = 399, [hashtable]$Cabeceras = @{})

    try {
        $r = Invoke-WebRequest -Uri $Url -Headers $Cabeceras -UseBasicParsing `
                               -TimeoutSec 30 -MaximumRedirection 0 -ErrorAction Stop
        $codigo = [int]$r.StatusCode
    } catch {
        $codigo = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
    }

    if ($codigo -ge $Min -and $codigo -le $Max) { Escribir-Ok "$Nombre responde HTTP $codigo" }
    else { Escribir-Mal "$Nombre respondio HTTP $codigo (se esperaba $Min-$Max) - $Url" }
}

# =============================================================================
#  Ejecucion
# =============================================================================

Write-Host ""
Write-Host "DESPLIEGUE DIGER - PortalDigital / API v1 / HondurasAgil" -ForegroundColor White
Write-Host "Servidor SQL: $ServidorSql" -ForegroundColor DarkGray
Write-Host ""

Verificar-Requisitos

if ($SoloVerificar) {
    Write-Host ""
    Write-Host "Solo verificacion. No se desplego nada." -ForegroundColor Cyan
    exit 0
}

$claveTexto = Convertir-Clave $ClaveApi

$destinoPortal     = Join-Path $RaizPublicacion 'portaldigital'
$destinoApi        = Join-Path $RaizPublicacion 'api'
$destinoVentanilla = Join-Path $RaizPublicacion 'hondurasagil'

$poolPortal     = 'DIGER_PortalDigital'
$poolApi        = 'DIGER_ApiPublica'
$poolVentanilla = 'DIGER_HondurasAgil'

$csPortal     = "Server=$ServidorSql;Database=$BasePortal;Trusted_Connection=True;TrustServerCertificate=True;"
$csVentanilla = "Server=$ServidorSql;Database=$BaseVentanilla;Trusted_Connection=True;TrustServerCertificate=True;"

# Publicar los tres ANTES de tocar IIS: si uno no compila, mejor enterarse ahora que
# con dos sitios ya parados.
$tmpPortal     = Publicar-Aplicacion (Join-Path $RepoPortal     'src\Web\Diger.TramitesEstado.Web.csproj') 'PortalDigital'
$tmpApi        = Publicar-Aplicacion (Join-Path $RepoPortal     'src\Presentation\Diger.TramitesEstado.Presentation.csproj') 'ApiPublica'
$tmpVentanilla = Publicar-Aplicacion (Join-Path $RepoVentanilla 'src\Web\Diger.VentanillaDigital.Web.csproj') 'HondurasAgil'

Migrar-Base $RepoPortal     'src\Infrastructure' 'src\Web' $BasePortal     'PortalDigital'
Migrar-Base $RepoVentanilla 'src\Infrastructure' 'src\Web' $BaseVentanilla 'HondurasAgil'

Escribir-Paso 'Grupos de aplicaciones'
foreach ($p in @($poolPortal, $poolApi, $poolVentanilla)) { Asegurar-GrupoDeAplicaciones $p }

Escribir-Paso 'Variables de entorno (aqui viven los secretos)'
Asegurar-VariableDeEntorno $poolPortal     'ASPNETCORE_ENVIRONMENT' 'Production'
Asegurar-VariableDeEntorno $poolPortal     'ConnectionStrings__DefaultConnection' $csPortal

Asegurar-VariableDeEntorno $poolApi        'ASPNETCORE_ENVIRONMENT' 'Production'
Asegurar-VariableDeEntorno $poolApi        'ConnectionStrings__DefaultConnection' $csPortal
Asegurar-VariableDeEntorno $poolApi        'PortalDigitalApi__ApiKey' $claveTexto

Asegurar-VariableDeEntorno $poolVentanilla 'ASPNETCORE_ENVIRONMENT' 'Production'
Asegurar-VariableDeEntorno $poolVentanilla 'ConnectionStrings__DefaultConnection' $csVentanilla
Asegurar-VariableDeEntorno $poolVentanilla 'PortalDigital__BaseUrl' `
    $(if ($HostApi) { "https://$HostApi" } else { "http://localhost:$PuertoApi" })
Asegurar-VariableDeEntorno $poolVentanilla 'PortalDigital__ApiKey' $claveTexto
# HondurasAgil no debe migrar su base sola al arrancar: eso lo hace este script.
Asegurar-VariableDeEntorno $poolVentanilla 'DatabaseInitialization__Enabled' 'false'

Escribir-Paso 'Instalando archivos'
Instalar-Publicacion $tmpPortal     $destinoPortal     $poolPortal     'PortalDigital'
Instalar-Publicacion $tmpApi        $destinoApi        $poolApi        'ApiPublica'
Instalar-Publicacion $tmpVentanilla $destinoVentanilla $poolVentanilla 'HondurasAgil'

Escribir-Paso 'Sitios de IIS'
Asegurar-Sitio 'DIGER - PortalDigital' $poolPortal     $destinoPortal     $PuertoPortal     $HostPortal
Asegurar-Sitio 'DIGER - API v1'        $poolApi        $destinoApi        $PuertoApi        $HostApi
Asegurar-Sitio 'DIGER - HondurasAgil'  $poolVentanilla $destinoVentanilla $PuertoVentanilla $HostVentanilla

Escribir-Paso 'Arrancando'
foreach ($p in @($poolPortal, $poolApi, $poolVentanilla)) {
    if ((Get-WebAppPoolState -Name $p).Value -ne 'Started') { Start-WebAppPool -Name $p }
    Escribir-Ok "Grupo '$p' en marcha"
}
Start-Sleep -Seconds 8

Escribir-Paso 'Pruebas de humo'
$baseApi        = if ($HostApi)        { "http://$HostApi"        } else { "http://localhost:$PuertoApi" }
$basePortal     = if ($HostPortal)     { "http://$HostPortal"     } else { "http://localhost:$PuertoPortal" }
$baseVentanilla = if ($HostVentanilla) { "http://$HostVentanilla" } else { "http://localhost:$PuertoVentanilla" }

# /salud no lleva clave a proposito: un monitor externo no debe custodiar un secreto.
Probar-Humo "$baseApi/api/v1/salud" 'API - salud' 200 200
# Sin clave debe ser 401. Si contesta 200, la autenticacion NO esta puesta.
Probar-Humo "$baseApi/api/v1/tramites" 'API - rechaza sin clave' 401 401
Probar-Humo "$baseApi/api/v1/tramites?tamano=1" 'API - catalogo con clave' 200 200 @{ 'X-Api-Key' = $claveTexto }
# Swagger NO debe estar publicado en produccion.
Probar-Humo "$baseApi/swagger/v1/swagger.json" 'API - Swagger cerrado' 404 404
Probar-Humo $basePortal     'PortalDigital' 200 399
Probar-Humo $baseVentanilla 'HondurasAgil'  200 399

Write-Host ""
if ($script:Fallos.Count -eq 0) {
    Write-Host "Despliegue terminado. Los tres sitios responden." -ForegroundColor Green
    Write-Host ""
    Write-Host "  PortalDigital  $basePortal"
    Write-Host "  API v1         $baseApi/api/v1/salud"
    Write-Host "  HondurasAgil   $baseVentanilla"
    Write-Host ""
    Write-Host "Falta a mano: certificados HTTPS y enlaces del 443, y el puerto de" -ForegroundColor Yellow
    Write-Host "autenticacion por certificado de PortalDigital. Ver DESPLIEGUE.md." -ForegroundColor Yellow
    exit 0
} else {
    Write-Host "Despliegue terminado CON $($script:Fallos.Count) FALLA(S):" -ForegroundColor Red
    $script:Fallos | ForEach-Object { Write-Host "  . $_" -ForegroundColor Red }
    exit 1
}
