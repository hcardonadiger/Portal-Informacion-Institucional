<#
.SYNOPSIS
    Convierte una plantilla llena en un script SQL idempotente de carga de proyectos.

.DESCRIPTION
    Valida primero y genera después: si algo no cuadra —una Ref huérfana, un correo que no
    existe, un estado mal escrito— no escribe el .sql y reporta hoja, fila y columna. Es
    deliberado: un script a medias contra la base de producción cuesta más caro que volver a
    pedirle el archivo al área.

    El SQL que produce se puede correr varias veces. Los proyectos se identifican por Nombre,
    los hitos por proyecto + nombre, los interesados por proyecto + nombre y los riesgos por
    proyecto + descripción; lo que ya existe se actualiza en vez de duplicarse.

    A diferencia de los scripts de carga anteriores, este sí escribe en BitacoraProyecto: una
    carga masiva que no deja rastro es indistinguible de una edición manual.

    2026-08-25 — LO QUE CAMBIÓ CON LA EDT:
    · La hoja «Hitos» de la plantilla carga lo que el portal ahora llama ENTREGABLES, en la tabla
      ProyectoEntregables. La hoja conserva su nombre para no invalidar las plantillas ya
      repartidas; los datos y las columnas son los mismos.
    · La plantilla NO carga actividades. El nivel de actividad —con fechas de inicio y fin y su
      porcentaje— se captura en la ficha del proyecto, en el portal.
    · La columna «Avance» del Excel se escribe como valor inicial, pero ya no manda: el portal
      calcula el avance del proyecto desde su estructura y lo recalcula en cuanto alguien toca un
      entregable o reporta una actividad. Un entregable sin actividades vale por su estado
      (pendiente 0 %, en proceso 50 %, cumplido 100 %), así que lo que se carga acá es coherente
      con lo que el portal va a mostrar mientras nadie desglose el proyecto.

.EXAMPLE
    .\Generar-SQL-Proyectos.ps1 -Archivo 'C:\temp\proyectos_gobdig.xlsx'
    .\Generar-SQL-Proyectos.ps1 -Archivo '.\llenada.xlsx' -Usuario sa -Clave '***' -Actor 'Henry Ortez'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Archivo,
    [string] $Servidor = 'localhost',
    [string] $BaseDatos = 'DigerTramitesEstado',
    [string] $Usuario,
    [string] $Clave,
    [string] $Actor = 'Carga masiva (plantilla Excel)',
    [string] $Salida
)

$ErrorActionPreference = 'Stop'

$rutaEntrada = (Resolve-Path $Archivo).Path
if (-not $Salida) {
    $Salida = Join-Path (Split-Path -Parent $PSScriptRoot) ("carga_proyectos_{0}.sql" -f (Get-Date -Format 'yyyyMMdd_HHmm'))
}

# ── Catálogos contra los que se valida ───────────────────────────────────────
function Get-Catalogo([string] $Consulta) {
    $sqlArgs = @('-S', $Servidor, '-d', $BaseDatos, '-h', '-1', '-W', '-s', '|', '-Q', "SET NOCOUNT ON; $Consulta")
    if ($Usuario) { $sqlArgs += @('-U', $Usuario, '-P', $Clave) } else { $sqlArgs += '-E' }
    $r = & sqlcmd.exe @sqlArgs
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd falló: $r" }
    $r | Where-Object { $_ -and $_.Trim() -and $_ -notmatch '^\(\d+ (rows|filas)' } | ForEach-Object { $_.Trim() }
}

Write-Host 'Leyendo catálogos...' -ForegroundColor Cyan
$instituciones = @{}; Get-Catalogo "SELECT Id FROM Instituciones" | ForEach-Object { $instituciones[$_] = $true }
$areas         = @{}; Get-Catalogo "SELECT Id FROM Areas"          | ForEach-Object { $areas[$_] = $true }
$unidades      = @{}; Get-Catalogo "SELECT Id FROM Unidades"       | ForEach-Object { $unidades[$_] = $true }

