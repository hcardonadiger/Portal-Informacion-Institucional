<#
.SYNOPSIS
    Importa el inventario SIGER desde el archivo Excel a la base de datos TramitesEstado_Dev.
.DESCRIPTION
    Lee todas las hojas del archivo Inventario_Tramites_SIGER.xlsx usando COM automation
    e inserta los datos en las tablas TramitesSiger y sus hijas.
.PARAMETER ExcelPath
    Ruta al archivo xlsx. Default: $env:USERPROFILE\Downloads\Inventario_Tramites_SIGER.xlsx
.PARAMETER Server
    Servidor SQL. Default: .
.PARAMETER Database
    Nombre de la base de datos. Default: TramitesEstado_Dev
#>
param(
    [string]$ExcelPath = "$env:USERPROFILE\Downloads\Inventario_Tramites_SIGER.xlsx",
    [string]$Server    = ".",
    [string]$Database  = "TramitesEstado_Dev",
    [string]$User      = "sa",
    [string]$Password  = "Hola123#"
)

$ErrorActionPreference = "Stop"
$connStr = "Server=$Server;Database=$Database;User ID=$User;Password=$Password;TrustServerCertificate=True"

function Invoke-Sql([string]$sql) {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = 300
    $result = $cmd.ExecuteNonQuery()
    $conn.Close()
    return $result
}

function Invoke-SqlScalar([string]$sql) {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $val = $cmd.ExecuteScalar()
    $conn.Close()
    return $val
}

function Q([string]$v) {
    if ([string]::IsNullOrEmpty($v)) { return "NULL" }
    return "N'" + $v.Replace("'", "''") + "'"
}

function ParseBool([string]$v) {
    if ($v -eq "Si" -or $v -eq "Sí") { return 1 }
    return 0
}

function ParseDate([string]$v) {
    if ([string]::IsNullOrWhiteSpace($v) -or $v -eq "No registrada") { return "NULL" }
    try {
        $d = [DateTime]::Parse($v)
        return "'" + $d.ToString("yyyy-MM-dd") + "'"
    } catch {
        return "NULL"
    }
}

Write-Host "Abriendo Excel: $ExcelPath" -ForegroundColor Cyan
$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false

