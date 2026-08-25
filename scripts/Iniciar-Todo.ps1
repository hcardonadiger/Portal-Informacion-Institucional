<#
.SYNOPSIS
    Levanta los tres sistemas en desarrollo y abre el navegador en cada uno.

.DESCRIPTION
    Existe por dos razones concretas:

    1. `dotnet run` desde la consola NO abre el navegador. Eso no es un fallo de
       configuracion: `launchBrowser` de launchSettings.json solo lo obedecen
       Visual Studio y `dotnet watch`. Desde la terminal hay que abrirlo aparte,
       y es lo que hace este script.

    2. Los puertos estaban chocando. La API venia configurada en 49176, que es
       EXACTAMENTE el puerto de autenticacion por certificado del portal interno.
       Con el portal arriba, la API no podia arrancar — y si no arranca, no hay
       navegador que abrir.

    Reparto actual, sin solapes:

        Portal interno  https://localhost:49175   (49176 = certificado, 49177 = http)
        API publica     https://localhost:7199    (http 5199)  -> abre en /swagger
        HondurasAgil    https://localhost:7180    (http 5180)

.EXAMPLE
    .\Iniciar-Todo.ps1                 # los tres
    .\Iniciar-Todo.ps1 -Solo Api       # solo la API
    .\Iniciar-Todo.ps1 -SinNavegador   # sin abrir nada
#>

param(
    [ValidateSet('Todos','Portal','Api','HondurasAgil')]
    [string] $Solo = 'Todos',

    [string] $RepoPortal     = (Split-Path -Parent $PSScriptRoot),
    [string] $RepoVentanilla = 'C:\Users\jgarcia\Documents\honduras-agil',

    [switch] $SinNavegador
)

$ErrorActionPreference = 'Stop'

$sistemas = \(
    \{ Nombre = 'Portal interno'
       Proyecto = (Join-Path $RepoPortal 'src\Web\Diger.TramitesEstado.Web.csproj')
       Url = 'https://localhost:49175'
       Puerto = 49175
       Perfil = $null }

    \{ Nombre = 'API publica'
       Proyecto = (Join-Path $RepoPortal 'src\Api\Diger.TramitesEstado.Api.csproj')
       Url = 'https://localhost:7199/swagger'
       Puerto = 7199
       Perfil = 'Diger.TramitesEstado.Api' }

    \{ Nombre = 'HondurasAgil'
       Proyecto = (Join-Path $RepoVentanilla 'src\Web\Diger.VentanillaDigital.Web.csproj')
       Url = 'https://localhost:7180'
       Puerto = 7180
       Perfil = 'https' }
)

$mapa = \{ 'Portal' = 'Portal interno'; 'Api' = 'API publica'; 'HondurasAgil' = 'HondurasAgil' }
if ($Solo -ne 'Todos') { $sistemas = $sistemas | Where-Object { $_.Nombre -eq $mapa[$Solo] } }

function Puerto-Ocupado {
    param([int]$Puerto)
    # -InformationLevel Quiet devuelve solo true/false y no tarda en fallar.
    try { return (Test-NetConnection -ComputerName 'localhost' -Port $Puerto -InformationLevel Quiet -WarningAction SilentlyContinue) }
    catch { return $false }
}

Write-Host ''
foreach ($s in $sistemas) {

    if (-not (Test-Path $s.Proyecto)) {
        Write-Host "[omitido] $($s.Nombre): no encuentro $($s.Proyecto)" -ForegroundColor Yellow
        continue
    }

    if (Puerto-Ocupado $s.Puerto) {
        # Ya esta arriba. Arrancar otra vez solo daria un error de puerto en uso.
        Write-Host "[ya arriba] $($s.Nombre) en $($s.Url)" -ForegroundColor DarkGray
        if (-not $SinNavegador) { Start-Process $s.Url }
        continue
    }

    Write-Host "[arrancando] $($s.Nombre)..." -ForegroundColor Cyan

    $argumentos = \('run', '--project', $s.Proyecto)
    if ($s.Perfil) { $argumentos += \('--launch-profile', $s.Perfil) }

    Start-Process -FilePath 'dotnet' -ArgumentList $argumentos -WindowStyle Minimized
}

if (-not $SinNavegador) {
    Write-Host ''
    Write-Host 'Esperando a que respondan antes de abrir el navegador...' -ForegroundColor DarkGray

    foreach ($s in $sistemas) {
        if (-not (Test-Path $s.Proyecto)) { continue }

        # Hasta 60 s: la primera compilacion de un proyecto frio tarda.
        $arriba = $false
        for ($i = 0; $i -lt 60; $i++) {
            if (Puerto-Ocupado $s.Puerto) { $arriba = $true; break }
            Start-Sleep -Seconds 1
        }

        if ($arriba) {
            Write-Host "  [ok] $($s.Nombre) -> $($s.Url)" -ForegroundColor Green
            Start-Process $s.Url
        } else {
            # Se dice, no se calla: un navegador abierto contra un puerto muerto
            # es mas confuso que no abrirlo.
            Write-Host "  [FALLA] $($s.Nombre) no respondio en 60 s. Mire su ventana." -ForegroundColor Red
        }
    }
}

Write-Host ''
Write-Host 'Cada sistema corre en su propia ventana. Ciérrela para detenerlo.' -ForegroundColor DarkGray