$usuarios = @{}
Get-Catalogo "SELECT LOWER(Correo) + '|' + CAST(Id AS nvarchar(50)) + '|' + Nombre FROM Usuarios WHERE Activo = 1" | ForEach-Object {
    $p = $_ -split '\|'
    if ($p.Count -ge 3) { $usuarios[$p[0]] = @{ Id = $p[1]; Nombre = ($p[2..($p.Count - 1)] -join '|') } }
}

$enums = @{
    Prioridad    = @('Alta', 'Media', 'Baja')
    EstadoProy   = @('Planificado', 'EnEjecucion', 'Suspendido', 'Cerrado', 'Cancelado')
    EstadoHito   = @('Pendiente', 'EnProceso', 'Completado', 'Cancelado')
    Nivel        = @('Alta', 'Media', 'Baja')
    RolInt       = @('Patrocinador', 'Ejecutor', 'ContraparteTecnica', 'Beneficiario', 'Regulador')
    CatRiesgo    = @('Tecnico', 'Institucional', 'Normativo', 'Financiero', 'Operativo', 'Externo')
    Estrategia   = @('Evitar', 'Mitigar', 'Transferir', 'Aceptar')
    EstadoRiesgo = @('Abierto', 'EnTratamiento', 'Materializado', 'Cerrado')
}

# ── Lectura del libro ────────────────────────────────────────────────────────
$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$libro = $excel.Workbooks.Open($rutaEntrada, 0, $true)

<# Devuelve las filas de una hoja como hashtables indexadas por encabezado.
   Se lee el rango completo de un tirón: celda por celda vía COM son ~50 ms cada una y una
   plantilla de 30 proyectos con hitos tarda minutos. #>
function Read-Hoja([string] $Nombre) {
    $ws = $libro.Worksheets | Where-Object { $_.Name -eq $Nombre }
    if (-not $ws) { throw "La plantilla no tiene la hoja «$Nombre». ¿Se renombró?" }

    $ultima = $ws.Cells.Item($ws.Rows.Count, 1).End(-4162).Row   # xlUp desde el fondo de la col A
    $nCols  = $ws.UsedRange.Columns.Count
    if ($ultima -lt 4) { return @() }

    $encabezados = @()
    for ($c = 1; $c -le $nCols; $c++) {
        $h = $ws.Cells.Item(3, $c).Value2
        $encabezados += ("$h" -replace '\s*\*\s*$', '').Trim()
    }

    $datos = $ws.Range($ws.Cells.Item(4, 1), $ws.Cells.Item($ultima, $nCols)).Value2
    $filas = @()
    for ($r = 4; $r -le $ultima; $r++) {
        $fila = @{ __fila = $r; __hoja = $Nombre }
        $vacia = $true
        for ($c = 1; $c -le $nCols; $c++) {
            $v = if ($ultima -eq 4 -and $nCols -eq 1) { $datos } else { $datos[($r - 3), $c] }
            $fila[$encabezados[$c - 1]] = $v
            if ($null -ne $v -and "$v".Trim()) { $vacia = $false }
        }
        if (-not $vacia) { $filas += $fila }
    }
    return $filas
}

Write-Host 'Leyendo la plantilla...' -ForegroundColor Cyan
$filasProy = Read-Hoja 'Proyectos'
$filasHito = Read-Hoja 'Hitos'
$filasInt  = Read-Hoja 'Interesados'
$filasRie  = Read-Hoja 'Riesgos'

$libro.Close($false)
$excel.Quit()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
[GC]::Collect(); [GC]::WaitForPendingFinalizers()

# ── Normalización ────────────────────────────────────────────────────────────
function Get-Texto($v) {
    if ($null -eq $v) { return $null }
    $s = "$v".Trim()
    if (-not $s) { return $null }
    return $s
}

# «GOBDIG — GOBIERNO DIGITAL» y «correo@x — Nombre» comparten formato: interesa lo de antes del guión.
function Get-Codigo($v) {
    $s = Get-Texto $v
    if (-not $s) { return $null }
    return ($s -split '\s+[—-]\s+')[0].Trim()
}