try {
    $wb = $excel.Workbooks.Open($ExcelPath)

    # Limpiar tablas existentes (en orden por FK)
    Write-Host "Limpiando tablas existentes..." -ForegroundColor Yellow
    Invoke-Sql "DELETE FROM TareasDigitalizacionSiger" | Out-Null
    Invoke-Sql "DELETE FROM EnlacesSiger" | Out-Null
    Invoke-Sql "DELETE FROM LugaresAtencionSiger" | Out-Null
    Invoke-Sql "DELETE FROM EntregablesSiger" | Out-Null
    Invoke-Sql "DELETE FROM RequisitosSiger" | Out-Null
    Invoke-Sql "DELETE FROM PasosSiger" | Out-Null
    Invoke-Sql "DELETE FROM TramitesSiger" | Out-Null

    # ── 1. Inventario (hoja principal) ─────────────────────────────────
    $ws = $wb.Sheets.Item("Inventario")
    $totalRows = $ws.UsedRange.Rows.Count
    Write-Host "Importando Inventario: $($totalRows - 1) registros..." -ForegroundColor Green

    $now = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss")
    $imported = 0

    for ($r = 2; $r -le $totalRows; $r++) {
        $idSiger    = $ws.Cells.Item($r, 1).Value2
        if ($null -eq $idSiger) { continue }
        $codigo     = $ws.Cells.Item($r, 2).Text
        $nombre     = $ws.Cells.Item($r, 3).Text
        $inst       = $ws.Cells.Item($r, 4).Text
        $sigla      = $ws.Cells.Item($r, 5).Text
        $dep        = $ws.Cells.Item($r, 6).Text
        $desc       = $ws.Cells.Item($r, 7).Text
        $obj        = $ws.Cells.Item($r, 8).Text
        $dirigido   = $ws.Cells.Item($r, 9).Text
        $estado     = $ws.Cells.Item($r, 10).Text
        $publicado  = ParseBool $ws.Cells.Item($r, 11).Text
        $enLinea    = ParseBool $ws.Cells.Item($r, 12).Text
        $enPlan     = ParseBool $ws.Cells.Item($r, 13).Text
        $vigencia   = $ws.Cells.Item($r, 15).Text
        $temp       = $ws.Cells.Item($r, 16).Text
        $diagrama   = $ws.Cells.Item($r, 21).Text
        $enlace     = $ws.Cells.Item($r, 22).Text
        $obs        = $ws.Cells.Item($r, 23).Text
        $fIngreso   = ParseDate $ws.Cells.Item($r, 24).Text
        $fModif     = ParseDate $ws.Cells.Item($r, 25).Text

        $sql = @"
INSERT INTO TramitesSiger
(IdSiger, Codigo, Nombre, Institucion, Sigla, Dependencia, Descripcion, Objetivo, DirigidoA,
 EstadoSiger, Publicado, DisponibleEnLinea, EnPlanDigitalizacion,
 VigenciaDocumento, Temporalidad, DiagramaUrl, EnlacePrincipal, ObservacionesDiger,
 FechaIngreso, UltimaModificacion, CreatedAt, CreatedBy)
VALUES
($([int]$idSiger), $(Q $codigo), $(Q $nombre), $(Q $inst), $(Q $sigla), $(Q $dep),
 $(Q $desc), $(Q $obj), $(Q $dirigido), $(Q $estado),
 $publicado, $enLinea, $enPlan,
 $(Q $vigencia), $(Q $temp), $(Q $diagrama), $(Q $enlace), $(Q $obs),
 $fIngreso, $fModif, '$now', N'import-siger')
"@
        Invoke-Sql $sql | Out-Null
        $imported++
        if ($imported % 100 -eq 0) { Write-Host "  $imported tramites..." }
    }
    Write-Host "  Total: $imported tramites importados" -ForegroundColor Green

    # Construir mapa IdSiger -> Id (PK)
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT Id, IdSiger FROM TramitesSiger"
    $reader = $cmd.ExecuteReader()
    $idMap = @{}
    while ($reader.Read()) {
        $idMap[[int]$reader["IdSiger"]] = [int]$reader["Id"]
    }
    $reader.Close()
    $conn.Close()
    Write-Host "Mapa de IDs construido: $($idMap.Count) entradas" -ForegroundColor Cyan

    # ── 2. Pasos_Procesos ──────────────────────────────────────────────
    $ws = $wb.Sheets.Item("Pasos_Procesos")
    $totalRows = $ws.UsedRange.Rows.Count
    Write-Host "Importando Pasos: $($totalRows - 1) registros..." -ForegroundColor Green
    $batch = [System.Text.StringBuilder]::new()
    $count = 0

    for ($r = 2; $r -le $totalRows; $r++) {
        $idSiger = $ws.Cells.Item($r, 1).Value2
        if ($null -eq $idSiger -or -not $idMap.ContainsKey([int]$idSiger)) { continue }
        $fk = $idMap[[int]$idSiger]
        $num  = [int]$ws.Cells.Item($r, 5).Value2
        $desc = $ws.Cells.Item($r, 6).Text
        $lug  = $ws.Cells.Item($r, 7).Text
        $sal  = $ws.Cells.Item($r, 8).Text
        $tpo  = $ws.Cells.Item($r, 9).Text

        [void]$batch.AppendLine("INSERT INTO PasosSiger (TramiteSigerId,NumeroPaso,Descripcion,LugarDependencia,SalidaResultado,TiempoRegistrado) VALUES ($fk,$num,$(Q $desc),$(Q $lug),$(Q $sal),$(Q $tpo));")
        $count++
        if ($count % 500 -eq 0) {
            Invoke-Sql $batch.ToString() | Out-Null
            $batch.Clear() | Out-Null
            Write-Host "  $count pasos..."
        }
    }
    if ($batch.Length -gt 0) { Invoke-Sql $batch.ToString() | Out-Null }
    Write-Host "  Total: $count pasos importados" -ForegroundColor Green

    # ── 3. Requisitos ──────────────────────────────────────────────────
    $ws = $wb.Sheets.Item("Requisitos")
    $totalRows = $ws.UsedRange.Rows.Count
    Write-Host "Importando Requisitos: $($totalRows - 1) registros..." -ForegroundColor Green
    $batch = [System.Text.StringBuilder]::new()
    $count = 0

    for ($r = 2; $r -le $totalRows; $r++) {
        $idSiger = $ws.Cells.Item($r, 1).Value2
        if ($null -eq $idSiger -or -not $idMap.ContainsKey([int]$idSiger)) { continue }
        $fk = $idMap[[int]$idSiger]
        $num  = [int]$ws.Cells.Item($r, 4).Value2
        $req  = $ws.Cells.Item($r, 5).Text
        $tipo = $ws.Cells.Item($r, 6).Text
        $doc  = $ws.Cells.Item($r, 7).Text
        $fmt  = $ws.Cells.Item($r, 8).Text

        [void]$batch.AppendLine("INSERT INTO RequisitosSiger (TramiteSigerId,Numero,Requisito,Tipo,DocumentoSoporte,Formato) VALUES ($fk,$num,$(Q $req),$(Q $tipo),$(Q $doc),$(Q $fmt));")
        $count++
        if ($count % 500 -eq 0) {
            Invoke-Sql $batch.ToString() | Out-Null
            $batch.Clear() | Out-Null
            Write-Host "  $count requisitos..."
        }
    }
    if ($batch.Length -gt 0) { Invoke-Sql $batch.ToString() | Out-Null }
    Write-Host "  Total: $count requisitos importados" -ForegroundColor Green

    # ── 4. Entregables ─────────────────────────────────────────────────
    $ws = $wb.Sheets.Item("Entregables")
    $totalRows = $ws.UsedRange.Rows.Count
    Write-Host "Importando Entregables: $($totalRows - 1) registros..." -ForegroundColor Green
    $batch = [System.Text.StringBuilder]::new()
    $count = 0

    for ($r = 2; $r -le $totalRows; $r++) {
        $idSiger = $ws.Cells.Item($r, 1).Value2
        if ($null -eq $idSiger -or -not $idMap.ContainsKey([int]$idSiger)) { continue }
        $fk = $idMap[[int]$idSiger]
        $num  = [int]$ws.Cells.Item($r, 4).Value2
        $ent  = $ws.Cells.Item($r, 5).Text
        $fmt  = $ws.Cells.Item($r, 6).Text
        $pres = $ws.Cells.Item($r, 7).Text

        [void]$batch.AppendLine("INSERT INTO EntregablesSiger (TramiteSigerId,Numero,Entregable,Formato,Presentacion) VALUES ($fk,$num,$(Q $ent),$(Q $fmt),$(Q $pres));")
        $count++
        if ($count % 500 -eq 0) {
            Invoke-Sql $batch.ToString() | Out-Null
            $batch.Clear() | Out-Null
            Write-Host "  $count entregables..."
        }
    }
    if ($batch.Length -gt 0) { Invoke-Sql $batch.ToString() | Out-Null }
    Write-Host "  Total: $count entregables importados" -ForegroundColor Green

    # ── 5. Lugares_Atencion ────────────────────────────────────────────
    $ws = $wb.Sheets.Item("Lugares_Atencion")
    $totalRows = $ws.UsedRange.Rows.Count
    Write-Host "Importando Lugares de Atencion: $($totalRows - 1) registros..." -ForegroundColor Green
    $batch = [System.Text.StringBuilder]::new()
    $count = 0

    for ($r = 2; $r -le $totalRows; $r++) {
        $idSiger = $ws.Cells.Item($r, 1).Value2
        if ($null -eq $idSiger -or -not $idMap.ContainsKey([int]$idSiger)) { continue }
        $fk = $idMap[[int]$idSiger]
        $num  = [int]$ws.Cells.Item($r, 4).Value2
        $lug  = $ws.Cells.Item($r, 5).Text
        $ciu  = $ws.Cells.Item($r, 6).Text
        $dir  = $ws.Cells.Item($r, 7).Text
        $tel  = $ws.Cells.Item($r, 8).Text

        [void]$batch.AppendLine("INSERT INTO LugaresAtencionSiger (TramiteSigerId,Numero,Lugar,Ciudad,Direccion,Telefonos) VALUES ($fk,$num,$(Q $lug),$(Q $ciu),$(Q $dir),$(Q $tel));")
        $count++
        if ($count % 500 -eq 0) {
            Invoke-Sql $batch.ToString() | Out-Null
            $batch.Clear() | Out-Null
            Write-Host "  $count lugares..."
        }
    }
    if ($batch.Length -gt 0) { Invoke-Sql $batch.ToString() | Out-Null }
    Write-Host "  Total: $count lugares importados" -ForegroundColor Green

    # ── 6. Enlaces ─────────────────────────────────────────────────────
    $ws = $wb.Sheets.Item("Enlaces")
    $totalRows = $ws.UsedRange.Rows.Count
    Write-Host "Importando Enlaces: $($totalRows - 1) registros..." -ForegroundColor Green
    $batch = [System.Text.StringBuilder]::new()
    $count = 0

    for ($r = 2; $r -le $totalRows; $r++) {
        $idSiger = $ws.Cells.Item($r, 1).Value2
        if ($null -eq $idSiger -or -not $idMap.ContainsKey([int]$idSiger)) { continue }
        $fk = $idMap[[int]$idSiger]
        $num  = [int]$ws.Cells.Item($r, 4).Value2
        $url  = $ws.Cells.Item($r, 5).Text
        $tipo = $ws.Cells.Item($r, 6).Text

        [void]$batch.AppendLine("INSERT INTO EnlacesSiger (TramiteSigerId,Numero,Url,Tipo) VALUES ($fk,$num,$(Q $url),$(Q $tipo));")
        $count++
        if ($count % 500 -eq 0) {
            Invoke-Sql $batch.ToString() | Out-Null
            $batch.Clear() | Out-Null
            Write-Host "  $count enlaces..."
        }
    }
    if ($batch.Length -gt 0) { Invoke-Sql $batch.ToString() | Out-Null }
    Write-Host "  Total: $count enlaces importados" -ForegroundColor Green

    # ── 7. Digitalizacion (tareas) ─────────────────────────────────────
    $ws = $wb.Sheets.Item("Digitalizacion")
    $totalRows = $ws.UsedRange.Rows.Count
    Write-Host "Importando Tareas de Digitalizacion: $($totalRows - 1) registros..." -ForegroundColor Green
    $batch = [System.Text.StringBuilder]::new()
    $count = 0

    for ($r = 2; $r -le $totalRows; $r++) {
        $idSiger = $ws.Cells.Item($r, 1).Value2
        if ($null -eq $idSiger -or -not $idMap.ContainsKey([int]$idSiger)) { continue }
        $fk = $idMap[[int]$idSiger]
        $num   = [int]$ws.Cells.Item($r, 4).Value2
        $desc  = $ws.Cells.Item($r, 5).Text
        $est   = $ws.Cells.Item($r, 6).Text
        $fecha = ParseDate $ws.Cells.Item($r, 7).Text

        [void]$batch.AppendLine("INSERT INTO TareasDigitalizacionSiger (TramiteSigerId,NumeroTarea,Descripcion,Estado,FechaCumplimiento) VALUES ($fk,$num,$(Q $desc),$(Q $est),$fecha);")
        $count++
        if ($count % 1000 -eq 0) {
            Invoke-Sql $batch.ToString() | Out-Null
            $batch.Clear() | Out-Null
            Write-Host "  $count tareas..."
        }
    }
    if ($batch.Length -gt 0) { Invoke-Sql $batch.ToString() | Out-Null }
    Write-Host "  Total: $count tareas importadas" -ForegroundColor Green

    Write-Host "`nImportacion completada exitosamente." -ForegroundColor Cyan
}
finally {
    $wb.Close($false)
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}
