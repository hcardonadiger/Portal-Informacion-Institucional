<#
.SYNOPSIS
    Piezas compartidas por los guiones del entorno de pruebas desechable.

.DESCRIPTION
    No se ejecuta suelto: los demas guiones de esta carpeta lo cargan con dot-sourcing.

    Todo lo que hay aca existe por una sola razon: que sea imposible que una prueba
    escriba en una base real. La barrera no es la buena intencion de quien ejecuta,
    es el sufijo obligatorio y la lista de intocables que se revisan en cada operacion
    destructiva.
#>

$ErrorActionPreference = 'Stop'

# Sufijo obligatorio de toda copia desechable. Ningun guion de esta carpeta crea, sobrescribe
# ni borra una base cuyo nombre no termine exactamente asi. Es la barrera principal.
# Sufijos de copia. Ningun guion de esta carpeta crea, sobrescribe ni borra una base cuyo
# nombre no termine en uno de estos. Es la barrera principal.
#
#   _E2E      copia de usar y tirar, para la corrida que demuestra que nada se toco.
#   _Sandbox  copia permanente. Es a la que apuntan los appsettings de Development, o sea
#             la que se usa al encender el sistema sin mas. Se ensucia sin culpa y se
#             refresca con Refrescar-Sandbox.ps1 cuando estorbe.
$script:SufijoCopia    = '_E2E'
$script:SufijoSandbox  = '_Sandbox'
$script:SufijosCopia   = @('_E2E', '_Sandbox')

# Cinturon ademas del tirante. Aunque alguien renombre una base real terminandola en _E2E,
# estos nombres no se tocan nunca.
$script:BasesIntocables = @(
    'DigerTramitesEstado',
    'DigerTramitesEstado_Unificada',
    'DigerTramitesEstado_Nueva',
    'DigerTramitesEstado_Pruebas',
    'DigerTramitesEstado_v4',
    'GestionGD_TEST',
    'TramitesEstado_Prod',
    'TramitesEstado_Prod_8',
    'TramitesEstado_Ensayo',
    'VentanillaDigital_Net',
    'master', 'model', 'msdb', 'tempdb'
)

# Donde se guarda el estado del entorno: la huella de las bases reales, el inventario de
# archivos subidos y los identificadores de los procesos levantados.
$script:CarpetaEstado = Join-Path $PSScriptRoot '.estado'

function Get-CarpetaEstado {
    if (-not (Test-Path $script:CarpetaEstado)) {
        $null = New-Item -ItemType Directory -Path $script:CarpetaEstado -Force
    }
    return $script:CarpetaEstado
}

function Assert-EsCopiaDesechable {
    <#
    .SYNOPSIS
        Corta la ejecucion si el nombre no es el de una copia desechable.
    .DESCRIPTION
        Se llama ANTES de cualquier RESTORE o DROP. Si esta funcion pasa, el nombre no puede
        ser el de una base real; si no pasa, no hay segunda oportunidad ni parametro que la
        salte, a proposito.
    #>
    param([Parameter(Mandatory)][string] $Base)

    if ($script:BasesIntocables -contains $Base) {
        throw "NEGADO: '$Base' esta en la lista de bases intocables. Este guion no la toca."
    }
    $coincide = $false
    foreach ($s in $script:SufijosCopia) {
        if ($Base.EndsWith($s, [StringComparison]::Ordinal)) { $coincide = $true; break }
    }
    if (-not $coincide) {
        throw "NEGADO: '$Base' no termina en $($script:SufijosCopia -join ' ni '). Solo se opera sobre copias."
    }
}

