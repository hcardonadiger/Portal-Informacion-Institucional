<#
.SYNOPSIS
    Baja las aplicaciones del entorno de pruebas.

.DESCRIPTION
    Se detiene por puerto, no solo por identificador de proceso. La razon: "dotnet run" no es
    la aplicacion, es quien la lanza; matar al padre puede dejar al hijo escuchando, y un hijo
    huerfano conectado a la copia impide despues borrarla.

    Las bases no se tocan aca. Eso es Borrar-EntornoPruebas.ps1.
#>

[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot '_Comun.ps1')

$estado = Get-CarpetaEstado
$fichaRuta = Join-Path $estado 'entorno.json'
if (-not (Test-Path $fichaRuta)) {
    Write-Aviso 'No hay entorno montado; no hay nada que detener.'
    return
}
$f = Get-Content $fichaRuta -Raw -Encoding UTF8 | ConvertFrom-Json

$puertos = @(
    [int]$f.PuertoPortalHttps, [int]$f.PuertoPortalCert, [int]$f.PuertoPortalHttp,
    [int]$f.PuertoApiHttps,    [int]$f.PuertoApiHttp,
    [int]$f.PuertoAgilHttps,   [int]$f.PuertoAgilHttp
)

Write-Host ''
Write-Paso 'Deteniendo las aplicaciones del entorno de pruebas...'

$muertos = @()
foreach ($p in $puertos) {
    $conexiones = Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue
    foreach ($c in $conexiones) {
        if ($c.OwningProcess -and $muertos -notcontains $c.OwningProcess) {
            try {
                Stop-Process -Id $c.OwningProcess -Force -ErrorAction Stop
                $muertos += $c.OwningProcess
                Write-Ok "puerto $p : proceso $($c.OwningProcess) detenido"
            } catch {
                Write-Aviso "puerto $p : no pude detener el proceso $($c.OwningProcess)"
            }
        }
    }
}

# Los "dotnet run" que quedaron sin hijo escuchando tambien se van.
$procRuta = Join-Path $estado 'procesos.json'
if (Test-Path $procRuta) {
    $lanzados = @(Get-Content $procRuta -Raw -Encoding UTF8 | ConvertFrom-Json)
    foreach ($l in $lanzados) {
        if ($l.Proceso -and $muertos -notcontains $l.Proceso) {
            Stop-Process -Id $l.Proceso -Force -ErrorAction SilentlyContinue
        }
    }
    Remove-Item $procRuta -Force -ErrorAction SilentlyContinue
}

if ($muertos.Count -eq 0) { Write-Aviso 'No habia nada escuchando en los puertos de pruebas.' }
Write-Host ''
