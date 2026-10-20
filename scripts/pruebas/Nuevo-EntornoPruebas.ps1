<#
.SYNOPSIS
    Monta un entorno de pruebas desechable: copias de las bases reales, y nada mas.

.DESCRIPTION
    El problema que resuelve: hay que probar el aplicativo de verdad -entrando, guardando,
    publicando- pero la base unificada tiene que poder fusionarse despues con la de TEST, y
    una prueba deja rastro que no se limpia con un DELETE: bitacoras, historial, sellos de
    UpdatedAt y UpdatedBy.

    La salida es no limpiar: se prueba sobre una copia que despues se destruye entera.

    Este guion hace tres cosas, en este orden:

      1. Toma una huella de las bases REALES (filas por tabla y contador de escrituras del
         motor) y guarda un inventario de los archivos ya subidos. Con eso, al terminar se
         puede demostrar que no se toco nada, en vez de suponerlo.

      2. Respalda las bases reales con COPY_ONLY -que no altera la cadena de respaldos- y
         las restaura con nombre nuevo terminado en _E2E.

      3. Deja escrito en .estado\entorno.json todo lo que necesitan los demas guiones.

    No modifica ningun appsettings. Las aplicaciones se apuntan a las copias por variables
    de entorno, que mueren con la ventana; un archivo de configuracion editado sobrevive y
    se olvida.

.EXAMPLE
    .\Nuevo-EntornoPruebas.ps1
    .\Nuevo-EntornoPruebas.ps1 -Rehacer
#>

[CmdletBinding()]
param(
    [string] $Instancia = 'LP-GD-JAGM\SQLEXPRESS',

    [string] $BasePortalReal     = 'DigerTramitesEstado_Unificada',
    [string] $BaseVentanillaReal = 'VentanillaDigital_Net',

    [string] $RepoPortal     = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [string] $RepoVentanilla = 'C:\Users\jgarcia\Documents\honduras-agil',

    [switch] $Rehacer
)

. (Join-Path $PSScriptRoot '_Comun.ps1')

$BasePortalCopia     = $BasePortalReal + $script:SufijoCopia
$BaseVentanillaCopia = $BaseVentanillaReal + $script:SufijoCopia

Assert-EsCopiaDesechable $BasePortalCopia
Assert-EsCopiaDesechable $BaseVentanillaCopia

Write-Host ''
Write-Host 'Montando entorno de pruebas desechable' -ForegroundColor White
Write-Host "  instancia .......... $Instancia"
Write-Host "  $BasePortalReal -> $BasePortalCopia"
Write-Host "  $BaseVentanillaReal -> $BaseVentanillaCopia"
Write-Host ''

$estado = Get-CarpetaEstado

# ---------------------------------------------------------------------------------------
# 1. Huella de las bases reales, antes de tocar nada
# ---------------------------------------------------------------------------------------
Write-Paso 'Tomando huella de las bases reales...'

$reales = @(
    [pscustomobject]@{ Nombre = $BasePortalReal;     Alias = 'portal' }
    [pscustomobject]@{ Nombre = $BaseVentanillaReal; Alias = 'ventanilla' }
)

foreach ($r in $reales) {
    if (-not (Test-BaseExiste -Instancia $Instancia -Base $r.Nombre)) {
        throw "No existe la base real '$($r.Nombre)' en $Instancia. Revise el parametro."
    }
    $huella = Get-HuellaBase -Instancia $Instancia -Base $r.Nombre
    Set-Content -Path (Join-Path $estado "huella-antes-$($r.Alias).txt") -Value ([string[]]$huella) -Encoding UTF8
    Write-Ok "$($r.Nombre): $($huella.Count) medidas guardadas"
}

# ---------------------------------------------------------------------------------------
# 2. Inventario de archivos subidos, para poder borrar despues solo lo nuevo
# ---------------------------------------------------------------------------------------
Write-Paso 'Inventariando archivos ya subidos...'

$carpetas = @(
    [pscustomobject]@{ Raiz = (Join-Path $RepoPortal     'src\Web\App_Data\uploads'); Alias = 'portal' }
    [pscustomobject]@{ Raiz = (Join-Path $RepoVentanilla 'src\Web\App_Data\uploads'); Alias = 'ventanilla' }
)

foreach ($c in $carpetas) {
    $inv = Get-InventarioArchivos -Raiz $c.Raiz
    # -Value y no tuberia: una lista vacia por tuberia no crea el archivo, y sin archivo
    # el desmontaje se abstiene de borrar y deja la basura puesta.
    Set-Content -Path (Join-Path $estado "archivos-antes-$($c.Alias).txt") -Value ([string[]]$inv) -Encoding UTF8
    Write-Ok "$($c.Alias): $($inv.Count) archivos ya existentes"
}

# ---------------------------------------------------------------------------------------
# 3. Respaldo COPY_ONLY y restauracion con nombre nuevo
# ---------------------------------------------------------------------------------------
New-CopiaDeBase -Instancia $Instancia -Origen $BasePortalReal     -Copia $BasePortalCopia     -Rehacer:$Rehacer
New-CopiaDeBase -Instancia $Instancia -Origen $BaseVentanillaReal -Copia $BaseVentanillaCopia -Rehacer:$Rehacer

# ---------------------------------------------------------------------------------------
# 4. Ficha del entorno para los demas guiones
# ---------------------------------------------------------------------------------------
# Puertos distintos a los de desarrollo (49175 / 7199 / 7180) para que el entorno de pruebas
# conviva con el normal sin que nadie se confunda de ventana.
$ficha = [ordered]@{
    Instancia           = $Instancia
    BasePortalReal      = $BasePortalReal
    BaseVentanillaReal  = $BaseVentanillaReal
    BasePortalCopia     = $BasePortalCopia
    BaseVentanillaCopia = $BaseVentanillaCopia
    RepoPortal          = $RepoPortal
    RepoVentanilla      = $RepoVentanilla
    PuertoPortalHttps   = 49185
    PuertoPortalCert    = 49186
    PuertoPortalHttp    = 49187
    PuertoApiHttps      = 7299
    PuertoApiHttp       = 5299
    PuertoAgilHttps     = 7280
    PuertoAgilHttp      = 5280
    ClaveApi            = 'clave-de-pruebas-e2e'
}
$ficha | ConvertTo-Json | Set-Content -Path (Join-Path $estado 'entorno.json') -Encoding UTF8

Write-Host ''
Write-Ok 'Entorno montado.'
Write-Host ''
Write-Host '  Siguiente paso:  .\Iniciar-EntornoPruebas.ps1' -ForegroundColor White
Write-Host '  Al terminar:     .\Borrar-EntornoPruebas.ps1' -ForegroundColor White
Write-Host ''
