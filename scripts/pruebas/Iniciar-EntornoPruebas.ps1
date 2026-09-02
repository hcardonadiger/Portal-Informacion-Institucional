<#
.SYNOPSIS
    Levanta GestionGD, la API y HondurasAgil apuntando a las copias desechables.

.DESCRIPTION
    Nada de esto toca un appsettings. La unica forma en que las aplicaciones saben a que base
    conectarse es por variables de entorno del proceso que se lanza, y esas variables mueren
    con el proceso. Un archivo de configuracion editado, en cambio, sobrevive al olvido: al
    dia siguiente uno arranca creyendo que esta en desarrollo y esta escribiendo en la copia,
    o peor, alguien lo devuelve a su sitio y las pruebas escriben en la real.

    Los puertos tambien son distintos a los de desarrollo, para que los dos entornos puedan
    convivir y para que la cinta verde de la pantalla no sea la unica pista de donde esta uno.

    La salida de cada aplicacion va a .estado\log-*.txt, no a una ventana. Asi se puede leer
    lo que paso sin estar mirando, y detener el entorno no depende de cerrar nada.

.EXAMPLE
    .\Iniciar-EntornoPruebas.ps1
    .\Iniciar-EntornoPruebas.ps1 -SincronizacionRapida
#>

[CmdletBinding()]
param(
    # Baja el ciclo pesado de HondurasAgil de 60 minutos a 1, para no esperar una hora a que
    # se note en el portal ciudadano un cambio hecho en GestionGD. Solo para pruebas.
    [switch] $SincronizacionRapida,

    [int] $EsperaSegundos = 120
)

. (Join-Path $PSScriptRoot '_Comun.ps1')

$estado = Get-CarpetaEstado
$fichaRuta = Join-Path $estado 'entorno.json'
if (-not (Test-Path $fichaRuta)) {
    throw "No hay entorno montado. Ejecute primero .\Nuevo-EntornoPruebas.ps1"
}
$f = Get-Content $fichaRuta -Raw -Encoding UTF8 | ConvertFrom-Json

# Si las copias no estan, no se arranca. Arrancar sin ellas significaria caer en la real.
foreach ($b in @($f.BasePortalCopia, $f.BaseVentanillaCopia)) {
    Assert-EsCopiaDesechable $b
    if (-not (Test-BaseExiste -Instancia $f.Instancia -Base $b)) {
        throw "Falta la copia '$b'. Ejecute .\Nuevo-EntornoPruebas.ps1 -Rehacer"
    }
}

function Get-Cadena {
    param([string] $Base)
    return "Server=$($f.Instancia);Database=$Base;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
}

function Test-PuertoOcupado {
    param([int] $Puerto)
    return [bool](Get-NetTCPConnection -LocalPort $Puerto -State Listen -ErrorAction SilentlyContinue)
}

function Start-Aplicacion {
    param(
        [Parameter(Mandatory)][string]    $Nombre,
        [Parameter(Mandatory)][string]    $Proyecto,
        [Parameter(Mandatory)][hashtable] $Variables,
        [Parameter(Mandatory)][int]       $PuertoEspera,
        [Parameter(Mandatory)][string]    $Alias
    )

    if (-not (Test-Path $Proyecto)) {
        Write-Malo "$Nombre : no encuentro $Proyecto"
        return $null
    }
    if (Test-PuertoOcupado $PuertoEspera) {
        Write-Aviso "$Nombre : el puerto $PuertoEspera ya esta ocupado; no se arranca otra vez."
        return $null
    }

    # Se ponen en el proceso actual y se quitan enseguida: Start-Process hereda el entorno de
    # quien lanza, y dejarlas puestas contaminaria el arranque siguiente.
    foreach ($k in $Variables.Keys) { Set-Item -Path "Env:$k" -Value $Variables[$k] }
    try {
        $log    = Join-Path $estado "log-$Alias.txt"
        $logErr = Join-Path $estado "log-$Alias-errores.txt"
        Write-Paso "Arrancando $Nombre..."
        $argumentos = @('run', '--no-launch-profile', '--project', $Proyecto)
        $p = Start-Process -FilePath 'dotnet' -PassThru -WindowStyle Hidden -ArgumentList $argumentos -RedirectStandardOutput $log -RedirectStandardError $logErr
        return $p
    }
    finally {
        foreach ($k in $Variables.Keys) { Remove-Item -Path "Env:$k" -ErrorAction SilentlyContinue }
    }
}

$intervaloMinutos = if ($SincronizacionRapida) { '1' } else { '60' }

