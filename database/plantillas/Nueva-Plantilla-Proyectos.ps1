<#
.SYNOPSIS
    Genera la plantilla Excel de captura de proyectos, con los catálogos vivos de la base.

.DESCRIPTION
    La plantilla es el formato que se le entrega a las áreas para que llenen sus proyectos.
    Se regenera —en vez de editarse a mano— porque las listas desplegables salen de la base:
    usuarios activos, áreas, unidades e instituciones cambian, y una plantilla con catálogos
    viejos produce importaciones que fallan por un correo o un código que ya no existe.

    El archivo que produce lo consume Generar-SQL-Proyectos.ps1.

.EXAMPLE
    .\Nueva-Plantilla-Proyectos.ps1
    .\Nueva-Plantilla-Proyectos.ps1 -Usuario sa -Clave '***' -Salida 'C:\temp\plantilla.xlsx'
#>
[CmdletBinding()]
param(
    [string] $Servidor = 'localhost',
    [string] $BaseDatos = 'DigerTramitesEstado',
    [string] $Usuario,
    [string] $Clave,
    [string] $Salida = (Join-Path $PSScriptRoot 'Plantilla_Importacion_Proyectos.xlsx'),

    # Contraseña para desproteger las hojas. Por omisión no lleva: la protección está para
    # evitar el borrado accidental de los catálogos, no para impedirle nada a quien sepa lo que
    # hace, y una contraseña olvidada deja la plantilla inservible. Se puede poner igual.
    [string] $ClaveProteccion
)

$ErrorActionPreference = 'Stop'

# ── Catálogos ────────────────────────────────────────────────────────────────
function Get-Catalogo([string] $Consulta) {
    # Ojo: no llamar $args a esta variable — es automática en PowerShell y se pisa sola.
    $sqlArgs = @('-S', $Servidor, '-d', $BaseDatos, '-h', '-1', '-W', '-s', '|', '-Q', "SET NOCOUNT ON; $Consulta")
    if ($Usuario) { $sqlArgs += @('-U', $Usuario, '-P', $Clave) } else { $sqlArgs += '-E' }

    $salida = & sqlcmd.exe @sqlArgs
    if ($LASTEXITCODE -ne 0) {
        # El caso frecuente: el usuario de Windows no tiene login en la instancia, así que la
        # autenticación integrada que este script usa por omisión no sirve. El error de sqlcmd
        # («Login failed for user DOMINIO\usuario») no sugiere la salida, así que se dice acá.
        # No se filtra por el texto del error: sqlcmd lo manda a stderr, así que $salida viene
        # vacío y no hay nada que buscarle.
        if (-not $Usuario) {
            throw "No se pudo entrar con la cuenta de Windows. Vuelva a correrlo indicando el " +
                  "usuario de SQL Server:`n`n" +
                  "    .\Nueva-Plantilla-Proyectos.ps1 -Usuario sa -Clave '<la clave>'`n"
        }
        throw "sqlcmd falló consultando el catálogo: $salida"
    }

    # sqlcmd cierra con una línea de conteo y a veces con vacías; se descartan.
    $salida | Where-Object { $_ -and $_.Trim() -and $_ -notmatch '^\(\d+ (rows|filas)' } | ForEach-Object { $_.Trim() }
}

Write-Host 'Leyendo catálogos de la base...' -ForegroundColor Cyan

$instituciones = Get-Catalogo "SELECT Id FROM Instituciones ORDER BY CASE WHEN Id='DIGER' THEN 0 ELSE 1 END, Id"
$areas         = Get-Catalogo "SELECT a.Id + ' — ' + a.Nombre FROM Areas a ORDER BY a.InstitucionId, a.Nombre"
$unidades      = Get-Catalogo "SELECT u.Id + ' — ' + u.Nombre FROM Unidades u ORDER BY u.Nombre"
$usuarios      = Get-Catalogo "SELECT u.Correo + ' — ' + u.Nombre FROM Usuarios u WHERE u.Activo = 1 ORDER BY u.Nombre"

if (-not $instituciones) { throw 'El catálogo de instituciones vino vacío: revise la conexión.' }