function Invoke-Sql {
    <#
    .SYNOPSIS
        Ejecuta una consulta con sqlcmd y devuelve las lineas de salida.
    .DESCRIPTION
        -b hace que sqlcmd devuelva codigo de error cuando el motor falla; sin eso un guion
        seguiria adelante creyendo que todo salio bien. -f 65001 es obligatorio: los nombres
        llevan tildes y sin UTF-8 salen rotos.
    #>
    param(
        [Parameter(Mandatory)][string] $Instancia,
        [Parameter(Mandatory)][string] $Consulta,
        [int] $TimeoutSegundos = 0
    )

    $salida = & sqlcmd -S $Instancia -E -b -h -1 -W -s '|' -f 65001 -t $TimeoutSegundos -Q $Consulta 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd fallo en $Instancia :`n$($salida -join [Environment]::NewLine)"
    }
    return $salida
}

function Test-BaseExiste {
    param(
        [Parameter(Mandatory)][string] $Instancia,
        [Parameter(Mandatory)][string] $Base
    )
    $r = Invoke-Sql -Instancia $Instancia -Consulta "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$Base') IS NULL THEN 'NO' ELSE 'SI' END;"
    return (($r | Where-Object { $_ -match '^(SI|NO)$' } | Select-Object -First 1) -eq 'SI')
}

function Get-HuellaBase {
    <#
    .SYNOPSIS
        Toma una huella barata de una base: filas por tabla y cuantas escrituras lleva el motor.
    .DESCRIPTION
        Sirve para demostrar, no para suponer, que las pruebas no tocaron la base real.

        Dos medidas complementarias:
          - filas por tabla: detecta altas y bajas.
          - user_updates de sys.dm_db_index_usage_stats: es el contador de operaciones de
            escritura del propio motor. Detecta ademas las modificaciones, que el conteo de
            filas no ve. Ojo: se reinicia cuando se reinicia el servicio de SQL Server, asi
            que solo vale comparado dentro de una misma sesion del motor.
    #>
    param(
        [Parameter(Mandatory)][string] $Instancia,
        [Parameter(Mandatory)][string] $Base
    )

    $consulta = @"
SET NOCOUNT ON;
-- CONVERT + COLLATE explicito: sys.tables.name viene con la intercalacion de la base y
-- concatenarlo con un literal da Msg 451 en las bases con intercalacion mezclada.
SELECT CONVERT(nvarchar(300), t.name) COLLATE Latin1_General_CI_AS + N'|' + CONVERT(nvarchar(20), SUM(p.rows))
FROM   sys.tables t
JOIN   sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
WHERE  t.is_ms_shipped = 0
GROUP  BY t.name
ORDER  BY t.name;
SELECT '__ESCRITURAS__|' + CONVERT(varchar(20), ISNULL(SUM(user_updates), 0))
FROM   sys.dm_db_index_usage_stats
WHERE  database_id = DB_ID();
SELECT '__ARRANQUE_MOTOR__|' + CONVERT(varchar(30), sqlserver_start_time, 126)
FROM   sys.dm_os_sys_info;
"@

    $lineas = Invoke-Sql -Instancia $Instancia -Consulta "USE [$Base]; $consulta"
    return @($lineas | Where-Object { $_ -match '\|' } | ForEach-Object { $_.Trim() })
}

function Get-InventarioArchivos {
    <#
    .SYNOPSIS
        Lista los archivos que ya existian bajo App_Data\uploads antes de las pruebas.
    .DESCRIPTION
        Las pruebas que suben un adjunto dejan basura en disco, no solo en la base. Con este
        inventario, al desmontar se puede borrar exactamente lo nuevo y nada mas.
    #>
    param([Parameter(Mandatory)][string] $Raiz)

    if (-not (Test-Path $Raiz)) { return @() }
    return @(Get-ChildItem -Path $Raiz -Recurse -File -ErrorAction SilentlyContinue |
             ForEach-Object { $_.FullName })
}

function Write-Paso  { param([string]$Texto) Write-Host "  $Texto" -ForegroundColor Cyan }
function Write-Ok    { param([string]$Texto) Write-Host "  [ok] $Texto" -ForegroundColor Green }
function Write-Aviso { param([string]$Texto) Write-Host "  [aviso] $Texto" -ForegroundColor Yellow }
function Write-Malo  { param([string]$Texto) Write-Host "  [FALLA] $Texto" -ForegroundColor Red }

function Get-PropiedadServidor {
    param(
        [Parameter(Mandatory)][string] $Instancia,
        [Parameter(Mandatory)][string] $Propiedad
    )
    $r = Invoke-Sql -Instancia $Instancia -Consulta "SET NOCOUNT ON; SELECT CONVERT(nvarchar(400), SERVERPROPERTY('$Propiedad'));"
    return ($r | Where-Object { $_ -match '\S' } | Select-Object -First 1).Trim()
}

function New-CopiaDeBase {
    <#
    .SYNOPSIS
        Copia una base real a otra con nombre nuevo, sin tocar la original.
    .DESCRIPTION
        La usan tanto el entorno desechable (_E2E) como el sandbox permanente (_Sandbox).
        El respaldo es COPY_ONLY a proposito: un respaldo normal reinicia la base diferencial
        de la base real, y copiarla no tiene por que cambiarle nada, ni su cadena de respaldos.
    #>
    param(
        [Parameter(Mandatory)][string] $Instancia,
        [Parameter(Mandatory)][string] $Origen,
        [Parameter(Mandatory)][string] $Copia,
        [switch] $Rehacer
    )

    # La barrera, justo antes de la operacion destructiva. Es barata y es el punto que importa.
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

    $rutaRespaldo = Get-PropiedadServidor -Instancia $Instancia -Propiedad 'InstanceDefaultBackupPath'
    $rutaDatos    = Get-PropiedadServidor -Instancia $Instancia -Propiedad 'InstanceDefaultDataPath'
    $bak          = Join-Path $rutaRespaldo ($Copia + '_origen.bak')

    Write-Paso "Respaldando $Origen (COPY_ONLY)..."
    $sqlBackup = "BACKUP DATABASE [$Origen] TO DISK = N'$bak' WITH COPY_ONLY, INIT, FORMAT, STATS = 25;"
    Invoke-Sql -Instancia $Instancia -TimeoutSegundos 900 -Consulta $sqlBackup | Out-Null

    # Los nombres logicos de la copia son los mismos que los del origen: se leen de ahi y se
    # arma el MOVE. Asi no hay que interpretar RESTORE FILELISTONLY, cuyas columnas cambian
    # entre versiones de SQL Server.
    # El COLLATE explicito no es cosmetico: sys.master_files devuelve name y type_desc con
    # intercalaciones distintas y concatenarlas sin mas da el Msg 451.
    $col = 'COLLATE Latin1_General_CI_AS'
    $sqlArchivos = "SET NOCOUNT ON; SELECT CONVERT(nvarchar(200), mf.name) $col + N'|' + CONVERT(nvarchar(60), mf.type_desc) $col FROM sys.master_files mf WHERE mf.database_id = DB_ID(N'$Origen') ORDER BY mf.file_id;"
    $archivos = Invoke-Sql -Instancia $Instancia -Consulta $sqlArchivos | Where-Object { $_ -match '\|' }

    $moves = foreach ($a in $archivos) {
        $partes  = $a.Trim().Split('|')
        $ext     = if ($partes[1] -eq 'LOG') { '_log.ldf' } else { '.mdf' }
        $destino = Join-Path $rutaDatos ($Copia + '_' + $partes[0] + $ext)
        "MOVE N'$($partes[0])' TO N'$destino'"
    }

    Write-Paso "Restaurando como $Copia..."
    $sqlRestore = "RESTORE DATABASE [$Copia] FROM DISK = N'$bak' WITH " + ($moves -join ', ') + ", REPLACE, RECOVERY, STATS = 25; ALTER DATABASE [$Copia] SET RECOVERY SIMPLE; ALTER DATABASE [$Copia] SET MULTI_USER;"
    Invoke-Sql -Instancia $Instancia -TimeoutSegundos 900 -Consulta $sqlRestore | Out-Null

    # El .bak intermedio ocupa lo mismo que la base y ya no hace falta.
    Remove-Item -Path $bak -Force -ErrorAction SilentlyContinue

    Write-Ok "$Copia lista"
}
