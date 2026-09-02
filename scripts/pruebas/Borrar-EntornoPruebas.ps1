<#
.SYNOPSIS
    Destruye el entorno de pruebas: procesos, archivos nuevos y copias de las bases.

.DESCRIPTION
    Este es el guion que cumple la promesa de "sin data basura". No limpia filas una por una
    -eso nunca alcanza, porque una prueba tambien deja bitacora, historial y sellos de quien
    modifico y cuando- sino que destruye la base entera.

    En orden:
      1. Baja las aplicaciones, para que nadie quede conectado a las copias.
      2. Comprueba que las bases reales siguen intactas, y lo dice.
      3. Borra los archivos que las pruebas subieron a App_Data\uploads, y solo esos: se
         comparan contra el inventario tomado al montar el entorno.
      4. Quita las copias y sus archivos de datos.

    Nunca borra una base que no termine en _E2E ni una que este en la lista de intocables.

.EXAMPLE
    .\Borrar-EntornoPruebas.ps1
    .\Borrar-EntornoPruebas.ps1 -ConservarArchivos
#>

[CmdletBinding()]
param(
    # Deja en disco lo que las pruebas subieron, por si hay que mirar un adjunto.
    [switch] $ConservarArchivos
)

. (Join-Path $PSScriptRoot '_Comun.ps1')

$estado = Get-CarpetaEstado
$fichaRuta = Join-Path $estado 'entorno.json'
if (-not (Test-Path $fichaRuta)) {
    Write-Aviso 'No hay entorno montado; no hay nada que borrar.'
    return
}
$f = Get-Content $fichaRuta -Raw -Encoding UTF8 | ConvertFrom-Json

Write-Host ''
Write-Host 'Desmontando el entorno de pruebas' -ForegroundColor White

# 1. Procesos
& (Join-Path $PSScriptRoot 'Detener-EntornoPruebas.ps1')

# 2. Comprobacion de las bases reales, antes de borrar la evidencia
& (Join-Path $PSScriptRoot 'Verificar-BasesReales.ps1')

# 3. Archivos que subieron las pruebas
if (-not $ConservarArchivos) {
    Write-Paso 'Borrando los archivos que subieron las pruebas...'

    $carpetas = @(
        [pscustomobject]@{ Raiz = (Join-Path $f.RepoPortal     'src\Web\App_Data\uploads'); Alias = 'portal' }
        [pscustomobject]@{ Raiz = (Join-Path $f.RepoVentanilla 'src\Web\App_Data\uploads'); Alias = 'ventanilla' }
    )

    foreach ($c in $carpetas) {
        $rutaInv = Join-Path $estado "archivos-antes-$($c.Alias).txt"
        if (-not (Test-Path $rutaInv)) {
            Write-Aviso "$($c.Alias): sin inventario previo; no borro nada por las dudas."
            continue
        }
        $antes  = @(Get-Content $rutaInv -Encoding UTF8)
        $ahora  = Get-InventarioArchivos -Raiz $c.Raiz
        $nuevos = @($ahora | Where-Object { $antes -notcontains $_ })

        foreach ($n in $nuevos) { Remove-Item -Path $n -Force -ErrorAction SilentlyContinue }

        # Las carpetas que quedaron vacias tambien sobran.
        if (Test-Path $c.Raiz) {
            Get-ChildItem -Path $c.Raiz -Recurse -Directory -ErrorAction SilentlyContinue |
                Sort-Object { $_.FullName.Length } -Descending |
                Where-Object { -not (Get-ChildItem -Path $_.FullName -Force -ErrorAction SilentlyContinue) } |
                Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
        }

        if ($nuevos.Count -gt 0) { Write-Ok "$($c.Alias): $($nuevos.Count) archivos nuevos borrados" }
        else                     { Write-Ok "$($c.Alias): no habia archivos nuevos" }
    }
}

# 4. Las copias
Write-Paso 'Quitando las copias de las bases...'

foreach ($copia in @($f.BasePortalCopia, $f.BaseVentanillaCopia)) {

    # La barrera, justo antes del DROP. Si el nombre no es de una copia desechable, se corta
    # aca y no se borra nada.
    Assert-EsCopiaDesechable $copia

    if (-not (Test-BaseExiste -Instancia $f.Instancia -Base $copia)) {
        Write-Aviso "$copia : ya no estaba."
        continue
    }

    # Los archivos hay que anotarlos antes del DROP: despues ya no hay a quien preguntarle.
    $consultaArchivos = "SET NOCOUNT ON; SELECT physical_name FROM sys.master_files WHERE database_id = DB_ID(N'$copia');"
    $fisicos = @(Invoke-Sql -Instancia $f.Instancia -Consulta $consultaArchivos |
                 Where-Object { $_ -match '\S' } | ForEach-Object { $_.Trim() })

    $sqlDrop = "ALTER DATABASE [$copia] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$copia];"
    Invoke-Sql -Instancia $f.Instancia -Consulta $sqlDrop | Out-Null

    # DROP DATABASE normalmente ya se lleva los archivos; si el motor los dejo, se van aca.
    foreach ($fis in $fisicos) { Remove-Item -Path $fis -Force -ErrorAction SilentlyContinue }

    Write-Ok "$copia borrada"
}

# El .bak intermedio, si un fallo a medio camino lo dejo tirado.
$rutaRespaldo = (Invoke-Sql -Instancia $f.Instancia -Consulta "SET NOCOUNT ON; SELECT CONVERT(nvarchar(400), SERVERPROPERTY('InstanceDefaultBackupPath'));" |
                 Where-Object { $_ -match '\S' } | Select-Object -First 1).Trim()
foreach ($copia in @($f.BasePortalCopia, $f.BaseVentanillaCopia)) {
    Remove-Item -Path (Join-Path $rutaRespaldo ($copia + '_origen.bak')) -Force -ErrorAction SilentlyContinue
}

# La ficha se va: sin ella, ningun guion de esta carpeta cree que hay un entorno vivo.
Remove-Item -Path $fichaRuta -Force -ErrorAction SilentlyContinue

Write-Host ''
Write-Host '  Entorno desmontado. No queda ninguna base _E2E.' -ForegroundColor Green
Write-Host '  Para volver a probar:  .\Nuevo-EntornoPruebas.ps1' -ForegroundColor White
Write-Host ''