# Ojo: el parámetro no se puede llamar $Error — es automática en PowerShell.
function Get-Fecha($v, [ref] $Mensaje) {
    if ($null -eq $v) { return $null }
    if ($v -is [double]) { return [DateTime]::FromOADate($v).ToString('yyyy-MM-dd') }
    $s = "$v".Trim()
    if (-not $s) { return $null }
    [datetime] $d = [datetime]::MinValue
    if ([datetime]::TryParse($s, [ref] $d)) { return $d.ToString('yyyy-MM-dd') }
    $Mensaje.Value = "fecha ilegible «$s»"
    return $null
}

function Get-Entero($v) {
    if ($null -eq $v) { return $null }
    $s = "$v".Trim() -replace '%', ''
    if (-not $s) { return $null }
    [int] $n = 0
    if ([int]::TryParse($s, [ref] $n)) { return $n }
    return $null
}

# ── Validación ───────────────────────────────────────────────────────────────
$errores = @()
function Add-Error($Fila, [string] $Columna, [string] $Mensaje) {
    $script:errores += [pscustomobject]@{
        Hoja = $Fila.__hoja; Fila = $Fila.__fila; Columna = $Columna; Problema = $Mensaje
    }
}

function Test-Enum($Fila, [string] $Columna, [string] $Lista, [switch] $Obligatorio) {
    $v = Get-Texto $Fila[$Columna]
    if (-not $v) {
        if ($Obligatorio) { Add-Error $Fila $Columna 'obligatorio y vino vacío' }
        return $null
    }
    # Se devuelve el valor del catálogo, no el que escribió el usuario: la comparación de
    # PowerShell ignora mayúsculas y la columna de la base guarda el enum como texto exacto.
    $ok = $enums[$Lista] | Where-Object { $_ -eq $v } | Select-Object -First 1
    if (-not $ok) { Add-Error $Fila $Columna "«$v» no es un valor válido. Use uno de: $($enums[$Lista] -join ', ')" ; return $null }
    return $ok
}

function Test-Usuario($Fila, [string] $Columna) {
    $correo = Get-Codigo $Fila[$Columna]
    if (-not $correo) { return $null }
    $u = $usuarios[$correo.ToLower()]
    if (-not $u) { Add-Error $Fila $Columna "el usuario «$correo» no existe o está inactivo en el portal"; return $null }
    return @{ Correo = $correo; Id = $u.Id; Nombre = $u.Nombre }
}

function Test-Fecha($Fila, [string] $Columna, [switch] $Obligatorio) {
    $err = $null
    $f = Get-Fecha $Fila[$Columna] ([ref] $err)
    if ($err) { Add-Error $Fila $Columna $err; return $null }
    if (-not $f -and $Obligatorio) { Add-Error $Fila $Columna 'obligatorio y vino vacío' }
    return $f
}

Write-Host 'Validando...' -ForegroundColor Cyan

$proyectos = @{}
$ordenRef  = @()

# La fila de ejemplo de la plantilla lleva Ref = EJEMPLO justamente para poder ignorarla sin
# adivinar por el contenido: mucha gente la deja puesta y sería un proyecto fantasma.
$ES_EJEMPLO = 'EJEMPLO'

