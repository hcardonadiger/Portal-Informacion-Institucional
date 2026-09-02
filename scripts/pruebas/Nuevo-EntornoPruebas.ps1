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
function Get-PropiedadServidor {
    param([string] $Propiedad)
    $r = Invoke-Sql -Instancia $Instancia -Consulta "SET NOCOUNT ON; SELECT CONVERT(nvarchar(400), SERVERPROPERTY('$Propiedad'));"
    return ($r | Where-Object { $_ -match '\S' } | Select-Object -First 1).Trim()
}

function New-CopiaDesechable {
    param(
        [Parameter(Mandatory)][string] $Origen,
        [Parameter(Mandatory)][string] $Copia
    )

    # Otra vez, justo antes de la operacion destructiva. Es barato y es el punto que importa.
    Assert-EsCopiaDesechable $Copia

    if (Test-BaseExiste -Instancia $Instancia -Base $Copia) {
        if (-not $Rehacer) {
            Write-Aviso "$Copia ya existe. Use -Rehacer si la quiere rehacer desde cero."
            return
        }
        Write-Paso "Quitando la copia anterior $Copia..."
        $sqlDrop = "ALTER DATABASE [$Copia] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Copia];"
        Invoke-Sql -Instancia $Instancia -Consulta $sqlDrop | Out-Null
    }

    $rutaRespaldo = Get-PropiedadServidor 'InstanceDefaultBackupPath'
    $rutaDatos    = Get-PropiedadServidor 'InstanceDefaultDataPath'
    $bak          = Join-Path $rutaRespaldo ($Copia + '_origen.bak')

    # COPY_ONLY a proposito: un respaldo normal reinicia la base diferencial de la base real.
    # Copiarla no puede cambiarle nada, ni siquiera su cadena de respaldos.
    Write-Paso "Respaldando $Origen (COPY_ONLY)..."
    $sqlBackup = "BACKUP DATABASE [$Origen] TO DISK = N'$bak' WITH COPY_ONLY, INIT, FORMAT, STATS = 25;"
    Invoke-Sql -Instancia $Instancia -TimeoutSegundos 900 -Consulta $sqlBackup | Out-Null

    # Los nombres logicos de la copia son los mismos que los del origen: se leen de ahi y se
    # arma el MOVE. Asi no hay que interpretar RESTORE FILELISTONLY, cuyas columnas cambian
    # entre versiones de SQL Server.
    # COLLATE explicito: sys.master_files devuelve name y type_desc con intercalaciones
    # distintas, y concatenarlas sin mas da el Msg 451. No es cosmetico: revienta el guion.
    $colSis = 'COLLATE Latin1_General_CI_AS'
    $sqlArchivos = "SET NOCOUNT ON; SELECT CONVERT(nvarchar(200), mf.name) $colSis + N'|' + CONVERT(nvarchar(60), mf.type_desc) $colSis FROM sys.master_files mf WHERE mf.database_id = DB_ID(N'$Origen') ORDER BY mf.file_id;"
    $archivos = Invoke-Sql -Instancia $Instancia -Consulta $sqlArchivos | Where-Object { $_ -match '\|' }

    $moves = foreach ($a in $archivos) {
        $partes  = $a.Trim().Split('|')
        $logico  = $partes[0]
        $ext     = if ($partes[1] -eq 'LOG') { '_log.ldf' } else { '.mdf' }
        $destino = Join-Path $rutaDatos ($Copia + '_' + $logico + $ext)
        "MOVE N'$logico' TO N'$destino'"
    }

    Write-Paso "Restaurando como $Copia..."
    $sqlRestore = "RESTORE DATABASE [$Copia] FROM DISK = N'$bak' WITH " + ($moves -join ', ') + ", REPLACE, RECOVERY, STATS = 25; ALTER DATABASE [$Copia] SET RECOVERY SIMPLE; ALTER DATABASE [$Copia] SET MULTI_USER;"
    Invoke-Sql -Instancia $Instancia -TimeoutSegundos 900 -Consulta $sqlRestore | Out-Null

    # El .bak intermedio ocupa lo mismo que la base y ya no hace falta.
    Remove-Item -Path $bak -Force -ErrorAction SilentlyContinue

    Write-Ok "$Copia lista"
}

New-CopiaDesechable -Origen $BasePortalReal     -Copia $BasePortalCopia
New-CopiaDesechable -Origen $BaseVentanillaReal -Copia $BaseVentanillaCopia

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