$varsPortal = @{
    'ASPNETCORE_ENVIRONMENT'               = 'Development'
    'ConnectionStrings__DefaultConnection' = (Get-Cadena $f.BasePortalCopia)
    # Program.cs de GestionGD arma sus puertos en codigo cuando el entorno es Development y
    # no obedece ASPNETCORE_URLS; la unica via limpia para moverlo son estas tres claves.
    'Ports__DevMain'                       = "$($f.PuertoPortalHttps)"
    'Ports__DevCert'                       = "$($f.PuertoPortalCert)"
    'Ports__DevHttp'                       = "$($f.PuertoPortalHttp)"
    # Sin esto, entrar por http devuelve 500: con tres puertos https escuchando,
    # UseHttpsRedirection no puede adivinar a cual redirigir.
    'ASPNETCORE_HTTPS_PORT'                = "$($f.PuertoPortalHttps)"
}

$varsApi = @{
    'ASPNETCORE_ENVIRONMENT'               = 'Development'
    'ASPNETCORE_URLS'                      = "https://localhost:$($f.PuertoApiHttps);http://localhost:$($f.PuertoApiHttp)"
    'ConnectionStrings__DefaultConnection' = (Get-Cadena $f.BasePortalCopia)
    'PortalDigitalApi__ApiKey'             = $f.ClaveApi
}

$varsAgil = @{
    'ASPNETCORE_ENVIRONMENT'               = 'Development'
    'ASPNETCORE_URLS'                      = "https://localhost:$($f.PuertoAgilHttps);http://localhost:$($f.PuertoAgilHttp)"
    'ConnectionStrings__DefaultConnection' = (Get-Cadena $f.BaseVentanillaCopia)
    'PortalDigital__BaseUrl'               = "http://localhost:$($f.PuertoApiHttp)/"
    'PortalDigital__ApiKey'                = $f.ClaveApi
    'PortalDigital__IntervaloMinutos'      = $intervaloMinutos
}

$apps = @(
    [pscustomobject]@{
        Nombre = 'GestionGD (portal interno)'; Alias = 'portal'
        Proyecto = (Join-Path $f.RepoPortal 'src\Web\Diger.TramitesEstado.Web.csproj')
        Puerto = [int]$f.PuertoPortalHttps
        Url = "https://localhost:$($f.PuertoPortalHttps)"
        Vars = $varsPortal
    }
    [pscustomobject]@{
        Nombre = 'API publica'; Alias = 'api'
        Proyecto = (Join-Path $f.RepoPortal 'src\Api\Diger.TramitesEstado.Api.csproj')
        Puerto = [int]$f.PuertoApiHttp
        Url = "http://localhost:$($f.PuertoApiHttp)/swagger"
        Vars = $varsApi
    }
    [pscustomobject]@{
        Nombre = 'HondurasAgil (portal ciudadano)'; Alias = 'agil'
        Proyecto = (Join-Path $f.RepoVentanilla 'src\Web\Diger.VentanillaDigital.Web.csproj')
        Puerto = [int]$f.PuertoAgilHttps
        Url = "https://localhost:$($f.PuertoAgilHttps)"
        Vars = $varsAgil
    }
)

Write-Host ''
Write-Host 'Levantando el entorno de pruebas' -ForegroundColor White
Write-Host "  base del portal ......... $($f.BasePortalCopia)"
Write-Host "  base del ciudadano ...... $($f.BaseVentanillaCopia)"
Write-Host ''

$lanzados = @()
foreach ($a in $apps) {
    $p = Start-Aplicacion -Nombre $a.Nombre -Proyecto $a.Proyecto -Variables $a.Vars -PuertoEspera $a.Puerto -Alias $a.Alias
    if ($p) { $lanzados += [pscustomobject]@{ Alias = $a.Alias; Proceso = $p.Id; Puerto = $a.Puerto } }
}
$lanzados | ConvertTo-Json | Set-Content -Path (Join-Path $estado 'procesos.json') -Encoding UTF8

Write-Host ''
Write-Paso "Esperando a que respondan (hasta $EsperaSegundos s cada uno)..."
foreach ($a in $apps) {
    $arriba = $false
    for ($i = 0; $i -lt $EsperaSegundos; $i++) {
        if (Test-PuertoOcupado $a.Puerto) { $arriba = $true; break }
        Start-Sleep -Seconds 1
    }
    if ($arriba) { Write-Ok "$($a.Nombre) -> $($a.Url)" }
    else         { Write-Malo "$($a.Nombre) no respondio. Vea .estado\log-$($a.Alias)-errores.txt" }
}

Write-Host ''
Write-Host '  Direcciones del entorno de pruebas:' -ForegroundColor White
foreach ($a in $apps) { Write-Host ('    ' + $a.Nombre.PadRight(34) + $a.Url) }
Write-Host ''
Write-Host '  La cinta de arriba de cada pantalla debe decir "Entorno de pruebas".' -ForegroundColor DarkGray
Write-Host '  Al terminar:  .\Borrar-EntornoPruebas.ps1' -ForegroundColor White
Write-Host ''