foreach ($f in $filasProy) {
    $ref = Get-Texto $f['Ref']
    if ($ref -eq $ES_EJEMPLO) { continue }
    if (-not $ref) { Add-Error $f 'Ref' 'obligatorio y vino vacío'; continue }
    if ($proyectos.ContainsKey($ref)) { Add-Error $f 'Ref' "«$ref» está repetida (ya se usó en la fila $($proyectos[$ref].Fila))"; continue }

    $nombre = Get-Texto $f['Nombre']
    if (-not $nombre) { Add-Error $f 'Nombre' 'obligatorio y vino vacío' }
    elseif ($nombre.Length -gt 300) { Add-Error $f 'Nombre' "son $($nombre.Length) caracteres; el máximo es 300" }

    $inst = Get-Codigo $f['Institución ejecutora']
    if (-not $inst) { Add-Error $f 'Institución ejecutora' 'obligatorio y vino vacío' }
    elseif (-not $instituciones.ContainsKey($inst)) { Add-Error $f 'Institución ejecutora' "la institución «$inst» no existe" }

    $area = Get-Codigo $f['Área']
    if ($area -and -not $areas.ContainsKey($area)) { Add-Error $f 'Área' "el área «$area» no existe" }

    $unidad = Get-Codigo $f['Unidad']
    if ($unidad -and -not $unidades.ContainsKey($unidad)) { Add-Error $f 'Unidad' "la unidad «$unidad» no existe" }
    if ($unidad -and -not $area) { Add-Error $f 'Unidad' 'se indicó unidad sin área; el filtro de alcance necesita las dos' }

    $objetivo = Get-Texto $f['Objetivo']
    if ($objetivo -and $objetivo.Length -gt 2000) { Add-Error $f 'Objetivo' "son $($objetivo.Length) caracteres; el máximo es 2000" }

    $avance = Get-Entero $f['Avance %']
    if ($null -ne $avance -and ($avance -lt 0 -or $avance -gt 100)) { Add-Error $f 'Avance %' "«$avance» está fuera de 0–100" }

    $proyectos[$ref] = @{
        Fila            = $f.__fila
        Ref             = $ref
        Nombre          = $nombre
        Objetivo        = $objetivo
        InstitucionId   = $inst
        AreaId          = $area
        UnidadId        = $unidad
        Responsable     = (Test-Usuario $f 'Responsable (correo)')
        Prioridad       = (Test-Enum $f 'Prioridad' 'Prioridad' -Obligatorio)
        Estado          = (Test-Enum $f 'Estado' 'EstadoProy' -Obligatorio)
        FechaInicioPlan = (Test-Fecha $f 'Inicio planificado')
        FechaFinPlan    = (Test-Fecha $f 'Fin planificado')
        FechaInicioReal = (Test-Fecha $f 'Inicio real')
        FechaFinReal    = (Test-Fecha $f 'Fin real')
        AvancePct       = $(if ($null -eq $avance) { 0 } else { $avance })
        Hitos           = @()
        Interesados     = @()
        Riesgos         = @()
    }
    $ordenRef += $ref
}

function Resolve-Ref($Fila) {
    $ref = Get-Texto $Fila['Ref proyecto']
    if ($ref -eq $ES_EJEMPLO) { return $null }
    if (-not $ref) { Add-Error $Fila 'Ref proyecto' 'obligatorio y vino vacío'; return $null }
    if (-not $proyectos.ContainsKey($ref)) { Add-Error $Fila 'Ref proyecto' "«$ref» no aparece en la hoja Proyectos"; return $null }
    return $ref
}

foreach ($f in $filasHito) {
    $ref = Resolve-Ref $f
    if (-not $ref) { continue }
    $nombre = Get-Texto $f['Hito']
    if (-not $nombre) { Add-Error $f 'Hito' 'obligatorio y vino vacío'; continue }

    $proyectos[$ref].Hitos += @{
        Orden       = Get-Entero $f['Orden']
        Nombre      = $nombre
        Descripcion = Get-Texto $f['Descripción']
        FechaPlan   = Test-Fecha $f 'Fecha planificada'
        FechaReal   = Test-Fecha $f 'Fecha real'
        Estado      = Test-Enum $f 'Estado' 'EstadoHito' -Obligatorio
        Responsable = Test-Usuario $f 'Responsable (correo)'
    }
}

