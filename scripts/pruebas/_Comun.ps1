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
$script:SufijoCopia = '_E2E'

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
    if (-not $Base.EndsWith($script:SufijoCopia, [StringComparison]::Ordinal)) {
        throw "NEGADO: '$Base' no termina en '$($script:SufijoCopia)'. Solo se opera sobre copias desechables."
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
