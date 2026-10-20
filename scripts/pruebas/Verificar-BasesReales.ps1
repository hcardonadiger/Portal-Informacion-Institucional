<#
.SYNOPSIS
    Comprueba que las bases reales siguen exactamente como estaban antes de las pruebas.

.DESCRIPTION
    Es la parte que convierte "no deberia haberla tocado" en "no la toco". Compara la huella
    que tomo Nuevo-EntornoPruebas.ps1 contra la de ahora.

    Dos medidas:
      - filas por tabla: ve altas y bajas.
      - contador de escrituras del motor (user_updates): ve ademas las modificaciones, que el
        conteo de filas no distingue.

    Si el servicio de SQL Server se reinicio entre medias, el contador de escrituras vuelve a
    cero y deja de servir; el guion lo detecta y lo dice, en vez de dar un falso verde.
#>

[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot '_Comun.ps1')

$estado = Get-CarpetaEstado
$fichaRuta = Join-Path $estado 'entorno.json'
if (-not (Test-Path $fichaRuta)) {
    throw "No hay huella con que comparar. Se toma al ejecutar .\Nuevo-EntornoPruebas.ps1"
}
$f = Get-Content $fichaRuta -Raw -Encoding UTF8 | ConvertFrom-Json

$bases = @(
    [pscustomobject]@{ Nombre = $f.BasePortalReal;     Alias = 'portal' }
    [pscustomobject]@{ Nombre = $f.BaseVentanillaReal; Alias = 'ventanilla' }
)

Write-Host ''
Write-Host 'Comprobando que las bases reales estan intactas' -ForegroundColor White
Write-Host ''

$todoBien = $true

foreach ($b in $bases) {
    $rutaAntes = Join-Path $estado "huella-antes-$($b.Alias).txt"
    if (-not (Test-Path $rutaAntes)) {
        Write-Aviso "$($b.Nombre): no hay huella previa; no puedo comparar."
        $todoBien = $false
        continue
    }

    $antes  = @(Get-Content $rutaAntes -Encoding UTF8)
    $ahora  = Get-HuellaBase -Instancia $f.Instancia -Base $b.Nombre
    $ahora | Set-Content -Path (Join-Path $estado "huella-despues-$($b.Alias).txt") -Encoding UTF8

    $arranqueAntes = ($antes | Where-Object { $_ -like '__ARRANQUE_MOTOR__*' }) -join ''
    $arranqueAhora = ($ahora | Where-Object { $_ -like '__ARRANQUE_MOTOR__*' }) -join ''
    $motorReiniciado = $arranqueAntes -ne $arranqueAhora

    # Si el motor se reinicio, el contador de escrituras arranco de cero y compararlo daria
    # una diferencia que no significa nada. Se saca de la comparacion y se avisa.
    if ($motorReiniciado) {
        $antes = $antes | Where-Object { $_ -notlike '__ESCRITURAS__*' -and $_ -notlike '__ARRANQUE_MOTOR__*' }
        $ahora = $ahora | Where-Object { $_ -notlike '__ESCRITURAS__*' -and $_ -notlike '__ARRANQUE_MOTOR__*' }
    }

    $diferencias = @(Compare-Object -ReferenceObject $antes -DifferenceObject $ahora)

    if ($diferencias.Count -eq 0) {
        if ($motorReiniciado) {
            Write-Aviso "$($b.Nombre): sin cambios en las filas, pero SQL Server se reinicio y el contador de escrituras ya no sirve de prueba."
        } else {
            Write-Ok "$($b.Nombre): intacta. Ni una fila ni una escritura."
        }
    } else {
        $todoBien = $false
        Write-Malo "$($b.Nombre): HAY DIFERENCIAS"
        foreach ($d in $diferencias) {
            $signo = if ($d.SideIndicator -eq '<=') { 'antes ' } else { 'ahora ' }
            Write-Host "        $signo $($d.InputObject)" -ForegroundColor Red
        }
    }
}

Write-Host ''
if ($todoBien) {
    Write-Host '  Las bases reales no se tocaron.' -ForegroundColor Green
} else {
    Write-Host '  Revise las diferencias antes de fusionar nada con la base de TEST.' -ForegroundColor Red
    Write-Host '  Ojo: si tuvo SSMS o Visual Studio escribiendo en la base real mientras probaba,' -ForegroundColor DarkGray
    Write-Host '  la diferencia puede ser suya y no de las pruebas.' -ForegroundColor DarkGray
}
Write-Host ''