foreach ($f in $filasInt) {
    $ref = Resolve-Ref $f
    if (-not $ref) { continue }

    # El interesado es un usuario del portal: el registro es lo que le abre el proyecto fuera de
    # su alcance, así que no hay forma de admitir a alguien sin cuenta.
    #
    # Ojo con el nombre de esta variable: NO puede llamarse $usuario. El parámetro del script es
    # [string] $Usuario y PowerShell no distingue mayúsculas, así que asignarle un hashtable lo
    # convierte a la cadena «System.Collections.Hashtable» — el Id se perdía en silencio y el SQL
    # salía con UsuarioId nulo.
    $usr = Test-Usuario $f 'Usuario (correo)'
    if (-not $usr) {
        if (-not (Get-Codigo $f['Usuario (correo)'])) { Add-Error $f 'Usuario (correo)' 'obligatorio y vino vacío' }
        continue
    }

    $yaEsta = $proyectos[$ref].Interesados | Where-Object { $_.Usuario.Correo -eq $usr.Correo }
    if ($yaEsta) {
        Add-Error $f 'Usuario (correo)' "«$($usr.Correo)» ya figura como interesado de $ref en este archivo"
        continue
    }

    $proyectos[$ref].Interesados += @{
        Usuario     = $usr
        Institucion = Get-Texto $f['Participa por']
        Cargo       = Get-Texto $f['Cargo']
        Rol         = Test-Enum $f 'Rol' 'RolInt' -Obligatorio
        Influencia  = Test-Enum $f 'Influencia' 'Nivel' -Obligatorio
        Notas       = Get-Texto $f['Notas']
    }
}

foreach ($f in $filasRie) {
    $ref = Resolve-Ref $f
    if (-not $ref) { continue }
    $desc = Get-Texto $f['Riesgo']
    if (-not $desc) { Add-Error $f 'Riesgo' 'obligatorio y vino vacío'; continue }
    if ($desc.Length -gt 500) { Add-Error $f 'Riesgo' "son $($desc.Length) caracteres; el máximo es 500" }

    $proyectos[$ref].Riesgos += @{
        Descripcion    = $desc
        Categoria      = Test-Enum $f 'Categoría' 'CatRiesgo' -Obligatorio
        Probabilidad   = Test-Enum $f 'Probabilidad' 'Nivel' -Obligatorio
        Impacto        = Test-Enum $f 'Impacto' 'Nivel' -Obligatorio
        Estrategia     = Test-Enum $f 'Estrategia' 'Estrategia' -Obligatorio
        Estado         = Test-Enum $f 'Estado' 'EstadoRiesgo' -Obligatorio
        Mitigacion     = Get-Texto $f['Mitigación']
        Responsable    = Test-Usuario $f 'Responsable (correo)'
        FechaDeteccion = Test-Fecha $f 'Detectado el' -Obligatorio
        FechaRevision  = Test-Fecha $f 'Revisar el'
    }
}

if ($errores.Count -gt 0) {
    Write-Host ''
    Write-Host "No se generó el SQL: hay $($errores.Count) problema(s) en la plantilla." -ForegroundColor Red
    Write-Host ''
    $errores | Sort-Object Hoja, Fila | Format-Table -AutoSize -Wrap
    exit 1
}

if ($proyectos.Count -eq 0) {
    Write-Host 'La hoja Proyectos está vacía: no hay nada que importar.' -ForegroundColor Yellow
    exit 1
}

# ── Generación del SQL ───────────────────────────────────────────────────────
function Q($v) {
    if ($null -eq $v -or "$v" -eq '') { return 'NULL' }
    return "N'" + ("$v" -replace "'", "''") + "'"
}
function QF($v) { if (-not $v) { 'NULL' } else { "CAST('$v' AS date)" } }
function QG($u) { if (-not $u) { 'NULL' } else { "CAST('$($u.Id)' AS uniqueidentifier)" } }
function QN($u) { if (-not $u) { 'NULL' } else { Q $u.Nombre } }

$sb = [System.Text.StringBuilder]::new()
function W([string] $t = '') { [void] $sb.AppendLine($t) }

$anio = (Get-Date).Year