# Enums del dominio. Van literales a propósito: son parte del contrato con el código
# (src/Domain/Enums/Enums.cs, RiesgoProyecto.cs, InteresadoProyecto.cs), no datos de la base,
# y si alguno cambia tiene que cambiar acá también.
$prioridades      = @('Alta', 'Media', 'Baja')
$estadosProyecto  = @('Planificado', 'EnEjecucion', 'Suspendido', 'Cerrado', 'Cancelado')
$estadosHito      = @('Pendiente', 'EnProceso', 'Completado', 'Cancelado')
$niveles          = @('Alta', 'Media', 'Baja')
$rolesInteresado  = @('Patrocinador', 'Ejecutor', 'ContraparteTecnica', 'Beneficiario', 'Regulador')
$categoriasRiesgo = @('Tecnico', 'Institucional', 'Normativo', 'Financiero', 'Operativo', 'Externo')
$estrategias      = @('Evitar', 'Mitigar', 'Transferir', 'Aceptar')
$estadosRiesgo    = @('Abierto', 'EnTratamiento', 'Materializado', 'Cerrado')

# ── Excel ────────────────────────────────────────────────────────────────────
$AZUL     = 6970168    # BGR de #1E3A5F, el azul del portal
$AMARILLO = 14804223   # #FFE699 — obligatorio
$GRIS     = 15921906   # #F2F2F2 — fila de ejemplo
$BLANCO   = 16777215
$TEXTOGRIS = 8421504

$FILAS_VALIDACION = 500   # hasta dónde llegan las listas desplegables

<# El destino se comprueba ANTES de abrir Excel.

   Antes se comprobaba al final, al momento de guardar: si el archivo estaba abierto —cosa que
   pasa seguido, porque uno lo tiene en pantalla mientras lo revisa— el script moría con Excel ya
   levantado y dejaba un proceso EXCEL.EXE huérfano, invisible y con el archivo tomado. #>
function Test-ArchivoLibre([string] $Ruta) {
    if (-not (Test-Path $Ruta)) { return $true }
    try {
        $fs = [IO.File]::Open($Ruta, 'Open', 'ReadWrite', 'None')
        $fs.Close(); $fs.Dispose()
        return $true
    } catch { return $false }
}

if (-not (Test-ArchivoLibre $Salida)) {
    throw "La plantilla está abierta en otro programa y no se puede sobrescribir:`n`n" +
          "    $Salida`n`n" +
          "Ciérrela en Excel y vuelva a correr el script, o mande la nueva a otro lado con -Salida.`n"
}

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$libro = $excel.Workbooks.Add()