W "-- ============================================================================"
W "-- Carga de proyectos desde plantilla Excel"
W "-- Origen : $(Split-Path -Leaf $rutaEntrada)"
W "-- Generado: $(Get-Date -Format 'yyyy-MM-dd HH:mm') por $Actor"
W "-- Proyectos: $($proyectos.Count) | Hitos: $(($proyectos.Values | ForEach-Object { $_.Hitos.Count } | Measure-Object -Sum).Sum) | Interesados: $(($proyectos.Values | ForEach-Object { $_.Interesados.Count } | Measure-Object -Sum).Sum) | Riesgos: $(($proyectos.Values | ForEach-Object { $_.Riesgos.Count } | Measure-Object -Sum).Sum)"
W "--"
W "-- Idempotente: los proyectos se reconocen por Nombre, los hijos por proyecto + nombre"
W "-- (descripción, en riesgos). Correrlo dos veces actualiza, no duplica."
W "--"
W "-- OJO: escribe Estado directo, sin pasar por Proyecto.CambiarEstado, así que no valida las"
W "-- transiciones ni dispara el evento que notifica al responsable. Para una carga inicial es"
W "-- lo que se quiere; para mover el estado de un proyecto vivo, usar el portal."
W "--"
W "--   sqlcmd -S $Servidor -d $BaseDatos -i $(Split-Path -Leaf $Salida)"
W "-- ============================================================================"
W ''
W '-- QUOTED_IDENTIFIER tiene que ir encendido: Proyectos.Codigo lleva un índice único filtrado'
W '-- (WHERE IsDeleted = 0) y SQL Server rechaza el INSERT si la opción viene apagada, que es'
W '-- justo como la deja sqlcmd por omisión.'
W 'SET QUOTED_IDENTIFIER ON;'
W 'SET ANSI_NULLS ON;'
W 'SET XACT_ABORT ON;'
W 'SET NOCOUNT ON;'
W 'GO'
W ''
W 'BEGIN TRANSACTION;'
W ''
W ('DECLARE @actor nvarchar(300) = ' + (Q $Actor) + ';')
W 'DECLARE @ahora datetime2 = SYSUTCDATETIME();'
W "DECLARE @prefijo nvarchar(20) = N'PRY-$anio-';"
W 'DECLARE @pid int, @nuevo bit, @n int;'
W ''

foreach ($ref in $ordenRef) {
    $p = $proyectos[$ref]
    W ("-- ── [$ref] $($p.Nombre) " + ('─' * [Math]::Max(3, 54 - $p.Nombre.Length)))
    W "SET @pid = (SELECT TOP 1 Id FROM Proyectos WHERE Nombre = $(Q $p.Nombre) AND IsDeleted = 0);"
    W 'SET @nuevo = CASE WHEN @pid IS NULL THEN 1 ELSE 0 END;'
    W ''
    W 'IF @nuevo = 1'
    W 'BEGIN'
    W '    SET @n = (SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(Codigo, LEN(@prefijo) + 1, 10) AS int)), 0) + 1'
    W '              FROM Proyectos WHERE Codigo LIKE @prefijo + N''%'');'
    W ''
    W '    INSERT INTO Proyectos (IsDeleted, Codigo, Nombre, Objetivo, InstitucionId, AreaId, UnidadId,'
    W '                           ResponsableId, Responsable, Prioridad, Estado,'
    W '                           FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal,'
    W '                           AvancePct, CreatedAt, CreatedBy)'
    W "    VALUES (0, @prefijo + FORMAT(@n, '00'), $(Q $p.Nombre), $(Q $p.Objetivo), $(Q $p.InstitucionId), $(Q $p.AreaId), $(Q $p.UnidadId),"
    W "            $(QG $p.Responsable), $(QN $p.Responsable), $(Q $p.Prioridad), $(Q $p.Estado),"
    W "            $(QF $p.FechaInicioPlan), $(QF $p.FechaFinPlan), $(QF $p.FechaInicioReal), $(QF $p.FechaFinReal),"
    W "            $($p.AvancePct), @ahora, @actor);"
    W ''
    W '    SET @pid = SCOPE_IDENTITY();'
    W 'END'
    W 'ELSE'
    W 'BEGIN'
    W '    UPDATE Proyectos SET'
    W "        Objetivo        = $(Q $p.Objetivo),"
    W "        InstitucionId   = $(Q $p.InstitucionId),"
    W "        AreaId          = $(Q $p.AreaId),"
    W "        UnidadId        = $(Q $p.UnidadId),"
    W "        ResponsableId   = $(QG $p.Responsable),"
    W "        Responsable     = $(QN $p.Responsable),"
    W "        Prioridad       = $(Q $p.Prioridad),"
    W "        Estado          = $(Q $p.Estado),"
    W "        FechaInicioPlan = $(QF $p.FechaInicioPlan),"
    W "        FechaFinPlan    = $(QF $p.FechaFinPlan),"
    W "        FechaInicioReal = $(QF $p.FechaInicioReal),"
    W "        FechaFinReal    = $(QF $p.FechaFinReal),"
    W "        AvancePct       = $($p.AvancePct),"
    W '        UpdatedAt       = @ahora,'
    W '        UpdatedBy       = @actor'
    W '    WHERE Id = @pid;'
    W 'END'
    W ''

    $orden = 0
    foreach ($h in $p.Hitos) {
        $orden++
        $ordenReal = if ($null -ne $h.Orden) { $h.Orden } else { $orden }
        W "-- hito: $($h.Nombre)"
        W "IF NOT EXISTS (SELECT 1 FROM ProyectoEntregables WHERE ProyectoId = @pid AND Nombre = $(Q $h.Nombre))"
        W '    INSERT INTO ProyectoEntregables (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable)'
        W "    VALUES (@pid, $ordenReal, $(Q $h.Nombre), $(Q $h.Descripcion), $(QF $h.FechaPlan), $(QF $h.FechaReal), $(Q $h.Estado), $(QG $h.Responsable), $(QN $h.Responsable));"
        W 'ELSE'
        W '    UPDATE ProyectoEntregables SET'
        W "        Orden = $ordenReal, Descripcion = $(Q $h.Descripcion), FechaPlan = $(QF $h.FechaPlan),"
        W "        FechaReal = $(QF $h.FechaReal), Estado = $(Q $h.Estado),"
        W "        ResponsableId = $(QG $h.Responsable), Responsable = $(QN $h.Responsable)"
        W "    WHERE ProyectoId = @pid AND Nombre = $(Q $h.Nombre);"
        W ''
    }

    # Los interesados se reconocen por usuario, no por nombre: es la clave única de la tabla y,
    # sobre todo, es a quién se le está abriendo el proyecto.
    foreach ($i in $p.Interesados) {
        W "-- interesado: $($i.Usuario.Nombre) <$($i.Usuario.Correo)> — pasa a ver el proyecto"
        W "IF NOT EXISTS (SELECT 1 FROM ProyectoInteresados WHERE ProyectoId = @pid AND UsuarioId = $(QG $i.Usuario))"
        W '    INSERT INTO ProyectoInteresados (ProyectoId, UsuarioId, Nombre, Correo, Institucion, Cargo, Rol, Influencia, Notas, RegistradoPor, RegistradoEn)'
        W "    VALUES (@pid, $(QG $i.Usuario), $(QN $i.Usuario), $(Q $i.Usuario.Correo), $(Q $i.Institucion), $(Q $i.Cargo), $(Q $i.Rol), $(Q $i.Influencia), $(Q $i.Notas), @actor, @ahora);"
        W 'ELSE'
        W '    UPDATE ProyectoInteresados SET'
        W "        Nombre = $(QN $i.Usuario), Correo = $(Q $i.Usuario.Correo),"
        W "        Institucion = $(Q $i.Institucion), Cargo = $(Q $i.Cargo),"
        W "        Rol = $(Q $i.Rol), Influencia = $(Q $i.Influencia), Notas = $(Q $i.Notas)"
        W "    WHERE ProyectoId = @pid AND UsuarioId = $(QG $i.Usuario);"
        W ''
    }

    foreach ($r in $p.Riesgos) {
        W "-- riesgo: $($r.Descripcion.Substring(0, [Math]::Min(60, $r.Descripcion.Length)))"
        W "IF NOT EXISTS (SELECT 1 FROM ProyectoRiesgos WHERE ProyectoId = @pid AND Descripcion = $(Q $r.Descripcion))"
        W '    INSERT INTO ProyectoRiesgos (ProyectoId, Descripcion, Categoria, Probabilidad, Impacto, Estrategia, Estado,'
        W '                                 Mitigacion, ResponsableId, Responsable, FechaDeteccion, FechaRevision, RegistradoPor, RegistradoEn)'
        W "    VALUES (@pid, $(Q $r.Descripcion), $(Q $r.Categoria), $(Q $r.Probabilidad), $(Q $r.Impacto), $(Q $r.Estrategia), $(Q $r.Estado),"
        W "            $(Q $r.Mitigacion), $(QG $r.Responsable), $(QN $r.Responsable), $(QF $r.FechaDeteccion), $(QF $r.FechaRevision), @actor, @ahora);"
        W 'ELSE'
        W '    UPDATE ProyectoRiesgos SET'
        W "        Categoria = $(Q $r.Categoria), Probabilidad = $(Q $r.Probabilidad), Impacto = $(Q $r.Impacto),"
        W "        Estrategia = $(Q $r.Estrategia), Estado = $(Q $r.Estado), Mitigacion = $(Q $r.Mitigacion),"
        W "        ResponsableId = $(QG $r.Responsable), Responsable = $(QN $r.Responsable),"
        W "        FechaRevision = $(QF $r.FechaRevision)"
        W "    WHERE ProyectoId = @pid AND Descripcion = $(Q $r.Descripcion);"
        W ''
    }

    # La bitácora es lo que distingue esta carga de una edición manual: sin entrada, nadie puede
    # reconstruir después de dónde salió el proyecto.
    $resumen = "Carga desde plantilla Excel ($(Split-Path -Leaf $rutaEntrada)): $($p.Hitos.Count) hito(s), $($p.Interesados.Count) interesado(s), $($p.Riesgos.Count) riesgo(s)."
    W 'INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)'
    W "VALUES (@pid, N'ModificacionFicha',"
    W "        CASE WHEN @nuevo = 1 THEN N'Proyecto creado. ' ELSE N'Proyecto actualizado. ' END + $(Q $resumen), @actor, @ahora);"
    W ''
}