# Desde acá Excel está levantado: si algo falla, hay que cerrarlo igual o queda un proceso
# huérfano reteniendo archivos.
try {

# La hoja que trae el libro nuevo se llama «Hoja1» o «Sheet1» según el idioma de Excel:
# se guarda la referencia ahora y se borra al final, en vez de buscarla por nombre.
$hojaInicial = $libro.Worksheets.Item(1)

function New-Hoja([string] $Nombre) {
    $h = $libro.Worksheets.Add([System.Reflection.Missing]::Value, $libro.Worksheets.Item($libro.Worksheets.Count))
    $h.Name = $Nombre
    $h.Cells.Font.Name = 'Arial'
    $h.Cells.Font.Size = 10
    return $h
}

function Set-Fila($Hoja, [int] $Fila, [string[]] $Valores) {
    for ($i = 0; $i -lt $Valores.Count; $i++) { $Hoja.Cells.Item($Fila, $i + 1).Value2 = $Valores[$i] }
}

<# Encabezado de una hoja de captura: título en A1, cabeceras en la fila 3.
   Las obligatorias se pintan de amarillo, que es la misma señal que usa la leyenda. #>
function Set-Encabezado($Hoja, [string] $Titulo, [string] $Ayuda, [string[]] $Columnas, [int[]] $Obligatorias, [int[]] $Anchos) {
    $Hoja.Cells.Item(1, 1).Value2 = $Titulo
    $Hoja.Cells.Item(1, 1).Font.Size = 14
    $Hoja.Cells.Item(1, 1).Font.Bold = $true
    $Hoja.Cells.Item(1, 1).Font.Color = $AZUL

    $Hoja.Cells.Item(2, 1).Value2 = $Ayuda
    $Hoja.Cells.Item(2, 1).Font.Italic = $true
    $Hoja.Cells.Item(2, 1).Font.Color = $TEXTOGRIS

    Set-Fila $Hoja 3 $Columnas
    $rango = $Hoja.Range($Hoja.Cells.Item(3, 1), $Hoja.Cells.Item(3, $Columnas.Count))
    $rango.Font.Bold = $true
    $rango.Font.Color = $BLANCO
    $rango.Interior.Color = $AZUL
    $rango.HorizontalAlignment = -4131
    $rango.VerticalAlignment = -4108
    $rango.WrapText = $true
    $Hoja.Rows.Item(3).RowHeight = 30

    foreach ($c in $Obligatorias) {
        $Hoja.Cells.Item(3, $c).Interior.Color = $AMARILLO
        $Hoja.Cells.Item(3, $c).Font.Color = 0
    }

    for ($i = 0; $i -lt $Anchos.Count; $i++) { $Hoja.Columns.Item($i + 1).ColumnWidth = $Anchos[$i] }

    # FreezePanes actúa sobre la ventana activa, así que la hoja tiene que estar al frente.
    $Hoja.Activate()
    $Hoja.Application.ActiveWindow.FreezePanes = $false
    $Hoja.Range('A4').Select() | Out-Null
    $Hoja.Application.ActiveWindow.FreezePanes = $true
    $rango.AutoFilter() | Out-Null
}

function Set-Ejemplo($Hoja, [string[]] $Valores) {
    Set-Fila $Hoja 4 $Valores
    $r = $Hoja.Range($Hoja.Cells.Item(4, 1), $Hoja.Cells.Item(4, $Valores.Count))
    $r.Font.Italic = $true
    $r.Font.Color = $TEXTOGRIS
    $r.Interior.Color = $GRIS
}

<# Lista desplegable sobre una columna entera de captura.
   xlValidateList = 3, xlValidAlertStop = 1: un valor fuera de la lista se rechaza en vez de
   avisar, porque el importador no tiene forma de adivinar a qué enum quiso referirse. #>
function Set-Lista($Hoja, [int] $Columna, [string] $Nombre) {
    $col = $Hoja.Range($Hoja.Cells.Item(4, $Columna), $Hoja.Cells.Item($FILAS_VALIDACION, $Columna))
    $col.Validation.Delete()
    $col.Validation.Add(3, 1, 1, "=$Nombre") | Out-Null
    $col.Validation.IgnoreBlank = $true
    $col.Validation.InCellDropdown = $true
    $col.Validation.ErrorTitle = 'Valor no permitido'
    $col.Validation.ErrorMessage = 'Elija uno de la lista. El importador solo reconoce esos valores.'
}

<#
    Deja editable solo la zona de captura y cierra el resto.

    En Excel todas las celdas nacen con Locked = True, pero eso no hace nada hasta que la hoja se
    protege: por eso hay que desbloquear primero lo que sí se llena y proteger después, y no al
    revés. Las filas 1 a 3 —título, ayuda y encabezados— quedan bloqueadas porque el importador
    busca las columnas por nombre y por posición: si alguien mueve una, el archivo deja de servir.

    La protección va sin contraseña salvo que se pida una. Lo que evita es el accidente, no al
    usuario decidido; quien de verdad necesite reacomodar algo entra por Revisar > Desproteger.
#>
function Protect-Hoja($Hoja, [switch] $TodoBloqueado) {
    $sinClave = [System.Type]::Missing
    $clave    = if ($ClaveProteccion) { $ClaveProteccion } else { $sinClave }

    if (-not $TodoBloqueado) {
        $Hoja.Cells.Locked = $false
        $Hoja.Range($Hoja.Rows.Item(1), $Hoja.Rows.Item(3)).Locked = $true
    }

    # Posicionales de Worksheet.Protect: se permiten formato, insertar y borrar filas, ordenar y
    # usar el autofiltro; se prohíbe tocar columnas, que es donde vive el contrato con el importador.
    $Hoja.Protect($clave, $true, $true, $false, $false,
                  $true,   # AllowFormattingCells
                  $true,   # AllowFormattingColumns  (ancho, no contenido)
                  $true,   # AllowFormattingRows
                  $false,  # AllowInsertingColumns
                  $true,   # AllowInsertingRows
                  $false,  # AllowInsertingHyperlinks
                  $false,  # AllowDeletingColumns
                  $true,   # AllowDeletingRows
                  $true,   # AllowSorting
                  $true,   # AllowFiltering
                  $false)  # AllowUsingPivotTables
}

function Set-Fecha($Hoja, [int] $Columna) {
    $col = $Hoja.Range($Hoja.Cells.Item(4, $Columna), $Hoja.Cells.Item($FILAS_VALIDACION, $Columna))
    $col.NumberFormat = 'yyyy-mm-dd'
    $col.HorizontalAlignment = -4108
}

# ── Hoja: Catálogos ──────────────────────────────────────────────────────────
# Va primero porque los rangos con nombre tienen que existir antes de que las otras hojas
# los referencien en sus validaciones.
$cat = New-Hoja 'Catalogos'
$cat.Cells.Item(1, 1).Value2 = 'Catálogos — no editar'
$cat.Cells.Item(1, 1).Font.Size = 14
$cat.Cells.Item(1, 1).Font.Bold = $true
$cat.Cells.Item(1, 1).Font.Color = $AZUL
$cat.Cells.Item(2, 1).Value2 = 'Los alimenta la base. Si falta un usuario o una unidad, créela primero en el portal y vuelva a generar la plantilla.'
$cat.Cells.Item(2, 1).Font.Italic = $true
$cat.Cells.Item(2, 1).Font.Color = $TEXTOGRIS

$listas = [ordered]@{
    'lstPrioridad'    = @{ Titulo = 'Prioridad';        Datos = $prioridades }
    'lstEstadoProy'   = @{ Titulo = 'Estado proyecto';  Datos = $estadosProyecto }
    'lstEstadoHito'   = @{ Titulo = 'Estado hito';      Datos = $estadosHito }
    'lstNivel'        = @{ Titulo = 'Nivel';            Datos = $niveles }
    'lstRolInt'       = @{ Titulo = 'Rol interesado';   Datos = $rolesInteresado }
    'lstCatRiesgo'    = @{ Titulo = 'Categoría riesgo'; Datos = $categoriasRiesgo }
    'lstEstrategia'   = @{ Titulo = 'Estrategia';       Datos = $estrategias }
    'lstEstadoRiesgo' = @{ Titulo = 'Estado riesgo';    Datos = $estadosRiesgo }
    'lstInstitucion'  = @{ Titulo = 'Institución';      Datos = $instituciones }
    'lstArea'         = @{ Titulo = 'Área';             Datos = $areas }
    'lstUnidad'       = @{ Titulo = 'Unidad';           Datos = $unidades }
    'lstUsuario'      = @{ Titulo = 'Usuario (correo)'; Datos = $usuarios }
}

$col = 1
foreach ($nombre in $listas.Keys) {
    $titulo = $listas[$nombre].Titulo
    $datos  = @($listas[$nombre].Datos)

    $cat.Cells.Item(4, $col).Value2 = $titulo
    $cat.Cells.Item(4, $col).Font.Bold = $true
    $cat.Cells.Item(4, $col).Font.Color = $BLANCO
    $cat.Cells.Item(4, $col).Interior.Color = $AZUL

    for ($i = 0; $i -lt $datos.Count; $i++) { $cat.Cells.Item(5 + $i, $col).Value2 = $datos[$i] }
    $cat.Columns.Item($col).ColumnWidth = 34

    $letra = [char]([int][char]'A' + $col - 1)   # 12 listas: no se pasa de la columna Z
    $libro.Names.Add($nombre, "=Catalogos!`$$letra`$5:`$$letra`$$(4 + $datos.Count)") | Out-Null
    $col++
}
$cat.Activate()
$cat.Application.ActiveWindow.FreezePanes = $false
$cat.Range('A5').Select() | Out-Null
$cat.Application.ActiveWindow.FreezePanes = $true

# ── Hoja: Proyectos ──────────────────────────────────────────────────────────
$hp = New-Hoja 'Proyectos'
Set-Encabezado $hp 'Proyectos' `
    'Una fila por proyecto. La Ref es un identificador que usted inventa (P1, P2…) y sirve para amarrar las hojas Hitos, Interesados y Riesgos: no se guarda en el sistema.' `
    @('Ref *', 'Nombre *', 'Objetivo', 'Institución ejecutora *', 'Área', 'Unidad', 'Responsable (correo)', 'Prioridad *', 'Estado *', 'Inicio planificado', 'Fin planificado', 'Inicio real', 'Fin real', 'Avance %') `
    @(1, 2, 4, 8, 9) `
    @(8, 42, 52, 20, 26, 26, 30, 11, 14, 15, 15, 14, 14, 10)

Set-Ejemplo $hp @('EJEMPLO', 'SOL — Secretaría de Finanzas', 'Habilitar en la plataforma SOL los 6 trámites de mayor demanda de SEFIN.', 'DIGER', 'GOBDIG — GOBIERNO DIGITAL', 'DITRA — DIGITALIZACION DE TRAMITES', 'hcardona@diger.gob.hn', 'Alta', 'EnEjecucion', '2026-03-02', '2026-11-30', '2026-03-09', '', '35')

Set-Lista $hp 4  'lstInstitucion'
Set-Lista $hp 5  'lstArea'
Set-Lista $hp 6  'lstUnidad'
Set-Lista $hp 7  'lstUsuario'
Set-Lista $hp 8  'lstPrioridad'
Set-Lista $hp 9  'lstEstadoProy'
Set-Fecha $hp 10; Set-Fecha $hp 11; Set-Fecha $hp 12; Set-Fecha $hp 13

$avance = $hp.Range($hp.Cells.Item(4, 14), $hp.Cells.Item($FILAS_VALIDACION, 14))
$avance.Validation.Delete()
$avance.Validation.Add(1, 1, 1, '0', '100') | Out-Null   # xlValidateWholeNumber, entre 0 y 100
$avance.Validation.ErrorTitle = 'Avance fuera de rango'
$avance.Validation.ErrorMessage = 'El avance es un entero de 0 a 100.'

# Las tres hojas siguientes validan su «Ref proyecto» contra esta columna, así que el nombre
# tiene que existir antes de que alguna lo mencione: Excel rechaza una validación que apunte
# a un nombre que todavía no definió.
$libro.Names.Add('lstRefProyecto', "=Proyectos!`$A`$4:`$A`$$FILAS_VALIDACION") | Out-Null

# ── Hoja: Hitos ──────────────────────────────────────────────────────────────
$hh = New-Hoja 'Hitos'
Set-Encabezado $hh 'Hitos' `
    'Entregables del cronograma. La Ref proyecto tiene que existir en la hoja Proyectos. Si deja el Orden vacío se numeran en el orden en que aparecen acá.' `
    @('Ref proyecto *', 'Orden', 'Hito *', 'Descripción', 'Fecha planificada', 'Fecha real', 'Estado *', 'Responsable (correo)') `
    @(1, 3, 7) `
    @(14, 8, 46, 54, 17, 15, 13, 30)

Set-Ejemplo $hh @('EJEMPLO','1', 'Levantamiento de los 6 trámites', 'Fichas técnicas validadas con la contraparte de SEFIN.', '2026-04-15', '2026-04-22', 'Completado', 'hcardona@diger.gob.hn')

Set-Lista $hh 1 'lstRefProyecto'
Set-Lista $hh 7 'lstEstadoHito'
Set-Lista $hh 8 'lstUsuario'
Set-Fecha $hh 5; Set-Fecha $hh 6

# ── Hoja: Interesados ────────────────────────────────────────────────────────
$hi = New-Hoja 'Interesados'
Set-Encabezado $hi 'Interesados' `
    'Tienen que ser usuarios del portal: quedar como interesado ES lo que le da acceso al proyecto, aunque la persona sea de otra institución. Si no tiene cuenta, hay que crearla antes.' `
    @('Ref proyecto *', 'Usuario (correo) *', 'Participa por', 'Cargo', 'Rol *', 'Influencia *', 'Notas') `
    @(1, 2, 5, 6) `
    @(14, 34, 26, 30, 20, 13, 50)

Set-Ejemplo $hi @('EJEMPLO', 'hcardona@diger.gob.hn', 'DIGER', 'Coordinador técnico', 'Ejecutor', 'Alta', 'Lleva la relación con la contraparte.')

Set-Lista $hi 1 'lstRefProyecto'
Set-Lista $hi 2 'lstUsuario'
Set-Lista $hi 5 'lstRolInt'
Set-Lista $hi 6 'lstNivel'

# ── Hoja: Riesgos ────────────────────────────────────────────────────────────
$hr = New-Hoja 'Riesgos'
Set-Encabezado $hr 'Riesgos' `
    'La severidad la calcula el portal: probabilidad × impacto (Alta=3, Media=2, Baja=1). 6 o más sale en rojo. Un riesgo que ya ocurrió se registra como Materializado, no como Abierto.' `
    @('Ref proyecto *', 'Riesgo *', 'Categoría *', 'Probabilidad *', 'Impacto *', 'Estrategia *', 'Estado *', 'Mitigación', 'Responsable (correo)', 'Detectado el *', 'Revisar el') `
    @(1, 2, 3, 4, 5, 6, 7, 10) `
    @(14, 50, 16, 14, 12, 13, 15, 50, 30, 14, 14)

Set-Ejemplo $hr @('EJEMPLO', 'SEFIN no designa contraparte técnica y el levantamiento se detiene.', 'Institucional', 'Media', 'Alta', 'Mitigar', 'Abierto', 'Escalar a Secretaría General con nota oficial a los 15 días.', 'hcardona@diger.gob.hn', '2026-03-10', '2026-05-10')

Set-Lista $hr 1  'lstRefProyecto'
Set-Lista $hr 3  'lstCatRiesgo'
Set-Lista $hr 4  'lstNivel'
Set-Lista $hr 5  'lstNivel'
Set-Lista $hr 6  'lstEstrategia'
Set-Lista $hr 7  'lstEstadoRiesgo'
Set-Lista $hr 9  'lstUsuario'
Set-Fecha $hr 10; Set-Fecha $hr 11

# ── Hoja: Instrucciones ──────────────────────────────────────────────────────
$ins = New-Hoja 'Instrucciones'
$ins.Columns.Item(1).ColumnWidth = 3
$ins.Columns.Item(2).ColumnWidth = 108

$lineas = @(
    @('T', 'Plantilla de carga de proyectos — DIGER'),
    @('S', 'Portafolio de Gobierno Digital · generada el ' + (Get-Date -Format 'dd/MM/yyyy')),
    @('', ''),
    @('H', 'Qué llenar'),
    @('P', 'Cuatro hojas: Proyectos, Hitos, Interesados y Riesgos. Solo la primera es obligatoria — un proyecto sin hitos, sin interesados y sin riesgos se importa igual.'),
    @('P', 'Los encabezados en amarillo con asterisco son obligatorios. Los demás pueden quedar vacíos.'),
    @('P', 'Cada hoja trae una fila de ejemplo en gris, con la Ref «EJEMPLO». Puede borrarla o dejarla: el importador ignora toda fila cuya Ref sea EJEMPLO.'),
    @('', ''),
    @('H', 'La columna «Ref»'),
    @('P', 'Es un identificador que usted inventa (P1, P2, P3…) para amarrar los hitos, interesados y riesgos con su proyecto. No se guarda en el sistema: existe solo dentro de este archivo.'),
    @('P', 'El código real del proyecto (PRY-2026-27) lo asigna el portal al importar. No lo escriba usted.'),
    @('', ''),
    @('H', 'Institución ejecutora'),
    @('P', 'Es quién EJECUTA el proyecto, no de quién trata. «SOL — CONSUCOOP» lo ejecuta DIGER, así que va DIGER. Esta columna decide quién puede ver el proyecto en el portal: si pone otra institución, DIGER deja de verlo.'),
    @('P', 'Área y Unidad son opcionales. Vacías = proyecto transversal, visible para toda la institución. Llenarlas lo restringe a esa área o unidad.'),
    @('', ''),
    @('H', 'Formatos'),
    @('P', 'Fechas: año-mes-día (2026-11-30). Las celdas ya vienen con ese formato.'),
    @('P', 'Avance: número entero de 0 a 100, sin el signo de porcentaje.'),
    @('P', 'Responsable e interesados: elíjalos de la lista desplegable, que trae los correos de los usuarios activos del portal. Si la persona no aparece, hay que crearle el usuario antes de importar.'),
    @('', ''),
    @('H', 'Los interesados dan acceso'),
    @('P', 'Quien figure como interesado PASA A VER ese proyecto completo —ficha, hitos, bitácora, bloqueos, riesgos y evidencia— aunque sea de otra institución, área o unidad. No es una lista de contactos: es a quién le está abriendo el proyecto.'),
    @('P', 'Por eso solo se admiten usuarios del portal. Un organismo sin cuenta (BID, PNUD, una cámara) no se puede registrar como interesado; si hace falta, primero se le crea el usuario.'),
    @('', ''),
    @('H', 'Listas desplegables'),
    @('P', 'Las columnas con lista solo aceptan los valores del catálogo. No son una sugerencia: el importador no reconoce nada fuera de esa lista y rechaza la fila.'),
    @('P', 'La hoja Catalogos es de consulta y está protegida, igual que esta. Las listas desplegables salen de ahí: si se borra, la plantilla deja de funcionar.'),
    @('', ''),
    @('H', 'Qué se puede y qué no'),
    @('P', 'Se puede: escribir en las filas de captura, agregar y borrar filas, ordenar, filtrar y ajustar el ancho de las columnas.'),
    @('P', 'No se puede: cambiar los encabezados, mover o borrar columnas, ni borrar hojas. El importador busca las columnas por nombre y por posición, así que un cambio ahí inutiliza el archivo.'),
    @('P', 'La protección no tiene contraseña: está para evitar el accidente, no para trabarlo. Si de verdad necesita reacomodar algo, use Revisar > Desproteger hoja — y avísele a quien va a importar el archivo.'),
    @('', ''),
    @('H', 'Al terminar'),
    @('P', 'Devuelva el archivo sin renombrar las hojas ni mover las columnas. El importador las busca por nombre y por posición.')
)

$fila = 1
foreach ($l in $lineas) {
    $tipo = $l[0]; $texto = $l[1]
    if (-not $texto) { $fila++; continue }

    $celda = $ins.Cells.Item($fila, 2)
    $celda.Value2 = $texto
    switch ($tipo) {
        'T' { $celda.Font.Size = 16; $celda.Font.Bold = $true; $celda.Font.Color = $AZUL }
        'S' { $celda.Font.Italic = $true; $celda.Font.Color = $TEXTOGRIS }
        'H' { $celda.Font.Size = 11; $celda.Font.Bold = $true; $celda.Font.Color = $AZUL }
        'P' { $celda.WrapText = $true; $ins.Rows.Item($fila).RowHeight = 26 }
    }
    $fila++
}

# Leyenda de colores, al pie: es la clave para leer las hojas de captura.
$fila += 1
$ins.Cells.Item($fila, 2).Value2 = 'Leyenda'
$ins.Cells.Item($fila, 2).Font.Bold = $true
$ins.Cells.Item($fila, 2).Font.Color = $AZUL
$fila++
$ins.Cells.Item($fila, 1).Interior.Color = $AMARILLO
$ins.Cells.Item($fila, 2).Value2 = 'Encabezado amarillo con asterisco: columna obligatoria.'
$fila++
$ins.Cells.Item($fila, 1).Interior.Color = $GRIS
$ins.Cells.Item($fila, 2).Value2 = 'Fila gris en cursiva con Ref «EJEMPLO»: muestra de formato. El importador la ignora.'

# ── Cierre ───────────────────────────────────────────────────────────────────
# Instrucciones de primera, Catalogos al final; se elimina la hoja vacía que trae el libro nuevo.
$hojaInicial.Delete()
$ins.Move($libro.Worksheets.Item(1))
$cat.Move([System.Reflection.Missing]::Value, $libro.Worksheets.Item($libro.Worksheets.Count))

# Protección, y en este orden: mover hojas es un cambio de estructura, así que el libro tiene que
# seguir desprotegido hasta acá.
Protect-Hoja $hp
Protect-Hoja $hh
Protect-Hoja $hi
Protect-Hoja $hr
Protect-Hoja $ins -TodoBloqueado
Protect-Hoja $cat -TodoBloqueado

# Lo que de verdad cuida los catálogos: sin esto se puede borrar la hoja Catalogos entera —y con
# ella todas las listas desplegables— aunque sus celdas estén bloqueadas.
if ($ClaveProteccion) { $libro.Protect($ClaveProteccion, $true, $false) }
else                  { $libro.Protect([System.Type]::Missing, $true, $false) }

$ins.Activate()
$ins.Range('A1').Select() | Out-Null

$dir = Split-Path -Parent $Salida
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
if (Test-Path $Salida) { Remove-Item $Salida -Force }

$libro.SaveAs($Salida, 51)   # 51 = xlOpenXMLWorkbook
$libro.Close($false)

}
finally {
    # Corre haya salido bien o mal. Sin esto, cualquier error entre medio deja EXCEL.EXE corriendo
    # sin ventana, y el usuario no tiene cómo darse cuenta salvo por el administrador de tareas.
    try { $excel.Quit() } catch { }
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
}

Write-Host "Plantilla generada: $Salida" -ForegroundColor Green
Write-Host ("Catálogos: {0} instituciones, {1} áreas, {2} unidades, {3} usuarios activos." -f `
    $instituciones.Count, $areas.Count, $unidades.Count, $usuarios.Count)