W '-- ── Verificación ────────────────────────────────────────────────────────────'
W 'SELECT p.Codigo, p.Nombre, p.Estado, p.Prioridad, p.AvancePct,'
W '       ISNULL(p.Responsable, N''— sin responsable —'') AS Responsable,'
W '       (SELECT COUNT(*) FROM ProyectoEntregables       h WHERE h.ProyectoId = p.Id) AS Hitos,'
W '       (SELECT COUNT(*) FROM ProyectoInteresados i WHERE i.ProyectoId = p.Id) AS Interesados,'
W '       (SELECT COUNT(*) FROM ProyectoRiesgos     r WHERE r.ProyectoId = p.Id) AS Riesgos'
W 'FROM Proyectos p'
W 'WHERE p.IsDeleted = 0 AND p.Nombre IN ('
W ('    ' + (($ordenRef | ForEach-Object { Q $proyectos[$_].Nombre }) -join ",`r`n    "))
W ')'
W 'ORDER BY p.Codigo;'
W ''
W 'COMMIT TRANSACTION;'
W "PRINT 'Carga completada: $($proyectos.Count) proyecto(s).';"

[IO.File]::WriteAllText($Salida, $sb.ToString(), (New-Object Text.UTF8Encoding $true))

Write-Host ''
Write-Host "SQL generado: $Salida" -ForegroundColor Green
Write-Host ("  {0} proyectos · {1} hitos · {2} interesados · {3} riesgos" -f `
    $proyectos.Count,
    ($proyectos.Values | ForEach-Object { $_.Hitos.Count } | Measure-Object -Sum).Sum,
    ($proyectos.Values | ForEach-Object { $_.Interesados.Count } | Measure-Object -Sum).Sum,
    ($proyectos.Values | ForEach-Object { $_.Riesgos.Count } | Measure-Object -Sum).Sum)
Write-Host ''
Write-Host 'Revise el script antes de correrlo:' -ForegroundColor Yellow
Write-Host "  sqlcmd -S $Servidor -d $BaseDatos -i `"$Salida`""
